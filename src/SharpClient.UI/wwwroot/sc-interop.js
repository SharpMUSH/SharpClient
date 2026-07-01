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

    // Re-fire the callback once the layout has actually settled. The first measurement can happen
    // before (a) the JetBrains Mono webfont has loaded — the fallback font has a different advance, so
    // the grid would be fitted to the wrong column width and lines wrap a few chars short — and (b) the
    // native WindowInsets bridge has pushed the real safe-area padding, which shrinks this box. Both
    // resolve asynchronously; re-measuring on fonts.ready and after a couple of frames re-fits the grid
    // to the true width. (document.fonts.ready is the same guard SharpMUSH.Client's metrics use.)
    const refire = () => {
        const cs2 = getComputedStyle(element);
        const px = parseFloat(cs2.paddingLeft || '0') + parseFloat(cs2.paddingRight || '0');
        const py = parseFloat(cs2.paddingTop || '0') + parseFloat(cs2.paddingBottom || '0');
        const w = Math.floor(element.clientWidth - px);
        const h = Math.floor(element.clientHeight - py);
        if (w > 0) {
            dotNetRef.invokeMethodAsync('OnResized', w, h);
        }
    };
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(refire);
    }
    requestAnimationFrame(() => requestAnimationFrame(refire));

    // Return the *content-box* size (padding excluded) to match what the ResizeObserver reports via
    // contentRect, so the initial NAWS/font calc and subsequent resizes use the same basis.
    const cs = getComputedStyle(element);
    const padX = parseFloat(cs.paddingLeft || '0') + parseFloat(cs.paddingRight || '0');
    const padY = parseFloat(cs.paddingTop || '0') + parseFloat(cs.paddingBottom || '0');
    return {
        width: Math.floor(element.clientWidth - padX),
        height: Math.floor(element.clientHeight - padY),
    };
}

/**
 * Measure the monospace character grid for the output element and SIZE THE FONT so exactly
 * `targetCols` columns span the available width — then report the resulting {cols, rows} for NAWS.
 *
 * The advance and line-height are MEASURED from hidden probes (a 200-char run averages out
 * sub-pixel rounding), never derived from a hard-coded 0.6em — that guess made an advertised 78
 * columns wrap ~2 short. A closed loop corrects for glyph-advance non-linearity (hinting): after the
 * ideal size is computed it re-measures `targetCols` chars and shrinks if they overflow. Font growth
 * is capped at `maxFont` (line-length cap; content left-aligns), floored at `minFont` (below that the
 * caller's --sc-cols track scrolls horizontally rather than rendering illegibly small).
 *
 * Ported from SharpMUSH.Client's terminalMetrics.js so the two clients agree on column math.
 * Applies the fitted size as --out-fs and the column count as --sc-cols on the element, and returns
 * { cols, rows }.
 *
 * @param {HTMLElement} element  the scrollable output element (padding is excluded)
 * @param {number} targetCols    preferred column width (e.g. MinColumns 78); 0 = natural grid
 * @param {number} minFont       minimum font px
 * @param {number} maxFont       maximum font px
 * @returns {{cols:number, rows:number}}
 */
