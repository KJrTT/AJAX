# Конспект: HTTP‑семантика, привязка моделей, DTO, валидация и Problem Details

Материал делится на пять тем:

1. HTTP‑семантика: методы, ETag, идемпотентность, антипаттерны.
    
2. Model binding: источники, атрибуты, кастомные биндеры.
    
3. DTO и маппинг.
    
4. Валидация: атрибуты, кастомные валидаторы, вложенные объекты, FluentValidation.
    
5. Ошибки: Problem Details, кастомизация, глобальная обработка исключений.
    

---

# Часть 1. HTTP‑семантика

## 1.1. Кэширование через `ETag` и `If-None-Match`

http

# Первый запрос
GET /users/1
→ 200 OK
ETag: "abc123"
# Второй запрос с тем же ETag
GET /users/1
If-None-Match: "abc123"
→ 304 Not Modified

Смысл: клиент говорит «у меня есть версия `abc123`, дай новую, только если она другая». Если версия та же — сервер отвечает `304` без тела. Экономия трафика и времени.

`304` — это не ошибка, а сигнал «используй закэшированную версию».

## 1.2. Оптимистичная блокировка через `ETag` и `If-Match`

http

# Клиент A читает ресурс
GET /doc/1 → ETag: "v5"
# Клиент B тоже читает → ETag: "v5"
# Клиент A обновляет
PUT /doc/1
If-Match: "v5"
→ 200 OK
# Клиент B пытается обновить с устаревшим ETag
PUT /doc/1
If-Match: "v5" (но сейчас уже "v6")
→ 412 Precondition Failed

Смысл: клиент говорит «примени изменение, только если текущая версия — `v5`». Если кто‑то уже изменил ресурс — `412`, клиент должен перечитать и повторить. Это защита от lost update.

## 1.3. Идемпотентность

`Idempotency-Key` — заголовок для `POST`/`PATCH`, который делает неидемпотентные операции идемпотентными. Ключ хранится на сервере (обычно 24 часа): повторный запрос с тем же ключом возвращает закэшированный ответ, а не выполняет операцию заново.

http

POST /payments
Idempotency-Key: <uuid>
{"amount": 100, "currency": "RUB"}
→ 201 Created
{"id": "pay_1", "amount": 100, "currency": "RUB"}

Повтор с тем же ключом вернёт тот же ответ, а не спишет деньги дважды.

## 1.4. Идемпотентность методов HTTP

|Метод|Идемпотентен|Безопасен|Смысл|
|---|---|---|---|
|`GET`|Да|Да|Только чтение|
|`HEAD`|Да|Да|Заголовки без тела|
|`POST`|**Нет**|Нет|Создание, неидемпотентная операция|
|`PUT`|Да|Нет|Полная замена|
|`PATCH`|**Не гарантируется**|Нет|Частичное изменение|
|`DELETE`|Да|Нет|Удаление|

`DELETE` идемпотентен: повторное удаление не меняет состояние.

http

DELETE /users/1
→ 204 No Content (удалено)
DELETE /users/1
→ 404 Not Found (уже удалено, но состояние тоже — удалено)

`404` на повторный `DELETE` — нормально: ресурса нет, состояние то же. Некоторые API возвращают `204` и на повтор — тоже допустимо.

## 1.5. REST‑маршруты

text

GET    /users        — список
GET    /users/1      — один
POST   /users        — создать (201 + Location)
PUT    /users/1      — заменить полностью
PATCH  /users/1      — изменить частично
DELETE /users/1      — удалить

## 1.6. Антипаттерны

- **`GET /deleteUser?id=1`** — `GET` не должен менять состояние. Поисковые роботы, префетчеры, прокси могут выполнить `GET` без ведома пользователя.
    
- **`POST /getUsers`** — `POST` для чтения. Ломает кэширование, семантику, инструменты.
    
- **`200 OK` с `{"error": "..."}`** — ошибка должна быть в HTTP‑статусе, а не в теле с успешным кодом. Клиенты и прокси не увидят ошибку.
    
