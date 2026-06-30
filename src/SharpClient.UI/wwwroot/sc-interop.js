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
 * Stops observing the element (called on component dispose).
 * @param {HTMLElement} element
 */
export function stopObserving(element) {
    if (_observers.has(element)) {
        _observers.get(element).disconnect();
        _observers.delete(element);
    }
}
