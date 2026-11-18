// Hata pop-up'ı (sunucudan gelen hata veya istemci tarafı ön kontrol).
window.showAlert = function (type, title, message) {
    document.getElementById('appAlertHeader').className = 'modal-header alert-' + type;
    document.getElementById('appAlertTitle').textContent = title;
    document.getElementById('appAlertBody').textContent = message;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('appAlert')).show();
};

function formatSize(bytes) {
    return bytes >= 1048576 ? (bytes / 1048576).toFixed(1).replace('.', ',') + ' MB' : Math.max(1, Math.round(bytes / 1024)) + ' KB';
}

// Uzantı ve boyut ön kontrolü (asıl kontrol sunucuda yapılır). Geçerliyse true döner.
function checkFile(input, file) {
    const allowed = (input.dataset.allowed || '').split(',').filter(Boolean);
    const ext = '.' + file.name.split('.').pop().toLowerCase();
    const maxBytes = parseInt(input.dataset.maxBytes || '0', 10);
    if (allowed.length && !allowed.includes(ext)) {
        showAlert('danger', 'Dosya kabul edilmedi', "'" + ext + "' formatı kabul edilmiyor. Kabul edilen formatlar: " + allowed.join(', '));
        return false;
    }
    if (maxBytes > 0 && file.size > maxBytes) {
        showAlert('danger', 'Dosya çok büyük', 'Dosya ' + formatSize(file.size) + '. En fazla ' + formatSize(maxBytes) + ' yüklenebilir.');
        return false;
    }
    return true;
}

document.addEventListener('DOMContentLoaded', function () {
    const modal = document.getElementById('appAlert');
    if (modal && modal.dataset.autoShow === 'true') bootstrap.Modal.getOrCreateInstance(modal).show();

    // Onay isteyen formlar (silme, tamamlama)
    document.querySelectorAll('form[data-confirm]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            if (!window.confirm(form.dataset.confirm)) e.preventDefault();
        });
    });

    // Sürükle-bırak dosya alanları
    document.querySelectorAll('[data-dropzone]').forEach(function (zone) {
        const input = zone.querySelector('input[type=file]');
        const label = zone.querySelector('[data-dz-file]');

        function show(file) {
            if (!file) { zone.classList.remove('has-file'); label.textContent = ''; return; }
            label.textContent = file.name + ' (' + formatSize(file.size) + ')';
            zone.classList.add('has-file');
        }

        input.addEventListener('change', function () {
            const file = input.files[0];
            if (file && !checkFile(input, file)) { input.value = ''; show(null); return; }
            show(file);
        });

        ['dragenter', 'dragover'].forEach(function (ev) {
            zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.add('dragover'); });
        });
        ['dragleave', 'drop'].forEach(function (ev) {
            zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.remove('dragover'); });
        });
        zone.addEventListener('drop', function (e) {
            const file = e.dataTransfer.files[0];
            if (!file) return;
            if (!checkFile(input, file)) return;
            const dt = new DataTransfer();
            dt.items.add(file);
            input.files = dt.files;
            show(file);
        });
    });

    // Yalnızca rakam alan kutular (TC kimlik no, telefon, sayılar): harf ve işaretler yazılamaz.
    document.querySelectorAll('input[data-digits]').forEach(function (input) {
        input.addEventListener('input', function () {
            let clean = input.value.replace(/\D/g, '');
            if (input.maxLength > 0) clean = clean.slice(0, input.maxLength);
            if (clean !== input.value) input.value = clean;
            validateDigits(input, false);
        });
        input.addEventListener('blur', function () { validateDigits(input, true); });
    });

    // Uyruk değişince kimlik numarası yeniden kontrol edilir.
    const nationality = document.querySelector('[data-nationality]');
    const nationalId = document.querySelector('input[data-tc]');
    if (nationality && nationalId) {
        nationality.addEventListener('input', function () { if (nationalId.value) validateDigits(nationalId, true); });
    }

    initPhoneInputs();
});

// Uzunluk ön kontrolü (asıl kontrol sunucuda yapılır). Hata, alanın altında gösterilir.
function validateDigits(input, onBlur) {
    let message = '';
    const v = input.value;
    if (v && input.hasAttribute('data-tc')) {
        // Uyruğa göre kural (proje dokümanı 6.2): T.C. → ilk hane 0 olamaz; yabancı → 99 ile başlar.
        const nat = document.querySelector('[data-nationality]');
        const turkish = isTurkish(nat ? nat.value : '');
        if (!turkish && v.length >= 2 && !v.startsWith('99')) message = 'Yabancı uyruklu öğrencilerin kimlik numarası 99 ile başlamalıdır.';
        else if (turkish && v[0] === '0') message = 'T.C. kimlik numarası 0 ile başlayamaz.';
        else if (v.length !== 11 && onBlur) message = 'Kimlik numarası 11 haneli olmalıdır (' + v.length + ' hane girildi).';
        else if (turkish && v.length === 11 && !tcChecksum(v)) message = 'T.C. kimlik numarası geçerli değil; lütfen rakamları kontrol edin.';
    } else if (v && input.dataset.min) {
        const min = parseInt(input.dataset.min, 10), max = input.maxLength;
        if (v[0] === '0') message = 'Numara 0 ile başlamamalı; ülke kodundan sonraki kısmı yazın.';
        else if (onBlur && v.length < min) message = min === max ? 'Numara ' + max + ' haneli olmalıdır.' : 'Numara ' + min + '-' + max + ' haneli olmalıdır.';
    }

    input.setCustomValidity(message);
    input.classList.toggle('is-invalid', !!message);
    const phone = input.closest('[data-phone]');
    if (phone) phone.classList.toggle('is-invalid', !!message);

    const field = (phone || input).parentElement;
    let note = field.querySelector('.field-error');
    if (message && !note) {
        note = document.createElement('div');
        note.className = 'field-error';
        note.id = input.id + '_err';
        input.setAttribute('aria-describedby', note.id);
        field.appendChild(note);
    }
    if (note) note.textContent = message;
}

