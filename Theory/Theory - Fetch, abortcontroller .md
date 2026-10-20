# Конспект: отмена запросов, тайм‑ауты и гонки при работе с `fetch`

## 1. Состояния запроса

При работе с асинхронным запросом удобно выделять пять состояний:

| Состояние | Что означает |
|-----------|--------------|
| `idle` | Запрос ещё не отправлялся |
| `loading` | Запрос в процессе |
| `success` | Запрос завершён успешно, есть данные |
| `error` | Запрос завершился ошибкой |
| `aborted` | Запрос отменён вручную (частный случай `error`, но это не «настоящая» ошибка) |

Разделение `aborted` и `error` важно: отмена — это нормальный сценарий (пользователь ушёл со страницы, ввёл новый символ), и показывать её как ошибку нельзя.

---

## 2. `AbortController` и `AbortSignal`

`AbortController` — объект с двумя ключевыми частями:

- `controller.abort()` — метод, который отменяет запрос;
- `controller.signal` — сигнал, который передаётся в `fetch`.

`fetch` принимает `signal` в опциях. Когда вызывается `abort()`, промис `fetch` отклоняется с `DOMException` с именем `AbortError`.

```js
const controller = new AbortController();

fetch('/api/users', { signal: controller.signal })
  .then(response => {
    // ВАЖНО: fetch не бросает ошибку на 404/500 — нужно проверить вручную.
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return response.json();
  })
  .then(data => console.log('Данные:', data))
  .catch(error => {
    // Отличаем отмену от настоящей ошибки.
    if (error.name === 'AbortError') {
      console.log('Запрос отменён — это не ошибка, просто игнорируем.');
      return;
    }
    console.error('Настоящая ошибка:', error);
  });

// Через 300 мс отменяем запрос (например, пользователь ввёл новый символ).
setTimeout(() => controller.abort(), 300);
```

Ключевые моменты:

- `fetch` **не** отклоняет промис на HTTP‑ошибки (404, 500) — статус надо проверять через `response.ok`.
- Отмена приходит как `AbortError` — её надо явно отличать от настоящих ошибок.
- `abort()` можно вызвать в любой момент: до отправки, во время, после — если запрос уже завершён, вызов ничего не делает.

---

## 3. Отмена по тайм‑ауту

`AbortSignal.timeout(ms)` — удобный способ отменить запрос по истечении времени. Не требует ручного создания контроллера.

```js
async function fetchWithTimeout(url, ms = 5000) {
  try {
    const response = await fetch(url, {
      signal: AbortSignal.timeout(ms),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return await response.json();
  } catch (error) {
    // signal.timeout бросает DOMException с именем 'TimeoutError'
    if (error.name === 'TimeoutError') {
      throw new Error(`Запрос превысил лимит ${ms} мс`);
    }
    throw error;
  }
}
```

Важно: тайм‑аут бросает **`TimeoutError`**, а не `AbortError`. Это позволяет отличить «пользователь отменил» от «сервер не ответил вовремя» и показать разные сообщения.

---

## 4. Объединение сигналов

`AbortSignal.any([...])` объединяет несколько сигналов в один: сработает первый из них.

```js
function requestWithCancelAndTimeout(url, userSignal, timeoutMs = 8000) {
  const timeoutSignal = AbortSignal.timeout(timeoutMs);
  const combinedSignal = AbortSignal.any([userSignal, timeoutSignal]);

  return fetch(url, { signal: combinedSignal });
}

// Использование:
const controller = new AbortController();
requestWithCancelAndTimeout('/api/data', controller.signal, 5000)
  .then(r => r.json())
  .then(console.log)
  .catch(e => {
    if (e.name === 'AbortError') return;      // отмена пользователем
    if (e.name === 'TimeoutError') console.warn('Таймаут');
  });

// Позже:
controller.abort();
```

