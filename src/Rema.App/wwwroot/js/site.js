// App bar: scroll-krymp, glidende indikator-pille, mobil-menu og butiks-menu.
(function () {
    "use strict";

    var appbar = document.querySelector("[data-appbar]");
    var toggle = document.querySelector("[data-nav-toggle]");
    var nav = document.getElementById("appnav");
    var menu = document.querySelector("[data-menu]");
    var menuBtn = document.querySelector("[data-menu-btn]");
    var pill = nav ? nav.querySelector("[data-nav-pill]") : null;

    function isMobile() { return window.matchMedia("(max-width: 991.98px)").matches; }

    // --- Krymp app-baren når man scroller -----------------------------
    if (appbar) {
        var scrolled = false;
        var onScroll = function () {
            var next = window.scrollY > 6;
            if (next !== scrolled) {
                scrolled = next;
                appbar.classList.toggle("is-scrolled", scrolled);
                // Baren skifter højde → flyt pillen med.
                if (window.__navPillReset) requestAnimationFrame(window.__navPillReset);
            }
        };
        window.addEventListener("scroll", onScroll, { passive: true });
        onScroll();
    }

    // --- Glidende indikator-pille ------------------------------------
    if (nav && pill && !isMobile()) {
        var links = [].slice.call(nav.querySelectorAll(".appbar__link"));

        var moveTo = function (el) {
            if (!el) { pill.style.opacity = "0"; return; }
            pill.style.opacity = "";
            pill.style.width = el.offsetWidth + "px";
            pill.style.transform = "translate(" + el.offsetLeft + "px, -50%)";
        };
        var activeEl = function () { return nav.querySelector(".appbar__link.active"); };
        window.__navPillReset = function () { moveTo(activeEl()); };
        var positionPill = window.__navPillReset;

        // Placér uden animation ved start, tænd så for overgangen.
        var start = activeEl();
        if (start) {
            pill.style.width = start.offsetWidth + "px";
            pill.style.transform = "translate(" + start.offsetLeft + "px, -50%)";
        }
        requestAnimationFrame(function () {
            nav.classList.add("pill-ready");
            positionPill();
        });

        links.forEach(function (l) {
            l.addEventListener("mouseenter", function () { if (!isMobile()) moveTo(l); });
            l.addEventListener("focus", function () { if (!isMobile()) moveTo(l); });
        });
        nav.addEventListener("mouseleave", function () { if (!isMobile()) positionPill(); });
        window.addEventListener("resize", function () { if (!isMobile()) positionPill(); });
        if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { if (!isMobile()) positionPill(); });
    }

    // --- Mobil-menu ------------------------------------------------
    if (toggle && nav) {
        toggle.addEventListener("click", function () {
            var open = nav.classList.toggle("is-open");
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
    }

    // --- Butiks-menu (popover) -----------------------------------
    if (menu && menuBtn) {
        menuBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            if (isMobile()) return; // på mobil er menuen altid åben i panelet
            var open = menu.classList.toggle("is-open");
            menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
        });

        var closeMenu = function () {
            menu.classList.remove("is-open");
            menuBtn.setAttribute("aria-expanded", "false");
        };
        document.addEventListener("click", function (e) { if (!menu.contains(e.target)) closeMenu(); });
        document.addEventListener("keydown", function (e) { if (e.key === "Escape") closeMenu(); });
    }

    // Ryd op når man går fra mobil til desktop.
    window.addEventListener("resize", function () {
        if (!isMobile() && nav && toggle) {
            nav.classList.remove("is-open");
            toggle.setAttribute("aria-expanded", "false");
            if (window.__navPillReset) window.__navPillReset();
        }
    });
})();
