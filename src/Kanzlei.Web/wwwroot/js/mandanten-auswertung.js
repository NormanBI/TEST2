// Mandanten-Auswertungen: Syncfusion-Tortendiagramme mit Kreuzfilter und Speicherung des Dashboard-Layouts je Benutzer.
// Kategorietexte werden ausschließlich über textContent bzw. SVG-Text ausgegeben, nie als HTML.
(function () {
    'use strict';
    var host = document.getElementById('mandanten-daten');
    var dashboardEl = document.getElementById('mandanten-dashboard');
    if (!host || !dashboardEl || !window.ej || !ej.charts) { return; }

    var dataUrl = host.getAttribute('data-daten-url');
    var layoutUrl = host.getAttribute('data-layout-url');
    var tokenInput = host.querySelector('input[name="__RequestVerificationToken"]');
    var mobileQuery = window.matchMedia('(max-width: 767px)');
    var dashboard = dashboardEl.ej2_instances[0];
    var anzeige = document.getElementById('mandanten-filter-anzeige');
    var resetButton = document.getElementById('mandanten-filter-reset');
    var meldung = document.getElementById('mandanten-meldung');

    // null = kein Filter, '' = "Ohne ...", sonst Kategorieschlüssel
    var filter = { status: null, typ: null };
    var charts = {};
    var saveTimer = null;

    function setMeldung(text) { meldung.textContent = text || ''; }

    function createChart(id, key) {
        var chart = new ej.charts.AccumulationChart({
            series: [{
                dataSource: [], xName: 'bezeichnung', yName: 'anzahl', type: 'Pie', innerRadius: '40%',
                dataLabel: { visible: true, position: 'Outside', name: 'bezeichnung', font: { fontWeight: '600' } },
                explode: true, explodeOffset: '8%', explodeIndex: -1
            }],
            legendSettings: { visible: true, position: 'Bottom' },
            tooltip: { enable: false },
            enableSmartLabels: true,
            enableAnimation: false,
            enableHtmlSanitizer: true,
            title: '',
            pointClick: function (args) { klick(key, chart, args); }
        });
        chart.appendTo('#' + id);
        return chart;
    }

    function filterText(name, wert) {
        if (wert === null) { return null; }
        return name + ': ' + (wert === '' ? (name === 'Status' ? 'Ohne Status' : 'Ohne Typ') : wert);
    }

    // Gewähltes Tortenstück hervorheben (herausgezogen), sofern es im Diagramm vorhanden ist.
    function markiere(chart, wert) {
        var daten = chart.series[0].dataSource;
        var index = -1;
        for (var i = 0; wert !== null && i < daten.length; i++) { if (daten[i].schluessel === wert) { index = i; } }
        chart.series[0].explodeIndex = index;
    }

    function zeigeFilter() {
        var teile = [filterText('Status', filter.status), filterText('Typ', filter.typ)].filter(Boolean);
        anzeige.textContent = teile.length ? 'Aktiver Filter – ' + teile.join(' · ') : 'Kein Filter aktiv.';
        resetButton.hidden = teile.length === 0;
    }

    function laden() {
        var query = [];
        if (filter.status !== null) { query.push('status=' + encodeURIComponent(filter.status)); }
        if (filter.typ !== null) { query.push('typ=' + encodeURIComponent(filter.typ)); }
        var url = dataUrl + (query.length ? '?' + query.join('&') : '');
        return fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
            .then(function (r) {
                if (!r.ok) { throw new Error('http ' + r.status); }
                return r.json();
            })
            .then(function (d) {
                setMeldung('');
                charts.status.series[0].dataSource = d.status;
                charts.typ.series[0].dataSource = d.typ;
                markiere(charts.status, filter.status);
                markiere(charts.typ, filter.typ);
                charts.status.refresh();
                charts.typ.refresh();
                zeigeFilter();
            })
            .catch(function () {
                filter.status = null; filter.typ = null;
                zeigeFilter();
                setMeldung('Die Auswertung konnte nicht geladen werden. Bitte Seite neu laden.');
            });
    }

    // Auf schmalen Bildschirmen stapelt das DashboardLayout die Kacheln einspaltig; Zeilenhöhe dort fest, damit Diagramme lesbar bleiben.
    function passeMobilAn() {
        if (mobileQuery.matches) { dashboard.cellAspectRatio = Math.max(2, dashboardEl.clientWidth / 70); }
        else { dashboard.cellAspectRatio = 1.2; }
    }

    function speichern() {
        // Das mobile (einspaltige) Layout wird nie als Desktop-Anordnung gespeichert.
        if (mobileQuery.matches || !tokenInput) { return; }
        var panels = dashboard.serialize().map(function (p) {
            return { id: p.id, row: p.row, col: p.col, sizeX: p.sizeX, sizeY: p.sizeY };
        });
        fetch(layoutUrl, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenInput.value },
            body: JSON.stringify(panels)
        }).then(function (r) {
            setMeldung(r.ok ? 'Layout gespeichert.' : 'Das Layout konnte nicht gespeichert werden.');
        }).catch(function () { setMeldung('Das Layout konnte nicht gespeichert werden.'); });
    }

    function layoutGeaendert() {
        window.clearTimeout(saveTimer);
        saveTimer = window.setTimeout(speichern, 400);
        window.setTimeout(function () { charts.status.refresh(); charts.typ.refresh(); }, 50);
    }

    charts.status = createChart('chart-status', 'status');
    charts.typ = createChart('chart-typ', 'typ');

    function klick(key, chart, args) {
        var data = chart.series[0].dataSource[args.pointIndex];
        if (!data) { return; }
        filter[key] = filter[key] === data.schluessel ? null : data.schluessel;
        laden();
    }

    resetButton.addEventListener('click', function () { filter.status = null; filter.typ = null; laden(); });
    dashboard.addEventListener('change', layoutGeaendert);
    dashboard.addEventListener('resizeStop', layoutGeaendert);
    mobileQuery.addEventListener('change', passeMobilAn);
    passeMobilAn();
    laden();
})();
