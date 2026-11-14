(function () {
    "use strict";

    let shortcutRef = null;
    let shortcutsAttached = false;
    let menusInitialized = false;

    const isEditable = (el) => {
        if (!el) return false;
        const tag = el.tagName;
        return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || el.isContentEditable;
    };

    function autosize(el) {
        if (!el || el.tagName !== "TEXTAREA") return;
        el.style.height = "auto";
        el.style.height = el.scrollHeight + "px";
    }

    function autosizeAll() {
        document.querySelectorAll("textarea.autosize").forEach(autosize);
    }

    function bindAutosize(el) {
        if (!el || el.tagName !== "TEXTAREA") return;
        if (!el.dataset.autosizeBound) {
            el.dataset.autosizeBound = "1";
            el.addEventListener("input", () => autosize(el));
        }
        autosize(el);
    }

    function focusFirst(root) {
        if (!root) return;
        const target = root.querySelector("input:not([type=hidden]):not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled])");
        if (target) target.focus();
    }

    function focusById(id) {
        const el = document.getElementById(id);
        if (el) {
            el.focus();
            if (el.tagName === "INPUT" && el.value) {
                el.setSelectionRange(el.value.length, el.value.length);
            }
        }
    }

    function scrollTop(el) {
        if (el) el.scrollTop = 0;
    }

    function scrollBottom(el) {
        if (el) el.scrollTop = el.scrollHeight;
    }

    function getSelection(el) {
        if (!el) return { start: 0, end: 0, text: "" };
        const start = el.selectionStart || 0;
        const end = el.selectionEnd || 0;
        return { start, end, text: (el.value || "").slice(start, end) };
    }

    function closeMenus() {
        document.querySelectorAll("details.menu[open]").forEach((d) => (d.open = false));
    }

    function initMenus() {
        if (menusInitialized) return;
        menusInitialized = true;
        document.addEventListener("click", (e) => {
            document.querySelectorAll("details.menu[open]").forEach((d) => {
                if (!d.contains(e.target)) d.open = false;
            });
        });
    }

    async function copy(text) {
        if (!text) return false;
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            const area = document.createElement("textarea");
            area.value = text;
            area.setAttribute("readonly", "");
            area.style.position = "fixed";
            area.style.opacity = "0";
            document.body.appendChild(area);
            area.select();
            let ok = false;
            try { ok = document.execCommand("copy"); } catch { ok = false; }
            document.body.removeChild(area);
            return ok;
        }
    }

    function keydownHandler(e) {
        if (!shortcutRef) return;
        const mod = e.ctrlKey || e.metaKey;
        const key = (e.key || "").toLowerCase();

        if (mod && !e.altKey && key === "s") {
            e.preventDefault();
            shortcutRef.invokeMethodAsync("SaveNow");
            return;
        }
        if (mod && e.altKey && key === "f") {
            e.preventDefault();
            shortcutRef.invokeMethodAsync("ToggleFocus");
            return;
        }
        if (mod && e.altKey && key === "a") {
            e.preventDefault();
            shortcutRef.invokeMethodAsync("ToggleAiFromShortcut");
            return;
        }
        if (e.altKey && !mod && e.key === "ArrowUp") {
            e.preventDefault();
            shortcutRef.invokeMethodAsync("NavigateBeat", -1);
            return;
        }
        if (e.altKey && !mod && e.key === "ArrowDown") {
            e.preventDefault();
            shortcutRef.invokeMethodAsync("NavigateBeat", 1);
            return;
        }
        if (e.key === "Escape") {
            closeMenus();
            shortcutRef.invokeMethodAsync("EscapeShortcut");
        }
    }

    function registerShortcuts(dotnetRef) {
        shortcutRef = dotnetRef;
        initMenus();
        if (!shortcutsAttached) {
            document.addEventListener("keydown", keydownHandler, true);
            shortcutsAttached = true;
        }
    }

    function unregisterShortcuts() {
        shortcutRef = null;
        if (shortcutsAttached) {
            document.removeEventListener("keydown", keydownHandler, true);
            shortcutsAttached = false;
        }
    }

    let resizeTimer = null;
    window.addEventListener("resize", () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(autosizeAll, 120);
    });

    window.scribe = {
        autosize,
        autosizeAll,
        bindAutosize,
        focusFirst,
        focusById,
        scrollTop,
        scrollBottom,
        getSelection,
        closeMenus,
        copy,
        isNarrow: () => window.matchMedia("(max-width: 1100px)").matches,
        registerShortcuts,
        unregisterShortcuts,
        isEditable
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initMenus);
    } else {
        initMenus();
    }
})();
