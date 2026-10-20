# Конспект: Pagination, filtering, sorting и search в AJAX

Серверная часть (DTO с валидацией, whitelist полей сортировки, Expression Trees, offset/cursor-пагинация, метаданные) разобрана в `Theory - Sql_etag.md`. Здесь — клиентская сторона и связка «клиент → сервер → клиент»: как в AJAX не терять состояние и не ловить гонки.

---

# Часть 1. Почему в AJAX это сложно

## 1.1. Три источника проблем

**Потеря состояния.** Пользователь отсортировал по имени, перешёл на страницу 2 — сортировка сбросилась, потому что клиент отправил только `page=2`. Сервер применил сортировку по умолчанию.

**Гонки запросов.** Быстрое переключение страниц или ввод в поиске порождает несколько параллельных запросов. Ответы приходят не в том порядке — на экране оказываются данные для устаревшего запроса.

**Выход за контроль.** Клиент передаёт `limit=1000000` или сортирует по секретному полю. Сервер обязан валидировать, но клиент тоже не должен отправлять заведомо плохие запросы.

## 1.2. Что должно быть в каждом AJAX-запросе

Полное состояние списка:

- `page` (или `cursor` для cursor-пагинации);
- `limit`;
- `sort` — поле и направление;
- `q` — поисковая строка;
- `filters` — все активные фильтры.

Если хотя бы один параметр не передан — сервер применит значение по умолчанию, и состояние «съедет». Правило: **клиент всегда отправляет полное состояние**, а не только изменившееся поле.

---

# Часть 2. Единая модель состояния на клиенте

## 2.1. Объект состояния

```js
const state = {
  page: 1,
  limit: 10,
  sort: 'createdAt:desc',
  q: '',
  filters: {
    status: 'active',
    category: 'books',
  },
};
```

Все параметры — в одном объекте. Это единый источник правды: при любом изменении (страница, сортировка, поиск, фильтр) обновляется одно поле, остальные сохраняются.

## 2.2. Сборка URL из состояния

```js
function buildUrl(base, state) {
  const params = new URLSearchParams({
    page: state.page,
    limit: state.limit,
    sort: state.sort,
  });

  if (state.q) params.set('q', state.q);

  for (const [key, value] of Object.entries(state.filters)) {
    if (value != null && value !== '') {
      params.set(key, value);
    }
  }

  return `${base}?${params}`;
}
```

- `sort` и `page`/`limit` передаются всегда — даже если совпадают со значениями по умолчанию. Это делает запрос самодостаточным: сервер не зависит от того, что клиент «помнит».
- `q` и фильтры — только если непустые: пустой `q=` и отсутствие `q` для сервера эквивалентны, но URL чище.
- `URLSearchParams` сам экранирует специальные символы — не нужно вручную вызывать `encodeURIComponent` для каждого значения.

## 2.3. Синхронизация с URL

Лучший способ не потерять состояние — хранить его в URL. Тогда:

- пользователь может скопировать ссылку и поделиться;
- кнопка «назад» работает;
- обновление страницы не сбрасывает состояние;
- deep link на конкретную страницу/сортировку/поиск.

```js
function readStateFromUrl() {
  const params = new URLSearchParams(window.location.search);
  return {
    page: Number(params.get('page')) || 1,
    limit: Number(params.get('limit')) || 10,
    sort: params.get('sort') || 'createdAt:desc',
    q: params.get('q') || '',
    filters: {
      status: params.get('status') || '',
      category: params.get('category') || '',
    },
  };
}

function writeStateToUrl(state) {
  const params = new URLSearchParams();
  params.set('page', state.page);
  params.set('limit', state.limit);
  params.set('sort', state.sort);
  if (state.q) params.set('q', state.q);

  for (const [key, value] of Object.entries(state.filters)) {
    if (value) params.set(key, value);
  }

  history.replaceState(null, '', `?${params}`);
}
```

`replaceState` вместо `pushState` — чтобы каждое изменение фильтра не засоряло историю. Если нужно, чтобы «назад» возвращал к предыдущему фильтру — `pushState`, но тогда история растёт быстро.

---

# Часть 3. Гонки при пагинации и сортировке

## 3.1. Проблема

Пользователь быстро кликает по страницам: 1 → 2 → 3. Три запроса уходят параллельно. Ответ для страницы 1 может прийти последним — и на экране окажутся данные для страницы 1, хотя выбрана страница 3.

Та же проблема при быстром переключении сортировки или фильтров.

## 3.2. Решение: отмена + счётчик

Та же комбинация, что и в поиске (см. `Theory - Fetch, abortcontroller .md`), но применённая к загрузке списка.

