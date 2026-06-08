(function () {
  if (window.__eksenScalarInternalAuthLoaded) return;
  window.__eksenScalarInternalAuthLoaded = true;

  const STORAGE_KEY = 'eksen.scalar.internal-auth';
  const config = (window.__eksenScalarInternalAuthConfig && typeof window.__eksenScalarInternalAuthConfig === 'object')
    ? window.__eksenScalarInternalAuthConfig
    : {};
  const INTERNAL_DOC_PATH = config.internalDocPath || '/openapi/internal';
  const TOKEN_ENDPOINT = config.tokenUrl || '/connect/token';
  const DEFAULT_CLIENT_ID = config.clientId || 'scalar';
  const DEFAULT_CLIENT_SECRET = config.clientSecret || '';
  const DEFAULT_USERNAME = config.username || '';
  const HOST_CLAIM = config.hostClaim || 'is_host';
  const BRAND_TEXT = config.brandText || 'Internal API';
  const SUBTITLE_TEXT = config.subtitleText || 'This document is available to host users only.';

  const state = {
    token: null,
    email: DEFAULT_USERNAME || null,
    clientId: DEFAULT_CLIENT_ID,
    clientSecret: DEFAULT_CLIENT_SECRET,
    loggingIn: false,
    error: null,
    pendingResolvers: [],
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
    return v === true || v === 'true' || v === 1 || v === '1';
  }

  function tokenExpired(token) {
    const payload = decodeJwt(token);
    if (!payload || !payload.exp) return false;
    return (payload.exp * 1000) < Date.now();
  }

  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
    if (saved && saved.token && tokenIsHost(saved.token) && !tokenExpired(saved.token)) {
      state.token = saved.token;
      state.email = saved.email || state.email;
      if (saved.clientId) {
        state.clientId = saved.clientId;
      }
    }
  } catch (_) {}

  function persist() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      token: state.token,
      email: state.email,
      clientId: state.clientId,
      clientSecret: state.clientSecret,
    }));
  }

  function clearAuth() {
    state.token = null;
    state.email = null;
    localStorage.removeItem(STORAGE_KEY);
    render();
  }

  function parseUrl(url) {
    try {
      return new URL(url, window.location.origin);
    } catch (_) {
      return null;
    }
  }

  function isInternalOpenApiUrl(url) {
    const parsed = parseUrl(url);
    if (!parsed) return false;
    return parsed.pathname.toLowerCase().startsWith(INTERNAL_DOC_PATH.toLowerCase());
  }

  function isSameOriginApiUrl(url) {
    const parsed = parseUrl(url);
    if (!parsed) return false;
    if (parsed.origin !== window.location.origin) return false;
    const path = parsed.pathname.toLowerCase();
    if (path.endsWith(TOKEN_ENDPOINT.toLowerCase())) return false;
    return true;
  }

  function waitForToken() {
    return new Promise((resolve) => {
      if (state.token) {
        resolve(state.token);
        return;
      }
      state.pendingResolvers.push(resolve);
    });
  }

  function resolvePending() {
    const token = state.token;
    const resolvers = state.pendingResolvers.slice();
    state.pendingResolvers.length = 0;
    for (const r of resolvers) {
      r(token);
    }
  }

  const originalFetch = window.fetch.bind(window);

  window.fetch = async function internalAuthFetch(input, init) {
    const url = typeof input === 'string'
      ? input
      : input instanceof URL
        ? input.toString()
        : input instanceof Request
          ? input.url
          : '';

    const gateInternalDoc = isInternalOpenApiUrl(url);
    const injectBearer = gateInternalDoc || isSameOriginApiUrl(url);

    if (gateInternalDoc && !state.token) {
      await waitForToken();
    }

    if (injectBearer && state.token) {
      init = init ? { ...init } : {};
      let headers;
      if (init.headers) {
        headers = new Headers(init.headers);
      } else if (input instanceof Request) {
        headers = new Headers(input.headers);
      } else {
        headers = new Headers();
      }
      const existing = headers.get('Authorization') || headers.get('authorization');
      if (!existing || !/^Bearer\s+/i.test(existing)) {
        headers.set('Authorization', 'Bearer ' + state.token);
      }
      init.headers = headers;
    }

    return originalFetch(input, init);
  };

  async function login(clientId, clientSecret, email, password) {
    state.loggingIn = true;
    state.error = null;
    render();

    try {
      const body = new URLSearchParams();
      body.set('grant_type', 'password');
      body.set('client_id', clientId || DEFAULT_CLIENT_ID);
      if (clientSecret) {
        body.set('client_secret', clientSecret);
      }
      body.set('username', email);
      body.set('password', password);
      body.set('scope', 'openid offline_access');

      const response = await originalFetch(TOKEN_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          Accept: 'application/json',
        },
        body: body.toString(),
      });

      if (!response.ok) {
        const text = await response.text().catch(() => '');
        let detail = text;
        try {
          const parsed = JSON.parse(text);
          detail = parsed.error_description || parsed.error || text;
        } catch (_) {}
        throw new Error('Sign-in failed (' + response.status + '): ' + detail);
      }

      const json = await response.json();
      const token = json && json.access_token;
      if (!token) {
        throw new Error('Token response is missing access_token.');
      }

      if (!tokenIsHost(token)) {
        throw new Error('This user does not have host privileges. Only host users may view the internal API document.');
      }

      state.token = token;
      state.email = email;
      state.clientId = clientId || DEFAULT_CLIENT_ID;
      state.clientSecret = clientSecret || '';
      persist();
      resolvePending();
      render();

      try {
        window.location.reload();
      } catch (_) {}
    } catch (err) {
      state.error = (err && err.message) ? err.message : String(err);
    } finally {
      state.loggingIn = false;
      render();
    }
  }

  let overlay = null;
  let logoutButton = null;

  function escapeHtml(s) {
    return String(s || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function render() {
    if (!overlay || !logoutButton) return;

    if (state.token) {
      overlay.style.display = 'none';
      document.documentElement.classList.remove('eks-internal-locked');
      logoutButton.style.display = 'inline-block';
      logoutButton.textContent = state.email ? ('Sign out (' + state.email + ')') : 'Sign out';
      return;
    }

    logoutButton.style.display = 'none';
    overlay.style.display = 'flex';
    document.documentElement.classList.add('eks-internal-locked');

    const errorHtml = state.error
      ? '<div class="eks-err">' + escapeHtml(state.error) + '</div>'
      : '';

    overlay.innerHTML =
      '<div class="eks-card">' +
      '<div class="eks-brand">' + escapeHtml(BRAND_TEXT) + '</div>' +
      '<div class="eks-subtitle">' + escapeHtml(SUBTITLE_TEXT) + '</div>' +
      '<form class="eks-form" data-form="login" autocomplete="on">' +
      '<label class="eks-label">E-mail<input type="email" class="eks-input" name="email" autocomplete="username" value="' + escapeHtml(state.email || '') + '" required autofocus /></label>' +
      '<label class="eks-label">Password<input type="password" class="eks-input" name="password" autocomplete="current-password" required /></label>' +
      errorHtml +
      '<button type="submit" class="eks-submit"' + (state.loggingIn ? ' disabled' : '') + '>' +
      (state.loggingIn ? 'Signing in…' : 'Sign in') +
      '</button>' +
      '</form>' +
      '</div>';

    const form = overlay.querySelector('[data-form="login"]');
    if (form) {
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        if (state.loggingIn) return;
        const email = form.querySelector('input[name="email"]').value.trim();
        const password = form.querySelector('input[name="password"]').value;
        if (!email || !password) return;
        login(DEFAULT_CLIENT_ID, DEFAULT_CLIENT_SECRET, email, password);
      });
    }
  }

  function mount() {
    if (document.getElementById('eks-internal-auth-overlay')) return;

    const style = document.createElement('style');
    style.textContent =
      'html.eks-internal-locked body > *:not(#eks-internal-auth-overlay):not(#eks-internal-auth-logout){filter:blur(6px) saturate(.4);pointer-events:none !important;user-select:none !important;}' +
      '#eks-internal-auth-overlay{position:fixed;inset:0;z-index:2147483646;display:none;align-items:center;justify-content:center;' +
      'background:color-mix(in srgb,var(--scalar-background-1,#0f172a) 80%,transparent);backdrop-filter:blur(10px);' +
      'font:var(--scalar-paragraph,14px)/1.45 var(--scalar-font,system-ui,-apple-system,sans-serif);color:var(--scalar-color-1,#0f172a);}' +
      '#eks-internal-auth-overlay .eks-card{background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#0f172a);' +
      'border:1px solid var(--scalar-border-color,#e5e7eb);border-radius:var(--scalar-radius-lg,14px);padding:28px;width:100%;max-width:400px;' +
      'box-shadow:var(--scalar-shadow-2,0 18px 48px rgba(0,0,0,.35));display:flex;flex-direction:column;gap:14px;}' +
      '#eks-internal-auth-overlay .eks-brand{font-weight:var(--scalar-bold,700);font-size:var(--scalar-heading-3,18px);color:var(--scalar-color-1,#0f172a);}' +
      '#eks-internal-auth-overlay .eks-subtitle{font-size:var(--scalar-small,12px);color:var(--scalar-color-2,#6b7280);margin-top:-8px;}' +
      '#eks-internal-auth-overlay .eks-form{display:flex;flex-direction:column;gap:10px;}' +
      '#eks-internal-auth-overlay .eks-divider{height:1px;background:var(--scalar-border-color,#e5e7eb);margin:4px 0;}' +
      '#eks-internal-auth-overlay .eks-label{display:flex;flex-direction:column;gap:4px;font-size:var(--scalar-mini,12px);' +
      'color:var(--scalar-color-2,#374151);font-weight:var(--scalar-semibold,600);text-transform:uppercase;letter-spacing:.04em;}' +
      '#eks-internal-auth-overlay .eks-input{padding:10px 12px;border:1px solid var(--scalar-border-color,#d1d5db);' +
      'border-radius:var(--scalar-radius,8px);font:var(--scalar-paragraph,14px) var(--scalar-font,system-ui,sans-serif);' +
      'outline:none;background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#0f172a);text-transform:none;letter-spacing:normal;}' +
      '#eks-internal-auth-overlay .eks-input:focus{border-color:var(--scalar-color-accent,#7c3aed);' +
      'box-shadow:0 0 0 3px var(--scalar-background-accent,rgba(124,58,237,.15));}' +
      '#eks-internal-auth-overlay .eks-err{background:color-mix(in srgb,var(--scalar-color-red,#dc2626) 12%,transparent);' +
      'border:1px solid color-mix(in srgb,var(--scalar-color-red,#dc2626) 30%,transparent);' +
      'color:var(--scalar-color-red,#991b1b);padding:8px 10px;border-radius:var(--scalar-radius,8px);font-size:var(--scalar-small,12px);' +
      'text-transform:none;letter-spacing:normal;font-weight:normal;}' +
      '#eks-internal-auth-overlay .eks-submit{padding:11px;border:none;border-radius:var(--scalar-radius,8px);' +
      'background:var(--scalar-button-1,var(--scalar-color-accent,#7c3aed));color:var(--scalar-button-1-color,#fff);' +
      'font:var(--scalar-semibold,600) var(--scalar-paragraph,14px) var(--scalar-font,system-ui,sans-serif);cursor:pointer;margin-top:4px;}' +
      '#eks-internal-auth-overlay .eks-submit:hover{background:var(--scalar-button-1-hover,var(--scalar-color-accent,#6d28d9));}' +
      '#eks-internal-auth-overlay .eks-submit:disabled{opacity:.6;cursor:progress;}' +
      '#eks-internal-auth-logout{position:fixed;top:12px;right:12px;z-index:2147483647;padding:6px 12px;' +
      'border:1px solid var(--scalar-border-color,#d1d5db);border-radius:var(--scalar-radius-lg,999px);' +
      'background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#0f172a);' +
      'font:var(--scalar-semibold,500) var(--scalar-mini,12px) var(--scalar-font,system-ui,sans-serif);cursor:pointer;display:none;' +
      'box-shadow:var(--scalar-shadow-1,0 2px 8px rgba(0,0,0,.1));max-width:280px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}' +
      '#eks-internal-auth-logout:hover{background:var(--scalar-background-2,#f3f4f6);}';
    document.head.appendChild(style);

    overlay = document.createElement('div');
    overlay.id = 'eks-internal-auth-overlay';
    document.body.appendChild(overlay);

    logoutButton = document.createElement('button');
    logoutButton.id = 'eks-internal-auth-logout';
    logoutButton.type = 'button';
    logoutButton.textContent = 'Sign out';
    logoutButton.addEventListener('click', () => {
      if (confirm('Are you sure you want to sign out?')) {
        clearAuth();
        try {
          window.location.reload();
        } catch (_) {}
      }
    });
    document.body.appendChild(logoutButton);

    render();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mount);
  } else {
    mount();
  }
})();

export default {};
