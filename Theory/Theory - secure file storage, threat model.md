# Конспект по теории: защищённое файловое хранилище и модель угроз

Ниже — теоретический материал, на который опирается проект: middleware в ASP.NET Core, аутентификация и авторизация по API‑ключу, защитные HTTP‑заголовки, валидация файлов по сигнатурам, безопасное хранение, модель угроз STRIDE и трассировка запросов.

---

## 1. Конвейер middleware в ASP.NET Core

### 1.1. Что такое middleware

Middleware — это компонент, который получает `HttpContext` и ссылку на следующий компонент (`RequestDelegate next`). Он может:

- выполнить код до `next()`;
- вызвать `next()` и передать управление дальше;
- выполнить код после возврата из `next()`;
- **не вызывать** `next()` — прервать конвейер.

Конвейер собирается в порядке регистрации. Запрос идёт «внутрь», ответ — «наружу»: это модель луковицы.

```csharp
app.Use(async (context, next) =>
{
    // до следующего middleware
    await next();
    // после возврата
});
```

### 1.2. Два способа писать middleware

**Inline** — прямо в `Program.cs`:

```csharp
app.Use(async (context, next) =>
{
    await next();
});
```

Просто, но зависимости приходится доставать из `context.RequestServices`.

**Класс с `IMiddleware`** — регистрируется как сервис, создаётся фабрикой на каждый запрос, зависимости внедряются через конструктор:

```csharp
public class RequestLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger) => _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        _logger.LogInformation("▶ {Method} {Path}", context.Request.Method, context.Request.Path);
        await next(context);
        _logger.LogInformation("◀ {Status}", context.Response.StatusCode);
    }
}

builder.Services.AddTransient<RequestLoggingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
```

**Класс с `RequestDelegate` в конструкторе** — классический вариант без регистрации в DI:

```csharp
public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // ...
        await _next(context);
    }
}

app.UseMiddleware<RequestIdMiddleware>();
```

### 1.3. Порядок middleware

Порядок критичен. Типичный порядок в защищённом приложении:

1. Обработчик исключений (`UseExceptionHandler`) — самый внешний, ловит всё.
2. HSTS (`UseHsts`) — принудительный HTTPS.
3. HTTPS‑редирект (`UseHttpsRedirection`).
4. Статические файлы (`UseStaticFiles`).
5. Собственные middleware до роутинга (RequestId, SecurityHeaders).
6. Маршрутизация (`UseRouting`).
7. Аутентификация (`UseAuthentication`).
8. Авторизация (`UseAuthorization`).
9. Antiforgery (если есть cookie + POST).
10. Endpoint‑ы (`MapControllers`, `MapControllerRoute`).

Почему так:

- **логирование и защитные заголовки — до роутинга**, чтобы покрывать все ответы, включая ошибки маршрутизации и статику;
- **аутентификация до авторизации** — иначе политики не увидят пользователя;
- **antiforgery после аутентификации** — чтобы работать с сессией;
- **статика до маршрутизации** — чтобы не гонять статику через MVC.

### 1.4. Прерывание конвейера

Middleware может не вызывать `next()`. Это способ реализовать «короткое замыкание»: валидация не прошла — сразу ответ, дальше не идём.

```csharp
if (string.IsNullOrWhiteSpace(form["name"]))
{
    context.Response.StatusCode = 400;
    await context.Response.WriteAsync("Ошибка: поле 'name' обязательно");
    return; // next() не вызывается
}
```

### 1.5. Код после `next()`

Код после `next()` выполняется, когда ответ уже сформирован. Это удобно для:

- замеров времени;
- логирования статуса;
- добавления заголовков;
- обёртки ответа.

Важно: если ответ уже начал отправляться (`Response.HasStarted`), менять статус или заголовки нельзя — будет исключение.

### 1.6. `OnStarting` и `OnCompleted`

`Response.OnStarting` — колбэк, который выполняется **перед отправкой ответа**, когда статус и заголовки уже сформированы, но ещё не отправлены. Это правильное место для добавления заголовков, которые не должны быть перезаписаны контроллером.

