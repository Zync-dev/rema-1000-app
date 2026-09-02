// Gulvplan-editor: placeringer (rigtige størrelser, rotation, opdeling, snap),
// frihånds-tegning / linjer / firkanter, genveje og auto-gem.
(function () {
    "use strict";

    var cfg = window.floorPlanConfig || {};
    var floor = document.getElementById("floor");
    var scroll = document.getElementById("floorScroll");
    if (!floor) return;

    var SVGNS = "http://www.w3.org/2000/svg";
    var SNAP = 8;                        // snap-afstand i planenheder
    var state = normalize(JSON.parse(document.getElementById("planData").textContent || "{}"));

    var KINDS = {};
    (JSON.parse(document.getElementById("kindData").textContent || "[]")).forEach(function (k) { KINDS[k.value] = k; });

    var zoom = 0.6;
    var mode = "select";
    var selBox = null, selShape = null;
    var drawColor = "#1f2733", drawWidth = 6;
    var dirty = false, saveTimer = null;

    var els = {
        add: document.getElementById("btnAdd"),
        addGroup: document.getElementById("addGroup"),
        drawGroup: document.getElementById("drawGroup"),
        drawColors: document.getElementById("drawColors"),
        drawWidth: document.getElementById("drawWidth"),
        newKind: document.getElementById("newKind"),
        zoomOut: document.getElementById("zoomOut"),
        zoomIn: document.getElementById("zoomIn"),
        zoomReset: document.getElementById("zoomReset"),
        canvasBtn: document.getElementById("canvasBtn"),
        canvasPop: document.getElementById("canvasPop"),
        canvasLabel: document.getElementById("canvasLabel"),
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
                    id: sh.id || crypto.randomUUID(), kind: sh.kind || "pen",
                    color: sh.color || "#1f2733", width: sh.width || 4,
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
        els.zoomReset.textContent = Math.round(zoom * 100) + "%";
        els.canvasLabel.textContent = m1(state.canvasWidth) + " × " + m1(state.canvasHeight) + " m";
        if (document.activeElement !== els.canvasW) els.canvasW.value = m1(state.canvasWidth);
        if (document.activeElement !== els.canvasH) els.canvasH.value = m1(state.canvasHeight);
    }
    function m1(units) { return (Math.round(units / 10) / 10).toString(); }

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

    var guides = [];
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

        paintGuides();

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
    function markSwatch(c, color) { c.querySelectorAll(".swatch").forEach(function (sw) { sw.classList.toggle("is-on", sw.dataset.color === color); }); }

    function planPoint(e) {
        var r = floor.getBoundingClientRect();
        return [clamp((e.clientX - r.left) / zoom, 0, state.canvasWidth), clamp((e.clientY - r.top) / zoom, 0, state.canvasHeight)];
    }

    // --- Snap ---------------------------------------------------------
    function computeSnap(b) {
        var linesX = [0, state.canvasWidth], linesY = [0, state.canvasHeight];
        state.boxes.forEach(function (o) {
            if (o.id === b.id) return;
            linesX.push(o.x, o.x + o.width, o.x + o.width / 2);
            linesY.push(o.y, o.y + o.height, o.y + o.height / 2);
        });
        var edgesX = [[b.x, 0], [b.x + b.width, b.width], [b.x + b.width / 2, b.width / 2]];
        var edgesY = [[b.y, 0], [b.y + b.height, b.height], [b.y + b.height / 2, b.height / 2]];
        var best = { dx: null, dy: null, gx: null, gy: null };
        edgesX.forEach(function (e) {
            linesX.forEach(function (L) {
                var d = L - e[0];
                if (Math.abs(d) <= SNAP && (best.dx === null || Math.abs(d) < Math.abs(best.dx))) { best.dx = d; best.gx = L; }
            });
        });
        edgesY.forEach(function (e) {
            linesY.forEach(function (L) {
                var d = L - e[0];
                if (Math.abs(d) <= SNAP && (best.dy === null || Math.abs(d) < Math.abs(best.dy))) { best.dy = d; best.gy = L; }
            });
        });
        return best;
    }

    function paintGuides() {
        floor.querySelectorAll(".snap-guide").forEach(function (g) { g.remove(); });
        guides.forEach(function (g) {
            var d = document.createElement("div");
            d.className = "snap-guide " + g.axis;
            if (g.axis === "v") d.style.left = g.at + "px"; else d.style.top = g.at + "px";
            floor.appendChild(d);
        });
    }

    // --- Interaktion ------------------------------------------------
    var drag = null, drawing = null;

    floor.addEventListener("pointerdown", function (e) {
        if (mode !== "select") {
            var p = planPoint(e);
            drawing = { id: crypto.randomUUID(), kind: mode, color: drawColor, width: drawWidth, points: mode === "pen" ? [p] : [p, p] };
            state.shapes.push(drawing);
            selShape = drawing.id; selBox = null;
            floor.setPointerCapture(e.pointerId);
            e.preventDefault(); render(); return;
        }
        var hit = e.target.closest(".shape-hit");
        if (hit) { selectShape(hit.getAttribute("data-sid")); e.preventDefault(); return; }
        var boxEl = e.target.closest(".fbox");
        if (!boxEl) { if (selBox || selShape) { selBox = selShape = null; render(); } return; }
        var b = boxById(boxEl.dataset.id);
        if (!b) return;
        // Vælg uden fuld gentegning, så det trukne element ikke bliver erstattet midt i trækket.
        selBox = b.id; selShape = null;
        floor.querySelectorAll(".fbox.selected").forEach(function (x) { x.classList.remove("selected"); });
        boxEl.classList.add("selected");
        renderInspector();
        var resizing = e.target.dataset.resize === "1";
        drag = { id: b.id, mode: resizing ? "resize" : "move", sx: e.clientX, sy: e.clientY, ox: b.x, oy: b.y, ow: b.width, oh: b.height, el: boxEl, moved: false };
        boxEl.setPointerCapture(e.pointerId);
        e.preventDefault();
    });

    floor.addEventListener("pointermove", function (e) {
        if (drawing) {
            var p = planPoint(e);
            if (drawing.kind === "pen") {
                var last = drawing.points[drawing.points.length - 1];
                if (Math.hypot(p[0] - last[0], p[1] - last[1]) > 3) drawing.points.push(p);
            } else drawing.points[1] = p;
            render(); return;
        }
        if (!drag) return;
        var b = boxById(drag.id);
        var dx = (e.clientX - drag.sx) / zoom, dy = (e.clientY - drag.sy) / zoom;
        drag.moved = true;
        guides = [];
        if (drag.mode === "move") {
            b.x = clamp(Math.round(drag.ox + dx), 0, state.canvasWidth - b.width);
            b.y = clamp(Math.round(drag.oy + dy), 0, state.canvasHeight - b.height);
            if (!e.altKey) {
                var sn = computeSnap(b);
                if (sn.dx !== null) { b.x = clamp(Math.round(b.x + sn.dx), 0, state.canvasWidth - b.width); guides.push({ axis: "v", at: sn.gx }); }
                if (sn.dy !== null) { b.y = clamp(Math.round(b.y + sn.dy), 0, state.canvasHeight - b.height); guides.push({ axis: "h", at: sn.gy }); }
            }
            drag.el.style.left = b.x + "px"; drag.el.style.top = b.y + "px";
        } else {
            b.width = clamp(Math.round(drag.ow + dx), 30, state.canvasWidth - b.x);
            b.height = clamp(Math.round(drag.oh + dy), 30, state.canvasHeight - b.y);
            if (!e.altKey) {
                var linesX = [state.canvasWidth], linesY = [state.canvasHeight];
                state.boxes.forEach(function (o) { if (o.id !== b.id) { linesX.push(o.x, o.x + o.width); linesY.push(o.y, o.y + o.height); } });
                linesX.forEach(function (L) { if (Math.abs(L - (b.x + b.width)) <= SNAP) { b.width = L - b.x; guides.push({ axis: "v", at: L }); } });
                linesY.forEach(function (L) { if (Math.abs(L - (b.y + b.height)) <= SNAP) { b.height = L - b.y; guides.push({ axis: "h", at: L }); } });
            }
            drag.el.style.width = b.width + "px"; drag.el.style.height = b.height + "px";
        }
        paintGuides();
    });

    function endPointer() {
        if (drawing) {
            var s = drawing; drawing = null;
            var ok = s.points.length >= 2;
            if (ok && s.kind !== "pen") { var r = rectOf(s); if (r.w < 6 && r.h < 6) ok = false; }
            if (!ok) { state.shapes = state.shapes.filter(function (x) { return x.id !== s.id; }); selShape = null; }
            markDirty(); render(); return;
        }
        if (drag) {
            var b = boxById(drag.id), moved = drag.moved; drag = null; guides = [];
            if (b && !kindOf(b).fixed) { els.pW.value = b.width; els.pH.value = b.height; }
            if (moved) markDirty();
            render();
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

    // --- Værktøj -----------------------------------------------
    function setMode(m) {
        mode = m;
        els.toolBtns.forEach(function (btn) { btn.classList.toggle("active", btn.dataset.mode === m); });
        els.addGroup.classList.toggle("d-none", m !== "select");
        els.drawGroup.classList.toggle("d-none", m === "select");
        els.drawGroup.classList.toggle("d-flex", m !== "select");
        els.hint.innerHTML = m === "select"
            ? 'Objekter snapper til hinanden (hold <kbd>Alt</kbd> fra) · <kbd>Delete</kbd> sletter · <kbd>Ctrl</kbd>+scroll zoomer.'
            : (m === "pen" ? 'Hold og træk for at tegne frihånd.' : (m === "line" ? 'Træk fra ende til ende.' : 'Træk et hjørne til det modsatte.'));
        if (m !== "select") selBox = null;
        render();
    }
    els.toolBtns.forEach(function (btn) { btn.addEventListener("click", function () { setMode(btn.dataset.mode); }); });

    els.drawColors.querySelectorAll(".swatch").forEach(function (sw) {
        sw.addEventListener("click", function () { drawColor = sw.dataset.color; markSwatch(els.drawColors, drawColor); });
    });
    markSwatch(els.drawColors, drawColor);
    els.drawWidth.addEventListener("change", function () { drawWidth = parseInt(els.drawWidth.value, 10) || 6; });

    els.shapeColors.querySelectorAll(".swatch").forEach(function (sw) {
        sw.addEventListener("click", function () { var s = shapeById(selShape); if (!s) return; s.color = sw.dataset.color; markDirty(); render(); });
    });
    els.shapeWidth.addEventListener("change", function () { var s = shapeById(selShape); if (!s) return; s.width = parseInt(els.shapeWidth.value, 10) || 6; markDirty(); render(); });
    els.delShape.addEventListener("click", function () { state.shapes = state.shapes.filter(function (s) { return s.id !== selShape; }); selShape = null; markDirty(); render(); });

    function bind(el, evt, fn) {
        el.addEventListener(evt, function () { var b = boxById(selBox); if (!b) return; fn(b, el); markDirty(); render(); });
    }
    bind(els.pLabel, "input", function (b, el) { b.label = el.value; });
    bind(els.pOffer, "input", function (b, el) { b.offer = el.value; });
    bind(els.pOfferB, "input", function (b, el) { b.offerB = el.value; });
    bind(els.pHighlight, "change", function (b, el) { b.highlight = el.checked; });
    bind(els.pKind, "change", function (b, el) {
        b.kind = el.value;
        var k = kindOf(b);
        if (k.fixed) { var portrait = b.height > b.width; b.width = portrait ? k.height : k.width; b.height = portrait ? k.width : k.height; keepInBounds(b); }
    });
    bind(els.pSplit, "change", function (b, el) { b.split = el.value; if (b.split === "None") b.offerB = ""; });
    bind(els.pW, "input", function (b, el) { b.width = clamp(parseInt(el.value, 10) || 30, 30, state.canvasWidth); keepInBounds(b); });
    bind(els.pH, "input", function (b, el) { b.height = clamp(parseInt(el.value, 10) || 30, 30, state.canvasHeight); keepInBounds(b); });

    els.pRotate.addEventListener("click", function () {
        var b = boxById(selBox); if (!b) return;
        var t = b.width; b.width = b.height; b.height = t;
        if (b.split === "LeftRight") b.split = "TopBottom"; else if (b.split === "TopBottom") b.split = "LeftRight";
        keepInBounds(b); markDirty(); render();
    });
    els.del.addEventListener("click", function () { state.boxes = state.boxes.filter(function (b) { return b.id !== selBox; }); selBox = null; markDirty(); render(); });

    els.add.addEventListener("click", function () {
        var k = KINDS[els.newKind.value] || KINDS.Andet;
        var n = state.boxes.length + 1;
        state.boxes.push({
            id: crypto.randomUUID(), label: String(n), offer: "", offerB: "",
            kind: els.newKind.value, split: "None", highlight: false,
            x: clamp(30 + (n % 6) * 24, 0, state.canvasWidth - k.width),
            y: clamp(30 + (n % 6) * 24, 0, state.canvasHeight - k.height),
            width: k.width, height: k.height,
        });
        selectBox(state.boxes[state.boxes.length - 1].id);
        markDirty();
    });

    // --- Zoom -------------------------------------------------
    function setZoom(z) { zoom = clamp(+z.toFixed(2), 0.2, 1.6); applyCanvas(); }
    els.zoomOut.addEventListener("click", function () { setZoom(zoom - 0.1); });
    els.zoomIn.addEventListener("click", function () { setZoom(zoom + 0.1); });
    els.zoomReset.addEventListener("click", function () { setZoom(0.6); });
    scroll.addEventListener("wheel", function (e) {
        if (!e.ctrlKey) return;
        e.preventDefault();
        setZoom(zoom + (e.deltaY < 0 ? 0.1 : -0.1));
    }, { passive: false });

    // --- Lærred-popover ------------------------------------
    els.canvasBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        els.canvasPop.hidden = !els.canvasPop.hidden;
    });
    document.addEventListener("click", function (e) {
        if (!els.canvasPop.hidden && !els.canvasPop.contains(e.target) && e.target !== els.canvasBtn) els.canvasPop.hidden = true;
    });
    function onCanvasSize() {
        var wm = parseFloat(String(els.canvasW.value).replace(",", ".")) || 14;
        var hm = parseFloat(String(els.canvasH.value).replace(",", ".")) || 9;
        state.canvasWidth = clamp(Math.round(wm * 100), 200, 6000);
        state.canvasHeight = clamp(Math.round(hm * 100), 200, 6000);
        state.boxes.forEach(keepInBounds);
        markDirty(); render();
    }
    els.canvasW.addEventListener("change", onCanvasSize);
    els.canvasH.addEventListener("change", onCanvasSize);

    // --- Genveje --------------------------------------------
    document.addEventListener("keydown", function (e) {
        var t = e.target;
        if (t && /^(input|textarea|select)$/i.test(t.tagName)) return;
        if (e.key === "Escape") { els.canvasPop.hidden = true; setMode("select"); selBox = selShape = null; render(); return; }
        if (e.key === "Delete" || e.key === "Backspace") {
            if (selBox) { state.boxes = state.boxes.filter(function (b) { return b.id !== selBox; }); selBox = null; markDirty(); render(); e.preventDefault(); }
            else if (selShape) { state.shapes = state.shapes.filter(function (s) { return s.id !== selShape; }); selShape = null; markDirty(); render(); e.preventDefault(); }
        }
    });

    // --- Gem ---------------------------------------------
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

    render();
})();