- **`401` вместо `403`** — `401 Unauthorized` значит «не аутентифицирован» (нет/невалидный токен), `403 Forbidden` — «аутентифицирован, но нет прав». Путаница ломает клиентскую логику (редирект на логин вместо сообщения «нет доступа»).
    

---

# Часть 2. Model binding

## 2.1. Что такое model binding

Model binding — механизм, который берёт данные из HTTP‑запроса и превращает их в параметры метода контроллера. Работает в несколько шагов:

1. **Binder info** — определяется источник данных (`source`), имя (`name`), биндер.
    
2. **Поиск** — биндер ищет значение в соответствующем источнике.
    
3. **Не нашёл** — ошибка в `ModelState`.
    
4. **Парсинг** — строка превращается в нужный тип (`int`, `DateTime` и т. п.).
    
5. **Ошибка парсинга** — тоже в `ModelState`.
    

## 2.2. Источники данных

|Источник|Атрибут|Пример|
|---|---|---|
|Route|`[FromRoute]`|`/products/42`|
|Query string|`[FromQuery]`|`/products?page=2`|
|Form|`[FromForm]`|`application/x-www-form-urlencoded`, `multipart/form-data`|
|Body|`[FromBody]`|JSON, XML|
|Header|`[FromHeader]`|`X-Api-Key: abc`|
|Services|`[FromServices]`|DI‑контейнер|
|Keyed services|`[FromKeyedServices]`|DI с ключом|

Простые типы (`int`, `string`, `bool`, `DateTime`) по умолчанию берутся из route/query/form. Сложные (`class`, `record`) — из body.

В `[ApiController]` `[FromBody]` подразумевается для сложных типов автоматически.

## 2.3. Комбинирование источников

csharp

[HttpPost("{categoryId}/products")]
public IActionResult Create(
    [FromRoute] int categoryId,
    [FromQuery] bool notify,
    [FromBody] CreateProductRequest request,
    [FromHeader(Name = "X-Request-Id")] string requestId)
{
    ...
}

Один метод может брать данные из разных источников. Каждый параметр явно помечен — это делает контракт понятным.

## 2.4. Специальные атрибуты

- **`[BindRequired]`** — если значение не найдено, в `ModelState` добавляется ошибка. Полезно, когда поле обязательно, но не имеет `[Required]` (например, `int` не может быть `null`, и без `[BindRequired]` отсутствие значения даст `0`).
    
- **`[BindNever]`** — свойство игнорируется биндером. Критическая защита от mass assignment:
    
    csharp
    
    public class UserDto
    {
        public string Name { get; set; }
        [BindNever]
        public bool IsAdmin { get; set; }   // клиент не сможет прислать true
    }
    
    Без `[BindNever]` клиент мог бы прислать `{"name": "x", "isAdmin": true}` и получить админские права.
    
- **`[ModelBinder(typeof(MyBinder))]`** — свой биндер для конкретного параметра.
    
- **`[FromServices]`** — сервис из DI в параметр метода.
    

## 2.5. Кастомный биндер

csharp

public class CsvIntArrayBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var raw = ctx.ValueProvider.GetValue(ctx.ModelName).FirstValue;
        if (string.IsNullOrEmpty(raw))
        {
            ctx.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }
        var arr = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(int.Parse)
                     .ToArray();
        ctx.Result = ModelBindingResult.Success(arr);
        return Task.CompletedTask;
    }
}
[HttpGet("by-ids")]
public IActionResult Get([ModelBinder(typeof(CsvIntArrayBinder))] int[] ids) => Ok(ids);

Разбор:

- **`ctx.ValueProvider.GetValue(ctx.ModelName)`** — получить сырое значение;
    
- **`ModelBindingResult.Failed()`** — биндер не смог;
    
- **`ModelBindingResult.Success(value)`** — успех;
    