Типичный сценарий: пользователь может отменить запрос вручную (ушёл со страницы), и одновременно действует тайм‑аут. Комбинированный сигнал срабатывает по первому событию, а по имени ошибки можно понять, что именно произошло.

---

## 5. Гонки HTTP‑запросов

### 5.1. Проблема

Классическая гонка: пользователь быстро печатает в поиске, запросы уходят параллельно, ответы приходят **не в том порядке**. Если рендерить каждый приходящий ответ, на экране окажутся данные для устаревшего запроса.

```js
// ❌ ПЛОХО: классическая гонка без защиты.
let currentQuery = '';

async function searchBad(query) {
  currentQuery = query;
  const response = await fetch(`/api/search?q=${encodeURIComponent(query)}`);
  const data = await response.json();

  // Если пользователь уже ввёл новое значение — этот ответ устарел,
  // но мы всё равно его рендерим. Это баг.
  renderResults(data);
}

// Пользователь печатает быстро:
searchBad('a');    // запрос 1 — вернётся через 800 мс
searchBad('ab');   // запрос 2 — вернётся через 200 мс
searchBad('abc');  // запрос 3 — вернётся через 400 мс
// Итог: отрендерятся данные для 'a', хотя в поле 'abc'.
```

### 5.2. Решение 1: отмена предыдущего запроса

```js
// ✅ ХОРОШО: отменяем предыдущий запрос перед новым.
let activeController = null;

async function searchWithAbort(query) {
  // Если есть активный запрос — отменяем его.
  if (activeController) {
    activeController.abort();
  }
  activeController = new AbortController();

  try {
    const response = await fetch(
      `/api/search?q=${encodeURIComponent(query)}`,
      { signal: activeController.signal }
    );
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    // Дополнительная проверка: не отменён ли запрос, пока мы парсили JSON.
    if (activeController.signal.aborted) return;

    renderResults(data);
  } catch (error) {
    // Отмена — это норма, не показываем пользователю.
    if (error.name === 'AbortError') return;
    renderError(error);
  }
}
```

Отмена экономит ресурсы: старый запрос не досылается, ответ не парсится. Но есть тонкость: даже после `abort()` ответ мог уже прийти и парситься — поэтому нужна дополнительная проверка `signal.aborted` перед рендером.

### 5.3. Решение 2: счётчик запросов

```js
// ✅ ХОРОШО: даже если отмена не сработала (например, ответ уже в пути),
// мы игнорируем устаревшие ответы по номеру.
let requestId = 0;

async function searchWithSeq(query) {
  const myId = ++requestId;

  try {
    const response = await fetch(`/api/search?q=${encodeURIComponent(query)}`);
    const data = await response.json();

    // Если за время запроса был запущен более новый — игнорируем этот.
    if (myId !== requestId) return;

    renderResults(data);
  } catch (error) {
    if (myId !== requestId) return; // устаревшая ошибка — тоже игнорируем
    renderError(error);
  }
}
```

Счётчик — страховка на случай, когда отмена не сработала (ответ уже в пути, парсинг уже начался). Каждый запрос получает номер; при завершении сравнивается с текущим максимальным. Если номер устарел — результат игнорируется.

### 5.4. Решение 3: отмена + счётчик (лучшее)

```js
// ✅ ЛУЧШЕ ВСЕГО: отмена + счётчик. Отмена экономит ресурсы,
// счётчик подстраховывает от гонок при уже начавшемся парсинге.
let currentController = null;
let currentRequestId = 0;

async function search(query, { onLoading, onSuccess, onError } = {}) {
  // Отменяем предыдущий запрос, если он ещё в процессе.
  if (currentController) currentController.abort();
  currentController = new AbortController();

  const myId = ++currentRequestId;
  const signal = currentController.signal;

  onLoading?.(true);

  try {
    const response = await fetch(
      `/api/search?q=${encodeURIComponent(query)}`,
      { signal }
    );

    // Проверяем HTTP-статус вручную.
    if (!response.ok) {
      throw new Error(`Сервер вернул ${response.status}`);
    }

    const data = await response.json();

    // Игнорируем, если это устаревший запрос (пока парсили JSON, пришёл новый).
    if (myId !== currentRequestId) return;

    onSuccess?.(data);
  } catch (error) {
    // Игнорируем устаревшие ошибки.
    if (myId !== currentRequestId) return;

    // Отмена — не ошибка.
    if (error.name === 'AbortError') return;

    onError?.(error);
  } finally {
    // Снимаем loading только если это всё ещё актуальный запрос.
    if (myId === currentRequestId) onLoading?.(false);
  }
}
```

