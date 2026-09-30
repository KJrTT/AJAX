(function () {
    'use strict';

    const root = document.getElementById('catalog');
    if (!root) return;

    const endpoint = root.dataset.endpoint;
    const errorBox = document.getElementById('catalog-error');
    const template = document.getElementById('product-card-template');

    function showError(msg) {
        errorBox.textContent = msg;
        errorBox.classList.remove('d-none');
    }

    function clearError() {
        errorBox.textContent = '';
        errorBox.classList.add('d-none');
    }

    function render(products) {
        if (!products.length) {
            root.innerHTML = '<p class="text-muted">Ничего не найдено.</p>';
            return;
        }

        const frag = document.createDocumentFragment();
        products.forEach(p => {
            const node = template.content.cloneNode(true);
            node.querySelector('[data-field="name"]').textContent = p.name;
            node.querySelector('[data-field="category"]').textContent = p.category;
            node.querySelector('[data-field="price"]').textContent =
                new Intl.NumberFormat('ru-RU').format(p.price);
            const btn = node.querySelector('[data-field="details"]');
            btn.dataset.id = p.id;
            frag.appendChild(node);
        });

        root.innerHTML = '';
        root.appendChild(frag);
    }

    async function load(category) {
        clearError();
        root.innerHTML = `<p class="text-muted">${root.dataset.loadingText}</p>`;

        const url = category
            ? ` ${endpoint}?category=${encodeURIComponent(category)}`
            : endpoint;

        try {
            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) throw new Error('HTTP ${res.status}');
            render(await res.json());
        } catch (err) {
            console.error(err);
            showError('Не удалось загрузить каталог: ' + err.message);
        }
    }

    // Фильтры
    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.addEventListener('click', () => load(btn.dataset.category || null));
    });

    // Кнопка "Подробнее" (делегирование)
    root.addEventListener('click', async e => {
        const btn = e.target.closest('.details-btn');
        if (!btn) return;

        const id = btn.dataset.id;
        try {
            const res = await fetch('${endpoint}/${id}');
            if (!res.ok) throw new Error('HTTP ${res.status}');
            const p = await res.json();
            alert('${p.name}\nКатегория: ${p.category}\nЦена: ${p.price} ₽');
        } catch (err) {
            showError('Не удалось загрузить товар: ' + err.message);
        }
    });

    load(null);
})();