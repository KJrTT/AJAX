# Конспект: Fetch, AbortController, тайм-ауты и гонки

## 1. Состояния запроса

|Состояние|Что означает|
|---|---|
|`idle`|Запрос ещё не отправлялся|
|`loading`|Запрос в процессе|
|`success`|Запрос завершён успешно, есть данные|
|`error`|Запрос завершился ошибкой|
|`aborted`|Запрос отменён вручную (частный случай `error`, но это не «настоящая» ошибка)|

Разделение `aborted` и `error` принципиально: отмена — нормальный сценарий (пользователь ушёл со страницы, ввёл новый символ). Показывать её как ошибку нельзя.

## 2. `AbortController` и `AbortSignal`

`AbortController` — объект с двумя частями:

- `controller.abort()` — отменяет запрос;
    
- `controller.signal` — сигнал, который передаётся в `fetch`.
    

Когда вызывается `abort()`, промис `fetch` отклоняется с `DOMException` с именем `AbortError`.

js

const controller = new AbortController();
fetch('/api/users', { signal: controller.signal })
  .then(response => {
    // fetch не бросает ошибку на 404/500 — проверяем вручную
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return response.json();
  })
  .then(data => console.log('Данные:', data))
  .catch(error => {
    // Отличаем отмену от настоящей ошибки
    if (error.name === 'AbortError') {
      console.log('Запрос отменён — это не ошибка, игнорируем.');
      return;
    }
    console.error('Настоящая ошибка:', error);
  });
// Через 300 мс отменяем (например, пользователь ввёл новый символ)
setTimeout(() => controller.abort(), 300);

Ключевое:

- `fetch` **не** отклоняет промис на HTTP-ошибки — статус проверяется через `response.ok`;
    
- отмена приходит как `AbortError` — её надо явно отличать;
    
- `abort()` можно вызвать в любой момент; если запрос уже завершён — ничего не произойдёт.
    

## 3. Отмена по тайм-ауту

`AbortSignal.timeout(ms)` — отмена по истечении времени без ручного создания контроллера.

js

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

Важно: тайм-аут бросает **`TimeoutError`**, а не `AbortError`. Это позволяет показать разные сообщения: «пользователь отменил» vs «сервер не ответил вовремя».

## 4. Объединение сигналов

`AbortSignal.any([...])` объединяет несколько сигналов: сработает первый.

js

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

Типичный сценарий: пользователь может отменить вручную, и одновременно действует тайм-аут. Комбинированный сигнал срабатывает по первому событию, а по имени ошибки видно, что именно произошло.

## 5. Гонки HTTP-запросов

### 5.1. Проблема

Пользователь быстро печатает в поиске, запросы уходят параллельно, ответы приходят не в том порядке. Если рендерить каждый приходящий ответ, на экране окажутся данные для устаревшего запроса.

js

// ❌ ПЛОХО: классическая гонка
let currentQuery = '';
async function searchBad(query) {
  currentQuery = query;
  const response = await fetch(`/api/search?q=${encodeURIComponent(query)}`);
  const data = await response.json();
  // Ответ может быть устаревшим, но мы всё равно его рендерим
  renderResults(data);
}
// Пользователь печатает быстро:
searchBad('a');    // вернётся через 800 мс
searchBad('ab');   // вернётся через 200 мс
searchBad('abc');  // вернётся через 400 мс
// Итог: отрендерятся данные для 'a', хотя в поле 'abc'

### 5.2. Решение 1: отмена предыдущего запроса

js

// ✅ Отменяем предыдущий запрос перед новым
let activeController = null;
async function searchWithAbort(query) {
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
    // Дополнительная проверка: не отменён ли запрос, пока парсили JSON
    if (activeController.signal.aborted) return;
    renderResults(data);
  } catch (error) {
    if (error.name === 'AbortError') return;
    renderError(error);
  }
}

Отмена экономит ресурсы, но есть тонкость: даже после `abort()` ответ мог уже прийти и парситься — нужна проверка `signal.aborted` перед рендером.

### 5.3. Решение 2: счётчик запросов

js