- **`int.Parse`** — если строка не число, бросит исключение. Лучше `int.TryParse` и `Failed()`:
    
    csharp
    
    if (!int.TryParse(part, out var n))
    {
        ctx.ModelState.AddModelError(ctx.ModelName, "Некорректное число");
        ctx.Result = ModelBindingResult.Failed();
        return Task.CompletedTask;
    }
    

Применение: CSV в query (`?ids=1,2,3`), base64 в заголовке, кастомные форматы дат.

## 2.6. ModelState

После биндинга и валидации все ошибки собираются в `ModelState`. Проверка:

csharp

if (!ModelState.IsValid)
    return BadRequest(ModelState);

В `[ApiController]` эта проверка делается автоматически: невалидная модель → `400` с `ValidationProblemDetails`. Явная проверка нужна, если хочется кастомной обработки.

## 2.7. Локализация парсинга

csharp

builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.SetDefaultCulture("en-US");
    o.SupportedCultures = new[] { new CultureInfo("en-US") };
});

Важно: парсинг `DateTime` и чисел зависит от культуры. `1.5` в `en-US` — это `1.5`, а в `ru-RU` — ошибка (там запятая). Явная настройка культуры избавляет от неожиданностей.

---

# Часть 3. DTO и маппинг

## 3.1. Зачем DTO

Доменная модель (`User`) содержит всё: `Id`, `Name`, `Role`, служебные поля. Клиенту нужно не всё, и не всё можно ему показывать.

csharp

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }   // ← клиент пришлёт "Admin"
}

Если использовать доменную модель как входную, клиент может прислать `Role: "Admin"` и повысить себе права. DTO решает это:

- **Request DTO** — что клиент присылает (только разрешённые поля);
    
- **Response DTO** — что сервер отдаёт (без внутренних полей);
    
- **Query DTO** — параметры фильтрации/пагинации.
    

## 3.2. `Optional<T>` для PATCH

В `PUT` клиент присылает полный объект. В `PATCH` — только изменяемые поля. Проблема: как отличить «поле не передано» от «поле передано со значением `null`»?

`Optional<T>` — обёртка с двумя состояниями:

csharp

public readonly struct Optional<T>
{
    public bool HasValue { get; }
    public T? Value { get; }
    public static Optional<T> None => default;
    public static Optional<T> Some(T value) => new(value);
}

В `PatchRequest`:

csharp

public class PatchUserRequest
{
    public Optional<string> Name { get; set; }
    public Optional<string?> Email { get; set; }
}

- `Name` не передан → `Optional.None` → не менять;
    
- `Name = "x"` → `Optional.Some("x")` → изменить на «x»;
    
- `Email = null` → `Optional.Some(null)` → очистить.
    

Без `Optional` пришлось бы использовать `string?` и не иметь возможности отличить «не передано» от «очистить».

## 3.3. Маппинг

csharp

public static ProductResponse ToResponse(this Product p) =>
    new(p.Id, p.Name, p.Price, p.Category.Name);

Расширение‑метод — простой и явный маппинг. Альтернативы:

- **AutoMapper** — `CreateMap<Product, ProductResponse>()`, маппинг по соглашению. Удобно, но скрывает логику и усложняет отладку;
    
- **ручной маппинг** — явно, предсказуемо, легко тестировать. Для небольших проектов лучше.
    

Главное правило: **доменная модель не должна утекать наружу**. DTO — это контракт, который меняется независимо от домена.

---

# Часть 4. Валидация

## 4.1. Атрибуты валидации

|Атрибут|Что делает|
|---|---|
|`[Required]`|Значение обязательно (не `null`, не пустая строка для `string`)|
|`[StringLength(max, MinimumLength = min)]`|Длина строки|
|`[MinLength]`, `[MaxLength]`|Для коллекций/строк|
|`[Range(min, max)]`|Числовой/датовый диапазон|
|`[EmailAddress]`|Формат email|
|`[Phone]`|Формат телефона|
|`[Url]`|Формат URL|
|`[RegularExpression(pattern)]`|Regex|
|`[Compare(otherProp)]`|Совпадение с другим свойством (пароль/подтверждение)|
|`[CreditCard]`|Алгоритм Луна|
|`[EnumDataType]`|Значение принадлежит enum|
|`[Remote]`|AJAX‑валидация на клиенте (MVC)|

