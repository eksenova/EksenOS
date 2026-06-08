(function () {
  if (window.__eksenScalarImpersonationLoaded) return;
  window.__eksenScalarImpersonationLoaded = true;

  const config = (window.__eksenScalarImpersonationConfig && typeof window.__eksenScalarImpersonationConfig === 'object')
    ? window.__eksenScalarImpersonationConfig
    : {};

  const STORAGE_KEY = 'eksen.scalar.impersonation';
  const TENANTS_CACHE_KEY = 'eksen.scalar.impersonation.tenants';
  const INTERNAL_AUTH_STORAGE_KEY = 'eksen.scalar.internal-auth';
  const TOKEN_ENDPOINT = config.tokenEndpoint || '/connect/token';
  const TENANTS_ENDPOINT = config.tenantsEndpoint || '/api/tenants?MaxResultCount=1000&Sorting=Name%20ASC';
  const GRANT_TYPE = config.grantType || 'tenant_impersonation';
  const CLIENT_ID = config.clientId || 'scalar';
  const HOST_CLAIM = config.hostClaim || 'is_host';
  const IMPERSONATING_CLAIM = config.impersonatingClaim || 'is_impersonating';

  const state = {
    capturedBearer: null,
    capturedBearerIsHost: false,
    capturedBearerFromInternal: null,
    impersonationToken: null,
    impersonatedTenantId: null,
    impersonatedTenantName: null,
    tenants: null,
    tenantsLoading: false,
    tenantsError: null,
    impersonating: false,
    impersonatingLabel: null,
    search: '',
  };

  function decodeJwt(token) {
    try {
      const parts = token.split('.');
      if (parts.length < 2) return null;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '==='.slice((payload.length + 3) % 4);
      const json = atob(padded);
      return JSON.parse(decodeURIComponent(escape(json)));
    } catch (_) {
      return null;
    }
  }

  function tokenIsHost(token) {
    const payload = decodeJwt(token);
    if (!payload) return false;
    const v = payload[HOST_CLAIM];
    if (v === true || v === 'true' || v === 1 || v === '1') return true;
    return false;
  }

  function tokenIsImpersonating(token) {
    const payload = decodeJwt(token);
    if (!payload) return false;
    const v = payload[IMPERSONATING_CLAIM];
    return v === true || v === 'true' || v === 1 || v === '1';
  }

  function tokenClientId(token) {
    const payload = decodeJwt(token);
    if (!payload) return null;
    return payload.client_id || payload.azp || payload.aud || null;
  }

  try {
    const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || '{}');
    if (saved && saved.impersonationToken) {
      state.impersonationToken = saved.impersonationToken;
      state.impersonatedTenantId = saved.impersonatedTenantId || null;
      state.impersonatedTenantName = saved.impersonatedTenantName || null;
    }
  } catch (_) {}

  try {
    const savedTenants = JSON.parse(sessionStorage.getItem(TENANTS_CACHE_KEY) || 'null');
    if (savedTenants && Array.isArray(savedTenants.items)) {
      state.tenants = savedTenants.items;
    }
  } catch (_) {}

  function tokenValid(token) {
    if (!tokenIsHost(token) || tokenIsImpersonating(token)) return false;
    if (token === state.impersonationToken) return false;
    const payload = decodeJwt(token);
    if (payload && payload.exp && (payload.exp * 1000) < Date.now()) return false;
    return true;
  }

  function readInternalAuthToken() {
    try {
      const parsed = JSON.parse(localStorage.getItem(INTERNAL_AUTH_STORAGE_KEY) || 'null');
      if (parsed && parsed.token && tokenValid(parsed.token)) {
        return parsed.token;
      }
    } catch (_) {}
    return null;
  }

  function syncFromInternalAuth() {
    const token = readInternalAuthToken();
    if (token && token !== state.capturedBearer) {
      state.capturedBearer = token;
      state.capturedBearerIsHost = true;
      render();
      return;
    }
    if (!token && state.capturedBearer && state.capturedBearer === state.capturedBearerFromInternal) {
      state.capturedBearer = null;
      state.capturedBearerFromInternal = null;
      state.capturedBearerIsHost = false;
      if (state.impersonationToken) {
        state.impersonationToken = null;
        state.impersonatedTenantId = null;
        state.impersonatedTenantName = null;
        sessionStorage.removeItem(STORAGE_KEY);
      }
      render();
    }
  }

  const initialInternal = readInternalAuthToken();
  if (initialInternal) {
    state.capturedBearer = initialInternal;
    state.capturedBearerIsHost = true;
    state.capturedBearerFromInternal = initialInternal;
  }

  window.addEventListener('storage', (e) => {
    if (!e || e.key === INTERNAL_AUTH_STORAGE_KEY || e.key === null) {
      syncFromInternalAuth();
    }
  });

  function captureBearer(token) {
    if (!token || token === state.impersonationToken) return;
    const isHost = tokenIsHost(token) && !tokenIsImpersonating(token);
    if (token === state.capturedBearer && isHost === state.capturedBearerIsHost) return;
    state.capturedBearer = token;
    state.capturedBearerIsHost = isHost;
    render();
  }

  function persist() {
    sessionStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        impersonationToken: state.impersonationToken,
        impersonatedTenantId: state.impersonatedTenantId,
        impersonatedTenantName: state.impersonatedTenantName,
      }),
    );
  }

  function persistTenants() {
    if (state.tenants) {
      sessionStorage.setItem(TENANTS_CACHE_KEY, JSON.stringify({ items: state.tenants }));
    }
  }

  function clearImpersonation() {
    state.impersonationToken = null;
    state.impersonatedTenantId = null;
    state.impersonatedTenantName = null;
    sessionStorage.removeItem(STORAGE_KEY);
    render();
  }

  const originalFetch = window.fetch.bind(window);

  function getRequestUrl(input) {
    if (typeof input === 'string') return input;
    if (input instanceof URL) return input.toString();
    if (input instanceof Request) return input.url;
    return '';
  }

  function isTokenEndpoint(url) {
    try {
      const parsed = new URL(url, window.location.origin);
      return parsed.pathname.toLowerCase().endsWith(TOKEN_ENDPOINT.toLowerCase());
    } catch (_) {
      return false;
    }
  }

  window.fetch = async function patchedFetch(input, init) {
    const url = getRequestUrl(input);
    init = init ? { ...init } : {};

    let headers;
    if (init.headers) {
      headers = new Headers(init.headers);
    } else if (input instanceof Request) {
      headers = new Headers(input.headers);
    } else {
      headers = new Headers();
    }

    const authHeader = headers.get('Authorization') || headers.get('authorization');

    if (authHeader && /^Bearer\s+/i.test(authHeader)) {
      const token = authHeader.replace(/^Bearer\s+/i, '').trim();
      captureBearer(token);

      if (state.impersonationToken && !isTokenEndpoint(url)) {
        headers.set('Authorization', 'Bearer ' + state.impersonationToken);
        init.headers = headers;

        if (input instanceof Request) {
          input = new Request(input, { headers });
        }
      }
    }

    const response = await originalFetch(input, init);

    if (isTokenEndpoint(url) && response.ok) {
      response.clone().json().then((json) => {
        if (json && typeof json.access_token === 'string') {
          captureBearer(json.access_token);
        }
      }).catch(() => {});
    }

    return response;
  };

  async function fetchTenants(force) {
    if (!state.capturedBearer) return;
    if (state.tenantsLoading) return;
    if (state.tenants && !force) return;

    state.tenantsLoading = true;
    state.tenantsError = null;
    render();

    try {
      const response = await originalFetch(TENANTS_ENDPOINT, {
        headers: {
          Accept: 'application/json',
          Authorization: 'Bearer ' + state.capturedBearer,
        },
      });
      if (!response.ok) {
        const text = await response.text().catch(() => '');
        throw new Error('Tenants request failed (' + response.status + '): ' + text);
      }
      const json = await response.json();
      const items = (json && (json.items || (json.Items))) || [];
      state.tenants = items
        .map((t) => ({ id: t.id || t.Id, name: t.name || t.Name }))
        .filter((t) => t.id && t.name)
        .sort((a, b) => a.name.localeCompare(b.name, 'tr'));
      persistTenants();
    } catch (err) {
      state.tenantsError = (err && err.message) ? err.message : String(err);
    } finally {
      state.tenantsLoading = false;
      render();
    }
  }

  async function impersonate(tenant) {
    if (!state.capturedBearer) {
      throw new Error('No host Bearer token captured yet.');
    }

    state.impersonating = true;
    state.impersonatingLabel = tenant.name || tenant.id || '';
    render();

    try {
      const body = new URLSearchParams();
      body.set('grant_type', GRANT_TYPE);
      body.set('client_id', tokenClientId(state.capturedBearer) || CLIENT_ID);
      body.set('tenant_id', tenant.id);

      const response = await originalFetch(TOKEN_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          Accept: 'application/json',
          Authorization: 'Bearer ' + state.capturedBearer,
        },
        body: body.toString(),
      });

      if (!response.ok) {
        const text = await response.text().catch(() => '');
        throw new Error('Impersonation failed (' + response.status + '): ' + text);
      }

      const json = await response.json();
      if (!json || !json.access_token) {
        throw new Error('Token endpoint response is missing access_token.');
      }

      state.impersonationToken = json.access_token;
      state.impersonatedTenantId = tenant.id;
      state.impersonatedTenantName = tenant.name;
      persist();
    } finally {
      state.impersonating = false;
      state.impersonatingLabel = null;
      render();
    }
  }

  let toggleButton = null;
  let panel = null;

  function filterTenants() {
    if (!state.tenants) return [];
    const q = state.search.trim().toLocaleLowerCase('tr');
    if (!q) return state.tenants;
    const prefix = [];
    const contains = [];
    for (const t of state.tenants) {
      const name = t.name.toLocaleLowerCase('tr');
      if (name.startsWith(q)) {
        prefix.push(t);
      } else if (name.includes(q)) {
        contains.push(t);
      }
    }
    return prefix.concat(contains);
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function render() {
    const visible = !!state.capturedBearerIsHost;

    if (!toggleButton || !panel) return;

    toggleButton.style.display = visible ? 'inline-block' : 'none';
    if (!visible) {
      panel.classList.remove('open');
      return;
    }

    const active = !!state.impersonationToken;
    toggleButton.dataset.active = active ? 'true' : 'false';
    toggleButton.textContent = active
      ? 'Impersonating: ' + (state.impersonatedTenantName || state.impersonatedTenantId || '')
      : 'Impersonate tenant';

    const activeBanner = active
      ? '<div class="eks-banner">' +
        '<div class="eks-banner-label">Currently impersonating</div>' +
        '<div class="eks-banner-name">' + escapeHtml(state.impersonatedTenantName || '') + '</div>' +
        '<div class="eks-banner-id">' + escapeHtml(state.impersonatedTenantId || '') + '</div>' +
        '<button class="eks-btn eks-btn--danger" data-action="stop">Return to host</button>' +
        '</div>'
      : '';

    panel.innerHTML =
      '<div class="eks-header">' +
      '<div class="eks-title">Tenant impersonation</div>' +
      '<button class="eks-refresh" data-action="refresh" title="Refresh tenants">↻</button>' +
      '</div>' +
      activeBanner +
      '<input type="text" class="eks-input" placeholder="Search tenants…" data-input="search" autocomplete="off" spellcheck="false" value="' + escapeHtml(state.search) + '" />' +
      '<div class="eks-list-wrap" data-role="list"></div>' +
      '<div class="eks-overlay" data-role="overlay" aria-live="polite"></div>';

    renderListOnly();

    const searchInput = panel.querySelector('[data-input="search"]');
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        state.search = e.target.value;
        renderListOnly();
      });
      if (document.activeElement !== searchInput && state.search) {
        searchInput.focus();
        searchInput.setSelectionRange(state.search.length, state.search.length);
      }
    }
  }

  function renderOverlay() {
    if (!panel) return;
    const overlay = panel.querySelector('[data-role="overlay"]');
    if (!overlay) return;

    const showList = state.tenantsLoading && (!state.tenants || state.tenants.length === 0);
    const showImpersonate = !!state.impersonating;
    const active = showList || showImpersonate;

    overlay.classList.toggle('open', active);
    if (!active) {
      overlay.innerHTML = '';
      return;
    }

    const label = showImpersonate
      ? 'Switching to ' + escapeHtml(state.impersonatingLabel || 'tenant') + '…'
      : 'Loading tenants…';

    overlay.innerHTML =
      '<div class="eks-spinner" aria-hidden="true">' +
      '<div class="eks-spinner-ring"></div>' +
      '<div class="eks-spinner-ring eks-spinner-ring--delay"></div>' +
      '</div>' +
      '<div class="eks-overlay-label">' + label + '</div>';
  }

  function renderListOnly() {
    if (!panel) return;
    const wrap = panel.querySelector('[data-role="list"]');
    if (!wrap) {
      renderOverlay();
      return;
    }

    let html;
    if (state.tenantsLoading && (!state.tenants || state.tenants.length === 0)) {
      html = '<div class="eks-status">&nbsp;</div>';
    } else if (state.tenantsError) {
      html = '<div class="eks-status" data-kind="error">' + escapeHtml(state.tenantsError) + '</div>';
    } else if (!state.tenants) {
      html = '<div class="eks-status">Click Refresh to load tenants.</div>';
    } else {
      const list = filterTenants();
      if (list.length === 0) {
        html = '<div class="eks-status">No tenants match.</div>';
      } else {
        html = '<div class="eks-list">' + list.map((t) => {
          const isActive = t.id === state.impersonatedTenantId;
          return (
            '<button type="button" class="eks-item' + (isActive ? ' eks-item--active' : '') + '" data-tenant-id="' + escapeHtml(t.id) + '" data-tenant-name="' + escapeHtml(t.name) + '">' +
            '<span class="eks-item-name">' + escapeHtml(t.name) + '</span>' +
            '<span class="eks-item-id">' + escapeHtml(t.id) + '</span>' +
            '</button>'
          );
        }).join('') + '</div>';
      }
    }

    wrap.innerHTML = html;
    renderOverlay();
  }

  function mount() {
    if (document.getElementById('eks-impersonate-toggle')) return;

    const style = document.createElement('style');
    style.textContent =
      '#eks-impersonate-toggle{position:fixed;bottom:18px;right:18px;z-index:2147483647;' +
      'padding:9px 14px;border:1px solid var(--scalar-border-color,#e5e7eb);border-radius:var(--scalar-radius-lg,999px);cursor:pointer;' +
      'font:var(--scalar-semibold,600) var(--scalar-mini,12px)/1.2 var(--scalar-font,system-ui,-apple-system,sans-serif);' +
      'background:var(--scalar-button-1,var(--scalar-background-2,#f1f5f9));color:var(--scalar-button-1-color,var(--scalar-color-1,#0f172a));' +
      'box-shadow:var(--scalar-shadow-2,0 4px 14px rgba(0,0,0,.12));max-width:320px;white-space:nowrap;' +
      'overflow:hidden;text-overflow:ellipsis;display:none;}' +
      '#eks-impersonate-toggle:hover{background:var(--scalar-button-1-hover,var(--scalar-background-3,#e2e8f0));}' +
      '#eks-impersonate-toggle[data-active="true"]{background:var(--scalar-color-red,#dc2626);color:#fff;border-color:transparent;}' +
      '#eks-impersonate-panel{position:fixed;bottom:64px;right:18px;z-index:2147483647;' +
      'display:none;flex-direction:column;gap:10px;padding:14px;width:360px;max-height:70vh;box-sizing:border-box;' +
      'overflow:hidden;background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#0f172a);' +
      'border:1px solid var(--scalar-border-color,#e5e7eb);border-radius:var(--scalar-radius-lg,12px);' +
      'box-shadow:var(--scalar-shadow-2,0 12px 32px rgba(0,0,0,.2));' +
      'font:var(--scalar-paragraph,13px)/1.45 var(--scalar-font,system-ui,-apple-system,sans-serif);}' +
      '#eks-impersonate-panel.open{display:flex;}' +
      '#eks-impersonate-panel .eks-header{display:flex;align-items:center;justify-content:space-between;}' +
      '#eks-impersonate-panel .eks-title{font-weight:var(--scalar-semibold,600);font-size:var(--scalar-mini,12px);' +
      'text-transform:uppercase;letter-spacing:.04em;color:var(--scalar-color-2,#475569);}' +
      '#eks-impersonate-panel .eks-refresh{border:1px solid var(--scalar-border-color,#e5e7eb);background:transparent;' +
      'cursor:pointer;font-size:14px;color:var(--scalar-color-2,#6b7280);padding:3px 8px;border-radius:var(--scalar-radius,6px);' +
      'font-family:var(--scalar-font,system-ui,sans-serif);}' +
      '#eks-impersonate-panel .eks-refresh:hover{background:var(--scalar-background-2,#f3f4f6);color:var(--scalar-color-1,#0f172a);}' +
      '#eks-impersonate-panel .eks-banner{padding:10px 12px;background:var(--scalar-background-2,#f8fafc);' +
      'border:1px solid var(--scalar-border-color,#e5e7eb);border-radius:var(--scalar-radius,8px);' +
      'display:flex;flex-direction:column;gap:4px;}' +
      '#eks-impersonate-panel .eks-banner-label{font-size:var(--scalar-micro,10px);text-transform:uppercase;' +
      'color:var(--scalar-color-2,#475569);letter-spacing:.05em;font-weight:var(--scalar-semibold,600);}' +
      '#eks-impersonate-panel .eks-banner-name{font-weight:var(--scalar-semibold,600);color:var(--scalar-color-1,#0f172a);}' +
      '#eks-impersonate-panel .eks-banner-id{font:var(--scalar-micro,11px) var(--scalar-font-code,ui-monospace,SFMono-Regular,monospace);' +
      'color:var(--scalar-color-2,#6b7280);word-break:break-all;}' +
      '#eks-impersonate-panel .eks-banner .eks-btn{margin-top:6px;}' +
      '#eks-impersonate-panel .eks-input{padding:8px 10px;border:1px solid var(--scalar-border-color,#d1d5db);' +
      'border-radius:var(--scalar-radius,6px);font:var(--scalar-paragraph,13px) var(--scalar-font,system-ui,sans-serif);' +
      'outline:none;background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#0f172a);}' +
      '#eks-impersonate-panel .eks-input:focus{border-color:var(--scalar-color-accent,#7c3aed);' +
      'box-shadow:0 0 0 3px var(--scalar-background-accent,rgba(124,58,237,.15));}' +
      '#eks-impersonate-panel .eks-list{display:flex;flex-direction:column;gap:2px;overflow-y:auto;max-height:40vh;' +
      'border:1px solid var(--scalar-border-color,#e5e7eb);border-radius:var(--scalar-radius,6px);padding:4px;}' +
      '#eks-impersonate-panel .eks-item{display:flex;flex-direction:column;gap:2px;padding:7px 10px;border:none;background:transparent;' +
      'text-align:left;cursor:pointer;border-radius:var(--scalar-radius,4px);color:var(--scalar-color-1,#0f172a);' +
      'font-family:var(--scalar-font,system-ui,sans-serif);}' +
      '#eks-impersonate-panel .eks-item:hover{background:var(--scalar-background-2,#f3f4f6);}' +
      '#eks-impersonate-panel .eks-item--active{background:var(--scalar-background-accent,#ede9fe);' +
      'color:var(--scalar-color-accent,#7c3aed);}' +
      '#eks-impersonate-panel .eks-item--active:hover{background:var(--scalar-background-accent,#ddd6fe);}' +
      '#eks-impersonate-panel .eks-item-name{font-weight:var(--scalar-semibold,500);}' +
      '#eks-impersonate-panel .eks-item-id{font:var(--scalar-micro,11px) var(--scalar-font-code,ui-monospace,SFMono-Regular,monospace);' +
      'color:var(--scalar-color-2,#6b7280);}' +
      '#eks-impersonate-panel .eks-btn{padding:9px 12px;border:none;border-radius:var(--scalar-radius,6px);cursor:pointer;' +
      'background:var(--scalar-button-1,var(--scalar-color-accent,#7c3aed));' +
      'color:var(--scalar-button-1-color,#fff);' +
      'font:var(--scalar-semibold,600) var(--scalar-mini,12px) var(--scalar-font,system-ui,sans-serif);}' +
      '#eks-impersonate-panel .eks-btn:hover{background:var(--scalar-button-1-hover,var(--scalar-color-accent,#6d28d9));}' +
      '#eks-impersonate-panel .eks-btn--danger{background:var(--scalar-color-red,#dc2626);color:#fff;}' +
      '#eks-impersonate-panel .eks-btn--danger:hover{background:#b91c1c;}' +
      '#eks-impersonate-panel .eks-status{font-size:var(--scalar-small,12px);color:var(--scalar-color-2,#6b7280);padding:8px;}' +
      '#eks-impersonate-panel .eks-status[data-kind="error"]{color:var(--scalar-color-red,#dc2626);}' +
      '#eks-impersonate-panel .eks-overlay{position:absolute;inset:0;z-index:5;display:none;flex-direction:column;' +
      'align-items:center;justify-content:center;gap:14px;padding:20px;' +
      'background:color-mix(in srgb,var(--scalar-background-1,#fff) 86%,transparent);' +
      'backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px);' +
      'border-radius:var(--scalar-radius-lg,12px);' +
      'animation:eks-overlay-in 160ms ease-out;}' +
      '#eks-impersonate-panel .eks-overlay.open{display:flex;}' +
      '#eks-impersonate-panel .eks-overlay-label{font:var(--scalar-semibold,500) var(--scalar-small,12px) var(--scalar-font,system-ui,sans-serif);' +
      'color:var(--scalar-color-2,#475569);text-align:center;letter-spacing:.01em;max-width:260px;line-height:1.45;}' +
      '#eks-impersonate-panel .eks-spinner{position:relative;width:42px;height:42px;}' +
      '#eks-impersonate-panel .eks-spinner-ring{position:absolute;inset:0;border-radius:50%;' +
      'border:2.5px solid transparent;border-top-color:var(--scalar-color-accent,#7c3aed);' +
      'border-right-color:var(--scalar-color-accent,#7c3aed);' +
      'animation:eks-spin 900ms cubic-bezier(.5,.1,.5,.9) infinite;}' +
      '#eks-impersonate-panel .eks-spinner-ring--delay{inset:5px;border-width:2px;opacity:.4;' +
      'border-top-color:var(--scalar-color-accent,#7c3aed);border-right-color:transparent;' +
      'animation:eks-spin-reverse 1200ms cubic-bezier(.5,.1,.5,.9) infinite;}' +
      '@keyframes eks-spin{0%{transform:rotate(0)}100%{transform:rotate(360deg)}}' +
      '@keyframes eks-spin-reverse{0%{transform:rotate(0)}100%{transform:rotate(-360deg)}}' +
      '@keyframes eks-overlay-in{0%{opacity:0}100%{opacity:1}}';
    document.head.appendChild(style);

    toggleButton = document.createElement('button');
    toggleButton.id = 'eks-impersonate-toggle';
    toggleButton.type = 'button';
    toggleButton.dataset.active = 'false';

    panel = document.createElement('div');
    panel.id = 'eks-impersonate-panel';

    document.body.appendChild(toggleButton);
    document.body.appendChild(panel);

    toggleButton.addEventListener('click', (e) => {
      e.stopPropagation();
      panel.classList.toggle('open');
      if (panel.classList.contains('open')) {
        fetchTenants(false);
      }
    });

    document.addEventListener('click', (e) => {
      if (!panel.classList.contains('open')) return;
      const target = e.target instanceof Node ? e.target : null;
      if (!target) return;
      if (panel.contains(target) || toggleButton.contains(target)) return;
      panel.classList.remove('open');
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && panel.classList.contains('open')) {
        panel.classList.remove('open');
      }
    });

    panel.addEventListener('click', async (e) => {
      const target = e.target instanceof HTMLElement ? e.target : null;
      if (!target) return;
      const actionEl = target.closest('[data-action]');
      if (actionEl) {
        const action = actionEl.dataset.action;
        if (action === 'stop') {
          clearImpersonation();
          return;
        }
        if (action === 'refresh') {
          fetchTenants(true);
          return;
        }
      }

      const itemEl = target.closest('[data-tenant-id]');
      if (itemEl) {
        const id = itemEl.dataset.tenantId;
        const name = itemEl.dataset.tenantName;
        try {
          await impersonate({ id: id, name: name });
        } catch (err) {
          alert((err && err.message) ? err.message : String(err));
        }
      }
    });

    render();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mount);
  } else {
    mount();
  }
})();

export default {};