```csharp
context.Response.OnStarting(() =>
{
    context.Response.Headers["Deprecation"] = "true";
    return Task.CompletedTask;
});
```

`Response.OnCompleted` — после отправки ответа. Удобно для очистки ресурсов.

---

## 2. Аутентификация и авторизация

### 2.1. Различие

- **Аутентификация** отвечает на вопрос «кто ты?». Результат — `ClaimsPrincipal` в `HttpContext.User`.
- **Авторизация** отвечает на вопрос «что тебе можно?». Проверяет политики и роли.

Порядок в конвейере: сначала `UseAuthentication`, потом `UseAuthorization`.

### 2.2. Схемы аутентификации

Схема — это именованный обработчик, который умеет аутентифицировать запрос. В приложении может быть несколько схем: cookie, Bearer, API‑ключ, custom.

```csharp
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });
```

`AddAuthentication(scheme)` задаёт схему по умолчанию. `AddScheme<TOptions, THandler>` регистрирует конкретную схему.

### 2.3. Кастомный обработчик API‑ключа

`AuthenticationHandler<TOptions>` — базовый класс. Основной метод — `HandleAuthenticateAsync`, который возвращает один из трёх результатов:

- `AuthenticateResult.Success(ticket)` — аутентификация прошла;
- `AuthenticateResult.Fail(reason)` — аутентификация не прошла (неверный ключ);
- `AuthenticateResult.NoResult()` — этот обработчик не применим (например, заголовка нет), пусть попробуют другие схемы.

```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    if (!Request.Headers.TryGetValue(HeaderName, out var provided))
        return Task.FromResult(AuthenticateResult.NoResult());

    var key = provided.ToString();
    if (string.IsNullOrWhiteSpace(key))
        return Task.FromResult(AuthenticateResult.Fail("Empty API key"));

    var readKey = _config["ApiKeys:Read"];
    var writeKey = _config["ApiKeys:Write"];

    var claims = new List<Claim>();
    if (!string.IsNullOrEmpty(readKey) && key == readKey)
        claims.Add(new Claim(ClaimTypes.Role, "Files.Read"));
    if (!string.IsNullOrEmpty(writeKey) && key == writeKey)
    {
        claims.Add(new Claim(ClaimTypes.Role, "Files.Read"));
        claims.Add(new Claim(ClaimTypes.Role, "Files.Write"));
    }

    if (claims.Count == 0)
        return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);
    return Task.FromResult(AuthenticateResult.Success(ticket));
}
```

Ключевые моменты:

- **`NoResult` vs `Fail`** — принципиально разные вещи. `NoResult` говорит «я не могу аутентифицировать этот запрос, попробуйте другие схемы». `Fail` говорит «аутентификация провалилась, дальше не надо». Если вернуть `Fail` на отсутствие заголовка, сломаются другие схемы.
- **Роли из ключа** — один ключ даёт только чтение, другой — чтение и запись. Это принцип минимальных привилегий на уровне ключей.
- **Сравнение ключей `==`** — учебное упрощение. В продакшене нужно сравнение за постоянное время (`CryptographicOperations.FixedTimeEquals`), чтобы исключить атаки по времени.
- **Ключи из конфигурации** — не хардкод. В реальном проекте — секреты, ротация, разные ключи для разных клиентов.

### 2.4. Политики авторизации

Политика — именованный набор требований. Применяется через `[Authorize(Policy = "...")]`.

```csharp
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy(Policies.ReadFiles, p => p.RequireRole("Files.Read"));
    opts.AddPolicy(Policies.WriteFiles, p => p.RequireRole("Files.Write"));
});
```

```csharp
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/files")]
public class FilesApiController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.WriteFiles)]
    public async Task<IActionResult> Upload(...) { ... }

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.ReadFiles)]
    public async Task<IActionResult> Download(...) { ... }
}
```

Уровни:

- на контроллере — общая аутентификация по схеме;
- на методе — конкретная политика.

Разделение чтения и записи — принцип минимальных привилегий: клиент с ключом только для чтения не сможет загрузить файл, даже если попытается.

### 2.5. `RequireRole` vs `RequireClaim`

