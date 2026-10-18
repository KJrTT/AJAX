(function () {
    'use strict';

    const root = document.getElementById('tasks');
    if (!root) return;

    const endpoint = root.dataset.endpoint;
    const errorBox = document.getElementById('tasks-error');
    const template = document.getElementById('task-card-template');

    function showError(msg) {
        errorBox.textContent = msg;
        errorBox.classList.remove('d-none');
    }

    function clearError() {
        errorBox.textContent = '';
        errorBox.classList.add('d-none');
    }

    function formatDate(value) {
        const d = new Date(value);
        return isNaN(d) ? '—' : d.toLocaleDateString('ru-RU');
    }

    function render(tasks) {
        if (!tasks.length) {
            root.innerHTML = '<p class="text-muted">Задач не найдено.</p>';
            return;
        }

        const frag = document.createDocumentFragment();
        tasks.forEach(t => {
            const node = template.content.cloneNode(true);
            node.querySelector('[data-field="title"]').textContent = t.title;
            node.querySelector('[data-field="category"]').textContent = t.category;
            node.querySelector('[data-field="status"]').textContent = t.status;
            node.querySelector('[data-field="priority"]').textContent = t.priority;
            node.querySelector('[data-field="dueDate"]').textContent = formatDate(t.dueDate);
            node.querySelector('[data-field="versions"]').dataset.id = t.id;
            frag.appendChild(node);
        });

        root.innerHTML = '';
        root.appendChild(frag);
    }

    async function load(status) {
        clearError();
        root.innerHTML = `<p class="text-muted">${root.dataset.loadingText}</p>`;

        const url = status
            ? `${endpoint}?status=${encodeURIComponent(status)}`
            : endpoint;

        try {
            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            render(await res.json());
        } catch (err) {
            console.error(err);
            showError('Не удалось загрузить задачи: ' + err.message);
        }
    }


    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.addEventListener('click', () => load(btn.dataset.status || null));
    });


    root.addEventListener('click', async e => {
        const btn = e.target.closest('.versions-btn');
        if (!btn) return;

        const id = btn.dataset.id;
        try {
            const res = await fetch(`${endpoint}/${id}/versions`, {
                headers: { 'Accept': 'application/json' }
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const versions = await res.json();

            if (!versions.length) {
                alert('У задачи пока нет версий.');
                return;
            }

            const lines = versions.map(v => {
                let line = `v${v.versionNumber} (${formatDate(v.createdAt)}): ${v.title}`;
                if (v.plannedStart || v.plannedEnd) {
                    line += `\n   План: ${formatDate(v.plannedStart)} → ${formatDate(v.plannedEnd)}`;
                }
                return line;
            });

            alert('Версии задачи:\n\n' + lines.join('\n'));
        } catch (err) {
            showError('Не удалось загрузить версии: ' + err.message);
        }
    });

    load(null);
})();