# Конспект: Dependency Injection — lifetime, scope и captive dependency

## 1. Что такое DI и время жизни

Dependency Injection (DI) — это механизм, при котором объект не создаёт свои зависимости сам, а получает их извне (обычно через конструктор). В ASP.NET Core DI-контейнер управляет не только созданием, но и **временем жизни** сервисов.

Встроенный контейнер поддерживает три времени жизни :

| Время жизни | Метод регистрации | Когда создаётся | Когда освобождается |
|-------------|------------------|-----------------|---------------------|
| **Singleton** | `AddSingleton<T>()` | Один раз за всё время работы приложения | При остановке приложения |
| **Scoped** | `AddScoped<T>()` | Один раз на HTTP-запрос (на scope) | В конце запроса (scope) |
| **Transient** | `AddTransient<T>()` | Каждый раз, когда запрашивается | В конце запроса, в котором был создан |

**Singleton** — для stateless-сервисов, кэшей, реестров, конфигурации. Должен быть потокобезопасным .

**Scoped** — для `DbContext`, репозиториев, use case-ов. Один экземпляр на запрос, разделяется всеми сервисами в рамках этого запроса .

**Transient** — для лёгких stateless-сервисов, которые не хранят состояние. Создаётся каждый раз при запросе из контейнера .

## 2. Что такое scope

Scope — это **область видимости** scoped-сервисов. В ASP.NET Core scope создаётся автоматически на каждый HTTP-запрос. Все scoped-сервисы, разрешённые в рамках одного запроса, получают один и тот же экземпляр.

```csharp
// В рамках одного запроса
public class MyController : Controller
{
    public MyController(IMyScopedService scoped1, IMyScopedService scoped2)
    {
        // scoped1 и scoped2 — один и тот же экземпляр
    }
}
```

Вне HTTP-запроса (например, в фоновом сервисе или консольном приложении) scope нужно создавать вручную:

```csharp
using var scope = serviceProvider.CreateScope();
var scopedService = scope.ServiceProvider.GetRequiredService<IMyScopedService>();
// ... используем ...
// scope.Dispose() освободит scoped-сервисы
```

Без scope scoped-сервис, запрошенный из корневого провайдера, фактически ведёт себя как singleton — это ошибка .

## 3. Captive dependency

Captive dependency (захваченная зависимость) — это ситуация, когда **сервис с более долгим временем жизни держит ссылку на сервис с более коротким временем жизни** .

Классический пример: singleton зависит от scoped.

```csharp
// ❌ Плохо: singleton зависит от scoped
public class MySingleton
{
    private readonly MyScopedService _scoped;

    public MySingleton(MyScopedService scoped) // captive dependency
    {
        _scoped = scoped;
    }
}

builder.Services.AddSingleton<MySingleton>();
builder.Services.AddScoped<MyScopedService>();
```

**Почему это проблема** :

- Scoped-сервис освобождается в конце запроса, но singleton продолжает держать на него ссылку. Дальше — использование освобождённого объекта.
- Scoped-сервисы часто **не потокобезопасны** (например, `DbContext`). Singleton разделяется всеми потоками — одновременный доступ к scoped-сервису из разных потоков приведёт к ошибкам.
- `DbContext` при таком захвате живёт всё время работы приложения, накапливает change tracker, утечка памяти.

**Как это выглядит в коде DI-контейнера:**

```csharp
// Singleton создаётся один раз, при первом запросе
// Он запрашивает MyScopedService
// Контейнер создаёт MyScopedService в текущем scope
// Singleton сохраняет ссылку на него
// В следующем запросе singleton уже существует и использует старый scoped-экземпляр
```

## 4. Обнаружение captive dependency

В .NET DI есть встроенная проверка, но она работает **не всегда** :

- **Development environment** — проверка включена. При построении провайдера или разрешении сервиса выбрасывается `InvalidOperationException` с сообщением вида «Cannot consume scoped service 'X' from singleton 'Y'» .
- **Production** — проверка **выключена** по умолчанию (для производительности). Captive dependency может «работать», но поведение непредсказуемо.
- **Ручное включение** — `ValidateScopes = true` при построении провайдера:
  ```csharp
  var provider = services.BuildServiceProvider(new ServiceProviderOptions
  {
      ValidateScopes = true
  });
  ```

Рекомендация: включать `ValidateScopes = true` везде, где это возможно. Производительность страдает незначительно, а баги ловятся на старте .

## 5. Другие виды несовпадения времён жизни

Captive dependency — частный случай **lifestyle mismatch**. Общее правило: **сервис не должен зависеть от сервиса с более коротким временем жизни**.

| Зависимость | Можно? | Комментарий |
|-------------|--------|-------------|
| Singleton → Singleton | Да | |
| Singleton → Scoped | **Нет** | Captive dependency |
| Singleton → Transient | **Нет** | Transient освобождается, singleton держит ссылку |
| Scoped → Singleton | Да | Singleton живёт дольше, безопасно |
| Scoped → Scoped | Да | |
| Scoped → Transient | Да (с оговорками) | Transient освобождается вместе со scope |
| Transient → Singleton | Да | |
| Transient → Scoped | Да | |
| Transient → Transient | Да | |

