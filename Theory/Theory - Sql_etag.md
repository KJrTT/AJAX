# Конспект: работа с БД (пагинация, сортировка, поиск) и оптимистичная блокировка через ETag

Материал делится на две большие темы:

1. **Запросы к БД**: пагинация, сортировка, поиск, безопасная динамическая сортировка, cursor‑пагинация.
2. **Оптимистичная блокировка**: ETag, условные заголовки, защита от lost update, обработка конфликтов.

---

# Часть 1. Работа с БД: пагинация, сортировка, поиск

## 1.1. Антипаттерн: сортировка через `if/else`

```csharp
// ❌ АНТИПРИМЕР
public IActionResult Index(string sort, string q, int page = 1)
{
    var query = _db.Users.AsQueryable();

    if (!string.IsNullOrEmpty(q))
        query = query.Where(u => u.Name.Contains(q));

    if (sort == "name")
        query = query.OrderBy(u => u.Name);
    else if (sort == "name_desc")
        query = query.OrderByDescending(u => u.Name);
    else if (sort == "email")
        query = query.OrderBy(u => u.Email);
    // ещё 20 полей → ещё 40 веток

    int pageSize = 10;
    var users = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    return View(users);
}
```

Проблемы:

- **комбинаторный взрыв**: 20 полей × 2 направления = 40 веток `if/else`;
- **невозможность множественной сортировки**: «сначала по имени, потом по дате» — не выразить;
- **нет валидации**: клиент может передать `sort=password` — и сервер отсортирует по секретному полю (утечка через порядок);
- **`page` без ограничений**: `page=0` или `page=-100` даст отрицательный `Skip`, а `page=999999` — лишний запрос;
- **`Contains` без ограничений**: пользователь может передать огромную строку поиска.

## 1.2. DTO для параметров запроса

```csharp
public class QueryDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be >= 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Limit must be 1..100")]
    public int Limit { get; set; } = 10;

    // "name:asc,createdAt:desc"
    public string? Sort { get; set; }

    [StringLength(200)]
    public string? Q { get; set; }
}
```

Что даёт DTO:

- **валидация через атрибуты**: `[Range]` не пропустит `page=0` или `limit=1000`;
- **явные значения по умолчанию**: `page=1`, `limit=10`;
- **ограничение длины поиска**: `[StringLength(200)]` защищает от огромных строк;
- **стандартный формат сортировки**: `"поле:направление,поле:направление"`.

Валидация срабатывает автоматически благодаря `[ApiController]` или явной проверке `ModelState`.

## 1.3. Разбор параметров в `QueryObject`

```csharp
public enum SortDirection { Asc, Desc }
public record SortRule(string Field, SortDirection Direction);

public class QueryObject
{
    public int Skip { get; }
    public int Take { get; }
    public int Page { get; }
    public int Limit { get; }
    public string? Search { get; }
    public IReadOnlyList<SortRule> Sorts { get; }

    public QueryObject(QueryDto dto, IEnumerable<string> allowedSortFields)
    {
        Page = dto.Page < 1 ? 1 : dto.Page;
        Limit = Math.Clamp(dto.Limit, 1, 100);
        Skip = (Page - 1) * Limit;
        Take = Limit;
        Search = string.IsNullOrWhiteSpace(dto.Q) ? null : dto.Q.Trim();
        Sorts = ParseSort(dto.Sort, allowedSortFields);
    }
    // ...
}
```

Ключевые моменты:

- **`Math.Clamp`** — двойная защита: даже если валидация пропущена, `Limit` будет в диапазоне 1..100;
- **`Skip` вычисляется один раз** — не в контроллере, не в репозитории, а в объекте запроса;
- **`Search` нормализуется**: пустая строка → `null`, иначе `Trim`;
- **`Sorts` — список правил**, а не одно поле. Это позволяет множественную сортировку.

## 1.4. Безопасный парсинг сортировки

```csharp
private static IReadOnlyList<SortRule> ParseSort(
    string? sort, IEnumerable<string> allowed)
{
    if (string.IsNullOrWhiteSpace(sort))
        return new[] { new SortRule("CreatedAt", SortDirection.Desc) };

    var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var result = new List<SortRule>();

    foreach (var part in sort.Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var bits = part.Split(':', 2);
        var field = bits[0].Trim();
        var dir = bits.Length > 1 && bits[1].Equals("asc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Asc
                    : SortDirection.Desc;

        if (!allowedSet.Contains(field))
            throw new ArgumentException($"Sort field '{field}' not allowed.");

        result.Add(new SortRule(field, dir));
    }
    return result;
}
```