- `RequireRole("admin")` — проверяет стандартный claim роли (`ClaimTypes.Role`);
- `RequireClaim("role", "admin")` — проверяет конкретный claim с конкретным значением.

В кастомных схемах (JWT) роль может быть в нестандартном claim. Тогда нужно настроить `RoleClaimType` в параметрах валидации или использовать `RequireClaim`.

---

## 3. Защитные HTTP‑заголовки

### 3.1. CSP (Content Security Policy)

CSP ограничивает, откуда браузер может загружать ресурсы и что может выполнять. Это главная защита от XSS.

```csharp
context.Response.Headers["Content-Security-Policy"] =
    "default-src 'self'; script-src 'self'; style-src 'self'; " +
    "img-src 'self' data:; connect-src 'self'; " +
    "object-src 'none'; base-uri 'self'";
```

Директивы:

- `default-src 'self'` — по умолчанию только свой домен;
- `script-src 'self'` — скрипты только свои (без inline!);
- `style-src 'self'` — стили только свои;
- `img-src 'self' data:` — картинки свои и data‑URI;
- `connect-src 'self'` — fetch/XHR только на свой домен;
- `object-src 'none'` — запрет плагинов (Flash, Java);
- `base-uri 'self'` — защита от подмены `<base>`.

**Разные политики для Development и Production** — компромисс:

- в Development разрешены `'unsafe-inline'`, `ws:`, `wss:` — иначе не работают hot reload и отладка;
- в Production — строгая политика без inline.

Это важный момент: `'unsafe-inline'` в продакшене практически обнуляет защиту CSP от XSS.

### 3.2. `X-Content-Type-Options: nosniff`

Запрещает браузеру «угадывать» MIME‑тип. Если сервер сказал `text/plain`, браузер не будет исполнять это как HTML или JS. Защита от MIME‑confusion атак.

### 3.3. `X-Frame-Options: DENY`

Запрещает встраивать страницу в iframe. Защита от clickjacking — когда злоумышленник накладывает невидимый iframe с вашим сайтом поверх своей страницы и заставляет пользователя кликать по вашим кнопкам.

Современная альтернатива — директива CSP `frame-ancestors 'none'`. Она гибче, но `X-Frame-Options` поддерживается старыми браузерами.

### 3.4. `Referrer-Policy: no-referrer`

Не передавать URL страницы в заголовке `Referer` при переходе на другие сайты. Защита от утечки чувствительных данных в URL (токены, id сессий).

Другие варианты: `same-origin` (передавать только своим), `strict-origin-when-cross-origin` (по умолчанию в современных браузерах).

### 3.5. HSTS (HTTP Strict Transport Security)

`UseHsts()` добавляет заголовок `Strict-Transport-Security: max-age=...`. Браузер запоминает, что сайт работает только по HTTPS, и в течение `max-age` не пытается зайти по HTTP.

Это защита от downgrade‑атак и SSL stripping. Важно: HSTS включается только для HTTPS‑ответов и только вне Development (иначе замучаетесь с локальной разработкой).

### 3.6. `Content-Disposition: attachment`

При скачивании файла:

```csharp
Response.Headers.ContentDisposition =
    $"attachment; filename*=UTF-8''{Uri.EscapeDataString(meta.Name)}";
```

`attachment` говорит браузеру «скачай, а не открывай». Это защита от XSS через загруженный HTML/JS: даже если злоумышленник загрузил HTML, браузер его скачает, а не выполнит.

`filename*=UTF-8''...` — RFC 5987, правильная передача не‑ASCII имён. `Uri.EscapeDataString` экранирует специальные символы.

---

## 4. Валидация загружаемых файлов

### 4.1. Принцип «defense in depth»

Проверять несколько независимых признаков, а не один. Файл считается валидным, только если **все** проверки прошли.

Три уровня:

1. **Расширение** — быстро, но подделывается переименованием.
2. **MIME‑тип от клиента** — тоже подделывается (клиент присылает что хочет).
3. **Сигнатура (magic bytes)** — сложнее подделать, потому что это содержимое файла.

### 4.2. Сигнатуры (magic bytes)

Первые байты файла, которые идентифицируют формат:

