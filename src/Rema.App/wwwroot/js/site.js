// App bar: mobil-menu og butiks-dropdown.
(function () {
    "use strict";

    var toggle = document.querySelector("[data-nav-toggle]");
    var nav = document.getElementById("appnav");
    var menu = document.querySelector("[data-menu]");
    var menuBtn = document.querySelector("[data-menu-btn]");

    function isMobile() { return window.matchMedia("(max-width: 991.98px)").matches; }

    if (toggle && nav) {
        toggle.addEventListener("click", function () {
            var open = nav.classList.toggle("is-open");
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
    }

    if (menu && menuBtn) {
        menuBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            // På mobil er menuen altid udfoldet i panelet.
            if (isMobile()) return;
            var open = menu.classList.toggle("is-open");
            menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
        });

        document.addEventListener("click", function (e) {
            if (!menu.contains(e.target)) close();
        });
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") close();
        });
        function close() {
            menu.classList.remove("is-open");
            menuBtn.setAttribute("aria-expanded", "false");
        }
    }

    // Luk mobil-menuen igen når man skifter til desktop.
    window.addEventListener("resize", function () {
        if (!isMobile() && nav && toggle) {
            nav.classList.remove("is-open");
            toggle.setAttribute("aria-expanded", "false");
        }
    });
})();