Что здесь важно:

- **whitelist полей** (`allowed`) — критическая защита. Клиент не может сортировать по произвольному полю (например, `password` или `internalFlag`). Иначе через порядок можно утекать данные;
- **формат `"field:asc,field2:desc"`** — множественная сортировка в одной строке;
- **`StringComparer.OrdinalIgnoreCase`** — регистронезависимое сравнение полей;
- **по умолчанию — `CreatedAt DESC`** — свежие записи сверху;
- **исключение на неизвестное поле** — лучше упасть с понятной ошибкой, чем молча проигнорировать.

**Альтернатива исключению** — вернуть `Result`/`BadRequest` с понятным сообщением. Исключение в конструкторе неудобно: его надо ловить в контроллере и превращать в 400.

## 1.5. Динамическая сортировка через Expression Trees

```csharp
public static IQueryable<T> ApplySort<T>(
    this IQueryable<T> source, IEnumerable<SortRule> sorts)
{
    IOrderedQueryable<T>? ordered = null;

    foreach (var rule in sorts)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var prop = Expression.PropertyOrField(param, rule.Field);
        var lambda = Expression.Lambda(prop, param);

        string method =
            ordered is null
                ? (rule.Direction == SortDirection.Asc ? "OrderBy" : "OrderByDescending")
                : (rule.Direction == SortDirection.Asc ? "ThenBy" : "ThenByDescending");

        var call = Expression.Call(
            typeof(Queryable), method,
            new[] { typeof(T), prop.Type },
            ordered?.Expression ?? source.Expression,
            Expression.Quote(lambda));

        ordered = (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
    }

    return ordered ?? source;
}
```

Разбор:

- **`Expression.Parameter`** — параметр `x` лямбды;
- **`Expression.PropertyOrField`** — доступ к свойству/полю по имени;
- **`Expression.Lambda`** — лямбда `x => x.Field`;
- **первый `OrderBy`/`OrderByDescending`**, последующие — `ThenBy`/`ThenByDescending` — это даёт множественную сортировку;
- **`Expression.Call`** — вызов метода `Queryable.OrderBy(source, lambda)`;
- **`Expression.Quote`** — оборачивает лямбду в `Expression<Func<...>>` (иначе EF не сможет её транслировать);
- **`CreateQuery<T>`** — строит `IQueryable` из выражения; EF транслирует его в SQL.

Почему через выражения, а не через рефлексию:

- **EF Core транслирует выражения в SQL**. Рефлексия (`PropertyInfo.GetValue`) выполнилась бы в памяти — пришлось бы вытащить все данные, отсортировать, потом пагинировать. Это катастрофа по производительности;
- **выражения — это данные**, а не скомпилированный код. EF анализирует их и строит SQL.

Ограничения:

- **только свойства/поля**, не вложенные пути (`Order.Customer.Name`). Для вложенных нужен `Expression.Property` по цепочке;
- **whitelist обязателен** — иначе клиент может отсортировать по навигации, которая не загружена, или по полю, которое не должно быть видно;
- **тип свойства** должен поддерживать сравнение (числа, строки, даты).

## 1.6. Репозиторий

```csharp
public class UserRepository
{
    private static readonly string[] AllowedSort =
        { "Id", "Name", "Email", "CreatedAt" };

    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public async Task<PagedResult<User>> FindManyAsync(
        QueryDto dto, CancellationToken ct = default)
    {
        var qo = new QueryObject(dto, AllowedSort);

        IQueryable<User> query = _db.Users.AsNoTracking();

        if (qo.Search is { } s)
            query = query.Where(u => u.Name.Contains(s) || u.Email.Contains(s));

        query = query.ApplySort(qo.Sorts);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip(qo.Skip)
            .Take(qo.Take)
            .ToListAsync(ct);

        return new PagedResult<User>(items, qo.Page, qo.Limit, total);
    }
}
```

Ключевые моменты:

- **`AllowedSort` — статический whitelist** — единый источник правды для допустимых полей;
- **`AsNoTracking`** — read‑only запрос, экономит память и CPU;
- **поиск через `||`** — ищет и по имени, и по email;
- **`CountAsync` до `Skip/Take`** — считает **общее** количество, а не страницу. Это нужно для метаданных пагинации;
- **`Skip`/`Take`** — EF транслирует в `OFFSET`/`FETCH` (или `LIMIT`/`OFFSET` в зависимости от провайдера);
- **порядок важен**: `Count` до `Skip/Take`, иначе посчитается только страница.

Обратите внимание: `CountAsync` выполняется **отдельным запросом**. Это два round‑trip к БД. Для больших таблиц `COUNT(*)` может быть дорогим — иногда его кэшируют или заменяют на приблизительное значение.

## 1.7. Метаданные пагинации

```csharp
public record PagedResult<T>(
    IReadOnlyList<T> Data,
    Meta Meta);

public record Meta(int Page, int Limit, int Total, int TotalPages, bool HasNext, bool HasPrev)
{
    public static Meta Create(int page, int limit, int total) => new(
        page,
        limit,
        total,
        (int)Math.Ceiling(total / (double)limit),
        page * limit < total,
        page > 1);
}
```

Пример ответа:

```json
{
  "data": [ /* ... */ ],
  "meta": {
    "page": 2, "limit": 10, "total": 143,
    "totalPages": 15, "hasNext": true, "hasPrev": true
  }
}
```

Зачем метаданные:

- **`total`** — общее количество записей;
- **`totalPages`** — сколько всего страниц;
- **`hasNext`/`hasPrev`** — можно ли листать дальше/назад. Клиент не вычисляет это сам — это ответственность сервера;
- **`page`/`limit`** — эхо параметров запроса (клиент может сверить).

`Math.Ceiling(total / (double)limit)` — округление вверх. Важно привести к `double`, иначе целочисленное деление даст неверный результат.

## 1.8. Cursor‑пагинация

Offset‑пагинация (`Skip`/`Take`) имеет проблему: при больших `OFFSET` БД всё равно читает и отбрасывает строки. Для миллионов записей это медленно. Кроме того, при вставке/удалении между запросами страницы «съезжают».

Cursor‑пагинация решает обе проблемы:

```csharp
public async Task<CursorPage<User>> FindWithCursorAsync(
    int? cursorId, int limit = 20, CancellationToken ct = default)
{
    var query = _db.Users
        .AsNoTracking()
        .OrderByDescending(u => u.Id);

    if (cursorId is { } id)
        query = (IOrderedQueryable<User>)query.Where(u => u.Id < id);

    var items = await query.Take(limit + 1).ToListAsync(ct);

    var hasNext = items.Count > limit;
    var data = hasNext ? items.Take(limit).ToList() : items;
    var next = hasNext ? data[^1].Id.ToString() : null;

    return new CursorPage<User>(data, next);
}
```

Как работает:

- **сортировка по уникальному полю** (`Id`) — иначе порядок нестабилен;
- **`WHERE Id < cursor`** — берём только записи после курсора. БД использует индекс, а не читает всё;
- **`Take(limit + 1)`** — берём на одну больше, чтобы узнать, есть ли следующая страница;
- **`hasNext`** — если записей больше `limit`, значит есть следующая;
- **`next`** — id последней записи на странице; клиент передаёт его в следующий запрос.

Плюсы:

- **быстро на больших объёмах** — индекс, а не `OFFSET`;
- **стабильно при вставках/удалениях** — курсор привязан к конкретной записи.

Минусы:

- **нельзя прыгнуть на произвольную страницу** — только «вперёд» (или «назад» с обратной сортировкой);
- **нужен уникальный стабильный ключ** сортировки;
- **сложнее с множественной сортировкой** — курсор должен включать все поля сортировки.

Когда использовать:

- **offset** — админки, небольшие объёмы, нужны номера страниц;
- **cursor** — лента, бесконечный скролл, большие объёмы, API для мобильных.

## 1.9. Что важно запомнить про запросы к БД

- **whitelist полей сортировки** — защита от утечки данных через порядок;
- **DTO с валидацией** — `Page`, `Limit`, `Sort`, `Q` с ограничениями;
- **`QueryObject`** — единая точка разбора параметров;
- **`Expression Trees`** — динамическая сортировка, транслируемая в SQL;
- **`Count` до `Skip/Take`** — для общего количества;
- **`AsNoTracking`** — для read‑only;
- **offset‑пагинация** — для небольших объёмов с номерами страниц;
- **cursor‑пагинация** — для больших объёмов и бесконечного скролла.