// ✅ Даже если отмена не сработала — игнорируем устаревшие по номеру
let requestId = 0;
async function searchWithSeq(query) {
  const myId = ++requestId;
  try {
    const response = await fetch(`/api/search?q=${encodeURIComponent(query)}`);
    const data = await response.json();
    // Если за время запроса был запущен более новый — игнорируем
    if (myId !== requestId) return;
    renderResults(data);
  } catch (error) {
    if (myId !== requestId) return; // устаревшая ошибка — тоже игнорируем
    renderError(error);
  }
}

Счётчик — страховка на случай, когда отмена не сработала (ответ уже в пути, парсинг уже начался).

### 5.4. Решение 3: отмена + счётчик (лучшее)

js

// ✅ ЛУЧШЕ ВСЕГО: отмена экономит ресурсы, счётчик страхует от гонок
let currentController = null;
let currentRequestId = 0;
async function search(query, { onLoading, onSuccess, onError } = {}) {
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
    if (!response.ok) {
      throw new Error(`Сервер вернул ${response.status}`);
    }
    const data = await response.json();
    // Игнорируем, если это устаревший запрос
    if (myId !== currentRequestId) return;
    onSuccess?.(data);
  } catch (error) {
    if (myId !== currentRequestId) return; // устаревшая ошибка
    if (error.name === 'AbortError') return; // отмена — не ошибка
    onError?.(error);
  } finally {
    // Снимаем loading только для актуального запроса
    if (myId === currentRequestId) onLoading?.(false);
  }
}

Две защиты дополняют друг друга:

- **отмена** — экономит ресурсы (не досылаем, не парсим);
    
- **счётчик** — защищает от гонок, когда отмена не сработала.
    

`finally` снимает `loading` только для актуального запроса — иначе устаревший запрос сбросил бы индикатор для нового.

## 6. React-хук `useFetch`

### 6.1. Reducer для согласованного состояния

js

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

Единый reducer не даёт противоречивых состояний (`loading=true` и `error!=null` одновременно). Каждый переход задаёт все три поля сразу.

### 6.2. Хук с отменой

js

import { useEffect, useReducer, useRef } from 'react';
export function useFetch(url, options = {}) {
  const [state, dispatch] = useReducer(reducer, {
    status: 'idle',
    data: null,
    error: null,
  });
  // useRef, чтобы не пересоздавать контроллер при ре-рендере
  const controllerRef = useRef(null);
  useEffect(() => {
    if (!url) return;
    // Отменяем предыдущий запрос (если URL изменился быстро)
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
        // Если запрос отменён — не трогаем состояние
        if (controller.signal.aborted) return;
        dispatch({ type: 'success', payload: data });
      })
      .catch(error => {
        if (error.name === 'AbortError') return;
        dispatch({ type: 'error', payload: error });
      });
    // Cleanup: при размонтировании или смене url
    // Именно он спасает от гонок и утечек памяти
    return () => controller.abort();
  }, [url]);
  return state;
}

Ключевое:

- `useRef` хранит контроллер между рендерами;
    
- `useEffect` с зависимостью `[url]` перезапускается при смене URL;
    
- **cleanup** (`return () => controller.abort()`) вызывается при размонтировании и смене URL — спасает от гонок и утечек;
    
- после `fetch` проверяется `signal.aborted` перед `dispatch`.
    

### 6.3. Компонент с состояниями

js

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

Компонент явно обрабатывает все состояния. Отмена сюда не доходит — хук её глотает.

## 7. Debounce + отмена

Debounce откладывает выполнение, пока пользователь не перестанет печатать. Отмена убивает предыдущий запрос, если пользователь всё-таки продолжил.

js

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

- **debounce** не даёт отправлять запрос на каждый символ — только когда пользователь приостановился;
    
- **abort** отменяет запрос, если пользователь всё-таки продолжил печатать до ответа.
    

## 8. Защита от двойной отправки формы

js

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

Три уровня защиты:

- флаг `isSubmitting` — не пускает повторный вызов;
    
- `button.disabled` — визуальная блокировка;
    
