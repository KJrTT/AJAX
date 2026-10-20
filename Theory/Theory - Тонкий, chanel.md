# Конспект: Channels в .NET и тонкий HTTP‑клиент

Материал делится на две независимые темы: фоновая обработка задач через `System.Threading.Channels` и построение «тонкого» типизированного HTTP‑клиента с классификацией ошибок.

---

# Часть 1. Channels и фоновая очередь задач

## 1. Зачем нужна очередь в фоне

Веб‑запрос должен отвечать быстро. Если операция долгая (отправка письма, генерация отчёта, обработка изображения), её выносят в фон: веб‑запрос только **ставит задачу в очередь** и сразу отвечает клиенту, а отдельный фоновый сервис забирает задачи и выполняет их.

`System.Threading.Channels` — встроенный в .NET механизм для такой очереди. Он даёт:

- асинхронную запись и чтение (`WriteAsync`/`ReadAsync`);
- ограничение ёмкости (backpressure);
- потокобезопасность без ручных блокировок;
- поддержку отмены через `CancellationToken`.

---

## 2. Интерфейс очереди

```csharp
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<CancellationToken, ValueTask> workItem);
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct);
}
```

Задача — это **делегат** `Func<CancellationToken, ValueTask>`, а не объект с данными. Это удобно: в замыкании можно захватить любые зависимости (id, сервисы, параметры), и очередь не знает, что именно выполняется.

`ValueTask` вместо `Task` — потому что операции часто завершаются синхронно (элемент уже в очереди или уже есть), и `ValueTask` позволяет избежать лишней аллокации.

---

## 3. Реализация с Bounded Channel

```csharp
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _channel;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueAsync(Func<CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _channel.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
```

Разбор опций:

- **`BoundedChannelOptions(capacity)`** — очередь ограничена. Это защита от переполнения памяти: если производители пишут быстрее, чем потребитель читает, очередь не растёт бесконечно.
- **`FullMode = BoundedChannelFullMode.Wait`** — при заполнении `WriteAsync` **ждёт**, пока потребитель не освободит место. Альтернативы: `DropOldest`, `DropNewest`, `DropWrite` — они отбрасывают элементы, что для задач обычно неприемлемо (задача потеряется).
- **`SingleReader = true`** — только один потребитель. Это позволяет каналу использовать более эффективные внутренние структуры без лишней синхронизации.
- **`SingleWriter = false`** — писателей много (веб‑запросы конкурентны), поэтому канал должен быть потокобезопасен со стороны записи.
- **`AllowSynchronousContinuations = false`** — продолжения после `await` не выполняются синхронно в потоке писателя/читателя. Это защита от неожиданного блокирования потока и от «захвата» чужого потока продолжением.

`Channel.CreateUnbounded` — альтернатива без ограничения ёмкости. Проще, но при всплеске нагрузки очередь может вырасти до OOM.

---

## 4. Потребитель: `BackgroundService`

```csharp
public class QueueConsumerService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<QueueConsumerService> _logger;

    public QueueConsumerService(IBackgroundTaskQueue queue, ILogger<QueueConsumerService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueueConsumerService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation("Начата обработка задачи в потоке {ThreadId}",
                    Environment.CurrentManagedThreadId);
                await workItem(stoppingToken);
                _logger.LogInformation("Задача завершена.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке фоновой задачи.");
            }
        }

        _logger.LogInformation("QueueConsumerService остановлен.");
    }
}
```

Ключевые моменты:

- `BackgroundService` — базовый класс для фоновых сервисов. Хост вызывает `StartAsync` при старте и `StopAsync` при остановке; `ExecuteAsync` — основной цикл.
- **Цикл `while (!stoppingToken.IsCancellationRequested)`** — стандартный паттерн. `stoppingToken` отменяется хостом при остановке приложения.
- **`DequeueAsync(stoppingToken)`** — ожидание элемента с поддержкой отмены. При остановке приложения `ReadAsync` бросит `OperationCanceledException` — цикл выйдет.
- **`catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)`** — фильтр по токену: ловим только отмену из‑за остановки, а не отмену внутри самой задачи. Это важно: задача может отменить свой внутренний токен, и это не повод останавливать цикл.
- **`catch (Exception)`** — ошибка одной задачи **не должна** ронять сервис. Логируем и продолжаем цикл. Иначе одна упавшая задача остановит обработку всех последующих.
- **`workItem(stoppingToken)`** — задача получает токен остановки. Если приложение останавливается, долгая задача может прерваться, если она уважает токен.