Пример:

csharp

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Нужны буквы и цифры")]
    public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
    [Range(18, 120)]
    public int Age { get; set; }
}

Все ошибки после валидации попадают в `ModelState`.

## 4.2. Кастомный атрибут

csharp

public class NotInFutureAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is DateTime dt && dt > DateTime.UtcNow)
            return new ValidationResult(
                ErrorMessage ?? "Дата не может быть в будущем",
                new[] { ctx.MemberName! });
        return ValidationResult.Success;
    }
}
public class EventRequest
{
    [NotInFuture]
    public DateTime Date { get; set; }
}

- **`ValidationContext`** — контекст: имя свойства (`MemberName`), объект, сервисы;
    
- **`ValidationResult.Success`** — валидно;
    
- **`ValidationResult(error, memberNames)`** — ошибка с указанием полей.
    

## 4.3. `IValidatableObject` для кросс‑полевой валидации

csharp

public class DateRangeRequest : IValidatableObject
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (To <= From)
            yield return new ValidationResult(
                "To должно быть больше From",
                new[] { nameof(To) });
        if ((To - From).TotalDays > 365)
            yield return new ValidationResult(
                "Диапазон не должен превышать год",
                new[] { nameof(From), nameof(To) });
    }
}

Когда нужно: правило зависит от нескольких полей (`From` < `To`), атрибут на одно поле это не выразит.

## 4.4. Вложенные объекты

csharp

public class OrderRequest
{
    [Required]
    public CustomerDto Customer { get; set; } = new();
    [MinLength(1)]
    public List<OrderItemDto> Items { get; set; } = new();
}

Валидация вложенных объектов работает автоматически: атрибуты внутри `CustomerDto` и `OrderItemDto` тоже проверяются. Ошибки попадают в `ModelState` с путями вида `Customer.Name`, `Items[0].Quantity`.

## 4.5. FluentValidation

Атрибуты удобны для простых правил, но для сложной логики (условные правила, зависимости, кастомные сообщения) лучше FluentValidation.

csharp

public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя обязательно")
            .MinimumLength(3)
            .MaximumLength(100);
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThan(1_000_000);
        RuleFor(x => x.CategoryId)
            .GreaterThan(0);
        When(x => x.Price > 10000, () =>
        {
            RuleFor(x => x.Name).MinimumLength(10)
                .WithMessage("Дорогие товары требуют длинного названия");
        });
    }
}
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();
builder.Services.AddFluentValidationAutoValidation();

Плюсы:

- **условные правила** (`When`);
    
- **цепочки** с понятными сообщениями;
    
- **тестируемость** — валидатор можно протестировать отдельно;
    
- **разделение** — валидатор рядом с DTO, но не внутри.
    

Минусы:

- **отдельный пакет**;
    
- **сложнее для простых случаев** — атрибуты короче.
    

`AddFluentValidationAutoValidation` встраивает FluentValidation в MVC pipeline: ошибки попадают в `ModelState` так же, как от атрибутов.

---

# Часть 5. Ошибки и Problem Details

## 5.1. Проблема: разные форматы ошибок

Без стандарта каждый эндпоинт отвечает по‑своему: `{"error": "..."}`, `{"message": "..."}`, просто строка. Клиенту приходится парсить всё это по‑разному.

Решение — **Problem Details (RFC 7807)**. Стандартный формат для ошибок API.

## 5.2. Структура Problem Details

json

{
  "type": "https://example.com/errors/not-found",
  "title": "Product not found",
  "status": 404,
  "detail": "Product with id 42 does not exist",
  "instance": "/api/products/42"
}