| Формат | Сигнатура (hex) |
|--------|-----------------|
| PNG | `89 50 4E 47` |
| JPEG | `FF D8 FF` |
| GIF | `47 49 46 38` (`GIF8`) |
| PDF | `25 50 44 46` (`%PDF`) |
| ZIP | `50 4B 03 04` (`PK..`) |

```csharp
var header = new byte[8];
var read = await stream.ReadAsync(header.AsMemory(0, 8), ct);
stream.Position = 0; // вернуть поток в начало для последующей записи

if (read < 4) return new(false, "Файл слишком короткий");

var extLower = ext.ToLowerInvariant();
if (Signatures.TryGetValue(extLower, out var sigs))
{
    var matches = sigs.Any(sig => header.Take(sig.Length).SequenceEqual(sig));
    if (!matches) return new(false, "Сигнатура файла не соответствует расширению");
}
```

Важно: после чтения первых байт **вернуть поток в начало** (`stream.Position = 0`), иначе при сохранении файл будет обрезан.

### 4.3. `.txt` — особый случай

У текстовых файлов нет сигнатуры. Вместо этого проверяется, что в первых байтах нет управляющих/бинарных символов:

```csharp
if (extLower == ".txt")
{
    if (header.Take(read).Any(b => b < 0x09 || (b > 0x0D && b < 0x20)))
        return new(false, "Содержимое не является текстом");
}
```

Допустимые управляющие символы: `\t` (0x09), `\n` (0x0A), `\v` (0x0B), `\f` (0x0C), `\r` (0x0D). Всё остальное ниже 0x20 — бинарный мусор.

### 4.4. Порядок проверок

От дешёвой к дорогой:

1. **Размер** — самая дешёвая, делается первой. Пустой файл или больше лимита — сразу отказ.
2. **Расширение** — дешёвая, но не авторитетная.
3. **Сигнатура** — дороже (чтение потока), но авторитетная.

Ранний отказ на дешёвых проверках экономит ресурсы.

### 4.5. Лимиты на нескольких уровнях

- **Kestrel** — глобальный лимит на размер тела запроса:
  ```csharp
  builder.WebHost.ConfigureKestrel(o =>
  {
      o.Limits.MaxRequestBodySize = FileValidationService.MaxSizeBytes;
  });
  ```
- **Атрибуты на методе** — дублируют лимит:
  ```csharp
  [RequestSizeLimit(FileValidationService.MaxSizeBytes)]
  [RequestFormLimits(MultipartBodyLengthLimit = FileValidationService.MaxSizeBytes)]
  ```
- **Валидатор** — проверка размера до чтения содержимого.

Это «defense in depth»: если один уровень обойдут, сработает другой. Глобальный лимит Kestrel защищает от DoS ещё до того, как запрос попадёт в приложение.

### 4.6. Чего не хватает в учебной реализации

- **Проверка реального MIME по сигнатуре** — в коде сохраняется `claimedType` от клиента, а не определённый по содержимому. В продакшене стоит определять MIME по сигнатуре и сохранять его.
- **Антивирусная проверка** — для загружаемых файлов часто нужна проверка на malware.
- **Проверка всего файла, а не только первых байтов** — злоумышленник может сделать валидную сигнатуру и вредоносное содержимое дальше.
- **Ограничение на количество файлов** — rate limiting на уровне пользователя/IP.

---

## 5. Безопасное хранение файлов

### 5.1. Случайный id вместо имени

```csharp
var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
```

16 байт криптостойкого генератора → 32 hex‑символа. Это защита от:

- **path traversal** — в id не может быть `../`;
- **коллизий** — вероятность ничтожна;
- **угадывания** — злоумышленник не может перебрать id.

Оригинальное имя хранится в метаданных, а не на диске.

### 5.2. Хранение вне webroot

Файлы лежат в `<root>/<id>.bin` и `<root>/<id>.meta.json`, где `root` — из конфигурации или `<base>/uploads`. Это **не** `wwwroot`, поэтому `UseStaticFiles` их не отдаёт. Доступ — только через API с проверкой ключа.

