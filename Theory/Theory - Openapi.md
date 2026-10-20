# Конспект: OpenAPI и Swagger

## 1. Что такое OpenAPI

OpenAPI — это спецификация (стандарт) описания REST API. Она позволяет и людям, и компьютерам понимать возможности API без доступа к исходному коду.

Спецификация обычно представлена в JSON или YAML (файл `openapi.json`). На её основе работают инструменты:

- Swagger UI — интерактивная документация;
- генераторы клиентов — код на C#, TypeScript и других языках;
- генераторы серверных заглушек;
- средства тестирования;
- шлюзы API (API Gateway).

Ключевая идея: **документ — это контракт**. Если он есть, клиент и сервер могут развиваться независимо, а инструменты автоматически генерируют код и проверки.

## 2. Swagger и OpenAPI — в чём разница

- **OpenAPI** — спецификация, стандарт. Описывает, как должен выглядеть документ.
- **Swagger** — семейство инструментов, которые работают с этой спецификацией: Swagger UI, Swagger Editor, Swagger Codegen.

Исторически спецификация называлась Swagger, потом её переименовали в OpenAPI. Swagger остался как название инструментов. Поэтому в коде вы видите `AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI`, хотя речь идёт об OpenAPI.

## 3. Инструменты для .NET

| Инструмент | Назначение |
|------------|------------|
| Swashbuckle | Генерация OpenAPI-документа и Swagger UI. Классический выбор для .NET 8 и ниже |
| NSwag | Генерация OpenAPI-документа, Swagger UI и клиентского кода на C#/TypeScript |
| Встроенный OpenAPI (.NET 9+) | Генерация документа без сторонних пакетов. Swagger UI подключается отдельно |

Swashbuckle и NSwag конкурируют, но решают похожие задачи. Swashbuckle проще, NSwag мощнее (особенно в генерации клиентов). Встроенная поддержка в .NET 9+ убирает зависимость от сторонних пакетов для генерации документа, но UI всё равно подключается отдельно.

## 4. Регистрация Swagger

```csharp
builder.Services.AddSwaggerGen();
```

Это минимальная регистрация. Дальше в конвейере:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

- `UseSwagger()` — отдаёт `swagger.json` по адресу `/swagger/v1/swagger.json`;
- `UseSwaggerUI()` — отдаёт интерактивную страницу `/swagger`.

Оборачивать в `IsDevelopment()` — стандартная практика: в продакшене документация часто не нужна или должна быть закрыта.

## 5. Метаданные API (`OpenApiInfo`)

```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Todo Management API",
        Version = "v1",
        Description = "API для управления задачами. Поддерживает фильтрацию и создание.",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });
});
```

Поля `OpenApiInfo`:

| Поле | Назначение | Пример |
|------|------------|--------|
| `Title` | Название API в документации | «Todo Management API» |
| `Version` | Версия API (не версия спецификации) | «1.0.0» |
| `Description` | Подробное описание, поддерживает Markdown | «API для управления задачами» |
| `Contact` | Контактные данные команды | `new OpenApiContact { Name = "Support", Email = "support@example.com" }` |
| `License` | Условия использования | `new OpenApiLicense { Name = "MIT", Url = new Uri(...) }` |

Эти данные отображаются в Swagger UI и попадают в `swagger.json`. Важно: `Version` — это версия **API**, а не версия спецификации OpenAPI (последняя задаётся отдельно и обычно не меняется).

`Description` поддерживает Markdown — можно добавлять заголовки, списки, ссылки, примеры.

## 6. Обогащение документации

Минимальный Swagger описывает маршруты и схемы, но без пояснений. Чтобы документация была полезной, её обогащают.

### 6.1. XML-комментарии

В `.csproj`:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

В коде:

```csharp
/// <summary>
/// Получить список задач с фильтрацией.
/// </summary>
/// <param name="status">Фильтр по статусу.</param>
/// <returns>Список задач.</returns>
[HttpGet]
public IActionResult GetAll(string? status) { ... }
```

В Swashbuckle:

```csharp
var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
c.IncludeXmlComments(xmlPath);
```

Комментарии подхватываются и попадают в описание методов и параметров.

### 6.2. Атрибуты ответов

```csharp
[ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public IActionResult GetById(Guid id) { ... }
```

Атрибут `[ProducesResponseType]` явно указывает, какие коды ответов может вернуть метод, и какой тип тела у каждого. Без него Swagger покажет только `200 OK` с типом возвращаемого значения, а остальные коды будут не видны.

