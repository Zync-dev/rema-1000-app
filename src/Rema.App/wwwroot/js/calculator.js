// Live-forhåndsvisning af avancekalkulatoren. Serveren laver den endelige beregning
// ved indsendelse; dette er kun for hurtig feedback mens man taster.
(function () {
    "use strict";

    var form = document.getElementById("calcForm");
    if (!form) return;

    var preview = document.getElementById("livePreview");
    var kr = new Intl.NumberFormat("da-DK", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    var pct = new Intl.NumberFormat("da-DK", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

    function val(name) {
        var el = form.querySelector('[data-calc="' + name + '"]');
        if (!el) return NaN;
        return parseFloat(String(el.value).replace(",", "."));
    }

    function currentMode() {
        var checked = form.querySelector('[data-calc-mode]:checked');
        return checked ? checked.getAttribute("data-calc-mode") : "price";
    }

    function showModeField() {
        var mode = currentMode();
        ["price", "margin", "markup"].forEach(function (m) {
            var wrap = form.querySelector('[data-calc-field="' + m + '"]');
            if (wrap) wrap.classList.toggle("d-none", m !== mode);
        });
    }

    function compute() {
        var cost = val("cost");
        var vatRate = val("vat") / 100;
        var deposit = val("deposit") || 0;
        var mode = currentMode();

        if (isNaN(cost) || isNaN(vatRate)) return null;

        var priceIncl;
        if (mode === "price") {
            priceIncl = val("price");
            if (isNaN(priceIncl)) return null;
        } else if (mode === "margin") {
            var dg = val("margin");
            if (isNaN(dg) || dg >= 100) return null;
            priceIncl = (cost / (1 - dg / 100)) * (1 + vatRate) + deposit;
        } else {
            var av = val("markup");
            if (isNaN(av) || av <= -100) return null;
            priceIncl = cost * (1 + av / 100) * (1 + vatRate) + deposit;
        }

        var net = (priceIncl - deposit) / (1 + vatRate);
        var vatAmount = priceIncl - deposit - net;
        var db = net - cost;

        return {
            priceIncl: priceIncl,
            priceExcl: net + deposit,
            net: net,
            vat: vatAmount,
            deposit: deposit,
            db: db,
            margin: net === 0 ? 0 : (db / net) * 100,
            markup: cost === 0 ? 0 : (db / cost) * 100,
            isLoss: db < 0
        };
    }

    function render() {
        showModeField();
        var r = compute();
        if (!r) return;

        preview.innerHTML =
            '<div class="stat">' +
              '<div class="result-figure ' + (r.isLoss ? "is-loss" : "is-ok") + '">' + pct.format(r.margin) + ' %</div>' +
              '<div class="stat__label">dækningsgrad · forhåndsvisning</div>' +
            '</div>' +
            '<dl class="kv">' +
            row("Dækningsbidrag (DB)", kr.format(r.db) + " kr.") +
            row("Avance", pct.format(r.markup) + " %") +
            row("Salgspris inkl. moms", kr.format(r.priceIncl) + " kr.") +
            row("Salgspris ekskl. moms", kr.format(r.priceExcl) + " kr.") +
            row("Nettoomsætning", kr.format(r.net) + " kr.") +
            row("Heraf moms", kr.format(r.vat) + " kr.") +
            (r.deposit > 0 ? row("Pant (uden moms)", kr.format(r.deposit) + " kr.") : "") +
            '</dl>';
    }

    function row(label, value) {
        return '<dt>' + label + '</dt><dd>' + value + '</dd>';
    }

    form.addEventListener("input", render);
    form.addEventListener("change", render);

    // Note-felt vises kun når "gem" er sat.
    var saveCheck = document.getElementById("saveCheck");
    var noteWrap = document.getElementById("noteWrap");
    function toggleNote() {
        if (saveCheck && noteWrap) noteWrap.classList.toggle("d-none", !saveCheck.checked);
    }
    if (saveCheck) saveCheck.addEventListener("change", toggleNote);

    showModeField();
    toggleNote();
})();