---

## 5. Регистрация и запуск

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(capacity: 5));
        services.AddHostedService<QueueConsumerService>();
    })
    .Build();

await host.StartAsync();

var queue = host.Services.GetRequiredService<IBackgroundTaskQueue>();

for (int i = 1; i <= 3; i++)
{
    int taskId = i;
    await queue.QueueAsync(async ct =>
    {
        await Task.Delay(1000, ct);
        Console.WriteLine($"  [Задача {taskId}] выполнена в потоке {Environment.CurrentManagedThreadId}");
    });
    Console.WriteLine($"Задача {taskId} поставлена в очередь.");
}

await Task.Delay(4000);

await host.StopAsync();
host.Dispose();
```

Разбор:

- **Очередь — singleton**: одна на всё приложение. Если бы она была scoped, каждый запрос получал бы свою очередь, и потребитель не видел бы задачи из других запросов.
- **`AddHostedService<QueueConsumerService>()`** — регистрирует фоновый сервис. Хост запускает его при старте и останавливает при остановке.
- **Замыкание `taskId`** — важная деталь: переменная цикла копируется в локальную, иначе все задачи увидели бы одно и то же значение (классическая ловушка C#).
- **`host.StartAsync()`** — запускает хост, в том числе фоновые сервисы. Без этого consumer не начнёт читать очередь.
- **`host.StopAsync()`** — корректная остановка: отменяет `stoppingToken`, даёт фоновым сервисам завершиться.

---

## 6. Что важно понять про Channels

- **Bounded vs Unbounded**: bounded защищает от OOM, но требует обработки переполнения (`Wait` — ждать, `Drop*` — терять). Unbounded проще, но опасен при всплесках.
- **Backpressure**: `FullMode.Wait` — это и есть backpressure: производитель замедляется, когда потребитель не успевает. Это лучше, чем потеря задач или OOM.
- **Один писатель / один читатель**: флаги `SingleReader`/`SingleWriter` — не просто оптимизация, а контракт. Если соврать, поведение станет непредсказуемым.
- **Отмена**: `ReadAsync(ct)` и `WriteAsync(ct)` поддерживают токен. При остановке приложения чтение и запись прерываются.
- **Ошибки задач не должны ронять цикл**: ловим `Exception` на уровне одной задачи, логируем, продолжаем.
- **Задача — делегат**: очередь не знает про домен, это универсальный механизм. Данные захватываются в замыкании.
- **`ValueTask` vs `Task`**: `ValueTask` эффективнее в горячем пути (частые синхронные завершения), но его нельзя `await`‑ить дважды и нельзя хранить «на потом» без осторожности.

---

# Часть 2. Тонкий HTTP‑клиент

## 1. Идея

«Тонкий» клиент — это слой, который:

- инкапсулирует транспорт (`HttpClient`, сериализация, заголовки);
- возвращает **результат**, а не бросает исключения на ожидаемые ошибки;
- классифицирует ошибки по видам, чтобы вызывающий код мог реагировать по‑разному;
- не содержит бизнес‑логики — только контракт конкретного API.

---

## 2. Результат запроса

```csharp
public readonly struct ApiResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public ApiError? Error { get; }
    public HttpStatusCode StatusCode { get; }

    private ApiResult(bool ok, T? data, ApiError? error, HttpStatusCode status)
    {
        IsSuccess = ok;
        Data = data;
        Error = error;
        StatusCode = status;
    }

    public static ApiResult<T> Ok(T data, HttpStatusCode status) =>
        new(true, data, null, status);

    public static ApiResult<T> Fail(ApiError error, HttpStatusCode status) =>
        new(false, default, error, status);
}
```

`readonly struct` — потому что результат короткоживущий и часто создаётся; структура избегает аллокации в куче. Все поля только для чтения, создание — через фабрики `Ok`/`Fail`.

Обратите внимание: **HTTP‑статус хранится отдельно** от `IsSuccess`. Это позволяет вызывающему коду различать «успех 200», «успех 201» и «ошибку 404» без парсинга строк.

---

## 3. Классификация ошибок

```csharp
public enum ApiErrorKind
{
    Network,
    Timeout,
    Http,
    Parse,
    Canceled
}

