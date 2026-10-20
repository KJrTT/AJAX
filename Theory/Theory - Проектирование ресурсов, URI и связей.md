# Конспект: URI, REST, HATEOAS, толстые и тонкие endpoint‑ы

Материал делится на четыре темы:

1. Проектирование URI и вложенность ресурсов.
2. Модель зрелости Ричардсона и HATEOAS.
3. Типы связей между ресурсами.
4. Толстый и тонкий endpoint: сравнение подходов.

---

# Часть 1. Проектирование URI

## 1.1. Базовые маршруты для ресурса

```
GET    /api/v1/users        — список пользователей
GET    /api/v1/users/{id}   — конкретный пользователь
POST   /api/v1/users        — создать
PUT    /api/v1/users/{id}   — заменить
PATCH  /api/v1/users/{id}   — частично обновить
DELETE /api/v1/users/{id}   — удалить
```

Принципы:

- **ресурс — существительное** (`users`), не глагол (`getUsers`);
- **множественное число** для коллекций (`users`), даже если в базе одна запись;
- **HTTP‑метод выражает действие**, URL — только идентифицирует ресурс;
- **версия в пути** (`/api/v1/...`) — один из способов версионирования; альтернативы — заголовок `api-version` или query‑параметр;
- **`{id}` — идентификатор ресурса**, не действие.

## 1.2. Вложенные ресурсы

```
GET  /api/v1/users/{id}/orders          — заказы пользователя
POST /api/v1/users/{id}/orders          — создать заказ для пользователя
GET  /api/v1/users/{id}/orders/{oid}    — конкретный заказ
```

Когда использовать вложенность:

- ресурс **принадлежит** родителю (заказ не существует без пользователя);
- нужен контекст (заказы именно этого пользователя);
- связь «один ко многим» очевидна.

Когда не использовать:

- ресурс может существовать отдельно (заказ можно получить и по `/orders/{oid}`, если он глобально уникален);
- вложенность глубже двух уровней (`/a/{id}/b/{id}/c/{id}`) — становится неудобно;
- клиенту чаще нужен ресурс без контекста родителя.

## 1.3. Плоский URI + ссылка

```
GET /orders/7
```

```json
{
  "id": 7,
  "userId": 42,
  "_links": { "user": "/users/42" }
}
```

Плоский URI короче и удобнее, если заказ можно получить независимо от пользователя. Связь с пользователем выражается **ссылкой в теле**, а не вложенностью в URL.

Это компромисс: URL не отражает иерархию, но клиент может перейти по ссылке.

---

# Часть 2. Модель зрелости Ричардсона

## 2.1. Уровни

| Уровень | Название | Что есть |
|---------|----------|----------|
| 0 | «Болото» | Один endpoint, всё через `POST` |
| 1 | Ресурсы | Разные URL для разных ресурсов (`/users/42`) |
| 2 | HTTP‑методы + статусы | `GET`/`POST`/`PUT`/`DELETE`, коды ответов |
| 3 | HATEOAS | Гипермедиа‑контроль |

**Level 0** — SOAP‑подобный подход: один URL (`/api`), всё через `POST`, действие в теле. Просто, но не использует HTTP.

**Level 1** — появляются ресурсы: `/users/42`, `/orders/7`. Но методы всё ещё `POST`.

**Level 2** — используются HTTP‑методы и статусы. `GET` для чтения, `POST` для создания, `PUT`/`PATCH` для изменения, `DELETE` для удаления. `200`/`201`/`204`/`400`/`404` — по назначению. **Это цель большинства API.**

**Level 3** — HATEOAS: ответ содержит ссылки на возможные действия. Клиент не знает URL заранее — он следует ссылкам из ответа.

## 2.2. Зачем Level 2 — цель

- **семантика HTTP**: кэширование, идемпотентность, безопасность методов;
- **инструменты**: Swagger, Postman, прокси, CDN понимают HTTP‑методы;
- **предсказуемость**: клиент знает, что `GET` не меняет состояние, `DELETE` идемпотентен;
- **простота**: не нужно изобретать свой протокол поверх HTTP.

Level 3 (HATEOAS) даёт гибкость, но сложнее в реализации и редко используется на практике. Большинство API останавливаются на Level 2.

---

# Часть 3. HATEOAS

## 3.1. Идея

HATEOAS (Hypermedia As The Engine Of Application State) — ответ содержит не только данные, но и **ссылки на возможные действия**. Клиент не хардкодит URL — он следует ссылкам.

## 3.2. Пример

```json
{
  "id": 42,
  "name": "Иван",
  "_links": {
    "self":   { "href": "/users/42", "method": "GET" },
    "orders": { "href": "/users/42/orders", "method": "GET" },
    "update": { "href": "/users/42", "method": "PUT" },
    "delete": { "href": "/users/42", "method": "DELETE" }
  }
}
```

Что даёт:

