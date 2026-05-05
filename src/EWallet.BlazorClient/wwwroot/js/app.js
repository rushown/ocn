// ─── EWallet WASM helper utilities ───────────────────────────────────────────

/**
 * Focus an element by CSS selector.
 * Called from Blazor components via JS interop.
 */
window.focusElement = function (selector) {
    const el = document.querySelector(selector);
    if (el) {
        el.focus();
        // Move cursor to end for text inputs
        if (el.setSelectionRange) {
            const len = (el.value || '').length;
            el.setSelectionRange(len, len);
        }
    }
};

/**
 * Read clipboard text (used for OTP paste).
 * Gracefully returns empty string on permission denial.
 */
window.readClipboard = async function () {
    try {
        if (navigator.clipboard && navigator.clipboard.readText) {
            return await navigator.clipboard.readText();
        }
    } catch {
        // Clipboard API unavailable or permission denied
    }
    return '';
};

/**
 * Copy text to clipboard with fallback for older browsers.
 */
window.copyToClipboard = async function (text) {
    try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
        // Fallback
        const el = document.createElement('textarea');
        el.value = text;
        el.style.position = 'fixed';
        el.style.opacity = '0';
        document.body.appendChild(el);
        el.select();
        document.execCommand('copy');
        document.body.removeChild(el);
        return true;
    } catch {
        return false;
    }
};

/**
 * Scroll element into view.
 */
window.scrollIntoView = function (selector) {
    const el = document.querySelector(selector);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

/**
 * Register service worker for PWA support.
 */
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js').catch(() => {
        // Service worker registration failed; ignore in dev
    });
}