---

# Часть 2. Оптимистичная блокировка через ETag

## 2.1. Проблема lost update

Представьте: два пользователя (A и B) одновременно редактируют документ.

```csharp
// ❌ ПЛОХОЙ пример: без проверки версии
app.MapGet("/doc", () => Results.Ok(document));

app.MapPut("/doc", (Document incoming) =>
{
    document.Text = incoming.Text;
    document.Version++;
    return Results.Ok(document);
});
```

Сценарий:

```bash
# A и B читают документ — оба видят version=1
curl http://localhost:5000/doc        # {"text":"Исходный текст","version":1}
curl http://localhost:5000/doc        # {"text":"Исходный текст","version":1}

# A сохраняет
curl -X PUT http://localhost:5000/doc \
  -H 'Content-Type: application/json' \
  -d '{"text":"Правка от A"}'

# B сохраняет поверх — ничего не подозревая
curl -X PUT http://localhost:5000/doc \
  -H 'Content-Type: application/json' \
  -d '{"text":"Правка от B"}'

# Итог: правка A потеряна
curl http://localhost:5000/doc        # {"text":"Правка от B","version":3}
```

Проблема: сервер не проверяет, что клиент работал с **актуальной** версией. Последний запрос перезаписывает всё, что было до него.

Это называется **lost update** — классическая проблема конкурентного доступа.

## 2.2. Что такое ETag

`ETag` (Entity Tag) — заголовок с идентификатором версии ресурса. Два вида:

```
ETag: "33a64df551425fcc55e4d42a148795d9f25f89d4"   // strong etag
ETag: W/"0815"                                       // weak etag
```

- **Strong ETag** — точное совпадение байт. Для строгой проверки.
- **Weak ETag** (`W/`) — «семантически эквивалентно». Для кэширования, где точное совпадение не критично.

Как генерируется:

- **хеш содержимого** — для статики, файлов;
- **номер версии из БД** — для изменяемых сущностей;
- **комбинация полей** — например, `id + updated_at`.

Свойства:

- **меняется при каждом изменении** ресурса;
- **разный для разных представлений** (JSON vs XML — разные ETag);
- **генерируется сервером**, клиент только читает и передаёт обратно.

## 2.3. Условные заголовки

| Заголовок | Методы | Когда выполняется |
|-----------|--------|-------------------|
| `If-None-Match` | GET, HEAD | Если ресурс **не** совпадает с ETag |
| `If-Match` | PUT, PATCH, DELETE | Если ресурс **совпадает** с ETag |
| `If-Modified-Since` | GET | Если изменён после даты |
| `If-Unmodified-Since` | PUT, PATCH | Если не изменён после даты |

`If-None-Match` используется для **кэширования**: клиент говорит «у меня есть версия X, дай новую, только если она другая». Если версия та же — `304 Not Modified` без тела.

`If-Match` используется для **оптимистичной блокировки**: клиент говорит «примени изменение, только если текущая версия — X». Если версия другая — `412 Precondition Failed`.

## 2.4. Шаг 1: сервер отдаёт ETag

```csharp
var document = new Document { Text = "Исходный текст", Version = 1 };

static string GetETag(Document doc) => $"\"v{doc.Version}\"";   // кавычки обязательны

app.MapGet("/doc", (HttpContext ctx) =>
{
    var etag = GetETag(document);
    ctx.Response.Headers.ETag = etag;

    var ifNoneMatch = ctx.Request.Headers.IfNoneMatch.ToString();
    if (ifNoneMatch == etag)
        return Results.StatusCode(304);       // тело не отправляем

    return Results.Ok(document);
});
```

Проверка через консоль:

```bash
curl -i http://localhost:5000/doc
# HTTP/1.1 200 OK
# ETag: "v1"
# {"text":"Исходный текст","version":1}

curl -i http://localhost:5000/doc -H 'If-None-Match: "v1"'
# HTTP/1.1 304 Not Modified      ← тело не передаётся, экономим трафик
```

Ключевые моменты:

- **кавычки в ETag обязательны** по спецификации. `ETag: v1` — невалидно, `ETag: "v1"` — валидно;
- **`304` без тела** — экономия трафика. Клиент использует закэшированную версию;
- **`If-None-Match`** — клиент присылает свой ETag; сервер сравнивает.

## 2.5. Шаг 2: оптимистичная блокировка

```csharp
app.MapPut("/doc", (HttpContext ctx, Document incoming) =>
{
    var currentETag = GetETag(document);
    var clientETag = ctx.Request.Headers.IfMatch.ToString();

    // --- ШАГ 1: клиент обязан сообщить, с какой версией он работал ---
    if (string.IsNullOrEmpty(clientETag))
    {
        return Results.Json(new
        {
            error = "Требуется заголовок If-Match",
            currentETag
        }, statusCode: 428);
    }

    // --- ШАГ 2: сравниваем версию клиента с текущей ---
    if (clientETag != currentETag)
    {
        return Results.Json(new
        {
            error = "Документ был изменён другим клиентом",
            currentETag,
            yourETag = clientETag
        }, statusCode: 412);
    }

    // --- ШАГ 3: версии совпали — можно безопасно применить изменение ---
    document.Text = incoming.Text;
    document.Version++;

    ctx.Response.Headers.ETag = GetETag(document);
    return Results.Ok(document);
});
```

Разбор:

- **`428 Precondition Required`** — если клиент не прислал `If-Match`. Это сигнал «обязательно используй условный запрос». Не все серверы возвращают 428 — можно и 400;
- **`412 Precondition Failed`** — если версии не совпали. Кто‑то изменил документ между чтением и записью;
- **`currentETag` в ответе** — клиент узнаёт актуальную версию, чтобы повторить;
- **`yourETag` в ответе** — для диагностики: клиент видит, с какой версией он работал;
- **новый ETag в ответе** — после успешного изменения клиент получает актуальную версию.

Сценарий:

```bash
# A и B читают документ, оба получают ETag: "v1"
curl -i http://localhost:5000/doc        # ETag: "v1"

# A успешно сохраняет
curl -X PUT http://localhost:5205/doc \
  -H 'Content-Type: application/json' \
  -H 'If-Match: "v1"' \
  -d '{"text":"Правка от A"}'
# → 200 OK, ETag: "v2"

# B пытается сохранить со СТАРЫМ ETag "v1"
curl -i -X PUT http://localhost:5000/doc \
  -H 'Content-Type: application/json' \
  -H 'If-Match: "v1"' \
  -d '{"text":"Правка от B"}'
# → HTTP/1.1 412 Precondition Failed
# {"error":"Документ был изменён другим клиентом","currentETag":"\"v2\"", ...}
```

Правка A сохранена, B получает конфликт и может повторить с актуальной версией.

## 2.6. Через контроллеры

```csharp
[ApiController]
[Route("api/doc")]
public class DocController : ControllerBase
{
    private static Document _doc = new() { Text = "Исходный текст", Version = 1 };

    private static string GetETag(Document d) => $"\"v{d.Version}\"";

    [HttpGet]
    public IActionResult Get()
    {
        var etag = GetETag(_doc);
        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch.ToString() == etag)
            return StatusCode(304);

        return Ok(_doc);
    }

    [HttpPut]
    public IActionResult Put([FromBody] Document incoming)
    {
        var currentETag = GetETag(_doc);
        var clientETag = Request.Headers.IfMatch.ToString();

        if (string.IsNullOrEmpty(clientETag))
            return StatusCode(428, new { error = "Требуется If-Match", currentETag });

        if (clientETag != currentETag)
            return StatusCode(412, new
            {
                error = "Документ был изменён другим клиентом",
                currentETag,
                yourETag = clientETag
            });

        _doc.Text = incoming.Text;
        _doc.Version++;

        Response.Headers.ETag = GetETag(_doc);
        return Ok(_doc);
    }
}
```

Логика та же, но через MVC‑контроллер. `Response.Headers.ETag` — установка заголовка ответа, `Request.Headers.IfMatch` — чтение заголовка запроса.

## 2.7. Обработка 412 на клиенте