export function measureGrid(element, targetCols, minFont, maxFont) {
    const minF = minFont > 0 ? minFont : 6;
    const maxF = Math.max(minF, maxFont > 0 ? maxFont : 24);
    const fallback = { cols: targetCols > 0 ? targetCols : 80, rows: 24 };
    if (!element) {
        return fallback;
    }

    const cs = getComputedStyle(element);
    const family = (cs.getPropertyValue('--mono') || '').trim() || 'monospace';
    const letter = cs.letterSpacing;
    const feature = cs.fontFeatureSettings;
    const lineHeightCss = cs.lineHeight && cs.lineHeight !== 'normal' ? cs.lineHeight : '1.2';
    const clampGrid = v => (v < 1 ? 1 : (v > 1000 ? 1000 : v));

    const runWidth = (fontPx, text) => {
        const p = document.createElement('span');
        p.style.cssText = 'position:absolute;visibility:hidden;white-space:pre;left:-9999px;top:0';
        p.style.fontFamily = family;
        p.style.fontSize = fontPx + 'px';
        p.style.letterSpacing = letter;
        p.style.fontFeatureSettings = feature;
        p.textContent = text;
        element.appendChild(p);
        const w = p.getBoundingClientRect().width;
        p.remove();
        return w;
    };

    const REF = 100;
    const advanceRatio = runWidth(REF, '0'.repeat(200)) / 200 / REF;
    if (!(advanceRatio > 0)) {
        return fallback;
    }

    let lineRatio = 1.2;
    {
        const p = document.createElement('span');
        p.style.cssText = 'position:absolute;visibility:hidden;white-space:pre;left:-9999px;top:0';
        p.style.fontFamily = family;
        p.style.fontSize = REF + 'px';
        p.style.lineHeight = lineHeightCss;
        p.textContent = '0\n0\n0\n0\n0';
        element.appendChild(p);
        const h = p.getBoundingClientRect().height / 5;
        p.remove();
        if (h > 0) {
            lineRatio = h / REF;
        }
    }

    const padX = (parseFloat(cs.paddingLeft) || 0) + (parseFloat(cs.paddingRight) || 0);
    const padY = (parseFloat(cs.paddingTop) || 0) + (parseFloat(cs.paddingBottom) || 0);
    const contentW = element.clientWidth - padX;
    const contentH = element.clientHeight - padY;

    let fontPx;
    let cols;
    if (targetCols > 0) {
        const targetW = contentW - 1; // a hair inside the box (avoid a sub-pixel scrollbar)
        let fit = targetW / targetCols / advanceRatio;
        if (fit > maxF) {
            fit = maxF;
        }
        for (let i = 0; i < 4; i++) {
            if (fit <= minF) {
                fit = minF;
                break;
            }
            const actual = runWidth(fit, '0'.repeat(targetCols));
            if (actual <= targetW) {
                break;
            }
            fit = Math.max(minF, fit * (targetW / actual));
        }
        fontPx = fit;
        cols = targetCols; // honour the preferred width; the --sc-cols track scrolls if it overflows
    } else {
        fontPx = parseFloat(cs.getPropertyValue('--out-fs')) || 14;
        cols = clampGrid(Math.floor(contentW / (advanceRatio * fontPx)));
    }

    element.style.setProperty('--out-fs', fontPx + 'px');
    element.style.setProperty('--sc-cols', String(cols));

    // Columns that ACTUALLY fit at the fitted font in the current content box. If this is less than
    // targetCols, the advertised width can't be shown and lines will wrap — the diagnostic the caller
    // logs so wrap issues can be traced from the exported log.
    const advancePx = advanceRatio * fontPx;
    const actualCols = advancePx > 0 ? Math.floor(contentW / advancePx) : 0;
    const vscroll = element.scrollHeight > element.clientHeight;

    return {
        cols,
        rows: clampGrid(Math.floor(contentH / (lineRatio * fontPx))),
        // diagnostics (see SessionScreen logging)
        fontPx: Math.round(fontPx * 100) / 100,
        clientW: element.clientWidth,
        contentW,
        padX,
        advanceRatio: Math.round(advanceRatio * 10000) / 10000,
        targetCols,
        actualCols,
        vscroll,
    };
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
 * Tracks the visual viewport and publishes its height to the --sc-app-height CSS variable on
 * <html>. The shell (.sc-shell) sizes itself to this variable, so when the Android soft keyboard
 * opens — which shrinks the visual viewport without changing 100vh — the layout shrinks too and the
 * input bar stays visible above the keyboard. Idempotent: only the first call wires the listeners.
 */
let _viewportSynced = false;
export function syncViewport() {
    if (_viewportSynced) {
        return;
    }
    _viewportSynced = true;

    const vv = window.visualViewport;
    const apply = () => {
        const h = vv ? vv.height : window.innerHeight;
        document.documentElement.style.setProperty('--sc-app-height', h + 'px');
    };

    apply();
    if (vv) {
        vv.addEventListener('resize', apply);
        vv.addEventListener('scroll', apply);
    }
    window.addEventListener('resize', apply);
    window.addEventListener('orientationchange', apply);
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
