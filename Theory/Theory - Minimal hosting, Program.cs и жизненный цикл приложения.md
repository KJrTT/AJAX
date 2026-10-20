# Конспект: Minimal hosting, Program.cs и жизненный цикл приложения

## 1. Что такое minimal hosting

До .NET 6 приложение ASP.NET Core собиралось из двух файлов: `Program.cs` (создание хоста и настройка конвейера) и `Startup.cs` (регистрация сервисов в `ConfigureServices` и настройка middleware в `Configure`). Начиная с .NET 6 появился **minimal hosting** — единая точка входа, где всё находится в `Program.cs`, а `Startup` больше не нужен.

Минимальный `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

Четыре шага:

1. **Создать builder** — `WebApplication.CreateBuilder(args)`.
2. **Зарегистрировать сервисы** — `builder.Services.*`.
3. **Построить приложение** — `builder.Build()`. С этого момента контейнер сервисов «заморожен», новые регистрации невозможны.
4. **Настроить конвейер и запустить** — `app.Use*`, `app.Map*`, `app.Run()`.

`Startup` остаётся доступным как опция, но в новых проектах его не используют.

## 2. Что делает `WebApplication.CreateBuilder`

`CreateBuilder` собирает всё необходимое для запуска:

- **конфигурацию** — `appsettings.json`, `appsettings.{Environment}.json`, переменные окружения, аргументы командной строки, user secrets;
- **логирование** — консоль, отладка, EventSource, EventLog (на Windows);
- **DI‑контейнер** — `builder.Services`;
- **веб‑хост** — Kestrel как сервер по умолчанию;
- **окружение** — `Development`, `Staging`, `Production` (из `ASPNETCORE_ENVIRONMENT`);
- **жизненный цикл** — `IHostApplicationLifetime`.

После `CreateBuilder` все эти сервисы уже зарегистрированы. Вы добавляете свои через `builder.Services`.

## 3. Конфигурация

Порядок источников (последний перекрывает предыдущие):

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. user secrets (только в Development);
4. переменные окружения;
5. аргументы командной строки.

Доступ:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");
var apiKey = builder.Configuration["ApiKeys:Read"];
```

Или через `IOptions<T>`:

```csharp
builder.Services.Configure<ApiKeysOptions>(builder.Configuration.GetSection("ApiKeys"));
```

## 4. Регистрация сервисов

```csharp
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IThreatCatalogService, ThreatCatalogService>();
builder.Services.AddHostedService<QueueConsumerService>();
builder.Services.AddHttpClient<IProductApi, ProductApi>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=shop.db"));
```

Времена жизни:

- **Singleton** — один экземпляр на всё приложение. Для stateless‑сервисов, кэшей, реестров.
- **Scoped** — один экземпляр на HTTP‑запрос. Для `DbContext`, репозиториев, use case‑ов.
- **Transient** — новый экземпляр на каждое разрешение. Для лёгких stateless‑сервисов, middleware через `IMiddleware`.

Ошибки в временах жизни:

- **scoped в singleton** — singleton живёт дольше, будет держать ссылку на scoped из первого запроса;
- **transient в singleton** — то же самое, но ещё и утечка экземпляров;
- **captive dependency** — singleton зависит от scoped; возникает при невнимательной регистрации.

## 5. Построение приложения

```csharp
var app = builder.Build();
```

После `Build()`:

- контейнер сервисов **заморожен** — `builder.Services.Add*` больше не сработает;
- middleware и endpoint‑ы ещё можно настраивать;
- `app.Services` доступен для разрешения сервисов (например, `IApiVersionDescriptionProvider` в конфигурации Swagger).

## 6. Конвейер middleware

Порядок критичен. Типичный порядок:

1. Обработчик исключений (`UseExceptionHandler`);
2. HSTS (`UseHsts`);
3. HTTPS‑редирект (`UseHttpsRedirection`);
4. Статические файлы (`UseStaticFiles`);
5. Собственные middleware (логирование, заголовки);
6. Маршрутизация (`UseRouting`);
7. Аутентификация (`UseAuthentication`);
8. Авторизация (`UseAuthorization`);
9. Antiforgery (`UseAntiforgery`);
10. Endpoint‑ы (`MapControllers`, `MapControllerRoute`).

Почему так:

- **логирование и заголовки — до роутинга**, чтобы покрывать все ответы;
- **аутентификация до авторизации** — иначе политики не увидят пользователя;
- **статика до роутинга** — чтобы не гонять статику через MVC.

Middleware может прервать конвейер, не вызывая `next()` — это «короткое замыкание». Код после `next()` выполняется на обратном пути, когда ответ уже сформирован.

## 7. Endpoint‑ы

```csharp
app.MapControllers();          // атрибутные контроллеры
app.MapControllerRoute(        // классические MVC-маршруты
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/hello", () => "Hello, World!");  // Minimal API
app.MapHealthChecks("/health");
```

`MapControllers` нужен для атрибутных маршрутов (`[Route("api/...")]`). `MapControllerRoute` — для маршрутов по шаблону. Minimal API (`MapGet`/`MapPost`) — для простых эндпоинтов без контроллеров.

## 8. Жизненный цикл приложения

Хост проходит несколько состояний, и на каждом срабатывают события:

| Событие | Когда | Что можно делать |
|---------|-------|------------------|
| `ApplicationStarted` | После старта хоста, до приёма запросов | Инициализация, прогрев кэшей |
| `ApplicationStopping` | Начало остановки, новые запросы не принимаются | Начать graceful shutdown |
| `ApplicationStopped` | Полная остановка | Финальная очистка |

Подписка:

```csharp
public class AppLifetimeService
{
    public AppLifetimeService(IHostApplicationLifetime lifetime)
    {
        lifetime.ApplicationStarted.Register(() => Console.WriteLine("Started"));
        lifetime.ApplicationStopping.Register(() => Console.WriteLine("Stopping"));
        lifetime.ApplicationStopped.Register(() => Console.WriteLine("Stopped"));
    }
}

builder.Services.AddSingleton<AppLifetimeService>();
```

`AppLifetimeService` — singleton, иначе подписки создадутся несколько раз.

## 9. Hosted services

`IHostedService` — фоновый сервис с методами `StartAsync` и `StopAsync`:

```csharp
public class LifetimeHostedService : IHostedService
{
    private readonly ILogger<LifetimeHostedService> _logger;

    public LifetimeHostedService(ILogger<LifetimeHostedService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("StartAsync");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("StopAsync");
        return Task.CompletedTask;
    }
}

builder.Services.AddHostedService<LifetimeHostedService>();
```

- `StartAsync` вызывается при старте, **до** `ApplicationStarted`;
- `StopAsync` вызывается при остановке, **после** `ApplicationStopping`;
- `BackgroundService` — базовый класс для фоновых циклов; переопределяется `ExecuteAsync`.

`AddHostedService` регистрирует сервис как singleton и добавляет его в список фоновых.

## 10. Порядок событий

При старте:

1. Хост создаётся;
2. `StartAsync` у всех hosted services;
3. `ApplicationStarted`;
4. Приём запросов.

При остановке:

1. `ApplicationStopping`;
2. Новые запросы не принимаются, текущие дообрабатываются;
3. `StopAsync` у hosted services;
4. `ApplicationStopped`.

Важно: `AppLifetimeService` должен создаться **до** старта hosted services, иначе подписка на `ApplicationStarted` пропустит событие. Поэтому `LifetimeHostedService` принимает его в конструктор — это гарантирует создание.

## 11. Остановка и graceful shutdown

`app.Run()` — блокирующий вызов, который запускает хост и ждёт остановки. Остановка происходит по:

- `Ctrl+C` (SIGINT);
- `SIGTERM` (Docker, Kubernetes);
- вызову `IHostApplicationLifetime.StopApplication()`.

При остановке:

- новые запросы не принимаются;
- текущие дообрабатываются (с таймаутом, по умолчанию 30 секунд — настраивается `HostOptions.ShutdownTimeout`);
- hosted services получают `StopAsync` с токеном отмены;
- после таймаута хост завершается принудительно.

Graceful shutdown важен для:

- завершения транзакций;
- отправки оставшихся сообщений в очередь;
- сброса буферов логов;
- корректного закрытия соединений с БД.

## 12. Разные режимы запуска

**Minimal hosting** (по умолчанию в .NET 6+):

```csharp
var builder = WebApplication.CreateBuilder(args);
// ...
var app = builder.Build();
// ...
app.Run();
```

**С `Startup`** (для совместимости):

```csharp
var builder = WebApplication.CreateBuilder(args);
var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app, app.Environment);
app.Run();
```

**Worker Service** (без HTTP):

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
```

**Тесты** — `WebApplicationFactory<T>` поднимает приложение в памяти, без реального порта.

## 13. Что важно понять

- **Minimal hosting** — единая точка входа, `Startup` не нужен.
- **`CreateBuilder`** собирает конфигурацию, логирование, DI, окружение.
- **`Build()` замораживает контейнер** — регистрации после него невозможны.
- **Порядок middleware критичен** — аутентификация до авторизации, статика до роутинга.
- **`MapControllers` и `MapControllerRoute`** — для разных стилей контроллеров; часто нужны оба.
- **События жизненного цикла** — `ApplicationStarted`, `ApplicationStopping`, `ApplicationStopped`.
- **Hosted services** — `StartAsync` до старта, `StopAsync` после начала остановки.
- **Graceful shutdown** — даёт время завершить текущие запросы и фоновые задачи.
- **Singleton‑подписки на события** — иначе подписки создадутся несколько раз или пропустят событие.

## 14. Частые ошибки

- **регистрация сервисов после `Build()`** — контейнер заморожен, исключение;
- **scoped в singleton** — captive dependency, утечка;
- **middleware в неправильном порядке** — аутентификация после авторизации, статика после MVC;
- **`MapControllers` без `MapControllerRoute`** — атрибутные маршруты работают, классические нет;
- **нет graceful shutdown** — обрываются транзакции и фоновые задачи;
- **`AppLifetimeService` не singleton** — подписки создаются несколько раз;
- **`ApplicationStarted` пропущен** — сервис создан после старта hosted services;
- **`app.Run()` без `await`** в асинхронном контексте — блокировка потока;
- **`StopAsync` без учёта токена отмены** — хост ждёт до таймаута, потом убивает;
- **`BackgroundService` без `try/catch`** — одно исключение останавливает весь цикл.