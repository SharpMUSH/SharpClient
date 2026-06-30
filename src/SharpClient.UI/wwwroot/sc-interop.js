// sc-interop.js — SharpClient JS interop helpers
// Measures an element and wires a ResizeObserver that invokes a .NET callback
// whenever the element's size changes.

const _observers = new Map();

/**
 * Measures the element and starts a ResizeObserver that calls dotNetRef.invokeMethodAsync
 * with the new dimensions on each resize.
 *
 * @param {object} dotNetRef - DotNetObjectReference wrapping the .NET callback target
 * @param {HTMLElement} element - The element to observe
 * @returns {{ width: number, height: number }} initial dimensions
 */
export function observeResize(dotNetRef, element) {
    // Tear down any previous observer on this element.
    if (_observers.has(element)) {
        _observers.get(element).disconnect();
        _observers.delete(element);
    }

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const rect = entry.contentRect;
            dotNetRef.invokeMethodAsync('OnResized', Math.floor(rect.width), Math.floor(rect.height));
        }
    });

    observer.observe(element);
    _observers.set(element, observer);

    return { width: Math.floor(element.clientWidth), height: Math.floor(element.clientHeight) };
}

/**
 * Sets the --out-fs CSS variable on the element itself. Because this element is a
 * closer ancestor of the output lines than .sc-shell, its --out-fs wins the cascade
 * over the layout-level value (which is derived from MaxFontSize).
 * @param {HTMLElement} element
 * @param {number} px
 */
export function setFontSize(element, px) {
    if (element) {
        element.style.setProperty('--out-fs', px + 'px');
    }
}

/**
 * Sticky auto-scroll: keeps the element pinned to the bottom when new content is
 * appended, unless the user has scrolled up. Idempotent per element.
 * @param {HTMLElement} element
 */
export function attachAutoScroll(element) {
    if (!element || element._scAutoScroll) {
        return;
    }
    element._scAutoScroll = true;

    const threshold = 24; // px from bottom still counts as "stuck"
    let stuck = true;
    const updateStuck = () => {
        stuck = (element.scrollHeight - element.scrollTop - element.clientHeight) <= threshold;
    };
    element.addEventListener('scroll', updateStuck, { passive: true });

    const observer = new MutationObserver(() => {
        if (stuck) {
            element.scrollTop = element.scrollHeight;
        }
    });
    observer.observe(element, { childList: true, subtree: true });

    // Start pinned to the bottom.
    element.scrollTop = element.scrollHeight;
}

/**
 * Stops observing the element (called on component dispose).
 * @param {HTMLElement} element
 */
export function stopObserving(element) {
    if (_observers.has(element)) {
        _observers.get(element).disconnect();
        _observers.delete(element);
    }
}
