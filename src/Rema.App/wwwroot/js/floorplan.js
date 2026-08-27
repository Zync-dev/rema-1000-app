// Gulvplan-editor: træk/skalér placeringer, redigér i inspektøren, auto-gem.
(function () {
    "use strict";

    var cfg = window.floorPlanConfig || {};
    var floor = document.getElementById("floor");
    var scroll = document.getElementById("floorScroll");
    if (!floor) return;

    var raw = document.getElementById("planData").textContent || "{}";
    var state = normalize(JSON.parse(raw));

    var kindLabels = {};
    document.querySelectorAll("#newKind option").forEach(function (o) { kindLabels[o.value] = o.textContent; });

    var zoom = 0.75;
    var selectedId = null;
    var dirty = false;

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
        pOffer: document.getElementById("pOffer"),
        pHighlight: document.getElementById("pHighlight"),
        del: document.getElementById("btnDelete"),
    };

    function normalize(s) {
        s = s || {};
        return {
            canvasWidth: clamp(s.canvasWidth || 1000, 200, 4000),
            canvasHeight: clamp(s.canvasHeight || 700, 200, 4000),
            boxes: (s.boxes || []).map(function (b) {
                return {
                    id: b.id || crypto.randomUUID(),
                    label: b.label || "",
                    offer: b.offer || "",
                    kind: b.kind || "Palle",
                    highlight: !!b.highlight,
                    x: b.x | 0, y: b.y | 0,
                    width: b.width || 120, height: b.height || 90
                };
            })
        };
    }

    function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }
    function boxById(id) { return state.boxes.find(function (b) { return b.id === id; }); }

    // --- Rendering --------------------------------------------------------
    function applyCanvas() {
        floor.style.width = state.canvasWidth + "px";
        floor.style.height = state.canvasHeight + "px";
        floor.style.transform = "scale(" + zoom + ")";
        scroll.style.height = (state.canvasHeight * zoom + 24) + "px";
        els.canvasW.value = state.canvasWidth;
        els.canvasH.value = state.canvasHeight;
    }

    function render() {
        applyCanvas();
        floor.innerHTML = "";
        state.boxes.forEach(function (b) {
            var el = document.createElement("div");
            el.className = "fbox kind-" + b.kind.toLowerCase() + (b.highlight ? " is-highlight" : "") + (b.id === selectedId ? " selected" : "");
            el.style.left = b.x + "px";
            el.style.top = b.y + "px";
            el.style.width = b.width + "px";
            el.style.height = b.height + "px";
            el.dataset.id = b.id;
            el.innerHTML =
                '<div class="fbox-label">' + escapeHtml(b.label || "–") + '</div>' +
                '<div class="fbox-offer">' + escapeHtml(b.offer || "") + '</div>' +
                '<div class="fbox-kind">' + escapeHtml(kindLabels[b.kind] || b.kind) + '</div>' +
                '<div class="fbox-resize" data-resize="1"></div>';
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
        els.pLabel.value = b.label;
        els.pKind.value = b.kind;
        els.pOffer.value = b.offer;
        els.pHighlight.checked = b.highlight;
    }

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    // --- Interaction -----------------------------------------------------
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
            origX: b.x, origY: b.y, origW: b.width, origH: b.height,
            el: boxEl
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
            b.x = clamp(Math.round(drag.origX + dx), 0, state.canvasWidth - b.width);
            b.y = clamp(Math.round(drag.origY + dy), 0, state.canvasHeight - b.height);
            drag.el.style.left = b.x + "px";
            drag.el.style.top = b.y + "px";
        } else {
            b.width = clamp(Math.round(drag.origW + dx), 40, state.canvasWidth - b.x);
            b.height = clamp(Math.round(drag.origH + dy), 40, state.canvasHeight - b.y);
            drag.el.style.width = b.width + "px";
            drag.el.style.height = b.height + "px";
        }
    });

    function endDrag() {
        if (!drag) return;
        drag = null;
        markDirty();
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

    // Klik på tomt gulv fravælger.
    floor.addEventListener("click", function (e) {
        if (e.target === floor) { selectedId = null; render(); }
    });

    // --- Inspector edits ------------------------------------------------
    function bindInspector(elem, evt, apply) {
        elem.addEventListener(evt, function () {
            var b = boxById(selectedId);
            if (!b) return;
            apply(b, elem);
            markDirty();
            render();
        });
    }
    bindInspector(els.pLabel, "input", function (b, el) { b.label = el.value; });
    bindInspector(els.pKind, "change", function (b, el) { b.kind = el.value; });
    bindInspector(els.pOffer, "input", function (b, el) { b.offer = el.value; });
    bindInspector(els.pHighlight, "change", function (b, el) { b.highlight = el.checked; });

    els.del.addEventListener("click", function () {
        state.boxes = state.boxes.filter(function (b) { return b.id !== selectedId; });
        selectedId = null;
        markDirty();
        render();
    });

    // --- Toolbar -------------------------------------------------------
    els.add.addEventListener("click", function () {
        var kind = els.newKind.value;
        var n = state.boxes.length + 1;
        var box = {
            id: crypto.randomUUID(),
            label: String(n),
            offer: "", kind: kind, highlight: false,
            x: 20 + (n % 8) * 12, y: 20 + (n % 8) * 12,
            width: kind === "Gondolender" ? 200 : 120,
            height: kind === "Gondolender" ? 60 : 90
        };
        state.boxes.push(box);
        select(box.id);
        markDirty();
        render();
    });

    els.zoom.addEventListener("input", function () {
        zoom = parseFloat(els.zoom.value);
        applyCanvas();
    });

    function onCanvasSize() {
        state.canvasWidth = clamp(parseInt(els.canvasW.value, 10) || 1000, 200, 4000);
        state.canvasHeight = clamp(parseInt(els.canvasH.value, 10) || 700, 200, 4000);
        markDirty();
        render();
    }
    els.canvasW.addEventListener("change", onCanvasSize);
    els.canvasH.addEventListener("change", onCanvasSize);

    // --- Saving ------------------------------------------------------
    var saveTimer = null;

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
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token()
            },
            body: JSON.stringify(state)
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

    // --- Init --------------------------------------------------------
    zoom = parseFloat(els.zoom.value) || 0.75;
    render();
})();