```js
let listController = null;
let listRequestId = 0;

async function loadList(state, { onLoading, onSuccess, onError } = {}) {
  if (listController) listController.abort();
  listController = new AbortController();

  const myId = ++listRequestId;
  const signal = listController.signal;

  onLoading?.(true);

  try {
    const url = buildUrl('/api/orders', state);
    const response = await fetch(url, { signal });

    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    // Устаревший запрос — игнорируем
    if (myId !== listRequestId) return;

    onSuccess?.(data);
  } catch (error) {
    if (myId !== listRequestId) return;
    if (error.name === 'AbortError') return;
    onError?.(error);
  } finally {
    if (myId === listRequestId) onLoading?.(false);
  }
}
```

- **abort** отменяет предыдущий запрос — экономит ресурсы;
- **счётчик** защищает от гонок, когда отмена не сработала (ответ уже в пути);
- **`finally` с проверкой** — `loading` снимается только для актуального запроса.

## 3.3. Почему нельзя просто «не отправлять, пока предыдущий в полёте»

Иногда советуют: «пока запрос в полёте, блокируй кнопки». Это работает для формы, но плохо для пагинации: пользователь кликает по страницам быстро, и блокировка делает интерфейс неотзывчивым. Отмена + счётчик лучше: старый запрос отменяется, новый уходит сразу.

---

# Часть 4. Debounce для поиска

## 4.1. Проблема

Ввод в поиске порождает запрос на каждый символ. При быстром наборе — десятки запросов, большинство устаревают, пока дойдут до сервера.

## 4.2. Debounce

```js
function debounce(fn, delay) {
  let timerId = null;
  return function (...args) {
    clearTimeout(timerId);
    timerId = setTimeout(() => fn.apply(this, args), delay);
  };
}

const debouncedSearch = debounce((query) => {
  state.q = query;
  state.page = 1; // поиск сбрасывает страницу
  writeStateToUrl(state);
  loadList(state);
}, 300);
```

- **debounce** откладывает отправку, пока пользователь не приостановился;
- **`state.page = 1`** — новый поиск сбрасывает страницу: результаты для старой страницы не имеют смысла;
- **`writeStateToUrl`** — состояние синхронизируется с URL.

## 4.3. Debounce + abort

Debounce уменьшает число запросов, но не устраняет гонки полностью: если пользователь печатает с паузами, запросы всё равно уходят. Abort в `loadList` отменяет предыдущий — комбинация debounce + abort + счётчик закрывает все случаи.

---

# Часть 5. Сброс страницы при смене фильтра или поиска

## 5.1. Правило

При смене поиска или фильтра страница сбрасывается на 1. Причина: новый фильтр может дать меньше данных, и страница 5 окажется пустой.

При смене сортировки страница **не** сбрасывается: сортировка не меняет набор данных, только порядок.

При смене `limit` страница сбрасывается: страница 5 при `limit=10` и `limit=100` — это разные данные.

## 5.2. Реализация

```js
function setSearch(q) {
  state.q = q;
  state.page = 1;
  writeStateToUrl(state);
  loadList(state);
}

function setFilter(key, value) {
  state.filters[key] = value;
  state.page = 1;
  writeStateToUrl(state);
  loadList(state);
}

function setSort(sort) {
  state.sort = sort;
  // страница не сбрасывается
  writeStateToUrl(state);
  loadList(state);
}

function setPage(page) {
  state.page = page;
  writeStateToUrl(state);
  loadList(state);
}

function setLimit(limit) {
  state.limit = limit;
  state.page = 1;
  writeStateToUrl(state);
  loadList(state);
}
```

Каждая функция меняет **только своё** поле, остальные сохраняются. Это ключ к тому, чтобы состояние не терялось.

---

# Часть 6. Полный цикл «клиент → сервер → клиент»

## 6.1. Клиент

```js
async function loadList(state, handlers) {
  // ... abort + счётчик ...

  const url = buildUrl('/api/orders', state);
  const response = await fetch(url, { signal });
  const data = await response.json();

  if (myId !== listRequestId) return;
  handlers.onSuccess?.(data);
}
```

## 6.2. Сервер

```csharp
[HttpGet]
public async Task<IActionResult> List([FromQuery] QueryDto dto, CancellationToken ct)
{
    var result = await _repo.FindManyAsync(dto, ct);
    return Ok(result);
}
```

`QueryDto` с валидацией, `QueryObject` с whitelist, `PagedResult` с метаданными — см. `Theory - Sql_etag.md`.

## 6.3. Ответ

