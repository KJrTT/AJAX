# Конспект: производительность SQL, `HttpClientFactory`, устойчивость и идемпотентность

Материал делится на три большие темы:

1. Работа с SQL/EF Core: N+1, проекции, `AsNoTracking`, `AsSplitQuery`, сырой SQL, Dapper.
2. `HttpClientFactory`: почему нельзя `new HttpClient()`, типизированные клиенты, `DelegatingHandler`.
3. Устойчивость: тайм‑ауты, retry, circuit breaker, идемпотентность и `Idempotency-Key`.

---

# Часть 1. SQL и EF Core

## 1.1. Проблема N+1

Классический N+1: сначала один запрос за списком сущностей, потом по одному запросу на каждую связанную сущность.

```csharp
// ❌ ПЛОХО: классический N+1
var customers = await _db.Customers.ToListAsync();

var result = new List<CustomerOrdersDto>();
foreach (var customer in customers)
{
    // N запросов: для каждого клиента отдельно тянем заказы
    var orders = await _db.Orders
        .Where(o => o.CustomerId == customer.Id)
        .ToListAsync();

    result.Add(new CustomerOrdersDto
    {
        Name = customer.Name,
        OrdersCount = orders.Count,
        TotalSum = orders.Sum(o => o.Total)
    });
}
```

Почему плохо:

- при 1000 клиентов — 1001 запрос к БД;
- каждый запрос — сетевой round‑trip, парсинг, планирование;
- суммарное время растёт линейно с числом клиентов;
- БД нагружается мелкими запросами вместо одного крупного.

Тот же N+1 возникает и при **lazy loading**: обращение к навигационному свойству в цикле «незаметно» порождает отдельный запрос на каждую сущность. В EF Core lazy loading по умолчанию **выключен**, но если его включить (`UseLazyLoadingProxies`), N+1 становится очень легко получить случайно.

## 1.2. Решение: проекция в один запрос

```csharp
// ✅ ХОРОШО: один запрос, только нужные колонки
var result = await _db.Customers
    .AsNoTracking()
    .Select(c => new CustomerOrdersDto
    {
        Id = c.Id,
        Name = c.Name,
        OrdersCount = c.Orders.Count,
        TotalSum = c.Orders.Sum(o => (decimal?)o.Total) ?? 0m
    })
    .ToListAsync();
```

Что здесь важно:

- **`AsNoTracking()`** — EF не отслеживает изменения. Для read‑only запросов это экономит память и CPU: не создаётся снапшот сущностей, не ведётся identity map.
- **`Select` — проекция** — EF генерирует SQL только с нужными колонками, а не `SELECT *`. Меньше данных по сети, меньше памяти.
- **`Count` и `Sum` внутри проекции** — EF транслирует их в SQL (`COUNT`, `SUM` с `GROUP BY`), а не считает в памяти. Это критично: если вытащить `Orders` в память и посчитать `Count()` там, получится N+1 или лишний объём данных.
- **`(decimal?)o.Total`** — приведение к nullable, чтобы `Sum` на пустой коллекции вернул `null`, а не бросил исключение. `?? 0m` превращает `null` в 0.

Итог: один SQL‑запрос с `LEFT JOIN`/`GROUP BY` (или подзапросами — зависит от провайдера), только нужные колонки, без отслеживания.

## 1.3. `Include` и декартов взрыв

```csharp
// ⚠️ РАБОТАЕТ, но тянет ВСЕ колонки и может дать декартов взрыв
var customers = await _db.Customers
    .Include(c => c.Orders)
        .ThenInclude(o => o.Items)
    .AsNoTracking()
    .ToListAsync();
```

Проблемы:

- **все колонки** — `Include` не умеет проекцию, он тянет сущности целиком;
- **декартов взрыв** — если у клиента 10 заказов и в каждом 10 позиций, `JOIN` даст 100 строк на клиента; при нескольких `Include` подряд количество строк перемножается;
- **дублирование данных** — клиент и заказы дублируются в каждой строке результата.

EF Core умеет «схлопывать» дубликаты при материализации (identity resolution), но данные всё равно передаются по сети и обрабатываются.

## 1.4. `AsSplitQuery`

```csharp
// ✅ Если связей много — разбиваем на несколько SQL
var customers2 = await _db.Customers
    .Include(c => c.Orders)
        .ThenInclude(o => o.Items)
    .AsSplitQuery()
    .AsNoTracking()
    .ToListAsync();
```

`AsSplitQuery` говорит EF выполнить **несколько SQL‑запросов** вместо одного с `JOIN`:

- первый — клиенты;
- второй — заказы для этих клиентов;
- третий — позиции для этих заказов.

Плюсы:

- нет декартова взрыва;
- меньше дублирования данных по сети.

Минусы:

- несколько round‑trip к БД вместо одного;
- **нет транзакционной согласованности** между запросами: данные могут измениться между ними.

Когда использовать:

- много `Include` с коллекциями;
- большие объёмы данных;
- read‑only сценарии, где небольшая несогласованность допустима.

Когда не использовать:

- нужна строгая согласованность (например, финансовые операции);
- задержка до БД критична (несколько round‑trip хуже одного).

Глобально можно включить через `QuerySplittingBehavior.SplitQuery` в настройках провайдера, но лучше включать точечно.

## 1.5. Сложные отчёты: сырой SQL

LINQ удобен для простых запросов, но для сложной аналитики (оконные функции, `RANK`, `OVER`) он становится нечитаемым или невыразимым. Тогда пишут SQL.

```csharp
public record TopCustomerDto(int Id, string Name, decimal Total, int Rank);

var sql = """
    SELECT c.Id, c.Name, SUM(o.Total) AS Total,
           RANK() OVER (ORDER BY SUM(o.Total) DESC) AS Rank
    FROM Customers c
    JOIN Orders o ON o.CustomerId = c.Id
    WHERE o.CreatedAt >= @since
    GROUP BY c.Id, c.Name
    ORDER BY Total DESC
    OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY
    """;

var top = await _db.Database
    .SqlQuery<TopCustomerDto>($"...") // EF8: SqlQuery<T>
    .ToListAsync();
```

Важно:

- **всегда параметризовать** — иначе SQL injection;
- **`SqlQuery<T>`** (EF8+) — типизированный результат, маппинг на запись/DTO;
- **`FromSqlRaw`/`FromSqlInterpolated`** — для запросов к `DbSet`;
- **`ExecuteSqlRaw`** — для команд без результата.

Оконные функции (`RANK() OVER (...)`) — пример того, что в LINQ либо невозможно, либо крайне неудобно. SQL здесь уместнее.

## 1.6. Dapper для read‑only

```csharp
services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(config.GetConnectionString("Default")));

public async Task<IEnumerable<TopCustomerDto>> GetTopAsync(DateTime since)
{
    const string sql = "SELECT ... WHERE o.CreatedAt >= @since";
    using var conn = _connectionFactory.Create();
    return await conn.QueryAsync<TopCustomerDto>(sql, new { since });
}
```

Dapper — микро‑ORM:

- **не отслеживает изменения** — быстрее и предсказуемее EF Core для read‑only;
- **минимальный оверхед** — тонкая обёртка над ADO.NET;
- **явный SQL** — вы контролируете запрос полностью;
- **маппинг на DTO** — по именам колонок.

Когда Dapper лучше EF:

- сложные отчёты;
- высокая нагрузка на чтение;
- нужен полный контроль над SQL;
- нет необходимости в change tracking.

Когда EF лучше:

- CRUD с отслеживанием изменений;
- миграции;
- LINQ‑запросы с динамической композицией;
- работа с навигациями.

Часто их комбинируют: EF для записи, Dapper для чтения.

## 1.7. Бенчмарк

```csharp
var sw = Stopwatch.StartNew();
var bad = await GetOrdersBad();
sw.Stop();
_logger.LogInformation("BAD: {Ms} ms", sw.ElapsedMilliseconds);
```

Простой замер — уже полезен. Для серьёзных замеров — BenchmarkDotNet: учитывает прогрев, JIT, GC, статистику. `Stopwatch` в продакшене — для быстрой проверки, не для точных выводов.

## 1.8. Что важно запомнить про SQL/EF

- **N+1** — главная проблема производительности; лечится проекцией и `Include`.
- **`AsNoTracking`** — для read‑only, экономит память и CPU.
- **Проекция `Select`** — только нужные колонки; `Count`/`Sum` внутри транслируются в SQL.
- **`Include`** — тянет все колонки и может дать декартов взрыв.
- **`AsSplitQuery`** — несколько SQL вместо одного `JOIN`; нет транзакционной согласованности.
- **Сырой SQL** — для сложной аналитики; всегда параметризовать.
- **Dapper** — для read‑only и сложных отчётов; EF — для CRUD и change tracking.
- **Lazy loading** — удобен, но легко даёт N+1; лучше явно управлять загрузкой.

---

