// Mandantengruppen: Anlegen, Filtern und Zuordnen (Drag & Drop sowie Tastatur-Alternative). Alle Texte werden nur über textContent ausgegeben.
(function () {
    'use strict';
    var host = document.getElementById('mg-app');
    if (!host) { return; }

    var urls = {
        daten: host.getAttribute('data-daten-url'), anlegen: host.getAttribute('data-anlegen-url'),
        zuordnen: host.getAttribute('data-zuordnen-url'), entfernen: host.getAttribute('data-entfernen-url')
    };
    var token = host.querySelector('input[name="__RequestVerificationToken"]').value;
    var meldung = document.getElementById('mg-meldung');
    var clientFilter = document.getElementById('mg-client-filter');
    var gruppenFilter = document.getElementById('mg-gruppen-filter');
    var clientsEl = document.getElementById('mg-clients');
    var gruppenEl = document.getElementById('mg-gruppen');
    var state = null;
    var timer = null;
    var requestNo = 0;

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) { e.className = cls; }
        if (text !== undefined && text !== null) { e.textContent = text; }
        return e;
    }

    function zeigeMeldung(text, fehler) {
        meldung.hidden = !text;
        meldung.textContent = text || '';
        meldung.className = 'alert py-2 ' + (fehler ? 'alert-danger' : 'alert-success');
    }

    function postJson(url, body) {
        return fetch(url, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(body)
        }).then(function (r) {
            if (r.ok) { return r.status === 204 ? null : r.json(); }
            return r.json().catch(function () { return {}; }).then(function (j) {
                throw new Error(j && j.fehler ? j.fehler : 'Die Aktion konnte nicht ausgeführt werden.');
            });
        });
    }

    function laden() {
        var no = ++requestNo;
        var q = '?gruppen=' + encodeURIComponent(gruppenFilter.value) + '&clients=' + encodeURIComponent(clientFilter.value);
        return fetch(urls.daten + q, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
            .then(function (r) { if (!r.ok) { throw new Error(); } return r.json(); })
            .then(function (d) { if (no === requestNo) { state = d; render(); } })
            .catch(function () { zeigeMeldung('Die Daten konnten nicht geladen werden.', true); });
    }

    function clientLabel(c) { return c.nummer + ' – ' + c.name; }

    function clientItem(c) {
        var li = el('li', 'mg-client');
        li.draggable = true;
        li.setAttribute('data-client-id', c.id);
        li.addEventListener('dragstart', function (ev) {
            ev.dataTransfer.setData('text/plain', c.id);
            ev.dataTransfer.effectAllowed = 'move';
            li.classList.add('mg-dragging');
        });
        li.addEventListener('dragend', function () { li.classList.remove('mg-dragging'); });

        var info = el('div', 'mg-client-info');
        info.appendChild(el('div', 'fw-semibold', clientLabel(c)));
        var sub = [c.organisation, c.niederlassung].filter(Boolean).join(' · ');
        if (sub) { info.appendChild(el('div', 'small text-muted', sub)); }
        li.appendChild(info);

        var aktionen = el('div', 'mg-client-aktionen');
        if (c.gruppeId === null || c.gruppeId === undefined) {
            var sel = el('select', 'form-select form-select-sm');
            sel.setAttribute('aria-label', 'Gruppe für ' + clientLabel(c));
            var opt0 = el('option', null, 'Gruppe wählen …'); opt0.value = '';
            sel.appendChild(opt0);
            state.gruppen.forEach(function (g) { var o = el('option', null, g.name); o.value = String(g.id); sel.appendChild(o); });
            var btn = el('button', 'btn btn-sm btn-outline-primary', 'Zuordnen');
            btn.type = 'button';
            btn.addEventListener('click', function () {
                if (!sel.value) { zeigeMeldung('Bitte wählen Sie zuerst eine Gruppe.', true); sel.focus(); return; }
                zuordnen(c.id, parseInt(sel.value, 10));
            });
            aktionen.appendChild(sel); aktionen.appendChild(btn);
        } else {
            var rm = el('button', 'btn btn-sm btn-outline-danger', 'Entfernen');
            rm.type = 'button';
            rm.setAttribute('aria-label', clientLabel(c) + ' aus der Gruppe entfernen');
            rm.addEventListener('click', function () { entfernen(c.id); });
            aktionen.appendChild(rm);
        }
        li.appendChild(aktionen);
        return li;
    }

    function dropZone(node, gruppeId) {
        node.addEventListener('dragover', function (ev) { ev.preventDefault(); ev.dataTransfer.dropEffect = 'move'; node.classList.add('mg-drop-aktiv'); });
        node.addEventListener('dragleave', function () { node.classList.remove('mg-drop-aktiv'); });
        node.addEventListener('drop', function (ev) {
            ev.preventDefault();
            node.classList.remove('mg-drop-aktiv');
            var id = ev.dataTransfer.getData('text/plain');
            if (!id) { return; }
            if (gruppeId === null) { entfernen(id); } else { zuordnen(id, gruppeId); }
        });
    }

    function render() {
        var frei = state.clients.filter(function (c) { return c.gruppeId === null || c.gruppeId === undefined; });
        document.getElementById('mg-clients-anzahl').textContent = String(frei.length);
        document.getElementById('mg-gruppen-anzahl').textContent = state.gruppen.length + ' von ' + state.gruppenGesamt;
        document.getElementById('mg-clients-info').textContent = state.clientsTreffer + ' von ' + state.clientsGesamt +
            ' Mandanten entsprechen dem Filter.' + (state.gekuerzt ? ' Die Anzeige ist begrenzt – bitte Filter verfeinern.' : '');

        clientsEl.textContent = '';
        if (frei.length === 0) { clientsEl.appendChild(el('li', 'text-muted small', 'Keine nicht zugeordneten Mandanten gefunden.')); }
        frei.forEach(function (c) { clientsEl.appendChild(clientItem(c)); });
        dropZone(clientsEl, null);

        gruppenEl.textContent = '';
        if (state.gruppen.length === 0) { gruppenEl.appendChild(el('div', 'text-muted small', 'Keine Gruppen gefunden.')); }
        state.gruppen.forEach(function (g) {
            var col = el('div', 'col-12 col-xl-6');
            var card = el('div', 'mg-gruppe');
            var head = el('div', 'd-flex justify-content-between align-items-center mb-2');
            head.appendChild(el('h3', 'h6 mb-0 mg-gruppe-name', g.name));
            head.appendChild(el('span', 'badge bg-primary', g.anzahl + (g.anzahl === 1 ? ' Mandant' : ' Mandanten')));
            card.appendChild(head);
            var liste = el('ul', 'mg-liste list-unstyled mb-0');
            liste.setAttribute('aria-label', 'Mandanten der Gruppe ' + g.name);
            var mitglieder = state.clients.filter(function (c) { return c.gruppeId === g.id; });
            if (mitglieder.length === 0) {
                liste.appendChild(el('li', 'text-muted small', g.anzahl === 0 ? 'Mandanten hierher ziehen.' : 'Kein Mandant entspricht dem Filter.'));
            }
            mitglieder.forEach(function (c) { liste.appendChild(clientItem(c)); });
            card.appendChild(liste);
            dropZone(card, g.id);
            col.appendChild(card);
            gruppenEl.appendChild(col);
        });
    }

    function name(id) {
        var c = state && state.clients.filter(function (x) { return x.id === id; })[0];
        return c ? c.name : 'Mandant';
    }

    function zuordnen(id, gruppeId) {
        var n = name(id);
        var g = state.gruppen.filter(function (x) { return x.id === gruppeId; })[0];
        postJson(urls.zuordnen, { clientId: id, gruppeId: gruppeId })
            .then(function () { zeigeMeldung(n + ' wurde der Gruppe „' + (g ? g.name : '') + '“ zugeordnet.', false); return laden(); })
            .catch(function (e) { zeigeMeldung(e.message, true); laden(); });
    }

    function entfernen(id) {
        var n = name(id);
        postJson(urls.entfernen, { clientId: id })
            .then(function () { zeigeMeldung(n + ' wurde aus der Gruppe entfernt und ist wieder nicht zugeordnet.', false); return laden(); })
            .catch(function (e) { zeigeMeldung(e.message, true); laden(); });
    }

    document.getElementById('mg-anlegen').addEventListener('submit', function (ev) {
        ev.preventDefault();
        var input = document.getElementById('mg-name');
        postJson(urls.anlegen, { name: input.value })
            .then(function (g) { input.value = ''; zeigeMeldung('Gruppe „' + g.name + '“ wurde angelegt.', false); return laden(); })
            .catch(function (e) { zeigeMeldung(e.message, true); input.focus(); });
    });

    function verzoegert() { clearTimeout(timer); timer = setTimeout(laden, 250); }
    clientFilter.addEventListener('input', verzoegert);
    gruppenFilter.addEventListener('input', verzoegert);

    laden();
})();