```json
{
  "data": [ /* ... */ ],
  "meta": {
    "page": 2,
    "limit": 10,
    "total": 143,
    "totalPages": 15,
    "hasNext": true,
    "hasPrev": true
  }
}
```

Клиент использует `meta` для рендера пагинации. `totalPages`, `hasNext`, `hasPrev` вычисляет сервер — клиент не дублирует эту логику.

## 6.4. Рендер

```js
function renderList(data, meta) {
  renderItems(data);
  renderPagination(meta);
  renderSortIndicator(state.sort);
  renderFilters(state.filters);
  renderSearchInput(state.q);
}
```

После каждого успешного ответа все элементы управления синхронизируются с `state`. Если пользователь изменил URL вручную — состояние читается из URL при загрузке страницы.

---

# Часть 7. Cursor-пагинация в AJAX

## 7.1. Отличие от offset

При offset-пагинации клиент передаёт `page` (или `skip`). При cursor — `cursor` (id последней записи) и `limit`. Cursor нельзя «прыгнуть на страницу 5» — только вперёд (или назад с обратной сортировкой).

## 7.2. Состояние

```js
const state = {
  cursor: null,      // null = первая страница
  limit: 20,
  sort: 'id:desc',
  q: '',
  filters: {},
};
```

## 7.3. Загрузка

```js
async function loadMore(state) {
  const params = new URLSearchParams({ limit: state.limit, sort: state.sort });
  if (state.cursor) params.set('cursor', state.cursor);
  if (state.q) params.set('q', state.q);

  const response = await fetch(`/api/feed?${params}`);
  const data = await response.json();

  // data.next — курсор следующей страницы или null
  state.cursor = data.next;
  appendItems(data.data);
}
```

Ключевое отличие: клиент **добавляет** элементы, а не заменяет. Cursor-пагинация обычно используется для бесконечного скролла, где важно не «перерисовать список», а «догрузить ещё».

## 7.4. Сброс при смене фильтра или поиска

При смене фильтра/поиска `cursor` сбрасывается в `null` — начинаем с первой страницы. При смене сортировки — тоже сбрасывается: курсор привязан к конкретному порядку.

```js
function setFilter(key, value) {
  state.filters[key] = value;
  state.cursor = null;
  writeStateToUrl(state);
  loadFirstPage(state);
}
```

---

# Часть 8. Что важно понять

- **Полное состояние в каждом запросе** — иначе сортировка или фильтр потеряются при пагинации.
- **Состояние в URL** — лучший способ не терять его при перезагрузке и делиться ссылкой.
- **Abort + счётчик для пагинации** — те же механизмы, что и в поиске: быстрые клики порождают гонки.
- **Debounce для поиска** — уменьшает число запросов, но не устраняет гонки; нужен abort.
- **Сброс страницы при смене поиска/фильтра/лимита** — иначе можно попасть на пустую страницу.
- **Сортировка не сбрасывает страницу** — набор данных не меняется, только порядок.
- **Cursor-пагинация — append, не replace** — для бесконечного скролла.
- **При смене фильтра/поиска/сортировки cursor сбрасывается** — курсор привязан к порядку.
- **Метаданные вычисляет сервер** — `totalPages`, `hasNext`, `hasPrev` не дублируются на клиенте.
- **Единый объект состояния** — единый источник правды; каждая функция меняет только своё поле.

---

# Часть 9. Частые ошибки

- **Клиент передаёт только изменившееся поле** — сортировка/фильтр сбрасываются при пагинации.
- **Состояние только в памяти** — теряется при перезагрузке, нельзя поделиться ссылкой.
- **Нет abort при пагинации** — быстрые клики дают гонки, на экране устаревшие данные.
- **Нет счётчика** — abort не всегда срабатывает (ответ уже в пути).
- **Debounce без abort** — при паузах в наборе запросы всё равно уходят параллельно.
- **Не сбрасывать страницу при смене поиска** — можно попасть на пустую страницу.
- **Сбрасывать страницу при смене сортировки** — пользователь теряет контекст без причины.
- **Не сбрасывать cursor при смене фильтра** — курсор привязан к старому порядку, данные будут некорректны.
- **`pushState` на каждое изменение фильтра** — история растёт, «назад» работает неожиданно.
- **Клиент вычисляет `totalPages` сам** — при расхождении с сервером пагинация ломается.
- **`URLSearchParams` без экранирования вручную** — лишние вызовы `encodeURIComponent` могут задвоить экранирование.
- **`limit` без ограничения на клиенте** — сервер отклонит, но лучше не отправлять заведомо плохой запрос.
- **Не синхронизировать элементы управления с состоянием после ответа** — если пользователь изменил URL вручную, UI покажет старое.