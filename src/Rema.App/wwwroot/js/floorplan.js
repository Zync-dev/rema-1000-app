// Gulvplan-editor: placeringer med rigtige størrelser, rotation, opdeling og auto-gem.
(function () {
    "use strict";

    var cfg = window.floorPlanConfig || {};
    var floor = document.getElementById("floor");
    var scroll = document.getElementById("floorScroll");
    if (!floor) return;

    var state = normalize(JSON.parse(document.getElementById("planData").textContent || "{}"));

    // Typedata: { FuldPalle: {value,label,width,height,fixed}, ... }
    var KINDS = {};
    (JSON.parse(document.getElementById("kindData").textContent || "[]")).forEach(function (k) { KINDS[k.value] = k; });

    var zoom = 0.6;
    var selectedId = null;
    var dirty = false;
    var saveTimer = null;

    var els = {
        add: document.getElementById("btnAdd"),
        newKind: document.getElementById("newKind"),
        zoom: document.getElementById("zoom"),
        canvasW: document.getElementById("canvasW"),
        canvasH: document.getElementById("canvasH"),
        save: document.getElementById("btnSave"),
        status: document.getElementById("saveStatus"),
        inspector: document.getElementById("inspector"),
        inspectorHint: document.getElementById("inspectorHint"),
        pLabel: document.getElementById("pLabel"),
        pKind: document.getElementById("pKind"),
        pRotate: document.getElementById("btnRotate"),
        pSize: document.getElementById("pSize"),
        pSizeEdit: document.getElementById("pSizeEdit"),
        pW: document.getElementById("pW"),
        pH: document.getElementById("pH"),
        pSplit: document.getElementById("pSplit"),
        pOffer: document.getElementById("pOffer"),
        pOfferLabel: document.getElementById("pOfferLabel"),
        pOfferBWrap: document.getElementById("pOfferBWrap"),
        pOfferB: document.getElementById("pOfferB"),
        pOfferBLabel: document.getElementById("pOfferBLabel"),
        pHighlight: document.getElementById("pHighlight"),
        del: document.getElementById("btnDelete"),
    };

    function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }
    function boxById(id) { return state.boxes.find(function (b) { return b.id === id; }); }
    function kindOf(b) { return KINDS[b.kind] || KINDS.Andet || { fixed: false, label: b.kind }; }

    function normalize(s) {
        s = s || {};
        return {
            canvasWidth: clamp(s.canvasWidth || 1400, 200, 6000),
            canvasHeight: clamp(s.canvasHeight || 900, 200, 6000),
            boxes: (s.boxes || []).map(function (b) {
                return {
                    id: b.id || crypto.randomUUID(),
                    label: b.label || "",
                    offer: b.offer || "",
                    offerB: b.offerB || "",
                    kind: b.kind || "FuldPalle",
                    split: b.split || "None",
                    highlight: !!b.highlight,
                    x: b.x | 0, y: b.y | 0,
                    width: b.width || 120, height: b.height || 80,
                };
            }),
        };
    }

    var SPLIT_LABELS = {
        LeftRight: ["venstre", "højre"],
        TopBottom: ["øverst", "nederst"],
    };

    // --- Rendering --------------------------------------------------------
    function applyCanvas() {
        floor.style.width = state.canvasWidth + "px";
        floor.style.height = state.canvasHeight + "px";
        floor.style.transform = "scale(" + zoom + ")";
        floor.style.marginRight = -(state.canvasWidth * (1 - zoom)) + "px";
        floor.style.marginBottom = -(state.canvasHeight * (1 - zoom)) + "px";
        scroll.style.height = Math.min(state.canvasHeight * zoom + 26, 620) + "px";
        els.canvasW.value = state.canvasWidth;
        els.canvasH.value = state.canvasHeight;
    }

    function esc(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    function cellHtml(text) {
        return '<div class="fbox-cell">' + (text ? '<span>' + esc(text) + '</span>' : '') + '</div>';
    }

    function render() {
        applyCanvas();
        floor.innerHTML = "";
        state.boxes.forEach(function (b) {
            var k = kindOf(b);
            var splitCls = b.split === "LeftRight" ? " split-lr" : (b.split === "TopBottom" ? " split-tb" : "");
            var el = document.createElement("div");
            el.className = "fbox kind-" + b.kind.toLowerCase() + splitCls +
                (b.highlight ? " is-highlight" : "") + (b.id === selectedId ? " selected" : "") +
                (k.fixed ? " is-fixed" : "");
            el.style.left = b.x + "px";
            el.style.top = b.y + "px";
            el.style.width = b.width + "px";
            el.style.height = b.height + "px";
            el.dataset.id = b.id;

            var cells = b.split === "None"
                ? cellHtml(b.offer)
                : cellHtml(b.offer) + cellHtml(b.offerB);

            el.innerHTML =
                '<span class="fbox-tag">' + esc(b.label || "–") + '</span>' +
                '<div class="fbox-cells">' + cells + '</div>' +
                '<span class="fbox-kind">' + esc(k.label || b.kind) + '</span>' +
                (k.fixed ? '' : '<div class="fbox-resize" data-resize="1"></div>');
            floor.appendChild(el);
        });
        renderInspector();
    }

    function renderInspector() {
        var b = boxById(selectedId);
        if (!b) {
            els.inspector.hidden = true;
            els.inspectorHint.hidden = false;
            return;
        }
        els.inspector.hidden = false;
        els.inspectorHint.hidden = true;

        var k = kindOf(b);
        els.pLabel.value = b.label;
        els.pKind.value = b.kind;
        els.pSplit.value = b.split;
        els.pOffer.value = b.offer;
        els.pOfferB.value = b.offerB;
        els.pHighlight.checked = b.highlight;

        // Størrelse
        els.pSize.textContent = b.width + " × " + b.height + " cm";
        els.pSizeEdit.classList.toggle("d-none", k.fixed);
        if (!k.fixed) { els.pW.value = b.width; els.pH.value = b.height; }

        // Opdeling → felt B + labels
        var split = SPLIT_LABELS[b.split];
        els.pOfferBWrap.classList.toggle("d-none", !split);
        els.pOfferLabel.textContent = split ? "Ugens vare (" + split[0] + ")" : "Ugens vare";
        if (split) els.pOfferBLabel.textContent = "Ugens vare (" + split[1] + ")";
    }

    // --- Move / resize -------------------------------------------------
    var drag = null;

    floor.addEventListener("pointerdown", function (e) {
        var boxEl = e.target.closest(".fbox");
        if (!boxEl) return;
        var b = boxById(boxEl.dataset.id);
        if (!b) return;
        select(b.id);

        var resizing = e.target.dataset.resize === "1";
        drag = {
            id: b.id, mode: resizing ? "resize" : "move",
            startX: e.clientX, startY: e.clientY,
            ox: b.x, oy: b.y, ow: b.width, oh: b.height, el: boxEl,
        };
        boxEl.setPointerCapture(e.pointerId);
        e.preventDefault();
    });

    floor.addEventListener("pointermove", function (e) {
        if (!drag) return;
        var b = boxById(drag.id);
        var dx = (e.clientX - drag.startX) / zoom;
        var dy = (e.clientY - drag.startY) / zoom;

        if (drag.mode === "move") {
            b.x = clamp(Math.round(drag.ox + dx), 0, state.canvasWidth - b.width);
            b.y = clamp(Math.round(drag.oy + dy), 0, state.canvasHeight - b.height);
            drag.el.style.left = b.x + "px";
            drag.el.style.top = b.y + "px";
        } else {
            b.width = clamp(Math.round(drag.ow + dx), 30, state.canvasWidth - b.x);
            b.height = clamp(Math.round(drag.oh + dy), 30, state.canvasHeight - b.y);
            drag.el.style.width = b.width + "px";
            drag.el.style.height = b.height + "px";
        }
    });

    function endDrag() {
        if (!drag) return;
        var b = boxById(drag.id);
        drag = null;
        if (b && kindOf(b).fixed === false) { els.pW.value = b.width; els.pH.value = b.height; }
        markDirty();
        renderInspector();
    }
    floor.addEventListener("pointerup", endDrag);
    floor.addEventListener("pointercancel", endDrag);

    function select(id) {
        if (selectedId === id) return;
        selectedId = id;
        floor.querySelectorAll(".fbox").forEach(function (el) {
            el.classList.toggle("selected", el.dataset.id === id);
        });
        renderInspector();
    }

    floor.addEventListener("click", function (e) {
        if (e.target === floor) { selectedId = null; render(); }
    });

    function keepInBounds(b) {
        b.x = clamp(b.x, 0, Math.max(0, state.canvasWidth - b.width));
        b.y = clamp(b.y, 0, Math.max(0, state.canvasHeight - b.height));
    }

    // --- Inspector edits --------------------------------------------
    function bind(el, evt, fn) {
        el.addEventListener(evt, function () {
            var b = boxById(selectedId);
            if (!b) return;
            fn(b, el);
            markDirty();
            render();
        });
    }

    bind(els.pLabel, "input", function (b, el) { b.label = el.value; });
    bind(els.pOffer, "input", function (b, el) { b.offer = el.value; });
    bind(els.pOfferB, "input", function (b, el) { b.offerB = el.value; });
    bind(els.pHighlight, "change", function (b, el) { b.highlight = el.checked; });

    bind(els.pKind, "change", function (b, el) {
        b.kind = el.value;
        var k = kindOf(b);
        if (k.fixed) {
            // Behold orientering (roteret hvis dybde > bredde).
            var portrait = b.height > b.width;
            b.width = portrait ? k.height : k.width;
            b.height = portrait ? k.width : k.height;
            keepInBounds(b);
        }
    });

    bind(els.pSplit, "change", function (b, el) {
        b.split = el.value;
        if (b.split === "None") b.offerB = "";
    });

    bind(els.pW, "input", function (b, el) {
        b.width = clamp(parseInt(el.value, 10) || 30, 30, state.canvasWidth);
        keepInBounds(b);
    });
    bind(els.pH, "input", function (b, el) {
        b.height = clamp(parseInt(el.value, 10) || 30, 30, state.canvasHeight);
        keepInBounds(b);
    });

    els.pRotate.addEventListener("click", function () {
        var b = boxById(selectedId);
        if (!b) return;
        var t = b.width; b.width = b.height; b.height = t;
        if (b.split === "LeftRight") b.split = "TopBottom";
        else if (b.split === "TopBottom") b.split = "LeftRight";
        keepInBounds(b);
        markDirty();
        render();
    });

    els.del.addEventListener("click", function () {
        state.boxes = state.boxes.filter(function (b) { return b.id !== selectedId; });
        selectedId = null;
        markDirty();
        render();
    });

    // --- Toolbar --------------------------------------------------
    els.add.addEventListener("click", function () {
        var kind = els.newKind.value;
        var k = KINDS[kind] || KINDS.Andet;
        var n = state.boxes.length + 1;
        var box = {
            id: crypto.randomUUID(),
            label: String(n),
            offer: "", offerB: "",
            kind: kind, split: "None", highlight: false,
            x: clamp(30 + (n % 6) * 24, 0, state.canvasWidth - k.width),
            y: clamp(30 + (n % 6) * 24, 0, state.canvasHeight - k.height),
            width: k.width, height: k.height,
        };
        state.boxes.push(box);
        select(box.id);
        markDirty();
        render();
    });

    els.zoom.addEventListener("input", function () { zoom = parseFloat(els.zoom.value); applyCanvas(); });

    function onCanvasSize() {
        state.canvasWidth = clamp(parseInt(els.canvasW.value, 10) || 1400, 200, 6000);
        state.canvasHeight = clamp(parseInt(els.canvasH.value, 10) || 900, 200, 6000);
        state.boxes.forEach(keepInBounds);
        markDirty();
        render();
    }
    els.canvasW.addEventListener("change", onCanvasSize);
    els.canvasH.addEventListener("change", onCanvasSize);

    // --- Saving ------------------------------------------------
    function markDirty() {
        dirty = true;
        els.status.textContent = "Ikke gemt";
        els.status.className = "save-status text-warning";
        clearTimeout(saveTimer);
        saveTimer = setTimeout(save, 1200);
    }

    function token() {
        var el = document.querySelector('#afForm input[name="__RequestVerificationToken"]');
        return el ? el.value : "";
    }

    function save() {
        clearTimeout(saveTimer);
        if (!dirty) return;
        els.status.textContent = "Gemmer…";
        els.status.className = "save-status text-muted";

        fetch(cfg.saveUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token() },
            body: JSON.stringify(state),
        }).then(function (r) {
            if (!r.ok) throw new Error("HTTP " + r.status);
            return r.json();
        }).then(function () {
            dirty = false;
            var t = new Date();
            els.status.textContent = "Gemt kl. " + t.toLocaleTimeString("da-DK", { hour: "2-digit", minute: "2-digit" });
            els.status.className = "save-status text-success";
        }).catch(function () {
            els.status.textContent = "Kunne ikke gemme – prøv igen";
            els.status.className = "save-status text-danger";
        });
    }

    els.save.addEventListener("click", function () { dirty = true; save(); });
    window.addEventListener("beforeunload", function (e) {
        if (dirty) { e.preventDefault(); e.returnValue = ""; }
    });

    zoom = parseFloat(els.zoom.value) || 0.6;
    render();
})();