public sealed record ApiError(
    ApiErrorKind Kind,
    string Message,
    HttpStatusCode? StatusCode = null,
    string? Code = null,
    object? Details = null);
```

Виды ошибок:

| Вид | Что означает | Что делать |
|-----|--------------|------------|
| `Network` | Нет соединения, DNS, обрыв | Показать «проверьте подключение», можно повторить |
| `Timeout` | Сервер не ответил вовремя | Показать «таймаут», можно повторить с backoff |
| `Http` | Сервер вернул 4xx/5xx | Зависит от статуса: 400 — показать валидацию, 401 — релогин, 404 — «не найдено», 5xx — повторить |
| `Parse` | Сервер вернул невалидный JSON | Это баг сервера или прокси, логировать, не повторять |
| `Canceled` | Запрос отменён клиентом | **Не ошибка** для UI, молча игнорировать |

`Code` и `Details` — для структурированных ошибок от сервера: например, `code = "validation_failed"`, `details = { field: "email", message: "..." }`.

---

## 4. Транспорт

```csharp
public sealed class HttpTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly Func<string?>? _getToken;

    public HttpTransport(HttpClient client, string baseUrl, Func<string?>? getToken = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _baseUrl = baseUrl.TrimEnd('/');
        _getToken = getToken;
    }
    // ...
}
```

Ключевые решения:

- **`HttpClient` внедряется снаружи** — клиент не создаёт его сам. Это позволяет переиспользовать один `HttpClient` (важно: создание нового на каждый запрос исчерпывает сокеты) или использовать `IHttpClientFactory`.
- **`baseUrl` без завершающего слэша** — чтобы не было двойных слэшей при склейке.
- **`getToken` — функция, а не строка** — токен берётся в момент запроса, а не при создании транспорта. Это позволяет обновлять токен (refresh) без пересоздания клиента.
- **`JsonOptions`** — camelCase для сериализации и регистронезависимый парсинг: сервер может вернуть `userName` или `UserName`, клиент поймёт оба.

Методы `GetAsync`/`PostAsync`/`PutAsync`/`PatchAsync`/`DeleteAsync` — тонкие обёртки над `SendAsync`, задающие только метод HTTP и наличие тела.

---

## 5. Основной метод `SendAsync`

### 5.1. Подготовка запроса

```csharp
var url = $"{_baseUrl}/{path.TrimStart('/')}";

using var request = new HttpRequestMessage(method, url);

request.Headers.Accept.ParseAdd("application/json");

var token = _getToken?.Invoke();
if (!string.IsNullOrEmpty(token))
    request.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