Две защиты дополняют друг друга:

- **отмена** — экономит ресурсы (не досылаем, не парсим);
- **счётчик** — защищает от гонок, когда отмена не сработала.

Обратите внимание на `finally`: `loading` снимается только для актуального запроса, иначе устаревший запрос мог бы сбросить индикатор загрузки для нового.

---

## 6. React‑хук `useFetch`

### 6.1. Reducer для согласованного состояния

```js
function reducer(state, action) {
  switch (action.type) {
    case 'start':
      return { status: 'loading', data: null, error: null };
    case 'success':
      return { status: 'success', data: action.payload, error: null };
    case 'error':
      return { status: 'error', data: null, error: action.payload };
    default:
      return state;
  }
}
```

Единый reducer не даёт противоречивых состояний: например, `loading=true` и `error!=null` одновременно. Каждый переход задаёт все три поля сразу.

### 6.2. Хук с отменой

```js
import { useEffect, useReducer, useRef } from 'react';

export function useFetch(url, options = {}) {
  const [state, dispatch] = useReducer(reducer, {
    status: 'idle',
    data: null,
    error: null,
  });

  // useRef, чтобы не пересоздавать контроллер при ре-рендере.
  const controllerRef = useRef(null);

  useEffect(() => {
    if (!url) return;

    // Отменяем предыдущий запрос (если URL изменился быстро).
    if (controllerRef.current) controllerRef.current.abort();
    const controller = new AbortController();
    controllerRef.current = controller;

    dispatch({ type: 'start' });

    fetch(url, { ...options, signal: controller.signal })
      .then(response => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
      })
      .then(data => {
        // Если запрос отменён — не трогаем состояние.
        if (controller.signal.aborted) return;
        dispatch({ type: 'success', payload: data });
      })
      .catch(error => {
        // Отмена — не ошибка, ничего не делаем.
        if (error.name === 'AbortError') return;
        dispatch({ type: 'error', payload: error });
      });

    // Cleanup: вызывается при размонтировании или смене url.
    // Именно он спасает от гонок и утечек памяти.
    return () => controller.abort();
  }, [url]);

  return state;
}
```

Ключевые моменты:

- `useRef` хранит контроллер между рендерами — не пересоздаётся;
- `useEffect` с зависимостью `[url]` перезапускается при смене URL;
- **cleanup** (`return () => controller.abort()`) вызывается при размонтировании компонента и при смене URL — именно он спасает от гонок и утечек;
- после `fetch` проверяется `signal.aborted` перед `dispatch` — на случай, если запрос был отменён, пока парсился JSON.

### 6.3. Компонент с состояниями

```js
import { useFetch } from './useFetch';

export function UserProfile({ userId }) {
  const { status, data, error } = useFetch(`/api/users/${userId}`);

  if (status === 'loading' || status === 'idle') {
    return <div className="spinner">Загрузка…</div>;
  }

  if (status === 'error') {
    return (
      <div className="error">
        <p>Ошибка: {error.message}</p>
        <button onClick={() => window.location.reload()}>Повторить</button>
      </div>
    );
  }

  return (
    <div>
      <h1>{data.name}</h1>
      <p>{data.email}</p>
    </div>
  );
}
```

Компонент явно обрабатывает все состояния: `idle`/`loading` — спиннер, `error` — сообщение и кнопка повтора, `success` — данные. Отмена сюда не доходит — хук её глотает.