# Часть 2. `HttpClientFactory`

## 2.1. Почему нельзя `new HttpClient()`

```csharp
// ❌ Утечка сокетов при высокой нагрузке
using var client = new HttpClient();
var response = await client.GetAsync("https://api.example.com/products");
```

`HttpClient` реализует `IDisposable`, и кажется логичным создавать его на каждый запрос. Но:

- **сокеты не освобождаются сразу** — `HttpClient` держит `HttpClientHandler`, который держит пул соединений;
- **`Dispose` закрывает соединения**, но они остаются в состоянии `TIME_WAIT` на минуты;
- при высокой нагрузке порты исчерпываются → `SocketException: Only one usage of each socket address is normally permitted`;
- создание нового `HttpClient` на каждый запрос — антипаттерн.

Альтернатива «один статический `HttpClient` на всё приложение» тоже не идеальна:

- **DNS‑изменения не подхватываются** — соединения живут, пока живёт `HttpClient`;
- **настройки не меняются** — нельзя обновить `BaseAddress` или заголовки без пересоздания.

## 2.2. `IHttpClientFactory`

Фабрика решает обе проблемы:

- **пул handler‑ов** переиспользуется, сокеты не исчерпываются;
- **handler ротируется** раз в 2 минуты (по умолчанию) — DNS‑изменения подхватываются;
- **настройки задаются при регистрации** — `BaseAddress`, `Timeout`, заголовки;
- **интеграция с DI** — зависимости внедряются в типизированный клиент;
- **интеграция с Polly/Resilience** — политики добавляются одной строкой.

```csharp
services.AddHttpClient<IProductApi, ProductApi>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddStandardResilienceHandler();
```

## 2.3. Типизированный клиент

```csharp
public interface IProductApi
{
    Task<ProductDto?> GetProductAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProductDto>> SearchAsync(string query, CancellationToken ct);
}

public class ProductApi : IProductApi
{
    private readonly HttpClient _http;
    private readonly ILogger<ProductApi> _logger;

    public ProductApi(HttpClient http, ILogger<ProductApi> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductAsync(int id, CancellationToken ct)
    {
        using var resp = await _http.GetAsync($"products/{id}", ct);

        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        return await resp.Content.ReadFromJsonAsync<ProductDto>(ct);
    }
}
```

Ключевые моменты:

- **`HttpClient` инжектится** — фабрика создаёт его с настроенным `BaseAddress` и заголовками;
- **относительный URL** — `products/{id}`, а не полный адрес;
- **`using var resp`** — `HttpResponseMessage` реализует `IDisposable`;
- **404 → `null`**, остальные ошибки → исключение через `EnsureSuccessStatusCode`. Это осознанное решение: «не найдено» — нормальный сценарий, остальное — ошибка;
- **`ReadFromJsonAsync<T>(ct)`** — десериализация с поддержкой отмены;
- **`CancellationToken`** — прокидывается везде, чтобы запрос можно было отменить.

## 2.4. `DelegatingHandler`

`DelegatingHandler` — это middleware для `HttpClient`. Позволяет добавить сквозную логику: корреляцию, логирование, аутентификацию, retry.

```csharp
public class CorrelationHandler : DelegatingHandler
{
    private readonly ILogger<CorrelationHandler> _logger;
    private const string HeaderName = "X-Correlation-Id";

    public CorrelationHandler(ILogger<CorrelationHandler> logger) => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, ct);
            sw.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Url} → {Status} за {Ms} ms, CorrelationId={Cid}",
                request.Method, request.RequestUri, (int)response.StatusCode,
                sw.ElapsedMilliseconds, correlationId);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "HTTP {Method} {Url} упал через {Ms} ms",
                request.Method, request.RequestUri, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

services.AddTransient<CorrelationHandler>();
services.AddHttpClient<IProductApi, ProductApi>(...)
        .AddHttpMessageHandler<CorrelationHandler>();
```

Ключевые моменты:

- **`DelegatingHandler` — как middleware**, но для исходящих HTTP‑запросов;
- **`base.SendAsync`** — вызывает следующий handler в цепочке;
- **`Activity.Current?.Id`** — trace id из `System.Diagnostics.Activity`, связывает логи нашего сервиса и внешнего;
- **`TryAddWithoutValidation`** — добавляет заголовок без проверки формата (полезно для кастомных заголовков);
- **замер времени** — до и после `base.SendAsync`;
- **логирование ошибок** — с прокидыванием исключения дальше;
- **регистрация как transient** — фабрика создаёт новый handler на каждый клиент;
- **`AddHttpMessageHandler<T>`** — добавляет handler в цепочку.

