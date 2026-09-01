// Gulvplan-editor: placeringer (rigtige størrelser, rotation, opdeling),
// frihånds-tegning / linjer / firkanter, genveje og auto-gem.
(function () {
    "use strict";

    var cfg = window.floorPlanConfig || {};
    var floor = document.getElementById("floor");
    var scroll = document.getElementById("floorScroll");
    if (!floor) return;

    var SVGNS = "http://www.w3.org/2000/svg";
    var state = normalize(JSON.parse(document.getElementById("planData").textContent || "{}"));

    var KINDS = {};
    (JSON.parse(document.getElementById("kindData").textContent || "[]")).forEach(function (k) { KINDS[k.value] = k; });

    var zoom = 0.6;
    var mode = "select";                 // select | pen | line | rect
    var selBox = null;                   // valgt boks-id
    var selShape = null;                 // valgt form-id
    var drawColor = "#1f2733";
    var drawWidth = 6;
    var dirty = false;
    var saveTimer = null;

    var els = {
        add: document.getElementById("btnAdd"),
        addGroup: document.getElementById("addGroup"),
        drawGroup: document.getElementById("drawGroup"),
        drawColors: document.getElementById("drawColors"),
        drawWidth: document.getElementById("drawWidth"),
        newKind: document.getElementById("newKind"),
        zoom: document.getElementById("zoom"),
        canvasW: document.getElementById("canvasW"),
        canvasH: document.getElementById("canvasH"),
        save: document.getElementById("btnSave"),
        status: document.getElementById("saveStatus"),
        hint: document.getElementById("toolHint"),
        toolBtns: [].slice.call(document.querySelectorAll(".tool-btn")),
        inspector: document.getElementById("inspector"),
        inspectorHint: document.getElementById("inspectorHint"),
        shapeInspector: document.getElementById("shapeInspector"),
        shapeColors: document.getElementById("shapeColors"),
        shapeWidth: document.getElementById("shapeWidth"),
        delShape: document.getElementById("btnDeleteShape"),
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
    function shapeById(id) { return state.shapes.find(function (s) { return s.id === id; }); }
    function kindOf(b) { return KINDS[b.kind] || KINDS.Andet || { fixed: false, label: b.kind }; }

    function normalize(s) {
        s = s || {};
        return {
            canvasWidth: clamp(s.canvasWidth || 1400, 200, 6000),
            canvasHeight: clamp(s.canvasHeight || 900, 200, 6000),
            boxes: (s.boxes || []).map(function (b) {
                return {
                    id: b.id || crypto.randomUUID(),
                    label: b.label || "", offer: b.offer || "", offerB: b.offerB || "",
                    kind: b.kind || "FuldPalle", split: b.split || "None", highlight: !!b.highlight,
                    x: b.x | 0, y: b.y | 0, width: b.width || 120, height: b.height || 80,
                };
            }),
            shapes: (s.shapes || []).map(function (sh) {
                return {
                    id: sh.id || crypto.randomUUID(),
                    kind: sh.kind || "pen",
                    color: sh.color || "#1f2733",
                    width: sh.width || 4,
                    points: (sh.points || []).map(function (p) { return [p[0], p[1]]; }),
                };
            }),
        };
    }

    var SPLIT_LABELS = { LeftRight: ["venstre", "højre"], TopBottom: ["øverst", "nederst"] };

    // --- Rendering --------------------------------------------------------
    function applyCanvas() {
        floor.style.width = state.canvasWidth + "px";
        floor.style.height = state.canvasHeight + "px";
        floor.style.transform = "scale(" + zoom + ")";
        floor.style.marginRight = -(state.canvasWidth * (1 - zoom)) + "px";
        floor.style.marginBottom = -(state.canvasHeight * (1 - zoom)) + "px";
        scroll.style.height = Math.min(state.canvasHeight * zoom + 26, 640) + "px";
        els.canvasW.value = state.canvasWidth;
        els.canvasH.value = state.canvasHeight;
        els.zoom.value = zoom;
    }

    function esc(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function cellHtml(text) {
        return '<div class="fbox-cell">' + (text ? '<span>' + esc(text) + '</span>' : '') + '</div>';
    }

    function rectOf(s) {
        var a = s.points[0], b = s.points[1] || a;
        return { x: Math.min(a[0], b[0]), y: Math.min(a[1], b[1]), w: Math.abs(b[0] - a[0]), h: Math.abs(b[1] - a[1]) };
    }

    function shapeSvg(s) {
        var on = s.id === selShape;
        var dash = on ? ' stroke-dasharray="' + (s.width * 2.2) + ' ' + (s.width * 1.6) + '"' : '';
        if (s.kind === "rect") {
            var r = rectOf(s);
            return '<rect class="shape-hit" data-sid="' + s.id + '" x="' + r.x + '" y="' + r.y + '" width="' + r.w + '" height="' + r.h + '" fill="none" stroke="#000" stroke-opacity="0" stroke-width="' + (s.width + 20) + '" pointer-events="stroke"/>' +
                '<rect x="' + r.x + '" y="' + r.y + '" width="' + r.w + '" height="' + r.h + '" fill="none" stroke="' + s.color + '" stroke-width="' + s.width + '"' + dash + '/>';
        }
        var pts = s.points.map(function (p) { return p[0].toFixed(1) + "," + p[1].toFixed(1); }).join(" ");
        return '<polyline class="shape-hit" data-sid="' + s.id + '" points="' + pts + '" fill="none" stroke="#000" stroke-opacity="0" stroke-width="' + (s.width + 22) + '" stroke-linecap="round" pointer-events="stroke"/>' +
            '<polyline points="' + pts + '" fill="none" stroke="' + s.color + '" stroke-width="' + s.width + '" stroke-linecap="round" stroke-linejoin="round"' + dash + '/>';
    }

    function render() {
        applyCanvas();
        floor.innerHTML = "";

        state.boxes.forEach(function (b) {
            var k = kindOf(b);
            var splitCls = b.split === "LeftRight" ? " split-lr" : (b.split === "TopBottom" ? " split-tb" : "");
            var el = document.createElement("div");
            el.className = "fbox kind-" + b.kind.toLowerCase() + splitCls +
                (b.highlight ? " is-highlight" : "") + (b.id === selBox ? " selected" : "") +
                (k.fixed ? " is-fixed" : "");
            el.style.left = b.x + "px"; el.style.top = b.y + "px";
            el.style.width = b.width + "px"; el.style.height = b.height + "px";
            el.dataset.id = b.id;
            var cells = b.split === "None" ? cellHtml(b.offer) : cellHtml(b.offer) + cellHtml(b.offerB);
            el.innerHTML =
                '<span class="fbox-tag">' + esc(b.label || "–") + '</span>' +
                '<div class="fbox-cells">' + cells + '</div>' +
                '<span class="fbox-kind">' + esc(k.label || b.kind) + '</span>' +
                (k.fixed ? '' : '<div class="fbox-resize" data-resize="1"></div>');
            floor.appendChild(el);
        });

        var svg = document.createElementNS(SVGNS, "svg");
        svg.setAttribute("class", "floor-shapes");
        svg.setAttribute("viewBox", "0 0 " + state.canvasWidth + " " + state.canvasHeight);
        svg.innerHTML = state.shapes.map(shapeSvg).join("");
        floor.appendChild(svg);

        floor.classList.toggle("mode-draw", mode !== "select");
        renderInspector();
    }

    function renderInspector() {
        var b = boxById(selBox), s = shapeById(selShape);
        els.inspector.hidden = !b;
        els.shapeInspector.hidden = !s;
        els.inspectorHint.hidden = !!(b || s);

        if (b) {
            var k = kindOf(b);
            els.pLabel.value = b.label;
            els.pKind.value = b.kind;
            els.pSplit.value = b.split;
            els.pOffer.value = b.offer;
            els.pOfferB.value = b.offerB;
            els.pHighlight.checked = b.highlight;
            els.pSize.textContent = b.width + " × " + b.height + " cm";
            els.pSizeEdit.classList.toggle("d-none", k.fixed);
            if (!k.fixed) { els.pW.value = b.width; els.pH.value = b.height; }
            var sp = SPLIT_LABELS[b.split];
            els.pOfferBWrap.classList.toggle("d-none", !sp);
            els.pOfferLabel.textContent = sp ? "Ugens vare (" + sp[0] + ")" : "Ugens vare";
            if (sp) els.pOfferBLabel.textContent = "Ugens vare (" + sp[1] + ")";
        }
        if (s) {
            els.shapeWidth.value = String(nearestWidth(s.width));
            markSwatch(els.shapeColors, s.color);
        }
    }

    function nearestWidth(w) { return [3, 6, 12].reduce(function (a, b) { return Math.abs(b - w) < Math.abs(a - w) ? b : a; }); }
    function markSwatch(container, color) {
        container.querySelectorAll(".swatch").forEach(function (sw) {
            sw.classList.toggle("is-on", sw.dataset.color === color);
        });
    }

    // --- Koordinater ----------------------------------------------------
    function planPoint(e) {
        var r = floor.getBoundingClientRect();
        return [
            clamp((e.clientX - r.left) / zoom, 0, state.canvasWidth),
            clamp((e.clientY - r.top) / zoom, 0, state.canvasHeight),
        ];
    }

    // --- Interaktion --------------------------------------------------
    var drag = null;      // box move/resize
    var drawing = null;   // ny form under tegning

    floor.addEventListener("pointerdown", function (e) {
        if (mode !== "select") {
            var p = planPoint(e);
            drawing = {
                id: crypto.randomUUID(), kind: mode, color: drawColor, width: drawWidth,
                points: mode === "pen" ? [p] : [p, p],
            };
            state.shapes.push(drawing);
            selShape = drawing.id; selBox = null;
            floor.setPointerCapture(e.pointerId);
            e.preventDefault();
            render();
            return;
        }

        var hit = e.target.closest(".shape-hit");
        if (hit) { selectShape(hit.getAttribute("data-sid")); e.preventDefault(); return; }

        var boxEl = e.target.closest(".fbox");
        if (!boxEl) { if (selBox || selShape) { selBox = selShape = null; render(); } return; }
        var b = boxById(boxEl.dataset.id);
        if (!b) return;
        selectBox(b.id);
        var resizing = e.target.dataset.resize === "1";
        drag = { id: b.id, mode: resizing ? "resize" : "move", sx: e.clientX, sy: e.clientY, ox: b.x, oy: b.y, ow: b.width, oh: b.height, el: boxEl };
        boxEl.setPointerCapture(e.pointerId);
        e.preventDefault();
    });

    floor.addEventListener("pointermove", function (e) {
        if (drawing) {
            var p = planPoint(e);
            if (drawing.kind === "pen") {
                var last = drawing.points[drawing.points.length - 1];
                if (Math.hypot(p[0] - last[0], p[1] - last[1]) > 3) drawing.points.push(p);
            } else {
                drawing.points[1] = p;
            }
            render();
            return;
        }
        if (!drag) return;
        var b = boxById(drag.id);
        var dx = (e.clientX - drag.sx) / zoom, dy = (e.clientY - drag.sy) / zoom;
        if (drag.mode === "move") {
            b.x = clamp(Math.round(drag.ox + dx), 0, state.canvasWidth - b.width);
            b.y = clamp(Math.round(drag.oy + dy), 0, state.canvasHeight - b.height);
            drag.el.style.left = b.x + "px"; drag.el.style.top = b.y + "px";
        } else {
            b.width = clamp(Math.round(drag.ow + dx), 30, state.canvasWidth - b.x);
            b.height = clamp(Math.round(drag.oh + dy), 30, state.canvasHeight - b.y);
            drag.el.style.width = b.width + "px"; drag.el.style.height = b.height + "px";
        }
    });

    function endPointer() {
        if (drawing) {
            var s = drawing; drawing = null;
            var ok = s.points.length >= 2;
            if (ok && s.kind !== "pen") {
                var r = rectOf(s);
                if (r.w < 6 && r.h < 6) ok = false;   // for lille – kasseret
            }
            if (!ok) { state.shapes = state.shapes.filter(function (x) { return x.id !== s.id; }); selShape = null; }
            markDirty();
            render();
            return;
        }
        if (drag) {
            var b = boxById(drag.id); drag = null;
            if (b && !kindOf(b).fixed) { els.pW.value = b.width; els.pH.value = b.height; }
            markDirty();
            renderInspector();
        }
    }
    floor.addEventListener("pointerup", endPointer);
    floor.addEventListener("pointercancel", endPointer);

    function selectBox(id) { selBox = id; selShape = null; render(); }
    function selectShape(id) { selShape = id; selBox = null; render(); }

    function keepInBounds(b) {
        b.x = clamp(b.x, 0, Math.max(0, state.canvasWidth - b.width));
        b.y = clamp(b.y, 0, Math.max(0, state.canvasHeight - b.height));
    }

    // --- Værktøjsvalg ----------------------------------------------
    function setMode(m) {
        mode = m;
        els.toolBtns.forEach(function (btn) { btn.classList.toggle("active", btn.dataset.mode === m); });
        els.addGroup.classList.toggle("d-none", m !== "select");
        els.drawGroup.classList.toggle("d-none", m === "select");
        els.drawGroup.classList.toggle("d-flex", m !== "select");
        els.hint.innerHTML = m === "select"
            ? 'Træk for at flytte · klik for at redigere · <kbd>Delete</kbd> sletter · <kbd>Ctrl</kbd>+scroll zoomer.'
            : (m === "pen" ? 'Hold og træk for at tegne frihånd.' : (m === "line" ? 'Træk fra ende til ende.' : 'Træk et hjørne til det modsatte.'));
        if (m !== "select") { selBox = null; }
        render();
    }
    els.toolBtns.forEach(function (btn) { btn.addEventListener("click", function () { setMode(btn.dataset.mode); }); });

    els.drawColors.querySelectorAll(".swatch").forEach(function (sw) {
        sw.addEventListener("click", function () {
            drawColor = sw.dataset.color;
            markSwatch(els.drawColors, drawColor);
        });
    });
    markSwatch(els.drawColors, drawColor);
    els.drawWidth.addEventListener("change", function () { drawWidth = parseInt(els.drawWidth.value, 10) || 6; });

    // --- Form-inspektør -------------------------------------------
    els.shapeColors.querySelectorAll(".swatch").forEach(function (sw) {
        sw.addEventListener("click", function () {
            var s = shapeById(selShape); if (!s) return;
            s.color = sw.dataset.color; markDirty(); render();
        });
    });
    els.shapeWidth.addEventListener("change", function () {
        var s = shapeById(selShape); if (!s) return;
        s.width = parseInt(els.shapeWidth.value, 10) || 6; markDirty(); render();
    });
    els.delShape.addEventListener("click", function () {
        state.shapes = state.shapes.filter(function (s) { return s.id !== selShape; });
        selShape = null; markDirty(); render();
    });

    // --- Boks-inspektør ------------------------------------------
    function bind(el, evt, fn) {
        el.addEventListener(evt, function () {
            var b = boxById(selBox); if (!b) return;
            fn(b, el); markDirty(); render();
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
            var portrait = b.height > b.width;
            b.width = portrait ? k.height : k.width;
            b.height = portrait ? k.width : k.height;
            keepInBounds(b);
        }
    });
    bind(els.pSplit, "change", function (b, el) { b.split = el.value; if (b.split === "None") b.offerB = ""; });
    bind(els.pW, "input", function (b, el) { b.width = clamp(parseInt(el.value, 10) || 30, 30, state.canvasWidth); keepInBounds(b); });
    bind(els.pH, "input", function (b, el) { b.height = clamp(parseInt(el.value, 10) || 30, 30, state.canvasHeight); keepInBounds(b); });

    els.pRotate.addEventListener("click", function () {
        var b = boxById(selBox); if (!b) return;
        var t = b.width; b.width = b.height; b.height = t;
        if (b.split === "LeftRight") b.split = "TopBottom";
        else if (b.split === "TopBottom") b.split = "LeftRight";
        keepInBounds(b); markDirty(); render();
    });
    els.del.addEventListener("click", function () {
        state.boxes = state.boxes.filter(function (b) { return b.id !== selBox; });
        selBox = null; markDirty(); render();
    });

    // --- Tilføj placering ---------------------------------------
    els.add.addEventListener("click", function () {
        var k = KINDS[els.newKind.value] || KINDS.Andet;
        var n = state.boxes.length + 1;
        var box = {
            id: crypto.randomUUID(), label: String(n), offer: "", offerB: "",
            kind: els.newKind.value, split: "None", highlight: false,
            x: clamp(30 + (n % 6) * 24, 0, state.canvasWidth - k.width),
            y: clamp(30 + (n % 6) * 24, 0, state.canvasHeight - k.height),
            width: k.width, height: k.height,
        };
        state.boxes.push(box);
        selectBox(box.id);
        markDirty();
    });

    // --- Zoom / lærred ------------------------------------------
    els.zoom.addEventListener("input", function () { zoom = parseFloat(els.zoom.value); applyCanvas(); });

    scroll.addEventListener("wheel", function (e) {
        if (!e.ctrlKey) return;
        e.preventDefault();
        zoom = clamp(+(zoom + (e.deltaY < 0 ? 0.1 : -0.1)).toFixed(2), 0.2, 1.6);
        applyCanvas();
    }, { passive: false });

    function onCanvasSize() {
        state.canvasWidth = clamp(parseInt(els.canvasW.value, 10) || 1400, 200, 6000);
        state.canvasHeight = clamp(parseInt(els.canvasH.value, 10) || 900, 200, 6000);
        state.boxes.forEach(keepInBounds);
        markDirty(); render();
    }
    els.canvasW.addEventListener("change", onCanvasSize);
    els.canvasH.addEventListener("change", onCanvasSize);

    // --- Genveje ----------------------------------------------
    document.addEventListener("keydown", function (e) {
        var t = e.target;
        if (t && /^(input|textarea|select)$/i.test(t.tagName)) return;
        if (e.key === "Escape") { setMode("select"); selBox = selShape = null; render(); return; }
        if (e.key === "Delete" || e.key === "Backspace") {
            if (selBox) { state.boxes = state.boxes.filter(function (b) { return b.id !== selBox; }); selBox = null; markDirty(); render(); e.preventDefault(); }
            else if (selShape) { state.shapes = state.shapes.filter(function (s) { return s.id !== selShape; }); selShape = null; markDirty(); render(); e.preventDefault(); }
        }
    });

    // --- Gem -------------------------------------------------
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
        }).then(function (r) { if (!r.ok) throw new Error("HTTP " + r.status); return r.json(); })
          .then(function () {
              dirty = false;
              var d = new Date();
              els.status.textContent = "Gemt kl. " + d.toLocaleTimeString("da-DK", { hour: "2-digit", minute: "2-digit" });
              els.status.className = "save-status text-success";
          }).catch(function () {
              els.status.textContent = "Kunne ikke gemme – prøv igen";
              els.status.className = "save-status text-danger";
          });
    }
    els.save.addEventListener("click", function () { dirty = true; save(); });
    window.addEventListener("beforeunload", function (e) { if (dirty) { e.preventDefault(); e.returnValue = ""; } });

    zoom = parseFloat(els.zoom.value) || 0.6;
    render();
})();
