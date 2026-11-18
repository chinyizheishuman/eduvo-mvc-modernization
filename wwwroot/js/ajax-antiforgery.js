(function ($) {
    "use strict";

    $.ajaxPrefilter(function (options, originalOptions, xhr) {
        if (options.crossDomain || /^(GET|HEAD|OPTIONS|TRACE)$/i.test(options.type)) {
            return;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token && token.value) {
            xhr.setRequestHeader("RequestVerificationToken", token.value);
        }
    });
})(window.jQuery);
