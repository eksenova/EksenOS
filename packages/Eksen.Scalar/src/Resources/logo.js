(function () {
  if (window.__eksenScalarLogoLoaded) return;
  window.__eksenScalarLogoLoaded = true;

  const config = (window.__eksenScalarLogoConfig && typeof window.__eksenScalarLogoConfig === 'object')
    ? window.__eksenScalarLogoConfig
    : {};

  const LOGO_URL = config.logoUrl || null;
  const LOGO_ALT = config.logoAltText || 'Logo';
  const FOOTER_TEXT = config.footerText || null;
  const FOOTER_TITLE = config.footerTitle || null;
  const HIDE_MCP = config.hideMcpControls !== false;
  const COLLAPSE = config.collapseSidebarCategories !== false;

  const LOGO_ID = 'eks-scalar-logo';
  const FOOTER_ID = 'eks-scalar-footer';

  const SIDEBAR_SELECTORS = [
    '.t-doc__sidebar',
    '.scalar-sidebar',
    'aside.sidebar',
    '[class*="sidebar"][class*="scalar"]',
    'aside[role="navigation"]',
  ];

  const SEARCH_SELECTORS = [
    '.sidebar-search',
    'button.sidebar-search-button',
    'button[aria-label*="earch"]',
    'input[type="search"]',
    '[class*="search"]',
  ];

  function ensureStyles() {
    if (document.getElementById('eks-scalar-logo-style')) return;
    const style = document.createElement('style');
    style.id = 'eks-scalar-logo-style';
    style.textContent =
      '#' + LOGO_ID + '{display:flex;align-items:center;justify-content:flex-start;' +
      'padding:22px 18px 18px 22px;margin:0;' +
      'border-bottom:1px solid var(--scalar-border-color,transparent);}' +
      '#' + LOGO_ID + ' img{display:block;max-width:220px;max-height:68px;width:auto;height:auto;object-fit:contain;' +
      '-webkit-user-drag:none;user-select:none;pointer-events:none;}';
    document.head.appendChild(style);
  }

  function findSidebar() {
    for (const sel of SIDEBAR_SELECTORS) {
      const el = document.querySelector(sel);
      if (el) return el;
    }
    return null;
  }

  function findSearchAnchor(sidebar) {
    for (const sel of SEARCH_SELECTORS) {
      const el = sidebar.querySelector(sel);
      if (el) {
        let anchor = el;
        while (anchor.parentElement && anchor.parentElement !== sidebar) {
          anchor = anchor.parentElement;
        }
        return anchor;
      }
    }
    return null;
  }

  function createLogoNode() {
    const wrap = document.createElement('div');
    wrap.id = LOGO_ID;
    const img = document.createElement('img');
    img.src = LOGO_URL;
    img.alt = LOGO_ALT;
    wrap.appendChild(img);
    return wrap;
  }

  function inject() {
    if (!LOGO_URL) return true;
    if (document.getElementById(LOGO_ID)) return true;

    const sidebar = findSidebar();
    if (!sidebar) return false;

    ensureStyles();

    const anchor = findSearchAnchor(sidebar);
    const node = createLogoNode();

    if (anchor && anchor.parentElement === sidebar) {
      sidebar.insertBefore(node, anchor);
    } else {
      sidebar.insertBefore(node, sidebar.firstChild);
    }
    return true;
  }

  const MCP_HIDE_STYLE_ID = 'eks-scalar-mcp-hide-style';

  function ensureMcpHideStyles() {
    if (document.getElementById(MCP_HIDE_STYLE_ID)) return;
    const style = document.createElement('style');
    style.id = MCP_HIDE_STYLE_ID;
    style.textContent =
      '[data-eks-hide-mcp="true"]{display:none !important;visibility:hidden !important;}';
    document.head.appendChild(style);
  }

  function isMcpElement(el) {
    if (!el || el.nodeType !== 1) return false;
    if (el.dataset && el.dataset.eksHideMcp === 'true') return false;
    const text = (el.textContent || '').trim();
    if (!text) return false;
    if (text.length > 40) return false;
    return /generate\s*mcp|mcp\s*server/i.test(text);
  }

  function hideMcpIn(root) {
    if (!HIDE_MCP) return;
    const scope = root && root.querySelectorAll ? root : document;
    const candidates = scope.querySelectorAll('button, a, li, [role="button"], [role="menuitem"]');
    for (const el of candidates) {
      if (isMcpElement(el)) {
        el.setAttribute('data-eks-hide-mcp', 'true');
      }
    }
  }

  let collapseDone = false;

  function collapseSidebarCategories(sidebar) {
    if (!COLLAPSE || collapseDone) return;
    const expanded = sidebar.querySelectorAll('[aria-expanded="true"]');
    if (expanded.length === 0) return;
    let clicked = 0;
    for (const btn of expanded) {
      const label = (btn.getAttribute('aria-label') || btn.textContent || '').trim();
      if (/search/i.test(label)) continue;
      try {
        btn.click();
        clicked++;
      } catch (_) {}
    }
    if (clicked > 0) {
      collapseDone = true;
    }
  }

  function ensureFooter() {
    if (!FOOTER_TEXT) return;
    if (document.getElementById(FOOTER_ID)) return;

    const styleId = 'eks-scalar-footer-style';
    if (!document.getElementById(styleId)) {
      const style = document.createElement('style');
      style.id = styleId;
      style.textContent =
        'html,body,main,.scalar-app-layout,.scalar-app,.t-doc,.t-doc__body,.scalar-content,' +
        '.references-layout,.section-container,[class*="scalar"][class*="scroll"],' +
        '[class*="scalar"][class*="layout"]{' +
        'scroll-padding-top:24px !important;scroll-padding-bottom:64px !important;}' +
        ':target,[id]:where(h1,h2,h3,h4,h5,h6,section,article,.section){' +
        'scroll-margin-top:24px;scroll-margin-bottom:64px;}' +
        '#' + FOOTER_ID + '{position:fixed;right:14px;bottom:12px;z-index:2147483645;' +
        'padding:5px 11px;border-radius:var(--scalar-radius-lg,999px);' +
        'font:var(--scalar-semibold,500) 10px/1.3 var(--scalar-font,system-ui,-apple-system,sans-serif);' +
        'letter-spacing:.05em;text-transform:uppercase;' +
        'color:var(--scalar-color-2,#6b7280);' +
        'background:color-mix(in srgb,var(--scalar-background-1,#fff) 80%,transparent);' +
        'border:1px solid var(--scalar-border-color,#e5e7eb);' +
        'box-shadow:var(--scalar-shadow-1,0 2px 6px rgba(0,0,0,.06));' +
        'backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px);' +
        'white-space:nowrap;pointer-events:none;user-select:none;opacity:.75;' +
        'transition:opacity 120ms ease;}' +
        '#' + FOOTER_ID + ':hover{opacity:1;}';
      document.head.appendChild(style);
    }

    const footer = document.createElement('div');
    footer.id = FOOTER_ID;
    const year = new Date().getFullYear();
    footer.textContent = FOOTER_TEXT.replace(/\{year\}/g, year);
    if (FOOTER_TITLE) {
      footer.title = FOOTER_TITLE.replace(/\{year\}/g, year);
    }
    document.body.appendChild(footer);
  }

  function rescrollToHash() {
    const hash = location.hash;
    if (!hash || hash === '#') return;
    const id = decodeURIComponent(hash.slice(1));
    if (!id) return;

    let attempts = 0;
    const maxAttempts = 25;
    let lastTop = null;
    let stableFor = 0;

    const tick = () => {
      attempts++;
      let target = null;
      try {
        target = document.getElementById(id) || document.querySelector('[name="' + CSS.escape(id) + '"]');
      } catch (_) {}

      if (target) {
        const rect = target.getBoundingClientRect();
        const absTop = Math.round(rect.top + window.scrollY);
        if (lastTop !== null && Math.abs(absTop - lastTop) < 2) {
          stableFor++;
        } else {
          stableFor = 0;
        }
        lastTop = absTop;

        try {
          target.scrollIntoView({ behavior: 'auto', block: 'start' });
        } catch (_) {}

        if (stableFor >= 2) return;
      }

      if (attempts < maxAttempts) {
        setTimeout(tick, 180);
      }
    };

    setTimeout(tick, 120);
  }

  function start() {
    ensureMcpHideStyles();
    hideMcpIn(document);
    ensureFooter();
    rescrollToHash();
    window.addEventListener('hashchange', rescrollToHash);

    let injected = inject();
    if (injected) {
      const sidebar = findSidebar();
      if (sidebar) collapseSidebarCategories(sidebar);
    }

    const observer = new MutationObserver((mutations) => {
      for (const m of mutations) {
        if (m.type !== 'childList') continue;
        for (const node of m.addedNodes) {
          if (node.nodeType !== 1) continue;
          if (HIDE_MCP && isMcpElement(node)) {
            node.setAttribute('data-eks-hide-mcp', 'true');
          } else if (node.querySelectorAll) {
            hideMcpIn(node);
          }
        }
      }

      if (!injected && inject()) {
        injected = true;
      }

      if (!collapseDone) {
        const sidebar = findSidebar();
        if (sidebar) collapseSidebarCategories(sidebar);
      }
    });

    observer.observe(document.documentElement, {
      childList: true,
      subtree: true,
    });

    setTimeout(() => observer.disconnect(), 60000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();

export default {};