Это ключевая защита: даже если злоумышленник узнал id, он не может получить файл напрямую по URL — нужен API‑ключ.

### 5.3. Проверка id при чтении

```csharp
if (string.IsNullOrWhiteSpace(id) || id.Length != 32 || !id.All(Uri.IsHexDigit))
    return null;
```

Строгая проверка формата id — защита от path traversal и от попыток подсунуть произвольный путь.

### 5.4. Метаданные рядом с файлом

Метаданные (оригинальное имя, размер, content type) хранятся в отдельном JSON рядом с бинарником. При скачивании они читаются и используются для `Content-Disposition`.

Альтернатива — БД. Файлы в файловой системе, метаданные в БД: это масштабируется лучше и позволяет искать/фильтровать.

### 5.5. Range‑запросы

```csharp
return File(stream, meta.ContentType, enableRangeProcessing: true);
```

`enableRangeProcessing: true` поддерживает докачку и воспроизведение больших файлов. Браузер может запросить часть файла (`Range: bytes=0-1023`), и сервер вернёт только её.

---

## 6. Трассировка запросов

### 6.1. `X-Request-Id`

`RequestIdMiddleware` проставляет `X-Request-Id` в запрос и ответ:

```csharp
var id = context.Request.Headers["X-Request-Id"].FirstOrDefault();
if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
    id = Guid.NewGuid().ToString("N");

context.Items["RequestId"] = id;
context.Response.Headers["X-Request-Id"] = id;
```

- если клиент прислал id (и он валидной длины) — используем его;
- иначе генерируем новый;
- кладём в `Items` (доступно внутри приложения) и в заголовок ответа (доступно клиенту).

Ограничение длины — защита от переполнения логов и header injection.

### 6.2. Лог‑scope

```csharp
using (context.RequestServices.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Request").BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
{
    await _next(context);
}
```

`BeginScope` добавляет `RequestId` во все сообщения логов внутри блока. Так все события одного запроса помечены одним id — по нему можно найти всю цепочку.

### 6.3. Correlation id в ошибках

Клиент получает `X-Request-Id` в ответе и показывает его в сообщении об ошибке. Пользователь передаёт id в поддержку — по нему в логах находится вся цепочка событий.

```js
const correlationId = response.headers.get('X-Request-Id') || requestId;
```

Это стандартная практика: без correlation id разбор инцидента превращается в археологию.

---

## 7. Модель угроз STRIDE

### 7.1. Что такое STRIDE

Методология классификации угроз от Microsoft. Каждая буква — класс угроз:

| Буква | Категория | Что означает | Пример |
|-------|-----------|--------------|--------|
| S | Spoofing | Подмена | Клиент выдаёт себя за другого |
| T | Tampering | Изменение | Подмена данных в запросе или файле |
| R | Repudiation | Отказ от авторства | Пользователь отрицает, что сделал действие |
| I | Information Disclosure | Утечка информации | Файл доступен не тому, кому надо |
| D | Denial of Service | Отказ в обслуживании | Огромный файл выедает память |
| E | Elevation of Privilege | Повышение привилегий | Аноним загружает файлы |

### 7.2. Границы доверия

Trust Boundary — линия, где данные переходят из одной зоны доверия в другую. На каждой такой линии нужен контроль.

Примеры из проекта:

- **TB-01**: Браузер → Web API. Контроль: HTTPS, аутентификация, валидация, rate limiting.
- **TB-02**: Web API → Файловое хранилище. Контроль: случайный id, проверка сигнатуры, хранение вне webroot, лимит размера.
- **TB-03**: Web API → БД (в будущем). Контроль: параметризованные запросы, минимальные права.

### 7.3. Угрозы и контрмеры

| Id | STRIDE | Угроза | Контрмеры |
|----|--------|--------|-----------|
| TH-001 | Spoofing | Подмена типа файла | Валидация magic bytes, whitelist расширений |
| TH-002 | Tampering | Path traversal через имя | Санитайзинг, генерация своего id |
| TH-003 | Information Disclosure | XSS через скачанный файл | `Content-Disposition: attachment`, `nosniff` |
| TH-004 | Elevation of Privilege | Загрузка без аутентификации | API‑key, политики Read/Write |
| TH-005 | DoS | Огромный файл | `RequestSizeLimit`, проверка в валидаторе |
| TH-006 | Repudiation | Нет трассировки | `X-Request-Id`, scope в логах |