---

## 7. Debounce + отмена

Debounce откладывает выполнение, пока пользователь не перестанет печатать. Отмена убивает предыдущий запрос, если пользователь всё‑таки продолжил.

```js
function debounce(fn, delay) {
  let timerId = null;
  return function (...args) {
    clearTimeout(timerId);
    timerId = setTimeout(() => fn.apply(this, args), delay);
  };
}

let searchController = null;

const runSearch = async (query) => {
  if (searchController) searchController.abort();
  searchController = new AbortController();

  try {
    const res = await fetch(
      `/api/search?q=${encodeURIComponent(query)}`,
      { signal: searchController.signal }
    );
    const data = await res.json();
    renderResults(data);
  } catch (e) {
    if (e.name === 'AbortError') return;
    renderError(e);
  }
};

const debouncedSearch = debounce(runSearch, 300);

document.querySelector('#search').addEventListener('input', (e) => {
  debouncedSearch(e.target.value);
});
```

Смысл комбинации:

- **debounce** не даёт отправлять запрос на каждый символ — только когда пользователь приостановился;
- **abort** отменяет запрос, если пользователь всё‑таки продолжил печатать до ответа.

---

## 8. Защита от двойной отправки формы

Гонка может возникнуть и при двойном клике по «Отправить» — уйдут два одинаковых запроса. Решение: блокировка кнопки + отмена предыдущего запроса.

```js
let submitController = null;
let isSubmitting = false;

async function handleSubmit(formData, button) {
  if (isSubmitting) return; // защита от повторного клика

  if (submitController) submitController.abort();
  submitController = new AbortController();

  isSubmitting = true;
  button.disabled = true;
  button.textContent = 'Отправка…';

  try {
    const res = await fetch('/api/submit', {
      method: 'POST',
      body: formData,
      signal: submitController.signal,
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    showSuccess();
  } catch (e) {
    if (e.name === 'AbortError') return;
    showError(e);
  } finally {
    isSubmitting = false;
    button.disabled = false;
    button.textContent = 'Отправить';
  }
}
```

Три уровня защиты:

- флаг `isSubmitting` — не пускает повторный вызов;
- `button.disabled` — визуальная блокировка;
- `abort()` предыдущего запроса — страховка, если что‑то пошло не так.

---

## 9. Универсальная обёртка `safeFetch`

Обёртка над `fetch`, которая:

- бросает настоящие ошибки для HTTP 4xx/5xx;
- не бросает для `AbortError` (возвращает `{ aborted: true }`);
- поддерживает тайм‑аут;
- различает отмену пользователем и тайм‑аут;
- различает сетевые ошибки и HTTP‑ошибки;
- парсит ответ по `Content-Type`.

```js
export async function safeFetch(url, { timeout = 10000, signal, ...init } = {}) {
  const timeoutSignal = AbortSignal.timeout(timeout);
  const combined = signal
    ? AbortSignal.any([signal, timeoutSignal])
    : timeoutSignal;

  try {
    const response = await fetch(url, { ...init, signal: combined });

    if (!response.ok) {
      // Пытаемся достать сообщение из тела ответа, если оно там есть.
      let detail = '';
      try {
        const body = await response.json();
        detail = body?.message ?? '';
      } catch {
        /* тело не JSON — игнорируем */
      }
      throw new Error(
        `HTTP ${response.status} ${response.statusText}${detail ? ': ' + detail : ''}`
      );
    }

    // Парсим в зависимости от Content-Type.
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/json')) return await response.json();
    return await response.text();
  } catch (error) {
    if (error.name === 'AbortError') {
      // Явно различаем отмену пользователем и таймаут.
      if (timeoutSignal.aborted && !signal?.aborted) {
        throw new Error('Превышено время ожидания ответа');
      }
      // Отмена пользователем — возвращаем маркер, не бросаем.
      return { aborted: true };
    }
    // Сетевые ошибки (нет интернета, DNS и т.п.) прилетают как TypeError.
    if (error instanceof TypeError) {
      throw new Error('Сетевая ошибка: проверьте подключение');
    }
    throw error;
  }
}
```