if (body != null)
{
    var json = JsonSerializer.Serialize(body, JsonOptions);
    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
}
```

- `using var request` — `HttpRequestMessage` реализует `IDisposable`, его надо освобождать.
- `Accept: application/json` — говорим серверу, что ждём JSON.
- Токен берётся в момент запроса через `_getToken?.Invoke()` — если функция вернула `null` или пустую строку, заголовок не ставится.
- Тело сериализуется с теми же опциями (camelCase).

### 5.2. Отправка с классификацией ошибок

```csharp
HttpResponseMessage response;
try
{
    response = await _client.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead,
        ct);
}
catch (TaskCanceledException) when (ct.IsCancellationRequested)
{
    return ApiResult<T>.Fail(
        new ApiError(ApiErrorKind.Canceled, "Запрос отменён"),
        HttpStatusCode.RequestTimeout);
}
catch (TaskCanceledException)
{
    return ApiResult<T>.Fail(
        new ApiError(ApiErrorKind.Timeout, "Таймаут запроса"),
        HttpStatusCode.RequestTimeout);
}
catch (HttpRequestException ex)
{
    return ApiResult<T>.Fail(
        new ApiError(ApiErrorKind.Network, ex.Message),
        HttpStatusCode.ServiceUnavailable);
}
```

Тонкие моменты:

- **`HttpCompletionOption.ResponseHeadersRead`** — не читать тело сразу, а вернуться, как только пришли заголовки. Тело читается отдельно. Это экономит память для больших ответов и позволяет обрабатывать стриминг.
- **`TaskCanceledException` — это и отмена, и таймаут**. `HttpClient` кидает одно и то же исключение в обоих случаях. Различить можно по `ct.IsCancellationRequested`: если токен запрошен — это отмена пользователем; иначе — сработал таймаут `HttpClient.Timeout`.
- **`HttpRequestException`** — сетевые ошибки: нет соединения, DNS, TLS. Это `Network`.
- **Порядок catch важен**: сначала более специфичный `when`, потом общий.

### 5.3. Парсинг тела

```csharp
T? parsed;
try
{
    var bodyText = await response.Content.ReadAsStringAsync(ct);
    parsed = string.IsNullOrWhiteSpace(bodyText)
        ? default
        : JsonSerializer.Deserialize<T>(bodyText, JsonOptions);
}
catch (JsonException ex)
{
    return ApiResult<T>.Fail(
        new ApiError(ApiErrorKind.Parse, ex.Message),
        response.StatusCode);
}
```

- Пустое тело — нормально (например, `204 No Content`), возвращаем `default`.
- Ошибка парсинга — `Parse`. Это может быть баг сервера или прокси, вернувшего HTML вместо JSON.

### 5.4. Обработка HTTP‑ошибок

```csharp
if (!response.IsSuccessStatusCode)
{
    string? code = null;
    string? message = null;
    object? details = null;
    try
    {
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        if (root.TryGetProperty("code", out var codeEl))
            code = codeEl.GetString();
        if (root.TryGetProperty("message", out var msgEl))
            message = msgEl.GetString();
        if (root.TryGetProperty("details", out var detEl))
            details = detEl.Clone();
    }
    catch { /* тело не JSON — оставляем null */ }

    return ApiResult<T>.Fail(
        new ApiError(
            ApiErrorKind.Http,
            message ?? $"HTTP {(int)response.StatusCode}",
            response.StatusCode,
            code,
            details),
        response.StatusCode);
}

return ApiResult<T>.Ok(parsed!, response.StatusCode);
```

- Ошибка HTTP — это **не исключение**, а нормальный результат. Возвращается `Fail` с `Kind = Http`.
- Пытаемся вытащить структурированную ошибку из тела: `code`, `message`, `details`. Если тело не JSON — игнорируем, оставляем `null`.
- `detEl.Clone()` — важно: `JsonDocument` освобождается (`using`), и элемент без `Clone()` стал бы невалидным после выхода из блока.
- `message ?? $"HTTP {(int)response.StatusCode}"` — если сервер не дал сообщение, используем статус.

---

## 6. Тонкий клиент конкретного API

```csharp
public sealed class UsersApi
{
    private readonly HttpTransport _transport;

    public UsersApi(HttpTransport transport) => _transport = transport;

    public Task<ApiResult<UserListResponse>> ListAsync(
        int page = 1, int limit = 20, CancellationToken ct = default) =>
        _transport.GetAsync<UserListResponse>(
            $"users?page={page}&limit={limit}", ct);

    public Task<ApiResult<User>> GetByIdAsync(string id, CancellationToken ct = default) =>
        _transport.GetAsync<User>($"users/{Uri.EscapeDataString(id)}", ct);

    public Task<ApiResult<User>> CreateAsync(
        CreateUserRequest request, CancellationToken ct = default) =>
        _transport.PostAsync<User>("users", request, ct);

    public Task<ApiResult<User>> UpdateAsync(
        string id, UpdateUserRequest request, CancellationToken ct = default) =>
        _transport.PatchAsync<User>($"users/{Uri.EscapeDataString(id)}", request, ct);

    public Task<ApiResult<object?>> DeleteAsync(string id, CancellationToken ct = default) =>
        _transport.DeleteAsync<object?>($"users/{Uri.EscapeDataString(id)}", ct);
}
```

Ключевые моменты:

- **`UsersApi` знает только контракт** — пути, DTO, методы. Транспорт — отдельно.
- **`Uri.EscapeDataString(id)`** — обязательная защита: id может содержать `/`, `?`, `#`, пробелы. Без экранирования получится неверный URL или инъекция.
- **DTO — записи** (`record`): `User`, `CreateUserRequest`, `UpdateUserRequest`, `UserListResponse`. Иммутабельны, лаконичны, хорошо сериализуются.
- **Возвращается `ApiResult<T>`** — вызывающий код обязан обработать и успех, и ошибку.