|Поле|Тип|Описание|
|---|---|---|
|`type`|string (URI)|Ссылка на документацию типа ошибки. По умолчанию `about:blank`|
|`title`|string|Краткое человекочитаемое описание|
|`status`|int|HTTP‑статус|
|`detail`|string|Детальное описание конкретного случая|
|`instance`|string (URI)|URI конкретного запроса (обычно path)|
|`extensions`|любые|Доп. поля: `traceId`, `timestamp`, `errors` и т. д.|

## 5.3. Валидационные ошибки

json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["Имя обязательно"],
    "Price": ["Price must be between 0.01 and 1000000"]
  },
  "traceId": "00-abc..."
}

`errors` — словарь «поле → список сообщений». Это `ValidationProblemDetails`, подтип `ProblemDetails`.

## 5.4. Автоматизация через `[ApiController]`

`[ApiController]` автоматически:

- проверяет `ModelState` и возвращает `400` с `ValidationProblemDetails`;
    
- обрабатывает `ProblemDetails` для стандартных статусов;
    
- биндит сложные типы из body.
    

Без `[ApiController]` всё это нужно делать вручную.

## 5.5. Возврат Problem Details

**Через `Problem(...)`:**

csharp

return Problem(
    title: "Product not found",
    detail: $"Product with id {id} does not exist",
    statusCode: StatusCodes.Status404NotFound,
    type: "https://example.com/errors/not-found");

**Через объект `ProblemDetails`:**

csharp

var pd = new ProblemDetails
{
    Status = 409,
    Title = "Conflict",
    Detail = "Product with this name already exists",
    Instance = HttpContext.Request.Path
};
pd.Extensions["traceId"] = HttpContext.TraceIdentifier;
return new ObjectResult(pd) { StatusCode = 409 };

**Через `ValidationProblem(ModelState)`:**

csharp

ModelState.AddModelError("Name", "Имя уже занято");
return ValidationProblem(ModelState);

## 5.6. `AddProblemDetails()`

csharp

builder.Services.AddProblemDetails();

Автоматически превращает стандартные ошибки (401, 403, 404, 500) в `ProblemDetails`. Без этого, например, `404` от маршрутизации вернётся пустым.

## 5.7. Кастомизация через `ProblemDetailsFactory`

csharp

public class CustomProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly IHostEnvironment _env;
    public CustomProblemDetailsFactory(IHostEnvironment env) => _env = env;
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext, int? statusCode = null,
        string? title = null, string? type = null,
        string? detail = null, string? instance = null)
    {
        var pd = new ProblemDetails
        {
            Status = statusCode ?? 500,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };
        Enrich(pd, httpContext);
        return pd;
    }
    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext, ModelStateDictionary modelStateDictionary,
        int? statusCode = null, string? title = null, string? type = null,
        string? detail = null, string? instance = null)
    {
        var vpd = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode ?? 400,
            Title = title ?? "Validation failed",
            Type = type,
            Detail = detail,
            Instance = instance
        };
        Enrich(vpd, httpContext);
        return vpd;
    }
    private void Enrich(ProblemDetails pd, HttpContext ctx)
    {
        pd.Extensions["traceId"] = ctx.TraceIdentifier;
        pd.Extensions["timestamp"] = DateTime.UtcNow;
        if (_env.IsDevelopment())
            pd.Extensions["environment"] = "Development";
    }
}
builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

Что даёт:

- **единое обогащение** всех ошибок (`traceId`, `timestamp`);
    
- **разное поведение** в Development и Production (например, стектрейс только в Development);
    
- **централизованная логика** вместо копирования в каждом контроллере.
    

## 5.8. Глобальная обработка исключений

csharp

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Unhandled exception");
        var (status, title) = ex switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };
        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = ex.Message,
            Instance = ctx.Request.Path
        };
        pd.Extensions["traceId"] = ctx.TraceIdentifier;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(pd, ct);
        return true;
    }
}
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();

Разбор:

- **`IExceptionHandler`** — новый интерфейс в .NET 8+;
    
- **`TryHandleAsync`** — возвращает `true`, если исключение обработано;
    