### 7.4. Security backlog

Каждая угроза должна быть либо **устранена** (кодом/конфигом), либо **явно принята** (Accepted) с обоснованием. Backlog — это конкретные задачи:

| Id | Угроза | Задача | Приоритет | Статус | Владелец |
|----|--------|--------|-----------|--------|----------|
| SB-001 | TH-001 | Внедрить проверку magic bytes | High | InProgress | backend |
| SB-002 | TH-002 | Покрыть тестами санитайзинг | High | Open | qa |
| SB-003 | TH-003 | Проверить заголовки | Medium | Done | backend |
| SB-004 | TH-004 | Ротация API‑ключей | High | Open | devops |
| SB-005 | TH-005 | Лимиты на reverse‑proxy | Medium | InProgress | devops |
| SB-006 | TH-006 | Пробросить correlation id в UI | Low | Done | frontend |

«Живая» модель угроз в репозитории — это практика «threat‑driven development»: безопасность — часть разработки, а не отдельный документ, который забывают обновить.

---

## 8. Тонкий клиент и обработка ошибок

### 8.1. Принципы тонкого клиента

- никакой бизнес‑логики — только транспорт;
- вся авторитетная валидация на сервере;
- явная работа с HTTP‑границами (2xx, 4xx, 5xx, сеть);
- никогда не доверять формату ответа (`response.json()` вслепую — плохо).

### 8.2. Классификация ошибок

```js
class ApiError extends Error {
    constructor(message, { code = 'unknown', status = 0, correlationId = null } = {}) {
        super(message);
        this.name = 'ApiError';
        this.code = code;
        this.status = status;
        this.correlationId = correlationId;
    }
}
```

- `code` — стабильный код для `switch` (`validation_failed`, `not_found`, `unauthorized`);
- `status` — HTTP‑статус;
- `correlationId` — для поиска в логах.

### 8.3. Маршрутизация по статусу

```js
if (response.ok) {
    return { data: await parseBody(response), status: response.status, correlationId };
}

if (response.status === 401 || response.status === 403) {
    throw new ApiError(body?.message || 'Нет доступа', {
        code: response.status === 401 ? 'unauthorized' : 'forbidden',
        status: response.status,
        correlationId
    });
}

if (response.status >= 400 && response.status < 500) {
    throw new ApiError(body?.message || `Ошибка ${response.status}`, {
        code: body?.code || 'client_error',
        status: response.status,
        correlationId
    });
}

throw new ApiError(body?.message || 'Ошибка сервера', {
    code: body?.code || 'server_error',
    status: response.status,
    correlationId
});
```

- 2xx — успех;
- 401/403 — специальные коды для ре‑аутентификации;
- 400–499 — ошибки клиента (повторять бессмысленно);
- 5xx — ошибки сервера (можно повторить с backoff).

### 8.4. Парсер тела

```js
async function parseBody(response) {
    const ct = response.headers.get('content-type') || '';

    if (ct.includes('application/json')) {
        try { return await response.json(); }
        catch { return null; }
    }

    const text = await response.text().catch(() => '');
    return text ? { message: text.slice(0, 500) } : null;
}
```

Почему не `response.json()` вслепую: на пути может стоять nginx, антивирус‑прокси или CDN, которые вернут HTML вместо JSON. Клиент не должен на этом падать.

### 8.5. Загрузка через XHR

`fetch` не умеет upload progress в большинстве браузеров. Для прогресса используется `XMLHttpRequest`:

```js
xhr.upload.onprogress = (e) => {
    if (e.lengthComputable && opts.onProgress) {
        opts.onProgress(Math.round((e.loaded / e.total) * 100));
    }
};
```

Это единственная причина использовать XHR вместо fetch. Всё остальное (заголовки, ошибки, abort) делается так же.

### 8.6. Скачивание

```js
const cd = response.headers.get('content-disposition') || '';
const match = /filename\*=UTF-8''([^;]+)/i.exec(cd);
const filename = match ? decodeURIComponent(match[1]) : id;
```

