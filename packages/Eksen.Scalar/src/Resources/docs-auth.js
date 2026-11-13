// Docs sign-in wall for Scalar: gates an OpenAPI document behind an OpenID Connect sign-in (authorization code
// with PKCE, in a popup) against any provider such as Keycloak. The server decides who may read the document;
// this plugin only obtains a token, attaches it to the document request and reacts to 401 and 403.
(function () {
  if (window.__eksenScalarDocsAuthLoaded) return;
  window.__eksenScalarDocsAuthLoaded = true;

  const config = (window.__eksenScalarDocsAuthConfig && typeof window.__eksenScalarDocsAuthConfig === 'object')
    ? window.__eksenScalarDocsAuthConfig
    : {};
  const text = Object.assign({
    signIn: 'Sign in',
    waiting: 'Waiting for the sign-in window…',
    hint: 'Sign-in opens in a new window.',
    signedInAs: 'Signed in as',
    signOut: 'Sign out',
    expired: 'Your session has expired. Sign in again.',
    forbidden: 'This account cannot open this document. Sign in with an account that has access.',
    popupBlocked: 'The sign-in window could not open. Allow pop-ups for this site and try again.',
    failed: 'Sign-in did not complete',
    missingClaim: 'This account does not have the access this document requires.',
  }, config.text || {});

  const CHANNEL = config.channel || 'eksen-docs-oauth';
  const STORAGE_KEY = config.storageKey || 'eksen.scalar.docs-auth';
  const DOCUMENT_PREFIX = String(config.documentPathPrefix || '/openapi/').toLowerCase();
  const API_PREFIXES = (config.apiPathPrefixes && config.apiPathPrefixes.length ? config.apiPathPrefixes : ['/api/'])
    .map((p) => String(p).toLowerCase());
  const SCOPE = config.scope || 'openid';
  const REFRESH_MARGIN_MS = 60 * 1000;
  const tokenUrl = new URL(config.tokenUrl, window.location.origin);

  const state = { session: null, busy: false, error: null, waiters: [], pending: null };
  const originalFetch = window.fetch.bind(window);

  function decodeJwt(token) {
    try {
      const part = String(token).split('.')[1];
      if (!part) return null;
      const normalized = part.replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized + '==='.slice((normalized.length + 3) % 4);
      return JSON.parse(decodeURIComponent(escape(atob(padded))));
    } catch (_) {
      return null;
    }
  }

  // Optional client-side precheck; the server remains the authority.
  function hasRequiredClaim(token) {
    const required = config.requiredClaim;
    if (!required || !required.name) return true;
    const payload = decodeJwt(token) || {};
    let value = payload;
    for (const segment of String(required.name).split('.')) {
      value = value == null ? undefined : value[segment];
    }
    const values = Array.isArray(value) ? value.map(String) : value == null ? [] : [String(value)];
    if (!required.values || required.values.length === 0) return values.length > 0;
    return required.values.some((v) => values.includes(String(v)));
  }

  function labelOf(token) {
    const payload = decodeJwt(token) || {};
    return payload.preferred_username || payload.email || payload.name || payload.sub || '';
  }

  function expiryOf(token) {
    const payload = decodeJwt(token);
    return payload && payload.exp ? payload.exp * 1000 : Date.now() + 5 * 60 * 1000;
  }

  function persist() {
    try {
      if (state.session) {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state.session));
      } else {
        sessionStorage.removeItem(STORAGE_KEY);
      }
    } catch (_) {}
    window.dispatchEvent(new CustomEvent('eksen-docs-auth-change'));
  }

  function restore() {
    try {
      const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null');
      if (!saved || !saved.accessToken) return;
      if (saved.expiresAt <= Date.now() && !saved.refreshToken) return;
      state.session = saved;
    } catch (_) {}
  }

  function setSession(session) {
    state.session = session;
    state.error = null;
    persist();
    for (const resolve of state.waiters.splice(0)) resolve();
  }

  function clearSession(message) {
    state.session = null;
    state.error = message || null;
    persist();
    render();
  }

  function urlOf(input) {
    try {
      const raw = typeof input === 'string' ? input
        : input instanceof URL ? input.toString()
          : input instanceof Request ? input.url : '';
      return new URL(raw, window.location.origin);
    } catch (_) {
      return null;
    }
  }

  function isDocumentRequest(url) {
    return !!url && url.origin === window.location.origin && url.pathname.toLowerCase().startsWith(DOCUMENT_PREFIX);
  }

  function isApiRequest(url) {
    if (!url || url.origin !== window.location.origin) return false;
    const path = url.pathname.toLowerCase();
    return API_PREFIXES.some((prefix) => path.startsWith(prefix));
  }

  async function postForm(url, values) {
    const body = new URLSearchParams();
    for (const [key, value] of Object.entries(values)) {
      if (value !== undefined && value !== null && value !== '') body.set(key, value);
    }
    const response = await originalFetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded', Accept: 'application/json' },
      body: body.toString(),
    });
    const raw = await response.text().catch(() => '');
    let json = null;
    try { json = raw ? JSON.parse(raw) : null; } catch (_) {}
    if (!response.ok) {
      const detail = (json && (json.error_description || json.error)) || raw || response.statusText;
      throw new Error(text.failed + ' (' + response.status + '): ' + detail);
    }
    return json || {};
  }

  function sessionFrom(json) {
    if (!json.access_token) throw new Error(text.failed + ': access_token is missing.');
    if (!hasRequiredClaim(json.access_token)) throw new Error(text.missingClaim);
    return {
      accessToken: json.access_token,
      refreshToken: json.refresh_token || null,
      idToken: json.id_token || null,
      expiresAt: json.expires_in ? Date.now() + json.expires_in * 1000 : expiryOf(json.access_token),
      label: labelOf(json.id_token || json.access_token),
    };
  }

  async function refreshIfNeeded() {
    const session = state.session;
    if (!session || session.expiresAt - REFRESH_MARGIN_MS > Date.now()) return;
    if (!session.refreshToken) {
      clearSession(text.expired);
      return;
    }
    try {
      const json = await postForm(tokenUrl, {
        grant_type: 'refresh_token',
        client_id: config.clientId,
        refresh_token: session.refreshToken,
      });
      setSession(sessionFrom(json));
    } catch (_) {
      clearSession(text.expired);
    }
  }

  function withHeader(input, init, name, value) {
    const next = init ? { ...init } : {};
    const headers = new Headers(next.headers || (input instanceof Request ? input.headers : undefined));
    if (!headers.has(name)) headers.set(name, value);
    next.headers = headers;
    return next;
  }

  window.fetch = async function docsAuthFetch(input, init) {
    const url = urlOf(input);

    if (isDocumentRequest(url)) {
      await refreshIfNeeded();
      if (!state.session) {
        render();
        await new Promise((resolve) => state.waiters.push(resolve));
      }
      const response = await originalFetch(input, withHeader(input, init, 'Authorization', 'Bearer ' + state.session.accessToken));
      if (response.status === 401) clearSession(text.expired);
      else if (response.status === 403) clearSession(text.forbidden);
      return response;
    }

    if (state.session && isApiRequest(url)) {
      await refreshIfNeeded();
      if (state.session) {
        return originalFetch(input, withHeader(input, init, 'Authorization', 'Bearer ' + state.session.accessToken));
      }
    }

    return originalFetch(input, init);
  };

  function base64Url(bytes) {
    let binary = '';
    for (const b of bytes) binary += String.fromCharCode(b);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  function randomString(length) {
    const bytes = new Uint8Array(length);
    crypto.getRandomValues(bytes);
    return base64Url(bytes);
  }

  async function challengeOf(verifier) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
    return base64Url(new Uint8Array(digest));
  }

  function redirectUri() {
    return new URL(config.callbackPath, window.location.origin).toString();
  }

  async function startLogin() {
    if (state.busy) return;
    state.error = null;
    const verifier = randomString(48);
    const loginState = randomString(24);
    const authorize = new URL(config.authorizeUrl, window.location.origin);
    authorize.searchParams.set('client_id', config.clientId);
    authorize.searchParams.set('response_type', 'code');
    authorize.searchParams.set('redirect_uri', redirectUri());
    authorize.searchParams.set('scope', SCOPE);
    authorize.searchParams.set('code_challenge', await challengeOf(verifier));
    authorize.searchParams.set('code_challenge_method', 'S256');
    authorize.searchParams.set('state', loginState);

    const width = 520;
    const height = 720;
    const left = Math.max(0, (window.screenX || 0) + (window.outerWidth - width) / 2);
    const top = Math.max(0, (window.screenY || 0) + (window.outerHeight - height) / 2);
    const popup = window.open(authorize.toString(), 'eksen-docs-login',
      'popup=yes,width=' + width + ',height=' + height + ',left=' + left + ',top=' + top);
    if (!popup) {
      state.error = text.popupBlocked;
      render();
      return;
    }

    state.busy = true;
    state.pending = { verifier, state: loginState, popup };
    render();

    const watcher = window.setInterval(() => {
      if (!state.pending || state.pending.popup !== popup) {
        window.clearInterval(watcher);
        return;
      }
      if (popup.closed) {
        window.clearInterval(watcher);
        window.setTimeout(() => {
          if (state.pending && state.pending.popup === popup) {
            state.pending = null;
            state.busy = false;
            render();
          }
        }, 800);
      }
    }, 500);
  }

  async function completeLogin(message) {
    const pending = state.pending;
    if (!pending || !message || message.type !== CHANNEL || message.state !== pending.state) return;
    state.pending = null;
    try { if (pending.popup && !pending.popup.closed) pending.popup.close(); } catch (_) {}

    if (message.error || !message.code) {
      state.busy = false;
      state.error = text.failed + ': ' + (message.errorDescription || message.error || 'unknown error');
      render();
      return;
    }

    try {
      const json = await postForm(tokenUrl, {
        grant_type: 'authorization_code',
        client_id: config.clientId,
        code: message.code,
        redirect_uri: redirectUri(),
        code_verifier: pending.verifier,
      });
      setSession(sessionFrom(json));
      window.location.reload();
    } catch (err) {
      state.busy = false;
      state.error = (err && err.message) ? err.message : String(err);
      render();
    }
  }

  try {
    const channel = new BroadcastChannel(CHANNEL);
    channel.addEventListener('message', (event) => completeLogin(event.data));
  } catch (_) {}
  window.addEventListener('message', (event) => {
    if (event.origin === window.location.origin) completeLogin(event.data);
  });

  function signOut() {
    const idToken = state.session && state.session.idToken;
    clearSession(null);
    if (config.endSessionUrl) {
      const end = new URL(config.endSessionUrl, window.location.origin);
      end.searchParams.set('client_id', config.clientId);
      end.searchParams.set('post_logout_redirect_uri', window.location.href.split('#')[0]);
      if (idToken) end.searchParams.set('id_token_hint', idToken);
      window.location.assign(end.toString());
      return;
    }
    window.location.reload();
  }

  window.__eksenDocsAuth = {
    getToken: () => (state.session ? state.session.accessToken : null),
    getLabel: () => (state.session ? state.session.label : null),
  };

  let overlay = null;

  function escapeHtml(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function renderDock() {
    const dock = window.__eksenScalarDock && window.__eksenScalarDock.slot('auth', 10);
    if (!dock) return;
    if (!state.session) {
      dock.innerHTML = '';
      return;
    }
    dock.innerHTML =
      '<div class="eks-chip"><span class="eks-chip-label"><small>' + escapeHtml(text.signedInAs) + '</small>' +
      escapeHtml(state.session.label || '') + '</span>' +
      '<button type="button" class="eks-link" data-action="sign-out">' + escapeHtml(text.signOut) + '</button></div>';
    dock.querySelector('[data-action="sign-out"]').addEventListener('click', signOut);
  }

  function render() {
    renderDock();
    if (!overlay) return;

    if (state.session) {
      overlay.hidden = true;
      document.documentElement.classList.remove('eks-docs-locked');
      return;
    }

    overlay.hidden = false;
    document.documentElement.classList.add('eks-docs-locked');
    const error = state.error ? '<p class="eks-err" role="alert">' + escapeHtml(state.error) + '</p>' : '';
    overlay.innerHTML =
      '<div class="eks-card" role="dialog" aria-modal="true" aria-labelledby="eks-docs-title" aria-describedby="eks-docs-subtitle">' +
      (config.logoUrl ? '<div class="eks-card-brand"><img src="' + escapeHtml(config.logoUrl) + '" alt="' + escapeHtml(config.logoAltText || '') + '"></div>' : '') +
      '<div class="eks-card-body">' +
      (config.eyebrow ? '<p class="eks-eyebrow">' + escapeHtml(config.eyebrow) + '</p>' : '') +
      '<h1 class="eks-title" id="eks-docs-title">' + escapeHtml(config.title || '') + '</h1>' +
      '<p class="eks-subtitle" id="eks-docs-subtitle">' + escapeHtml(config.subtitle || '') + '</p>' +
      error +
      '<button type="button" class="eks-submit" data-action="login"' + (state.busy ? ' disabled' : '') + '>' +
      escapeHtml(state.busy ? text.waiting : text.signIn) + '</button>' +
      '<p class="eks-hint">' + escapeHtml(text.hint) + '</p>' +
      '</div></div>';
    const button = overlay.querySelector('[data-action="login"]');
    button.addEventListener('click', startLogin);
    if (!state.busy) button.focus();
  }

  function mount() {
    if (document.getElementById('eks-docs-auth-overlay')) return;
    const style = document.createElement('style');
    style.textContent =
      'html.eks-docs-locked body > *:not(#eks-docs-auth-overlay):not(#eks-dock){visibility:hidden;}' +
      '#eks-docs-auth-overlay{position:fixed;inset:0;z-index:2147483646;display:flex;align-items:center;justify-content:center;padding:16px;box-sizing:border-box;' +
      'background:var(--eks-wall-background,var(--scalar-background-2,#eeeeee));' +
      'font:var(--scalar-paragraph,15px)/1.5 var(--scalar-font,system-ui,-apple-system,sans-serif);color:var(--scalar-color-1,#212121);}' +
      '#eks-docs-auth-overlay[hidden]{display:none;}' +
      '#eks-docs-auth-overlay .eks-card{width:100%;max-width:420px;overflow:hidden;background:var(--scalar-background-1,#fff);' +
      'border:1px solid var(--scalar-border-color,#e0e0e0);border-radius:var(--scalar-radius-xl,var(--scalar-radius-lg,12px));}' +
      '#eks-docs-auth-overlay .eks-card-brand{display:flex;align-items:center;min-height:56px;padding:0 24px;background:var(--eks-brand-background,var(--scalar-color-accent,#5e35b1));}' +
      '#eks-docs-auth-overlay .eks-card-brand img{display:block;height:26px;width:auto;}' +
      '#eks-docs-auth-overlay .eks-card-body{display:flex;flex-direction:column;gap:12px;padding:24px;}' +
      '#eks-docs-auth-overlay .eks-eyebrow{margin:0;font-size:var(--scalar-small,13px);font-weight:var(--scalar-semibold,600);color:var(--scalar-color-accent,#5e35b1);}' +
      '#eks-docs-auth-overlay .eks-title{margin:0;font:var(--scalar-bold,700) var(--scalar-heading-2,26px)/1.15 var(--eks-display-font,var(--scalar-font,system-ui,sans-serif));}' +
      '#eks-docs-auth-overlay .eks-subtitle{margin:0;color:var(--scalar-color-2,#616161);}' +
      '#eks-docs-auth-overlay .eks-hint{margin:0;font-size:var(--scalar-small,13px);color:var(--scalar-color-2,#616161);}' +
      '#eks-docs-auth-overlay .eks-err{margin:0;padding:10px 12px;border:1px solid var(--scalar-color-red,#d50000);border-radius:var(--scalar-radius,6px);' +
      'background:var(--scalar-background-danger,#ffebee);color:var(--scalar-color-red,#b71c1c);font-size:var(--scalar-small,13px);}' +
      '#eks-docs-auth-overlay .eks-submit{min-height:44px;margin-top:4px;padding:0 20px;border:1px solid transparent;border-radius:var(--scalar-radius,6px);cursor:pointer;' +
      'background:var(--scalar-button-1,var(--scalar-color-accent,#5e35b1));color:var(--scalar-button-1-color,#fff);' +
      'font:var(--scalar-semibold,600) 16px/1.2 var(--scalar-font,system-ui,sans-serif);}' +
      '#eks-docs-auth-overlay .eks-submit:hover{background:var(--scalar-button-1-hover,#512da8);}' +
      '#eks-docs-auth-overlay .eks-submit:focus-visible{outline:2px solid var(--scalar-color-accent,#5e35b1);outline-offset:2px;}' +
      '#eks-docs-auth-overlay .eks-submit:disabled{cursor:progress;opacity:.7;}';
    document.head.appendChild(style);

    overlay = document.createElement('div');
    overlay.id = 'eks-docs-auth-overlay';
    overlay.hidden = true;
    document.body.appendChild(overlay);
    render();
  }

  restore();
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mount);
  } else {
    mount();
  }
})();

export default {};
