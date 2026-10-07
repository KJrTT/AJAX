// =============================================================================
// files-page.js — логика страницы /Files.
// Работает поверх FilesClient (см. files-client.js).
// =============================================================================

(function () {
    'use strict';

    const STORAGE_KEY = 'securefiles.uploads';

    function loadHistory() {
        try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]'); }
        catch { return []; }
    }

    function saveHistory(items) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
    }

    function addToHistory(meta) {
        const items = loadHistory();
        items.unshift(meta);
        saveHistory(items);
        renderHistory();
    }

    function removeFromHistory(id) {
        const items = loadHistory().filter(x => x.id !== id);
        saveHistory(items);
        renderHistory();
    }

    function renderHistory() {
        const items = loadHistory();
        const tbody = document.getElementById('filesBody');
        const empty = document.getElementById('emptyMessage');
        if (!tbody || !empty) return;

        tbody.innerHTML = '';

        if (items.length === 0) {
            empty.style.display = '';
            return;
        }
        empty.style.display = 'none';

        for (const item of items) {
            const tr = document.createElement('tr');

            const tdName = document.createElement('td');
            tdName.textContent = item.name;

            const tdSize = document.createElement('td');
            tdSize.textContent = formatSize(item.size);

            const tdType = document.createElement('td');
            tdType.textContent = item.contentType;

            const tdId = document.createElement('td');
            tdId.innerHTML = '<code>' + item.id + '</code>';

            const tdActions = document.createElement('td');

            const btnDownload = document.createElement('button');
            btnDownload.textContent = '⬇ Скачать';
            btnDownload.className = 'btn btn-sm btn-primary';
            btnDownload.addEventListener('click', () => downloadFile(item));

            const btnRemove = document.createElement('button');
            btnRemove.textContent = '✕';
            btnRemove.className = 'btn btn-sm btn-outline-danger ms-1';
            btnRemove.addEventListener('click', () => removeFromHistory(item.id));

            tdActions.appendChild(btnDownload);
            tdActions.appendChild(btnRemove);

            tr.appendChild(tdName);
            tr.appendChild(tdSize);
            tr.appendChild(tdType);
            tr.appendChild(tdId);
            tr.appendChild(tdActions);
            tbody.appendChild(tr);
        }
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' Б';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' КБ';
        return (bytes / 1024 / 1024).toFixed(2) + ' МБ';
    }

    async function uploadFile() {
        const file = document.getElementById('fileInput').files[0];
        const apiKey = document.getElementById('apiKey').value;
        const result = document.getElementById('result');
        const progress = document.getElementById('progress');
        const uploadBtn = document.getElementById('uploadBtn');

        if (!file) { result.className = 'text-danger'; result.textContent = 'Выберите файл.'; return; }
        if (!apiKey) { result.className = 'text-danger'; result.textContent = 'Введите X-Api-Key.'; return; }

        uploadBtn.disabled = true;
        uploadBtn.textContent = 'Загрузка…';
        result.textContent = '';
        progress.value = 0;

        try {
            const res = await FilesClient.upload(file, { apiKey, onProgress: p => progress.value = p });
            result.className = 'text-success';
            result.textContent = 'Загружено: ' + res.data.name + ' (' + res.data.id + ')';

            addToHistory({
                id: res.data.id,
                name: res.data.name ?? file.name,
                size: res.data.size ?? file.size,
                contentType: res.data.contentType ?? file.type,
                uploadedAt: new Date().toISOString()
            });

            document.getElementById('fileInput').value = '';
            progress.value = 0;
        } catch (e) {
            result.className = 'text-danger';
            result.textContent = e.code === 'unauthorized' ? 'Ключ не принят.'
                : e.code === 'validation_failed' ? 'Файл отклонён: ' + e.message
                    : `[${e.code}] ${e.message}`;
        } finally {
            uploadBtn.disabled = false;
            uploadBtn.textContent = 'Загрузить';
        }
    }

    async function downloadFile(item) {
        const apiKey = document.getElementById('apiKey').value;
        try {
            const { blob, filename } = await FilesClient.download(item.id, { apiKey });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename || item.name || item.id;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) {
            alert('Не удалось скачать: ' + e.message);
        }
    }

    // ⚠️ ЭТО ГЛАВНОЕ — без этого кнопка не привяжется!
    document.addEventListener('DOMContentLoaded', () => {
        const uploadBtn = document.getElementById('uploadBtn');
        if (uploadBtn) uploadBtn.addEventListener('click', uploadFile);
        renderHistory();
    });
})();