- `abort()` предыдущего запроса — страховка.
    

## 9. Универсальная обёртка `safeFetch`

js

export async function safeFetch(url, { timeout = 10000, signal, ...init } = {}) {
  const timeoutSignal = AbortSignal.timeout(timeout);
  const combined = signal
    ? AbortSignal.any([signal, timeoutSignal])
    : timeoutSignal;
  try {
    const response = await fetch(url, { ...init, signal: combined });
    if (!response.ok) {
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
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/json')) return await response.json();
    return await response.text();
  } catch (error) {
    if (error.name === 'AbortError') {
      // Явно различаем отмену пользователем и таймаут
      if (timeoutSignal.aborted && !signal?.aborted) {
        throw new Error('Превышено время ожидания ответа');
      }
      // Отмена пользователем — возвращаем маркер, не бросаем
      return { aborted: true };
    }
    // Сетевые ошибки (нет интернета, DNS) прилетают как TypeError
    if (error instanceof TypeError) {
      throw new Error('Сетевая ошибка: проверьте подключение');
    }
    throw error;
  }
}

Тонкости:

- **отмена пользователем** → `{ aborted: true }`, не бросается;
    
- **тайм-аут** → обычная ошибка с понятным сообщением. Различить по `timeoutSignal.aborted && !signal?.aborted`;
    
- **сетевые ошибки** (`TypeError`) отделяются от HTTP-ошибок;
    
- **тело** парсится по `Content-Type`, а не вслепую — если прокси вернул HTML, клиент не упадёт.
    

## 10. Паттерн «последний запрос побеждает»

js

function createLatestOnly() {
  let controller = null;
  let id = 0;
  return async function latestOnly(fn) {
    if (controller) controller.abort();
    controller = new AbortController();
    const myId = ++id;
    try {
      const result = await fn(controller.signal);
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
// Быстрые клики — отрендерится только последний
loadUser(1);
loadUser(2);
loadUser(3); // победит этот

- функция `fn` получает `signal` — она должна передать его в `fetch`, иначе отмена не сработает;
    
- устаревшие результаты возвращают `undefined`;
    
- устаревшие ошибки тоже глотаются;
    
- один хелпер покрывает и отмену, и защиту от гонок.
    

## 11. Сводка: что выбирать

|Сценарий|Решение|
|---|---|
|Запрос может зависнуть|`AbortSignal.timeout(ms)`|
|Пользователь может отменить|`AbortController` + `signal`|
|И то, и другое|`AbortSignal.any([userSignal, timeoutSignal])`|
|Быстрый ввод в поиске|debounce + abort|
|Быстрые клики по разным id|abort + счётчик (или `createLatestOnly`)|
|Двойная отправка формы|флаг + `button.disabled` + abort|
|React-компонент|`useEffect` с cleanup-`abort()` + reducer|
|Универсальная обёртка|`safeFetch` с классификацией ошибок|

## 12. Частые ошибки

- **Забыть проверить `response.ok`** — `fetch` не бросает на 404/500.
    
- **Не отличать `AbortError` от настоящей ошибки** — пользователь увидит «ошибка», хотя он просто ушёл со страницы.
    
- **Не отличать `TimeoutError` от `AbortError`** — сообщения должны быть разными.
    
- **Не проверять `signal.aborted` после `await`** — запрос мог быть отменён, пока парсился JSON.
    
- **Не снимать `loading` в `finally` с проверкой актуальности** — устаревший запрос сбросит индикатор для нового.
    
- **Не блокировать кнопку при отправке формы** — двойной клик отправит два запроса.
    
- **Не передавать `signal` в `fetch`** — отмена не сработает, хотя контроллер создан.
    
- **Не различать сетевые ошибки (`TypeError`) и HTTP-ошибки** — сообщения пользователю должны быть разными.
    
- **Парсить тело «вслепую» через `.json()`** — если прокси вернул HTML, клиент упадёт.
    
- **Забыть cleanup в `useEffect`** — при размонтировании компонента запрос продолжит выполняться, а `dispatch` в размонтированный компонент может привести к утечке.