`DelegatingHandler` — правильное место для:

- корреляции (`X-Correlation-Id`);
- логирования HTTP‑запросов;
- добавления аутентификации (`Authorization`);
- retry/circuit breaker (через Polly);
- кэширования ответов.

## 2.5. Что важно запомнить про `HttpClientFactory`

- **Не создавать `HttpClient` на каждый запрос** — исчерпание сокетов.
- **Не использовать один статический `HttpClient`** — DNS‑изменения не подхватываются, настройки не обновляются.
- **`IHttpClientFactory`** — правильное решение: пул handler‑ов + ротация + DI.
- **Типизированные клиенты** — инкапсулируют контракт API, зависимости внедряются.
- **`DelegatingHandler`** — middleware для исходящих запросов: корреляция, логирование, аутентификация.
- **`CancellationToken`** — прокидывать везде, чтобы запрос можно было отменить.

---

# Часть 3. Устойчивость и идемпотентность

## 3.1. Зачем нужна устойчивость

Внешние сервисы ненадёжны: сеть рвётся, серверы перегружены, ответы приходят с задержкой. Без обработки ошибок один медленный внешний сервис может «положить» весь ваш сервис: потоки заняты ожиданием, очередь растёт, пользователи страдают.

Паттерны устойчивости:

- **timeout** — не ждать бесконечно;
- **retry** — повторить транзиентную ошибку;
- **circuit breaker** — перестать ходить в упавший сервис;
- **bulkhead** — ограничить число одновременных запросов;
- **fallback** — вернуть запасной ответ.

## 3.2. Пайплайн устойчивости

```csharp
services.AddHttpClient<IPaymentApi, PaymentApi>(c =>
{
    c.BaseAddress = new Uri("https://payments.example.com/");
})
.AddResilienceHandler("payment-pipeline", builder =>
{
    // 1. Общий таймаут на попытку — 5 секунд
    builder.AddTimeout(TimeSpan.FromSeconds(5));

    // 2. Retry: 3 попытки с экспоненциальным backoff + jitter
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(500),
        BackoffType = DelayBackoffType.Exponential, // 0.5s → 1s → 2s
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError
                            || r.StatusCode == HttpStatusCode.RequestTimeout
                            || r.StatusCode == HttpStatusCode.TooManyRequests)
    });

    // 3. Circuit Breaker: размыкаемся, если >50% запросов упали
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 10,
        BreakDuration = TimeSpan.FromSeconds(15),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
    });

    // 4. Attempt timeout — таймаут на одну попытку (важно!)
    builder.AddTimeout(TimeSpan.FromSeconds(3));
});
```

### 3.2.1. Timeout

- **общий таймаут** — на всю операцию, включая все retry;
- **attempt timeout** — на одну попытку.

Порядок в пайплайне важен: общий таймаут — снаружи, attempt timeout — внутри retry. Иначе retry будет пытаться, пока не сработает общий таймаут, и все попытки уложатся в одну.

### 3.2.2. Retry

- **`MaxRetryAttempts = 3`** — всего 4 попытки (первая + 3 повтора);
- **`BackoffType.Exponential`** — задержка растёт: 0.5s → 1s → 2s;
- **`UseJitter = true`** — случайный разброс, чтобы все клиенты не долбили сервер одновременно (thundering herd);
- **`ShouldHandle`** — какие ошибки повторять: только транзиентные (5xx, 408, 429). Не повторять 4xx: 400/401/403/404 — это ошибки клиента, повтор не поможет.

### 3.2.3. Circuit Breaker

- **`FailureRatio = 0.5`** — размыкается, если больше половины запросов упали;
- **`SamplingDuration = 30s`** — окно, в котором считается доля ошибок;
- **`MinimumThroughput = 10`** — не считаем, пока не набрали 10 запросов (защита от случайных ошибок на малом объёме);
- **`BreakDuration = 15s`** — сколько держим разомкнутым, прежде чем попробовать снова.

Состояния circuit breaker:

- **Closed** — всё работает, запросы идут;
- **Open** — разомкнут, запросы сразу отклоняются (не тратим время на таймауты);
- **Half‑Open** — после `BreakDuration` пропускаем один пробный запрос; если успех — закрываемся, если ошибка — снова открываемся.

Circuit breaker защищает и ваш сервис (не тратит потоки на упавший внешний), и внешний (не добивает его запросами, когда он и так перегружен).

