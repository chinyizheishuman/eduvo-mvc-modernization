(() => {
    const search = document.getElementById('catalogue-search');
    if (!search) return;

    const items = Array.from(document.querySelectorAll('[data-catalogue-item]'));
    const section = items[0].closest('[data-add-url]');
    const count = document.getElementById('catalogue-count');
    const empty = document.getElementById('catalogue-empty');

    search.closest('.catalogue-search').hidden = false;
    search.addEventListener('input', () => {
        const query = search.value.trim().toLowerCase();
        let visible = 0;
        for (const item of items) {
            const text = item.querySelector('.product-title').textContent + ' ' +
                item.querySelector('.catalogue-description').textContent;
            item.hidden = !text.toLowerCase().includes(query);
            if (!item.hidden) visible++;
        }
        count.textContent = visible + (visible === 1 ? ' product' : ' products');
        empty.hidden = visible !== 0;
    });

    if (!window.jQuery) return;

    for (const item of items) {
        const quantity = item.querySelector('.qty-input');
        const decrease = item.querySelector('[data-quantity-step="-1"]');
        const increase = item.querySelector('[data-quantity-step="1"]');
        const add = item.querySelector('.btn-add-cart');
        const feedback = item.querySelector('.catalogue-feedback');
        const stock = Number(item.dataset.stock);
        let pending = false;

        function updateControls() {
            decrease.disabled = pending || Number(quantity.value) <= 1;
            increase.disabled = pending || Number(quantity.value) >= stock;
            add.setAttribute('aria-disabled', String(pending));
        }

        function changeQuantity(step) {
            quantity.value = Math.max(1, Math.min(stock, Number(quantity.value) + step));
            updateControls();
        }

        decrease.addEventListener('click', () => changeQuantity(-1));
        increase.addEventListener('click', () => changeQuantity(1));
        updateControls();

        add.disabled = false;
        add.addEventListener('click', () => {
            if (pending) return;
            pending = true;
            updateControls();
            add.textContent = 'Adding…';
            feedback.textContent = '';

            $.ajax({
                url: section.dataset.addUrl,
                type: 'POST',
                data: { productId: item.dataset.productId, quantity: quantity.value }
            })
                .done(response => {
                    feedback.textContent = response.message || 'Could not update your cart.';
                    feedback.classList.toggle('text-danger', !response.success);
                    if (response.success) {
                        const cart = document.createElement('a');
                        cart.href = section.dataset.cartUrl;
                        cart.textContent = 'View cart';
                        feedback.append(cart);
                        quantity.value = 1;
                    }
                })
                .fail(() => {
                    feedback.textContent = 'Could not update your cart. Try again.';
                    feedback.classList.add('text-danger');
                })
                .always(() => {
                    pending = false;
                    add.textContent = 'Add to cart';
                    updateControls();
                });
        });
    }
})();