- **клиент не хардкодит URL** — если сервер изменит путь, клиент продолжит работать;
- **сервер управляет доступными действиями** — если пользователь не может удалять, ссылки `delete` не будет;
- **самодокументируемость** — из ответа видно, что можно сделать с ресурсом.

## 3.3. Проблемы HATEOAS

- **сложность реализации** — нужно генерировать ссылки в каждом ответе;
- **размер ответа** — ссылки занимают место;
- **клиенты редко используют** — большинство SDK и клиентов хардкодят URL;
- **нет единого стандарта** — `_links`, `links`, `href`, HAL, JSON:API — разные форматы;
- **тестирование** — сложнее проверить, что все ссылки корректны.

Поэтому Level 3 — редкость. Но элементы HATEOAS полезны: например, `Location` в `201 Created` или ссылки на связанные ресурсы.

---

# Часть 4. Типы связей между ресурсами

## 4.1. Один к одному

```
/users/5/profile
```

У пользователя один профиль. Вложенность логична: профиль не существует без пользователя.

## 4.2. Один ко многим

```
/users/5/orders
```

У пользователя много заказов. Вложенность логична для контекста «заказы этого пользователя».

Альтернатива — плоский URI `/orders?userId=5`. Оба варианта допустимы: вложенность выражает принадлежность, query‑параметр — фильтрацию.

## 4.3. Много ко многим

```
/conets/{id}/request
```

Много ко многим сложнее: обе сущности существуют независимо. Обычно используют:

- плоские URI для обеих (`/users/{id}`, `/projects/{id}`);
- отдельный ресурс связи (`/memberships?userId=5&projectId=7`);
- вложенность только для контекста (`/projects/{id}/members`).

Вложенность «много ко многим» глубже одного уровня становится неудобной.

---

# Часть 5. Толстый endpoint

## 5.1. Пример

```csharp
app.MapPost("/orders", async (CreateOrderRequest req, AppDbContext db, IEmailService email) =>
{
    // 1. Валидация
    if (string.IsNullOrWhiteSpace(req.CustomerEmail))
        return Results.BadRequest("Email required");
    if (req.Items.Count == 0)
        return Results.BadRequest("No items");

    // 2. Проверка бизнес-правил
    var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == req.CustomerEmail);
    if (customer is null) return Results.NotFound();
    if (customer.IsBlocked) return Results.Forbid();

    // 3. Логика
    var order = new Order { CustomerId = customer.Id, ... };
    foreach (var item in req.Items)
    {
        var product = await db.Products.FindAsync(item.ProductId);
        if (product is null) return Results.BadRequest("Product not found");
        if (product.Stock < item.Quantity) return Results.Conflict("Not enough stock");
        product.Stock -= item.Quantity;
        order.Items.Add(new OrderItem { ... });
    }
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    // 4. Side effects
    await email.SendAsync(customer.Email, "Order created");

    // 5. Маппинг ответа
    return Results.Created($"/orders/{order.Id}", new { order.Id, order.Total });
});
```

## 5.2. Проблемы толстого endpoint

- **всё в одном месте** — валидация, бизнес‑логика, доступ к БД, side effects, маппинг;
- **сложно тестировать** — нужно поднимать БД, мокать email, проверять HTTP‑ответ;
- **нарушение SRP** — endpoint отвечает за всё сразу;
- **дублирование** — та же логика нужна в другом endpoint (например, `POST /orders/bulk`) — копипаста;
- **сложно переиспользовать** — логику нельзя вызвать из консоли, фонового сервиса, теста;
- **транзакции и side effects смешаны** — email отправляется после `SaveChanges`, но если email упадёт, заказ уже создан;
- **тестирование через HTTP** — медленно, зависит от инфраструктуры.

## 5.3. Когда толстый endpoint допустим

- **очень простой сценарий** — один запрос, один `SaveChanges`, без side effects;
- **прототип** — быстро проверить идею;
- **редкий endpoint** — не стоит выделять use case ради одного вызова.

Но как только появляется бизнес‑логика, side effects или потребность в переиспользовании — пора выделять use case.

---

# Часть 6. Тонкий endpoint

## 6.1. Пример

```csharp
app.MapPost("/orders", async (CreateOrderRequest req, IUseCase<CreateOrderCommand, Result<OrderId>> useCase) =>
{
    var command = new CreateOrderCommand(req.CustomerEmail, req.Items);
    var result = await useCase.ExecuteAsync(command);
    return result.ToHttpResult();
});
```

Endpoint делает три вещи:

1. **Маппит** Request DTO в Command.
2. **Вызывает** use case.
3. **Маппит** Result в HTTP‑ответ.

Вся бизнес‑логика — в use case.

## 6.2. Use case

```csharp
public sealed class CreateOrderUseCase
{
    public Task<Result<OrderId>> ExecuteAsync(CreateOrderCommand cmd, CancellationToken ct);
}
```

Один класс — один сценарий. Зависимости (репозитории, сервисы) внедряются через конструктор.

