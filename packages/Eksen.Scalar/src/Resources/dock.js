// Shared dock for Eksen Scalar plugins: one fixed, flat bar that session and impersonation controls render
// into, so they share a single place on the page instead of each floating its own button.
(function () {
  if (window.__eksenScalarDock) return;

  const STYLE =
    '#eks-dock{position:fixed;right:16px;bottom:16px;z-index:2147483646;display:flex;flex-wrap:wrap;justify-content:flex-end;align-items:center;gap:8px;max-width:calc(100vw - 32px);' +
    'font:var(--scalar-small,13px)/1.3 var(--scalar-font,system-ui,-apple-system,sans-serif);}' +
    '#eks-dock:empty{display:none;}' +
    '#eks-dock .eks-chip{display:inline-flex;align-items:center;gap:8px;min-height:36px;box-sizing:border-box;padding:0 12px;max-width:100%;' +
    'border:1px solid var(--scalar-border-color,#e0e0e0);border-radius:var(--scalar-radius,6px);background:var(--scalar-background-1,#fff);' +
    'color:var(--scalar-color-1,#212121);box-shadow:var(--scalar-shadow-1,none);}' +
    '#eks-dock .eks-chip-label{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:260px;}' +
    '#eks-dock .eks-chip-label small{display:block;font-size:var(--scalar-mini,11px);color:var(--scalar-color-2,#616161);}' +
    '#eks-dock button.eks-chip{cursor:pointer;font:inherit;font-weight:var(--scalar-semibold,600);}' +
    '#eks-dock button.eks-chip:hover{border-color:var(--scalar-color-accent,#5e35b1);}' +
    '#eks-dock button.eks-chip:focus-visible,#eks-dock .eks-link:focus-visible{outline:2px solid var(--scalar-color-accent,#5e35b1);outline-offset:2px;}' +
    '#eks-dock .eks-chip[data-tone="warning"]{background:var(--scalar-color-accent,#5e35b1);border-color:var(--scalar-color-accent,#5e35b1);color:var(--scalar-button-1-color,#fff);}' +
    '#eks-dock .eks-chip[data-tone="warning"] small{color:inherit;opacity:.85;}' +
    '#eks-dock .eks-link{border:0;background:transparent;padding:4px 0;cursor:pointer;font:inherit;font-weight:var(--scalar-semibold,600);' +
    'color:var(--scalar-color-accent,#5e35b1);text-decoration:underline;text-underline-offset:2px;}' +
    '#eks-dock .eks-chip[data-tone="warning"] .eks-link{color:inherit;}' +
    '@media (max-width:640px){#eks-dock{left:12px;right:12px;bottom:12px;max-width:none;flex-direction:column;align-items:stretch;}' +
    '#eks-dock .eks-chip{width:100%;min-height:44px;justify-content:space-between;}#eks-dock .eks-chip-label{max-width:none;}' +
    'body:has(#eks-dock .eks-chip){padding-bottom:calc(var(--eks-dock-height,0px) + 24px);}}';

  let host = null;
  const slots = new Map();

  function ensureHost() {
    if (host) return host;
    if (!document.body) return null;
    const style = document.createElement('style');
    style.textContent = STYLE;
    document.head.appendChild(style);
    host = document.createElement('div');
    host.id = 'eks-dock';
    host.setAttribute('role', 'region');
    host.setAttribute('aria-label', 'Documentation session');
    document.body.appendChild(host);
    return host;
  }

  // Returns the element for a named slot; slots keep their order of first request.
  function slot(name, order) {
    const root = ensureHost();
    if (!root) return null;
    let el = slots.get(name);
    if (!el) {
      el = document.createElement('div');
      el.dataset.slot = name;
      el.style.order = String(order || 0);
      el.style.display = 'contents';
      root.appendChild(el);
      slots.set(name, el);
    }
    return el;
  }

  // Reserve room under the page content for the dock, so it never hides the last lines on narrow screens.
  function measure() {
    if (!host) return;
    document.documentElement.style.setProperty('--eks-dock-height', host.getBoundingClientRect().height + 'px');
  }
  if (typeof ResizeObserver === 'function') {
    const observer = new ResizeObserver(measure);
    const watch = () => { if (host) observer.observe(host); };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => { ensureHost(); watch(); });
    else { ensureHost(); watch(); }
  }

  window.__eksenScalarDock = { slot };
})();