---

## 7. Использование

```csharp
private static readonly HttpClient SharedClient = new()
{
    Timeout = TimeSpan.FromSeconds(30)
};

public static async Task Main()
{
    var transport = new HttpTransport(
        SharedClient,
        baseUrl: "https://api.example.com/v1",
        getToken: () => Environment.GetEnvironmentVariable("API_TOKEN"));

    var usersApi = new UsersApi(transport);
    // ...
}
```

- **`HttpClient` — `static readonly`** и переиспользуется. Создание нового на каждый запрос исчерпывает сокеты (TIME_WAIT). Альтернатива — `IHttpClientFactory`.
- **`Timeout = 30s`** — общий таймаут клиента. В дополнение к нему можно передавать `CancellationToken` с собственным таймаутом на конкретный запрос.
- **Токен из переменной окружения** — не хардкодим секреты в коде.

### 7.1. Успешный GET

```csharp
var list = await usersApi.ListAsync(page: 1, limit: 10);
if (list.IsSuccess)
{
    Console.WriteLine($"Пользователей: {list.Data!.Total}");
    foreach (var u in list.Data.Items)
        Console.WriteLine($"  {u.Id}: {u.Name} <{u.Email}>");
}
else
{
    HandleError(list.Error!);
}
```

### 7.2. Валидация при создании

```csharp
var created = await usersApi.CreateAsync(
    new CreateUserRequest("ivan@example.com", "Иван"));

if (created.IsSuccess)
{
    Console.WriteLine($"Создан: {created.Data!.Id}");
}
else if (created.Error!.Kind == ApiErrorKind.Http
      && created.StatusCode == HttpStatusCode.BadRequest)
{
    Console.WriteLine($"Валидация: {created.Error.Message}");
    Console.WriteLine($"Детали: {created.Error.Details}");
}
else
{
    HandleError(created.Error);
}
```

Здесь видно смысл разделения `Kind` и `StatusCode`: `Kind = Http` говорит «это HTTP‑ошибка», а `StatusCode = 400` уточняет «это валидация, покажи пользователю детали».

