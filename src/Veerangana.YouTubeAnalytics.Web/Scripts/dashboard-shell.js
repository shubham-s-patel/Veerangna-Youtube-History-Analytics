(function ($, window, document) {
    "use strict";

    var body = document.body;
    var sidebar = document.getElementById("analytics-sidebar");
    var dateRange = document.getElementById("date-range");
    var customDateFields = document.getElementById("custom-date-fields");

    function setSidebar(open) {
        if (!sidebar) {
            return;
        }

        body.classList.toggle("sidebar-open", open);
        sidebar.setAttribute("aria-hidden", open ? "false" : "true");
    }

    function updateCustomDateFields() {
        if (!dateRange || !customDateFields) {
            return;
        }

        var customSelected = dateRange.value === "custom";
        customDateFields.classList.toggle("is-visible", customSelected);

        $(customDateFields)
            .find("input")
            .prop("required", customSelected);
    }

    function preserveFilters() {
        if (!window.location.search) {
            return;
        }

        $(".js-preserve-filters").each(function () {
            var link = this;

            if (link.search) {
                return;
            }

            link.href += window.location.search;
        });
    }

    $(document).on("click", ".js-sidebar-open", function () {
        setSidebar(true);
    });

    $(document).on("click", ".js-sidebar-close", function () {
        setSidebar(false);
    });

    $(document).on("keydown", function (event) {
        if (event.key === "Escape") {
            setSidebar(false);
        }
    });

    $(document).on("click", ".js-refresh-page", function () {
        var button = $(this);
        button.addClass("is-loading").prop("disabled", true);
        window.location.reload();
    });

    if (dateRange) {
        dateRange.addEventListener("change", updateCustomDateFields);
    }

    if ($.fn.tooltip) {
        $("[data-toggle='tooltip']").tooltip({ container: "body" });
    }

    updateCustomDateFields();
    preserveFilters();
})(window.jQuery, window, document);
