// =============================================================================
// files-client.js — тонкий клиент для Files API.
//
// ПРИНЦИПЫ "тонкого" клиента:
//   1. Никакой бизнес-логики — только транспорт HTTP.
//   2. Вся валидация на сервере; клиент валидирует только UX-вещи
//      (например, чтобы сразу показать "файл слишком большой").
//   3. Явная работа с HTTP-границами:
//        - 2xx      → успех,
//        - 4xx      → ошибка клиента (валидация, auth) — не повторяем,
//        - 5xx/сеть → ошибка сервера — можно повторить с backoff,
//        - 401/403  → отдельный сигнал на ре-аутентификацию.
//   4. Никогда не доверяем формату ответа: и 200, и 4xx могут быть JSON,
//      но может прийти и HTML от прокси. Парсим аккуратно.
// =============================================================================

(function (global) {
    'use strict';

    const API_BASE = '/api/files';

    /**
     * Ошибка API с полями:
     *   code       — стабильный код для switch-case ('validation_failed', 'not_found', ...)
     *   status     — HTTP-статус
     *   correlationId — id для поиска в логах сервера
     */
    class ApiError extends Error {
        constructor(message, { code = 'unknown', status = 0, correlationId = null } = {}) {
            super(message);
            this.name = 'ApiError';
            this.code = code;
            this.status = status;
            this.correlationId = correlationId;
        }
    }

    /**
     * Универсальный парсер тела ответа.
     *
     * HTTP-граница: сервер может ответить JSON'ом, а может — HTML'ом
     * (например, nginx 502, или антивирус-прокси 403).
     * Никогда не вызываем response.json() "вслепую".
     */
    async function parseBody(response) {
        const ct = response.headers.get('content-type') || '';

        // JSON — ожидаемый формат для нашего API.
        if (ct.includes('application/json')) {
            try { return await response.json(); }
            catch { return null; }
        }

        // Всё остальное — читаем как текст, чтобы хотя бы залогировать.
        const text = await response.text().catch(() => '');
        return text ? { message: text.slice(0, 500) } : null;
    }

    /**
     * Общая обёртка вокруг fetch.
     *
     * @param {string} url
     * @param {RequestInit} init
     * @param {{signal?: AbortSignal}} opts
     */
    async function request(url, init = {}, opts = {}) {
        // Заголовки, которые добавляем автоматически.
        // X-Request-Id — для коррелированного логирования.
        const requestId =
            (global.crypto && crypto.randomUUID && crypto.randomUUID()) ||
            Math.random().toString(36).slice(2);

        const headers = Object.assign(
            { 'X-Request-Id': requestId },
            init.headers || {}
        );

        let response;
        try {
            response = await fetch(url, {
                ...init,
                headers,
                // credentials 'same-origin' — куки не уйдут на сторонние домены.
                credentials: 'same-origin',
                // signal: позволяет отменить запрос (пользователь ушёл со страницы).
                signal: opts.signal
            });
        } catch (e) {
            // Сетевой сбой: DNS, offline, CORS, abort.
            if (e.name === 'AbortError') throw e;
            throw new ApiError('Сеть недоступна', { code: 'network_error', status: 0 });
        }

        const correlationId = response.headers.get('X-Request-Id') || requestId;

        // ---- HTTP-ГРАНИЦА: маршрутизация по статусу -----------------------
        if (response.ok) {
            return { data: await parseBody(response), status: response.status, correlationId };
        }

        const body = await parseBody(response);

        // 401/403 — специальные коды: клиент должен попросить ре-аутентификацию.
        if (response.status === 401 || response.status === 403) {
            throw new ApiError(body?.message || 'Нет доступа', {
                code: response.status === 401 ? 'unauthorized' : 'forbidden',
                status: response.status,
                correlationId
            });
        }

        // 400/404/409/413/415 — ошибки клиента: повторять бессмысленно.
        if (response.status >= 400 && response.status < 500) {
            throw new ApiError(body?.message || `Ошибка ${response.status}`, {
                code: body?.code || 'client_error',
                status: response.status,
                correlationId
            });
        }

        // 5xx — сервер. Можно повторить, но здесь просто пробрасываем наверх.
        throw new ApiError(body?.message || 'Ошибка сервера', {
            code: body?.code || 'server_error',
            status: response.status,
            correlationId
        });
    }

    // -----------------------------------------------------------------------
    // Публичный API клиента
    // -----------------------------------------------------------------------
    const FilesClient = {
        /**
         * Загрузка файла.
         * @param {File} file
         * @param {{apiKey: string, onProgress?: (pct:number)=>void, signal?: AbortSignal}} opts
         */
        upload(file, opts) {
            // Клиентская валидация — только чтобы не гонять мегабайты зря.
            // Авторитетная валидация всё равно на сервере.
            if (!file || !(file instanceof File)) {
                return Promise.reject(new ApiError('Не выбран файл', { code: 'no_file' }));
            }

            return new Promise((resolve, reject) => {
                const form = new FormData();
                form.append('file', file, file.name);

                // XHR вместо fetch — только из-за progress-события.
                // fetch не умеет upload progress в большинстве браузеров.
                const xhr = new XMLHttpRequest();
                xhr.open('POST', API_BASE);
                xhr.setRequestHeader('X-Api-Key', opts.apiKey);
                xhr.setRequestHeader('X-Request-Id',
                    (global.crypto?.randomUUID?.() || Math.random().toString(36).slice(2)));

                if (opts.signal) {
                    opts.signal.addEventListener('abort', () => xhr.abort());
                }

                xhr.upload.onprogress = (e) => {
                    if (e.lengthComputable && opts.onProgress) {
                        opts.onProgress(Math.round((e.loaded / e.total) * 100));
                    }
                };

                xhr.onload = () => {
                    const cid = xhr.getResponseHeader('X-Request-Id');
                    let body = null;
                    try { body = JSON.parse(xhr.responseText); } catch { /* ignore */ }

                    if (xhr.status >= 200 && xhr.status < 300) {
                        resolve({ data: body, status: xhr.status, correlationId: cid });
                    } else {
                        reject(new ApiError(body?.message || `Ошибка ${xhr.status}`, {
                            code: body?.code || 'client_error',
                            status: xhr.status,
                            correlationId: cid
                        }));
                    }
                };

                xhr.onerror = () => reject(new ApiError('Сеть недоступна', { code: 'network_error' }));
                xhr.onabort = () => reject(new DOMException('Aborted', 'AbortError'));

                xhr.send(form);
            });
        },

        /**
         * Скачивание файла по id.
         * Возвращает Blob — вызывающий код сам решает, что с ним делать.
         */
        async download(id, opts = {}) {
            // Валидация id на клиенте — просто чтобы не делать заведомо плохой запрос.
            if (!/^[0-9a-f]{32}$/i.test(id)) {
                throw new ApiError('Некорректный id файла', { code: 'bad_id' });
            }

            const response = await fetch(`${API_BASE}/${encodeURIComponent(id)}`, {
                method: 'GET',
                headers: {
                    'X-Api-Key': opts.apiKey,
                    'X-Request-Id': crypto.randomUUID?.() ?? ''
                },
                credentials: 'same-origin',
                signal: opts.signal
            });

            if (!response.ok) {
                const body = await parseBody(response);
                throw new ApiError(body?.message || `Ошибка ${response.status}`, {
                    code: body?.code || 'client_error',
                    status: response.status,
                    correlationId: response.headers.get('X-Request-Id')
                });
            }

            // Имя из Content-Disposition, если есть — иначе generic.
            const cd = response.headers.get('content-disposition') || '';
            const match = /filename\*=UTF-8''([^;]+)/i.exec(cd);
            const filename = match ? decodeURIComponent(match[1]) : id;

            const blob = await response.blob();
            return { blob, filename };
        }
    };

    // Экспорт в window.
    global.FilesClient = FilesClient;
    global.FilesApiError = ApiError;
})(window);