### 7.3. Отмена

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
var canceled = await usersApi.GetByIdAsync("42", cts.Token);
if (!canceled.IsSuccess && canceled.Error!.Kind == ApiErrorKind.Canceled)
{
    Console.WriteLine("Запрос отменён — это не ошибка UI");
}
```

`Canceled` — **не ошибка** для пользователя. Это нормальный сценарий: ушёл со страницы, ввёл новый символ. UI должен молча игнорировать.

### 7.4. 404 — это не исключение

```csharp
var missing = await usersApi.GetByIdAsync("does-not-exist");
if (!missing.IsSuccess
 && missing.Error!.Kind == ApiErrorKind.Http
 && missing.StatusCode == HttpStatusCode.NotFound)
{
    Console.WriteLine("Пользователь не найден");
}
```

Традиционный `HttpClient` бросил бы `HttpRequestException` на 404 (если не настроен иначе). Здесь 404 — нормальный результат, который можно обработать по‑разному в зависимости от контекста.

### 7.5. Централизованная обработка ошибок

```csharp
private static void HandleError(ApiError error)
{
    switch (error.Kind)
    {
        case ApiErrorKind.Network:
            Console.Error.WriteLine($"Сеть недоступна: {error.Message}");
            break;
        case ApiErrorKind.Timeout:
            Console.Error.WriteLine("Таймаут запроса");
            break;
        case ApiErrorKind.Parse:
            Console.Error.WriteLine($"Сервер вернул мусор: {error.Message}");
            break;
        case ApiErrorKind.Http:
            Console.Error.WriteLine($"HTTP {(int)error.StatusCode!}: {error.Message}");
            break;
        case ApiErrorKind.Canceled:
            // молча
            break;
    }
}
```

`switch` по `Kind` — центральное место, где решается, что показать пользователю. `Canceled` молча — это принципиально.

---

## 8. Что важно понять про тонкий клиент

- **Исключения vs результат**: ожидаемые ошибки (сеть, таймаут, HTTP) — это **результат**, а не исключение. Исключения остаются для действительно исключительных ситуаций (баг, нарушение контракта).
- **Классификация ошибок — ключ к правильной реакции**: `Network` — «проверьте подключение», `Timeout` — «повторить», `Http 400` — «показать валидацию», `Http 401` — «релогин», `Canceled` — молча. Без классификации вызывающий код вынужден парсить сообщения.
- **`Canceled` — не ошибка**: это нормальный сценарий. Если показывать его как ошибку, UI будет мигать сообщениями при каждом быстром вводе.
- **`HttpClient` переиспользуется**: статический экземпляр или `IHttpClientFactory`. Новый на каждый запрос — исчерпание сокетов.
- **`getToken` — функция**: токен берётся в момент запроса, а не при создании клиента. Это позволяет обновлять токен без пересоздания.
- **`Uri.EscapeDataString`** — обязательное экранирование параметров пути. Без него — неверные URL и потенциальные инъекции.
- **`ResponseHeadersRead`** — не читать тело сразу. Экономит память и позволяет обрабатывать стриминг.
- **`TaskCanceledException` — и отмена, и таймаут**: различаются по `ct.IsCancellationRequested`. Без этого различия нельзя показать правильное сообщение.
- **`JsonDocument` + `Clone()`**: документ освобождается, и без `Clone()` элемент стал бы невалидным.
- **`readonly struct` для результата**: короткоживущий объект, аллокация в куче не нужна.
- **Разделение транспорта и контракта**: `HttpTransport` знает про HTTP, `UsersApi` — про пути и DTO. Это позволяет переиспользовать транспорт для разных API и тестировать контракт отдельно.

---

# Сводка: что выбирать

| Задача | Решение |
|--------|---------|
| Фоновая обработка задач | `Channel<T>` + `BackgroundService` |
| Ограничение очереди | `BoundedChannelOptions(capacity)` + `FullMode.Wait` |
| Много писателей, один читатель | `SingleWriter = false`, `SingleReader = true` |
| Ошибка одной задачи не должна ронять цикл | `try/catch (Exception)` внутри цикла |
| Корректная остановка | `while (!stoppingToken.IsCancellationRequested)` + `catch (OperationCanceledException) when (...)` |
| HTTP‑клиент с классификацией ошибок | `ApiResult<T>` + `ApiErrorKind` |
| Различение отмены и таймаута | `TaskCanceledException` + `ct.IsCancellationRequested` |
| Безопасные URL | `Uri.EscapeDataString` для параметров пути |
| Переиспользование соединений | Один `HttpClient` или `IHttpClientFactory` |
| Структурированные ошибки от сервера | `JsonDocument` + `TryGetProperty("code"/"message"/"details")` |

---

# Частые ошибки

- **`SingleReader`/`SingleWriter` соврали** — поведение канала станет непредсказуемым. Флаги — это контракт, а не подсказка.
- **`Unbounded` без нужды** — при всплеске нагрузки очередь вырастет до OOM.
- **`Drop*` в `FullMode` для задач** — задачи потеряются молча. Для фоновой обработки обычно нужен `Wait`.
- **Ошибка одной задачи роняет `BackgroundService`** — без `try/catch` внутри цикла одна упавшая задача остановит обработку всех последующих.
- **Забыть `await host.StartAsync()`** — фоновый сервис не начнёт работать.
- **Замыкание переменной цикла** — все задачи увидят последнее значение. Копировать в локальную переменную.
- **Новый `HttpClient` на каждый запрос** — исчерпание сокетов. Переиспользовать или `IHttpClientFactory`.
- **Не различать отмену и таймаут** — сообщения пользователю будут неверными.
- **Показывать `Canceled` как ошибку** — UI будет мигать при быстром вводе.
- **Не экранировать параметры пути** — неверные URL и потенциальные инъекции.
- **`JsonDocument` без `Clone()` для вложенных элементов** — элемент станет невалидным после освобождения документа.
- **Читать тело сразу без `ResponseHeadersRead`** — лишняя память для больших ответов.
- **Бросать исключения на HTTP 4xx/5xx** — ожидаемые ошибки должны быть результатом, а не исключением.