// Sunucudaki NationalIdRules.IsTurkish ile aynı: boş, "T.C.", "Türkiye" vb. → T.C. uyruklu.
function isTurkish(nationality) {
    const map = { 'ç': 'c', 'ğ': 'g', 'ı': 'i', 'ö': 'o', 'ş': 's', 'ü': 'u', 'â': 'a', 'î': 'i', 'û': 'u' };
    const key = (nationality || '').toLocaleLowerCase('tr').replace(/[çğıöşüâîû]/g, function (c) { return map[c]; }).replace(/[^a-z0-9]/g, '');
    return key === '' || ['tc', 'turkiye', 'turk', 'turkiyecumhuriyeti', 'tcvatandasi'].includes(key);
}

function tcChecksum(id) {
    const d = id.split('').map(Number);
    const odd = d[0] + d[2] + d[4] + d[6] + d[8];
    const even = d[1] + d[3] + d[5] + d[7];
    const tenth = ((odd * 7 - even) % 10 + 10) % 10;
    const eleventh = d.slice(0, 10).reduce(function (a, b) { return a + b; }, 0) % 10;
    return d[9] === tenth && d[10] === eleventh;
}

// Telefon alanları: soldaki "+90" düğmesine tıklayınca ülke listesi açılır (ülke adı veya koda göre aranabilir).
function initPhoneInputs() {
    const data = document.getElementById('country-codes');
    if (!data) return;
    const countries = JSON.parse(data.textContent);
    let openMenu = null;

    function close() {
        if (!openMenu) return;
        openMenu.menu.remove();
        openMenu.button.setAttribute('aria-expanded', 'false');
        openMenu = null;
    }

    document.addEventListener('click', function (e) {
        if (openMenu && !openMenu.wrapper.contains(e.target)) close();
    });

    document.querySelectorAll('[data-phone]').forEach(function (wrapper, index) {
        const button = wrapper.querySelector('.phone-cc');
        const hidden = wrapper.querySelector('[data-phone-code]');
        const number = wrapper.querySelector('input[type=tel]');

        function select(c) {
            hidden.value = c.code;
            button.textContent = '+' + c.code;
            button.title = c.name;
            button.setAttribute('aria-label', 'Ülke kodu: ' + c.name + ' +' + c.code + '. Değiştirmek için tıklayın');
            number.maxLength = c.max;
            number.dataset.min = c.min;
            number.placeholder = c.code === '90' ? '5321234567' : '';
            if (number.value.length > c.max) number.value = number.value.slice(0, c.max);
            close();
            validateDigits(number, number.value.length > 0);
            number.focus();
        }

        button.addEventListener('click', function () {
            if (openMenu && openMenu.wrapper === wrapper) { close(); return; }
            close();
            const menu = document.createElement('div');
            menu.className = 'phone-menu';
            const listId = 'cc-list-' + index;
            menu.innerHTML = '<input type="search" placeholder="Ülke adı veya kod ara" aria-label="Ülke ara" autocomplete="off"><ul role="listbox"></ul>';
            const search = menu.querySelector('input');
            const list = menu.querySelector('ul');
            list.id = listId;
            search.setAttribute('aria-controls', listId);
            let items = [];
            let active = 0;

            function render() {
                const q = search.value.trim().toLocaleLowerCase('tr').replace('+', '');
                items = countries.filter(function (c) {
                    return !q || c.name.toLocaleLowerCase('tr').includes(q) || c.code.startsWith(q);
                });
                list.innerHTML = '';
                if (!items.length) { list.innerHTML = '<li class="empty">Sonuç yok</li>'; return; }
                items.forEach(function (c) {
                    const li = document.createElement('li');
                    li.setAttribute('role', 'option');
                    li.setAttribute('aria-selected', c.code === hidden.value ? 'true' : 'false');
                    const name = document.createElement('span');
                    name.textContent = c.name;
                    const code = document.createElement('span');
                    code.className = 'cc';
                    code.textContent = '+' + c.code;
                    li.append(name, code);
                    li.addEventListener('click', function () { select(c); });
                    list.appendChild(li);
                });
                highlight();
            }
            function highlight() {
                list.querySelectorAll('li[role=option]').forEach(function (li, i) { li.classList.toggle('active', i === active); });
                const el = list.children[active];
                if (el) el.scrollIntoView({ block: 'nearest' });
            }

            search.addEventListener('input', function () { active = 0; render(); });
            search.addEventListener('keydown', function (e) {
                if (e.key === 'ArrowDown') { e.preventDefault(); active = Math.min(items.length - 1, active + 1); highlight(); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); active = Math.max(0, active - 1); highlight(); }
                else if (e.key === 'Enter') { e.preventDefault(); if (items[active]) select(items[active]); }
                else if (e.key === 'Escape') { close(); button.focus(); }
            });

            wrapper.appendChild(menu);
            button.setAttribute('aria-expanded', 'true');
            openMenu = { wrapper: wrapper, menu: menu, button: button };
            active = Math.max(0, countries.findIndex(function (c) { return c.code === hidden.value; }));
            render();
            search.focus();
        });
    });
}