В .NET 10+ у атрибута появился параметр `Description` — можно добавить текстовое описание ответа.

### 6.3. Схемы и примеры

Swashbuckle генерирует схемы из типов автоматически. Можно обогатить их:

- `[Required]`, `[Range]`, `[StringLength]` — ограничения попадают в схему;
- `[DefaultValue]` — значение по умолчанию;
- `[Example]` — пример значения (в .NET 9+ есть атрибут `[Example]`).

Примеры полезны: клиент видит, как выглядит реальный запрос/ответ, а не только типы.

## 7. Безопасность и версионирование

### 7.1. Защита Swagger UI

По умолчанию документация доступна всем. Если API внутренний, её стоит закрыть:

```csharp
app.MapSwagger().RequireAuthorization();
```

Или обернуть в проверку окружения/роли:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### 7.2. Версионирование

Если у API несколько версий, Swashbuckle поддерживает отдельные документы:

```csharp
c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
c.SwaggerDoc("v2", new OpenApiInfo { Title = "API", Version = "v2" });
```

В UI появляется выпадающий список версий. Каждая версия — отдельный документ.

С версионированием через `Asp.Versioning` конфигуратор Swagger проходит по всем описанным версиям и создаёт документ на каждую.

### 7.3. Множественные документы (.NET 9+)

Встроенная поддержка позволяет генерировать несколько OpenAPI-документов:

```csharp
builder.Services.AddOpenApi("v1");
builder.Services.AddOpenApi("v2");
```

Это удобно, когда нужно разделить публичное и внутреннее API, или разные группы эндпоинтов.

## 8. Генерация клиентов

Одно из главных преимуществ OpenAPI — автоматическая генерация типизированных клиентов.

Инструменты:

- **NSwag** — генерация C#/TypeScript клиентов, MSBuild-пакеты, NSwagStudio;
- **OpenAPI Generator** — генерация клиентов на множестве языков;
- **Kiota** — генератор от Microsoft.

Что это даёт:

- не нужно вручную писать HTTP-запросы;
- клиент синхронизирован с сервером: изменилась схема — регенерировали клиент;
- типы и модели совпадают;
- меньше ошибок в рантайме.

Плата — зависимость от инструмента генерации и его версии. Иногда проще написать тонкий клиент вручную (как в конспекте про `HttpTransport`), особенно если API маленькое или меняется редко.

## 9. Что важно понять про OpenAPI

- **OpenAPI — контракт**, а Swagger — инструменты. Не путать спецификацию и реализацию.
- **Документ генерируется из кода** (через атрибуты, типы, XML-комментарии) или пишется вручную (design-first). Первый подход проще для существующего кода, второй — для контракт-first разработки.
- **Метаданные API** (`Title`, `Version`, `Description`, `Contact`, `License`) — не формальность: они попадают в документацию и видны клиентам.
- **`Version` в `OpenApiInfo` — версия API**, не версия спецификации.
- **`[ProducesResponseType]`** — обязательный атрибут для API, где важны коды ошибок. Без него Swagger покажет только успешный ответ.
- **XML-комментарии** — самый дешёвый способ обогатить документацию. Включаются одной строкой в `.csproj` и одной в `AddSwaggerGen`.
- **Swagger в продакшене** — часто не нужен. Закрывать через `IsDevelopment()` или авторизацию.
- **Генерация клиентов** — главная выгода OpenAPI для больших проектов. Для маленьких API ручной тонкий клиент может быть проще.
- **Встроенная поддержка в .NET 9+** убирает зависимость от сторонних пакетов для генерации документа, но UI всё равно подключается отдельно.

## 10. Частые ошибки

- **Swagger в продакшене без защиты** — раскрывает структуру API;
- **нет `[ProducesResponseType]`** — документация показывает только `200`, клиенты не знают про ошибки;
- **нет XML-комментариев** — документация есть, но бесполезная;
- **`Version` в `OpenApiInfo` путают с версией спецификации** — это версия API;
- **один документ на все версии API** — клиенты не понимают, какая версия актуальна;
- **генерация клиента без версионирования** — при breaking change клиент ломается молча;
- **`UseSwagger()` без `UseSwaggerUI()`** — документ есть, но UI нет (или наоборот);
- **`IncludeXmlComments` без `<GenerateDocumentationFile>`** — файла нет, исключение при старте;
- **ручное редактирование `swagger.json`** — при следующей генерации изменения потеряются.