// Eduvo session guard.
// The app keeps login state in server-side session. When the session dies
// (idle timeout, app restart) while the browser cookie is still valid, AJAX
// endpoints answer HTTP 200 with { success: false, message: "Unauthorized" }.
// Without this guard the page silently freezes at zeros or ignores clicks.
// With it, the browser is redirected to the matching login page.
(function () {
    "use strict";

    function isUnauthorized(payload) {
        try {
            var data = payload;
            if (typeof data === "string") {
                if (data.length === 0 || data.charAt(0) !== "{") return false;
                data = JSON.parse(data);
            }
            if (!data || data.success !== false) return false;
            var msg = (data.message || data.error || "").toString().toLowerCase();
            return msg.indexOf("unauthorized") !== -1;
        } catch (e) {
            return false;
        }
    }

    function redirectToLogin() {
        var path = window.location.pathname || "";
        var login = "/User/Login";
        if (path.indexOf("/Admin") === 0) {
            login = "/Admin/Login";
        } else if (path.indexOf("/Seller") === 0) {
            login = "/Seller/Login";
        }
        window.location.replace(login + "?expired=1");
    }

    function bind() {
        if (typeof window.jQuery === "undefined") return;
        window.jQuery(document).ajaxSuccess(function (event, xhr, options, payload) {
            if (isUnauthorized(payload)) {
                redirectToLogin();
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", bind);
    } else {
        bind();
    }
})();