```csharp
async Task<Document> UpdateDocumentAsync(HttpClient http, string newText)
{
    const int maxRetries = 3;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        // 1. Читаем актуальную версию + её ETag
        var getRes = await http.GetAsync("/doc");
        var doc = await getRes.Content.ReadFromJsonAsync<Document>();
        var etag = getRes.Headers.ETag!.ToString();

        Console.WriteLine($"Попытка {attempt}: работаем с версией {etag}");

        // 2. Отправляем изменение, привязав его к прочитанной версии
        var request = new HttpRequestMessage(HttpMethod.Put, "/doc")
        {
            Content = JsonContent.Create(new { text = newText })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        var putRes = await http.SendAsync(request);

        // 3. Если кто-то опередил — повторяем весь цикл
        if (putRes.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Конфликт версий, повторяем...");
            continue;
        }

        if (!putRes.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)putRes.StatusCode}");

        return (await putRes.Content.ReadFromJsonAsync<Document>())!;
    }

    throw new Exception("Не удалось сохранить: слишком много конфликтов");
}
```

Ключевые моменты:

- **цикл «прочитал → изменил → отправил»** — при конфликте повторяется целиком, а не только PUT;
- **`If-Match` с актуальным ETag** — клиент сообщает, с какой версией работает;
- **`412` → `continue`** — повторяем с новой версией;
- **ограничение попыток** — защита от бесконечного цикла при постоянных конфликтах;
- **`TryAddWithoutValidation`** — ETag содержит кавычки, стандартная валидация может их не пропустить.

Важно: при повторе клиент **перечитывает** документ. Если бы он просто повторял PUT с тем же телом, он бы затёр чужие изменения. Перечитывание + применение своего изменения поверх актуальной версии — правильный подход.

## 2.8. Что важно запомнить про ETag

- **`ETag` — идентификатор версии** ресурса. Strong — точное совпадение, weak — семантическое;
- **`If-None-Match`** — для кэширования (`GET`): «дай, если отличается»;
- **`If-Match`** — для оптимистичной блокировки (`PUT`/`PATCH`/`DELETE`): «примени, если совпадает»;
- **`304 Not Modified`** — ресурс не изменился, тело не передаётся;
- **`412 Precondition Failed`** — версии не совпали, кто‑то опередил;
- **`428 Precondition Required`** — клиент не прислал `If-Match`, а сервер его требует;
- **кавычки в ETag обязательны**;
- **клиент при 412 должен перечитать ресурс** и повторить с новым ETag, а не слать тот же запрос;
- **ограничение попыток** — защита от бесконечного цикла.

---

# Сводка: что выбирать

| Задача | Решение |
|--------|---------|
| Пагинация в админке | Offset (`Skip`/`Take`) + `Meta` |
| Бесконечный скролл, большие объёмы | Cursor‑пагинация |
| Сортировка по нескольким полям | `SortRule[]` + `Expression Trees` |
| Защита от сортировки по секретному полю | Whitelist полей |
| Поиск по нескольким полям | `Where(u => u.Name.Contains(s) \|\| u.Email.Contains(s))` |
| Общее количество записей | `CountAsync` до `Skip/Take` |
| Кэширование GET | `ETag` + `If-None-Match` → `304` |
| Оптимистичная блокировка | `ETag` + `If-Match` → `412` |
| Клиент при конфликте | Перечитать ресурс, повторить с новым ETag |

---

# Частые ошибки

**Запросы к БД:**

- **сортировка через `if/else`** — комбинаторный взрыв, нет множественной сортировки;
- **нет whitelist полей** — клиент сортирует по секретному полю;
- **`page=0` или `limit=1000`** — без валидации;
- **`Contains` без ограничения длины** — огромная строка поиска;
- **`Count` после `Skip/Take`** — считает страницу, а не всё;
- **сортировка через рефлексию** — выполняется в памяти, а не в SQL;
- **`Skip` с большим значением** — БД читает и отбрасывает строки;
- **нет `AsNoTracking`** — лишняя память и CPU для read‑only.

**ETag:**

- **ETag без кавычек** — невалидно по спецификации;
- **нет `If-Match` на PUT** — lost update;
- **`412` без повторного чтения** — клиент затирает чужие изменения;
- **бесконечный retry** — без ограничения попыток;
- **weak ETag для строгой проверки** — может пропустить реальное изменение;
- **разный ETag для одного ресурса** — клиент не сможет сравнить;
- **нет `428`** — клиент не знает, что нужен `If-Match`;
- **`304` с телом** — нарушение спецификации, лишний трафик.