- **маппинг исключений в статусы** — `KeyNotFoundException` → 404, `ArgumentException` → 400, `UnauthorizedAccessException` → 403, остальное → 500;
    
- **логирование** — всегда, до формирования ответа;
    
- **`Detail = ex.Message`** — в продакшене это может утекать внутренние детали. Лучше в Production возвращать общее сообщение, а детали писать в лог;
    
- **`application/problem+json`** — правильный Content-Type для Problem Details;
    
- **`UseExceptionHandler()`** — middleware, который вызывает зарегистрированные `IExceptionHandler`.
    

## 5.9. Что важно понять про обработку ошибок

- **HTTP‑статус — часть контракта**. `200 OK` с `{"error": "..."}` — антипаттерн;
    
- **`401` vs `403`** — «не аутентифицирован» vs «нет прав». Путаница ломает клиентскую логику;
    
- **Problem Details — стандарт** — клиенты и инструменты понимают его без документации;
    
- **`[ApiController]`** автоматизирует валидацию и Problem Details;
    
- **кастомизация через `ProblemDetailsFactory`** — единое обогащение ошибок;
    
- **глобальная обработка исключений** — последняя линия защиты; необработанное исключение не должно утекать клиенту;
    
- **не возвращать внутренние детали** в продакшене — только `traceId` для поиска в логах.
    

---

# Сводка: что выбирать

|Задача|Решение|
|---|---|
|Кэширование GET|`ETag` + `If-None-Match` → `304`|
|Оптимистичная блокировка|`ETag` + `If-Match` → `412`|
|Идемпотентный POST|`Idempotency-Key` + кэш на сервере|
|Защита от mass assignment|`[BindNever]` на чувствительных полях|
|Обязательное поле без `[Required]`|`[BindRequired]`|
|Кастомный формат в query/header|`IModelBinder`|
|Разделение вход/выход|Request/Response/Query DTO|
|Частичное обновление|`Optional<T>` для PATCH|
|Простая валидация|Атрибуты (`[Required]`, `[Range]`, ...)|
|Кросс‑полевая валидация|`IValidatableObject`|
|Сложные правила|FluentValidation|
|Стандартные ошибки API|Problem Details|
|Кастомизация ошибок|`ProblemDetailsFactory`|
|Необработанные исключения|`IExceptionHandler`|

---

# Частые ошибки

**HTTP:**

- **`GET` с побочными эффектами** — префетчеры и роботы выполнят его без ведома пользователя;
    
- **`POST` для чтения** — ломает кэширование и семантику;
    
- **`200` с ошибкой в теле** — клиенты и прокси не увидят ошибку;
    
- **`401` вместо `403`** — клиент уйдёт на редирект логина вместо сообщения «нет доступа»;
    
- **`ETag` без кавычек** — невалидно;
    
- **`412` без перечитывания** — клиент затирает чужие изменения.
    

**Binding:**

- **нет `[BindNever]`** на чувствительных полях — mass assignment;
    
- **`int.Parse` в кастомном биндере** без `TryParse` — исключение вместо ошибки в `ModelState`;
    
- **нет настройки культуры** — `DateTime` парсится по‑разному в разных локалях.
    

**DTO:**

- **доменная модель как вход** — клиент управляет полями, которыми не должен;
    
- **нет `Optional<T>`** в PATCH — нельзя отличить «не передано» от «очистить».
    

**Валидация:**

- **только клиентская валидация** — обходится тривиально;
    
- **кросс‑полевые правила в атрибутах** — невозможно выразить;
    
- **`[Required]` на `int`** — бессмысленно, `int` не бывает `null`; нужен `int?` или `[Range]`.
    

**Ошибки:**

- **возврат `ex.Message` в продакшене** — утечка внутренних деталей;
    
- **нет `traceId`** — невозможно найти инцидент в логах;
    
- **нет `AddProblemDetails()`** — стандартные ошибки (404, 500) возвращаются пустыми;
    
- **нет глобального обработчика** — необработанное исключение утекает клиенту как HTML или пустой 500.