Разбор тонкостей:

- **отмена пользователем** → возвращается `{ aborted: true }`, не бросается — вызывающий код сам решает, что делать;
- **тайм‑аут** → бросается обычная ошибка с понятным сообщением. Различить их можно по `timeoutSignal.aborted && !signal?.aborted`;
- **сетевые ошибки** (`TypeError` — нет интернета, DNS) отделяются от HTTP‑ошибок;
- **тело ответа** парсится по `Content-Type`, а не «вслепую» — если сервер вернул HTML вместо JSON, клиент не упадёт.

---

## 10. Паттерн «последний запрос побеждает»

Универсальный хелпер, который гарантирует, что будет обработан только результат **последнего** вызова. Внутри — отмена + счётчик в одной обёртке.

```js
function createLatestOnly() {
  let controller = null;
  let id = 0;

  return async function latestOnly(fn) {
    if (controller) controller.abort();
    controller = new AbortController();
    const myId = ++id;

    try {
      const result = await fn(controller.signal);
      // Если за это время был вызван новый — игнорируем.
      if (myId !== id) return undefined;
      return result;
    } catch (e) {
      if (myId !== id) return undefined;
      throw e;
    }
  };
}

// Использование:
const latest = createLatestOnly();

async function loadUser(id) {
  return latest(async (signal) => {
    const res = await fetch(`/api/users/${id}`, { signal });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  });
}

// Быстрые клики по разным пользователям — отрендерится только последний.
loadUser(1);
loadUser(2);
loadUser(3); // победит этот
```

Особенности:

- функция `fn` получает `signal` — она должна передать его в `fetch`, иначе отмена не сработает;
- устаревшие результаты возвращают `undefined` — вызывающий код может это проверить;
- устаревшие ошибки тоже глотаются — они не должны всплывать у вызывающего кода;
- один хелпер покрывает и отмену, и защиту от гонок.

---

## 11. Сводка: что выбирать

| Сценарий | Решение |
|----------|----------|
| Запрос может зависнуть | `AbortSignal.timeout(ms)` |
| Пользователь может отменить | `AbortController` + `signal` |
| И то, и другое | `AbortSignal.any([userSignal, timeoutSignal])` |
| Быстрый ввод в поиске | debounce + abort |
| Быстрые клики по разным id | abort + счётчик (или `createLatestOnly`) |
| Двойная отправка формы | флаг + `button.disabled` + abort |
| React‑компонент | `useEffect` с cleanup‑`abort()` + reducer |
| Универсальная обёртка | `safeFetch` с классификацией ошибок |

---

## 12. Частые ошибки

- **Забыть проверить `response.ok`** — `fetch` не бросает на 404/500.
- **Не отличать `AbortError` от настоящей ошибки** — пользователь увидит «ошибка», хотя он просто ушёл со страницы.
- **Не отличать `TimeoutError` от `AbortError`** — сообщения должны быть разными.
- **Не проверять `signal.aborted` после `await`** — запрос мог быть отменён, пока парсился JSON.
- **Не снимать `loading` в `finally` с проверкой актуальности** — устаревший запрос сбросит индикатор для нового.
- **Не блокировать кнопку при отправке формы** — двойной клик отправит два запроса.
- **Не передавать `signal` в `fetch`** — отмена не сработает, хотя контроллер создан.
- **Не различать сетевые ошибки (`TypeError`) и HTTP‑ошибки** — сообщения пользователю должны быть разными.
- **Парсить тело «вслепую» через `.json()`** — если прокси вернул HTML, клиент упадёт.
- **Забыть cleanup в `useEffect`** — при размонтировании компонента запрос продолжит выполняться, а `dispatch` в размонтированный компонент может привести к утечке.