Simple Injector, например, считает Transient «короткоживущим» и запрещает инъекцию Transient в Singleton . Встроенный контейнер .NET более снисходителен, но правило «не захватывай короткоживущее» универсально.

## 6. Как исправить captive dependency

**Вариант 1: изменить время жизни зависимости на более долгое.**

```csharp
// Если singleton действительно нужен
builder.Services.AddSingleton<MyScopedService>(); // но только если сервис потокобезопасен!
```

Подходит, если сервис stateless и потокобезопасен. Для `DbContext` — **не подходит**.

**Вариант 2: понизить время жизни потребителя.**

```csharp
builder.Services.AddScoped<MySingleton>(); // теперь оба scoped
```

Подходит, если singleton не обязан жить всё приложение.

**Вариант 3: использовать `IServiceScopeFactory` для создания scope внутри singleton.**

```csharp
public class MySingleton
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MySingleton(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void DoWork()
    {
        using var scope = _scopeFactory.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<MyScopedService>();
        // используем scoped только внутри этого метода
    }
}
```

Это правильный паттерн для фоновых сервисов (`IHostedService`), которым нужен `DbContext` . Scope создаётся на одну операцию, scoped-сервис освобождается сразу после.

**Вариант 4: инъекция фабрики.**

```csharp
public class MySingleton
{
    private readonly Func<MyScopedService> _factory;

    public MySingleton(Func<MyScopedService> factory)
    {
        _factory = factory;
    }

    public void DoWork()
    {
        var scoped = _factory(); // новый экземпляр на каждый вызов
    }
}
```

## 7. Связь с AJAX

AJAX-запросы (fetch, XMLHttpRequest) — это **обычные HTTP-запросы**. На сервере каждый AJAX-запрос проходит через тот же конвейер middleware, что и обычный запрос, и получает **свой scope**.

```
AJAX GET /api/orders/5
  → новый scope
  → scoped-сервисы (DbContext, репозиторий) создаются заново
  → запрос обрабатывается
  → scope освобождается, scoped-сервисы dispose
```

Это значит:

- **Scoped-сервисы безопасны в AJAX-эндпоинтах** — каждый запрос получает чистый экземпляр.
- **Captive dependency не возникает из-за AJAX**. Проблема — только в регистрации сервисов (singleton зависит от scoped), а не в способе вызова.
- **Фоновые задачи, запущенные из AJAX-запроса**, не должны захватывать scoped-сервисы из запроса. Если нужно запустить долгую операцию в фоне — использовать `IServiceScopeFactory` или очередь (Channel).

Пример проблемы: контроллер получает scoped `DbContext`, запускает `Task.Run` с этим `DbContext`, возвращает ответ. Запрос завершается, scope освобождается, `DbContext` dispose, а фоновая задача продолжает его использовать — ошибка.

```csharp
// ❌ Плохо: захват scoped DbContext в фоновую задачу
public async Task<IActionResult> Process()
{
    var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

    _ = Task.Run(async () =>
    {
        await db.Orders.AddAsync(new Order()); // DbContext уже освобождён!
        await db.SaveChangesAsync();
    });

    return Ok();
}

// ✅ Хорошо: создание scope внутри фоновой задачи
public async Task<IActionResult> Process(IServiceScopeFactory scopeFactory)
{
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders.AddAsync(new Order());
        await db.SaveChangesAsync();
    });

    return Ok();
}
```

## 8. Что важно понять

- **Правило**: сервис не должен зависеть от сервиса с более коротким временем жизни.
- **Captive dependency** — singleton держит scoped (или transient). Опасен: освобождённый объект, непотокобезопасность, утечки.
- **Development** ловит captive dependency, **Production** — нет (по умолчанию). Включайте `ValidateScopes = true`.
- **`IServiceScopeFactory`** — правильный способ использовать scoped-сервисы в singleton (фоновые задачи, hosted services).
- **AJAX-запросы** — обычные HTTP-запросы, каждый получает свой scope. Проблема не в AJAX, а в регистрации сервисов.
- **Фоновые задачи, запущенные из запроса**, не должны захватывать scoped-сервисы. Создавайте scope внутри задачи.
- **Transient в singleton** — тоже mismatch, хотя .NET его не всегда ловит. Transient освобождается, singleton держит ссылку.

## 9. Частые ошибки

- **Singleton зависит от `DbContext`** — captive dependency, утечка change tracker.
- **`Task.Run` с scoped-сервисом из запроса** — сервис освобождается до завершения задачи.
- **`ValidateScopes` выключен в Production** — captive dependency не ловится, баги всплывают в рантайме.
- **Singleton с transient-зависимостью** — transient освобождается, singleton использует освобождённый объект.
- **Разрешение scoped-сервиса из корневого провайдера** — фактически singleton, состояние «протекает» между запросами.
- **Забыть `using` для scope** — scope не освобождается, scoped-сервисы не dispose.
- **Использовать `IServiceProvider` напрямую в singleton** без создания scope — captive dependency.