### 3.2.4. Порядок в пайплайне

Порядок стратегий в Polly/`ResiliencePipeline`:

1. **Timeout (общий)** — снаружи, ограничивает всю операцию.
2. **Retry** — повторяет попытки.
3. **Circuit Breaker** — решает, пускать ли запрос.
4. **Attempt Timeout** — внутри retry, ограничивает одну попытку.

Если перепутать, получится:

- retry снаружи общего таймаута — общий таймаут не сработает;
- attempt timeout снаружи retry — таймаут на все попытки, а не на одну;
- circuit breaker внутри retry — каждая попытка будет считаться отдельно, что искажает статистику.

## 3.3. `ResiliencePipeline` вручную

```csharp
public class PaymentService
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public PaymentService(ResiliencePipelineProvider<string> provider)
    {
        _pipeline = provider.GetPipeline<HttpResponseMessage>("payment-pipeline");
    }

    public async Task<PaymentResult> PayAsync(PaymentRequest req, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            var resp = await SendAsync(req, token);
            return Parse(resp);
        }, ct);
    }
}
```

Когда нужен ручной пайплайн:

- операция не HTTP (например, запрос к БД);
- нужен общий пайплайн для нескольких клиентов;
- нужна тонкая настройка, которой нет в `AddResilienceHandler`;
- нужно обернуть несколько операций в одну политику.

`ResiliencePipelineProvider` — реестр именованных пайплайнов. `GetPipeline<T>(name)` — получить пайплайн по имени.

## 3.4. Идемпотентность

### 3.4.1. Проблема

Retry повторяет запрос. Если запрос **неидемпотентный** (например, `POST /orders`), повтор создаст **второй заказ**. Пользователь нажал «Оплатить», запрос ушёл, ответ потерялся, retry — и списалось дважды.

Идемпотентность — свойство операции: сколько раз её ни выполни, результат один. `GET`, `PUT`, `DELETE` идемпотентны по спецификации HTTP. `POST` — нет.

### 3.4.2. `Idempotency-Key` на клиенте

```csharp
public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest req, CancellationToken ct)
{
    // Ключ генерируем ОДИН раз и передаём при ретраях.
    var idempotencyKey = req.IdempotencyKey ?? Guid.NewGuid().ToString();

    using var msg = new HttpRequestMessage(HttpMethod.Post, "orders");
    msg.Headers.Add("Idempotency-Key", idempotencyKey);
    msg.Content = JsonContent.Create(req);

    var resp = await _http.SendAsync(msg, ct);
    resp.EnsureSuccessStatusCode();

    return await resp.Content.ReadFromJsonAsync<OrderResult>(ct)!;
}
```

Ключевые моменты:

- **ключ генерируется один раз** — до retry, а не внутри;
- **ключ передаётся при каждом повторе** — сервер по нему понимает, что это тот же запрос;
- **если клиент сам повторяет запрос** (например, пользователь нажал кнопку дважды), он должен передать **тот же** ключ.

### 3.4.3. `IdempotencyMiddleware` на сервере

```csharp
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;

    public IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache)
    {
        _next = next; _cache = cache;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Method != HttpMethods.Post &&
            ctx.Request.Method != HttpMethods.Patch)
        {
            await _next(ctx);
            return;
        }

        var key = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(key)) { await _next(ctx); return; }

        var cacheKey = $"idem:{ctx.Request.Path}:{key}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(cached);
            return;
        }

        var originalBody = ctx.Response.Body;
        using var mem = new MemoryStream();
        ctx.Response.Body = mem;

        try
        {
            await _next(ctx);

            if (ctx.Response.StatusCode is >= 200 and < 300)
            {
                mem.Position = 0;
                var body = await new StreamReader(mem).ReadToEndAsync();
                await _cache.SetStringAsync(cacheKey, body,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    });
                mem.Position = 0;
            }

            await mem.CopyToAsync(originalBody);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }
    }
}
```

Разбор:

- **только POST/PATCH** — GET/PUT/DELETE идемпотентны по спецификации;
- **ключ в заголовке `Idempotency-Key`** — стандартный подход;
- **`cacheKey` включает путь** — разные эндпоинты не должны пересекаться;
- **если ключ уже в кэше** — возвращаем закэшированный ответ, не выполняя операцию;
- **перехват тела ответа** через `MemoryStream` — чтобы сохранить его в кэш;
- **кэшируем только 2xx** — иначе клиент застрянет на ошибке (ошибку можно повторить с тем же ключом);
- **`finally`** — восстанавливаем оригинальный `Response.Body`, даже если было исключение;
- **`AbsoluteExpirationRelativeToNow = 24h`** — ключ живёт сутки; после этого повтор снова выполнит операцию. Время выбирается под бизнес‑требования.