## 6.3. `Result<T>` для обработки ошибок и значений

```csharp
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
}

public sealed record Error(string Code, string Message, ErrorType Type);

public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Failure }
```

`Result<T>` — альтернатива исключениям для **ожидаемых** ошибок:

- **`IsSuccess`** — успех/неуспех;
- **`Value`** — результат при успехе;
- **`Error`** — описание ошибки при неуспехе;
- **`ErrorType`** — категория для маппинга в HTTP‑статус.

## 6.4. Маппинг `Result` в HTTP

```csharp
public static IResult ToHttpResult<T>(this Result<T> result)
{
    if (result.IsSuccess)
        return Results.Ok(result.Value);

    return result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(result.Error),
        ErrorType.NotFound => Results.NotFound(result.Error),
        ErrorType.Conflict => Results.Conflict(result.Error),
        ErrorType.Forbidden => Results.Forbid(),
        _ => Results.StatusCode(500)
    };
}
```

Маппинг в одном месте — endpoint не думает о статусах.

## 6.5. Преимущества тонкого endpoint

- **тестируемость** — use case тестируется без HTTP и БД (через моки репозиториев);
- **переиспользование** — тот же use case вызывается из консоли, фонового сервиса, другого endpoint;
- **SRP** — endpoint отвечает за транспорт, use case — за сценарий;
- **явные зависимости** — use case принимает репозитории через конструктор, видно, что нужно;
- **единообразие ошибок** — `Result<T>` + `ErrorType` дают консистентный маппинг;
- **транзакции и side effects** — можно вынести в отдельные шаги внутри use case, контролировать порядок;
- **тестирование сценариев** — unit‑тесты на use case быстрые и не зависят от инфраструктуры.

## 6.6. Недостатки тонкого endpoint

- **больше классов** — Command, UseCase, Result, Error, маппинг;
- **оверхед для простых сценариев** — для CRUD без логики может быть избыточно;
- **нужна дисциплина** — легко скатиться обратно в толстый endpoint, если «просто добавить одну проверку».

---

# Часть 7. Сравнение подходов

| Критерий | Толстый endpoint | Тонкий endpoint |
|----------|------------------|-----------------|
| Валидация | В endpoint | В use case / DTO |
| Бизнес‑логика | В endpoint | В use case |
| Доступ к БД | В endpoint | В репозиториях через use case |
| Side effects | В endpoint | В use case (явно) |
| Тестирование | Через HTTP | Unit‑тесты use case |
| Переиспользование | Копипаста | Вызов use case |
| SRP | Нарушен | Соблюдён |
| Количество классов | Мало | Больше |
| Подходит для | Прототип, простой CRUD | Реальная бизнес‑логика |

---

# Сводка: что выбирать

| Задача | Решение |
|--------|---------|
| Простой CRUD | Толстый endpoint допустим |
| Бизнес‑логика, side effects | Тонкий endpoint + use case |
| Повторное использование логики | Use case |
| Ожидаемые ошибки | `Result<T>` + `ErrorType` |
| Маппинг ошибок в HTTP | Расширение `ToHttpResult` |
| Версионирование | `/api/v1/...` или заголовок |
| Вложенность ресурсов | Только для принадлежности, не глубже 2 уровней |
| Связь «много ко многим» | Плоские URI + отдельный ресурс связи |
| HATEOAS | Редко; элементы (`Location`, ссылки) полезны |

---

# Частые ошибки

**URI:**

- **глаголы в URL** (`/getUsers`, `/deleteUser`) — действие должно быть в HTTP‑методе;
- **вложенность глубже 2 уровней** — неудобно и нечитаемо;
- **версия в теле** вместо URL/заголовка — клиенты не смогут выбрать версию;
- **`/users/{id}/orders/{oid}/items/{iid}`** — слишком глубокая иерархия.

**REST:**

- **Level 0** — всё через `POST` на один URL;
- **`200 OK` с ошибкой** — клиенты и прокси не увидят проблему;
- **`POST` для чтения** — ломает кэширование и семантику;
- **HATEOAS без необходимости** — сложность без выгоды.

**Толстый endpoint:**

- **вся логика в endpoint** — невозможно переиспользовать и тестировать;
- **side effects после `SaveChanges`** — если side effect упадёт, данные уже изменены;
- **транзакции не контролируются** — несколько `SaveChanges` без транзакции;
- **дублирование** — та же логика копируется в другой endpoint.

**Тонкий endpoint:**

- **исключения вместо `Result<T>`** для ожидаемых ошибок — теряется контроль;
- **`Result<T>` без `ErrorType`** — невозможно маппить в HTTP‑статус;
- **маппинг в каждом endpoint** — дублирование; вынести в расширение;
- **оверхед для простого CRUD** — Command, UseCase, Result, Error ради одного `SaveChanges`;
- **use case знает про HTTP** — возвращает `IActionResult` вместо `Result<T>`; это нарушает разделение слоёв.