Имя файла извлекается из `Content-Disposition` (RFC 5987). Если заголовка нет — используется id.

`URL.createObjectURL(blob)` + программный клик по ссылке + `URL.revokeObjectURL` — стандартный способ скачать Blob.

---

## 9. Унифицированные ошибки API

```csharp
public record ApiError(string Code, string Message, string? CorrelationId = null);
```

Принципы:

- **не возвращать внутренние исключения** — только код и безопасное сообщение;
- **детали — в логах** по correlation id;
- **стабильные коды** — клиент может на них реагировать (`validation_failed`, `not_found`, `no_file`).

`ProblemDetails` (RFC 7807) — стандартный формат для API:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "validation_failed",
  "status": 400,
  "detail": "Расширение '.exe' не разрешено",
  "instance": "/api/files"
}
```

В MVC обычно используется `ApiError`, в API — `ProblemDetails`. Оба подхода допустимы, главное — единообразие.

---

## 10. Сводка: что важно запомнить

**Middleware:**

- порядок критичен: логирование и заголовки — до роутинга, аутентификация — до авторизации;
- middleware может прервать конвейер, не вызывая `next()`;
- код после `next()` — на обратном пути;
- `OnStarting` — правильное место для заголовков, которые не должны быть перезаписаны.

**Аутентификация:**

- `NoResult` vs `Fail` — принципиально разные результаты;
- роли из ключа — принцип минимальных привилегий;
- сравнение ключей `==` — учебное упрощение, в продакшене нужно constant‑time.

**Заголовки:**

- CSP — главная защита от XSS, но `'unsafe-inline'` её обнуляет;
- `nosniff` — защита от MIME‑confusion;
- `X-Frame-Options: DENY` — защита от clickjacking;
- `Content-Disposition: attachment` — защита от XSS через скачанный файл.

**Валидация файлов:**

- defense in depth: расширение + сигнатура + размер;
- сигнатура — авторитетная, расширение и MIME — нет;
- лимиты на нескольких уровнях: Kestrel + атрибуты + валидатор.

**Хранение:**

- случайный id вместо имени — защита от path traversal и угадывания;
- хранение вне webroot — доступ только через API;
- строгая проверка формата id при чтении.

**Трассировка:**

- `X-Request-Id` в запросе и ответе;
- лог‑scope с `RequestId`;
- correlation id в ошибках для поддержки.

**Модель угроз:**

- STRIDE — шесть классов угроз;
- trust boundaries — где нужен контроль;
- backlog — каждая угроза либо устранена, либо явно принята.

**Тонкий клиент:**

- ошибки — результат, а не исключение;
- классификация по видам (`Network`, `Timeout`, `Http`, `Parse`, `Canceled`);
- не парсить тело вслепую — проверять `Content-Type`;
- XHR только для upload progress.

---

## 11. Частые ошибки

- **Middleware в неправильном порядке** — аутентификация после авторизации, статика после MVC, логирование после обработчика исключений.
- **`Fail` вместо `NoResult`** на отсутствие заголовка — ломает другие схемы аутентификации.
- **Сравнение ключей `==`** — уязвимость к атакам по времени.
- **`'unsafe-inline'` в CSP продакшена** — обнуляет защиту от XSS.
- **Нет `Content-Disposition: attachment`** — браузер откроет загруженный HTML и выполнит его.
- **Проверка только расширения** — злоумышленник переименует `.exe` в `.jpg`.
- **Чтение сигнатуры без возврата `Position = 0`** — файл сохранится обрезанным.
- **Оригинальное имя на диске** — path traversal.
- **Хранение файлов в webroot** — прямой доступ без авторизации.
- **Нет проверки формата id** — path traversal через id.
- **Нет correlation id** — разбор инцидента превращается в археологию.
- **`response.json()` вслепую** — падение на HTML от прокси.
- **Возврат внутренних исключений клиенту** — утечка информации о системе.
- **Одна модель угроз «на полке»** — устаревает и не используется. Модель должна жить в репозитории и обновляться вместе с кодом.