### 3.4.4. Что важно понять про идемпотентность

- **Ключ генерируется клиентом**, не сервером. Иначе сервер не отличит повтор от нового запроса.
- **Ключ должен быть уникальным** для каждой логической операции. GUID — хороший выбор.
- **Кэш должен быть распределённым** (Redis), иначе при нескольких экземплярах сервера ключ не найдётся на другом экземпляре.
- **Кэшировать только успешные ответы** — иначе ошибка «залипнет» и повтор не поможет.
- **TTL ключа** — компромисс между памятью и защитой. Слишком короткий — повтор через час создаст дубль; слишком длинный — кэш растёт.
- **Идемпотентность — не замена транзакциям**. Она защищает от повторного выполнения, но не от частичного выполнения. Для финансовых операций нужны и транзакции, и идемпотентность.

## 3.5. Точечный таймаут на конкретный вызов

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
cts.CancelAfter(TimeSpan.FromSeconds(2));

try
{
    var product = await _productApi.GetProductAsync(id, cts.Token);
}
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    _logger.LogWarning("Таймаут при получении товара {Id}", id);
    throw new TimeoutException($"Product API timeout for {id}");
}
```

- **`CreateLinkedTokenSource(ct)`** — новый токен, связанный с исходным: отмена любого из них отменяет оба;
- **`CancelAfter(2s)`** — таймаут на конкретный вызов, независимо от `HttpClient.Timeout`;
- **`catch (OperationCanceledException) when (!ct.IsCancellationRequested)`** — отмена из‑за таймаута, а не от клиента. Если клиент сам отменил — пробрасываем дальше;
- **`throw new TimeoutException(...)`** — превращаем отмену в понятную ошибку таймаута.

Зачем точечный таймаут, если есть `HttpClient.Timeout`:

- `HttpClient.Timeout` — глобальный для всех запросов клиента;
- разные операции могут требовать разных таймаутов;
- точечный таймаут можно комбинировать с retry;
- можно отличить таймаут от отмены пользователем.

---

# Сводка: что выбирать

| Задача | Решение |
|--------|---------|
| N+1 в EF Core | Проекция `Select` + `AsNoTracking` |
| Много `Include` с коллекциями | `AsSplitQuery` |
| Read-only запросы | `AsNoTracking` |
| Сложная аналитика (оконные функции) | Сырой SQL + `SqlQuery<T>` |
| Высокая нагрузка на чтение | Dapper |
| CRUD с change tracking | EF Core |
| HTTP-клиент | `IHttpClientFactory` + типизированный клиент |
| Сквозная логика для HTTP | `DelegatingHandler` |
| Транзиентные ошибки | Retry с exponential backoff + jitter |
| Упавший внешний сервис | Circuit breaker |
| Общий таймаут операции | Timeout снаружи retry |
| Таймаут одной попытки | Attempt timeout внутри retry |
| Неидемпотентный POST | `Idempotency-Key` + кэш на сервере |
| Точечный таймаут | `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter` |

---

# Частые ошибки

- **N+1 из-за lazy loading** — обращение к навигации в цикле.
- **`Include` без проекции** — тянет все колонки.
- **Несколько `Include` подряд** — декартов взрыв.
- **`AsSplitQuery` без понимания последствий** — нет транзакционной согласованности.
- **`Count`/`Sum` в памяти вместо SQL** — лишний объём данных и медленно.
- **SQL без параметров** — SQL injection.
- **`new HttpClient()` на каждый запрос** — исчерпание сокетов.
- **Один статический `HttpClient`** — DNS не обновляется.
- **Retry на 4xx** — повтор не поможет, только нагрузка.
- **Retry без jitter** — thundering herd.
- **Retry снаружи общего таймаута** — общий таймаут не сработает.
- **Circuit breaker внутри retry** — искажает статистику.
- **Идемпотентность без ключа** — повтор создаёт дубль.
- **Ключ генерируется на сервере** — сервер не отличит повтор от нового запроса.
- **Кэширование ошибок в idempotency** — клиент застрянет на ошибке.
- **Не распределённый кэш для idempotency** — при нескольких экземплярах ключ не найдётся.
- **Не различать таймаут и отмену пользователем** — неверные сообщения и логи.