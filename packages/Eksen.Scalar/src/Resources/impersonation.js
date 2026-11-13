// Impersonation for Scalar. Two modes:
//  - "token": exchange the caller's bearer at the token endpoint for a token scoped to the chosen subject
//    (for example a tenant) and send that token instead.
//  - "header": keep the caller's own token and send the chosen subject's id in a request header; the server
//    checks that the caller may impersonate and acts as that subject.
// The control appears only once the subjects endpoint answers the caller's token with 200, so it stays hidden
// from everyone the server would refuse.
(function () {
  if (window.__eksenScalarImpersonationLoaded) return;
  window.__eksenScalarImpersonationLoaded = true;

  const config = (window.__eksenScalarImpersonationConfig && typeof window.__eksenScalarImpersonationConfig === 'object')
    ? window.__eksenScalarImpersonationConfig
    : {};
  const text = Object.assign({
    open: 'Impersonate tenant',
    active: 'Impersonating',
    title: 'Tenant impersonation',
    search: 'Search tenants…',
    stop: 'Return to host',
    loading: 'Loading…',
    switching: 'Switching…',
    empty: 'No matches.',
    close: 'Close',
  }, config.text || {});

  const MODE = config.mode === 'header' ? 'header' : 'token';
  const STORAGE_KEY = config.storageKey || 'eksen.scalar.impersonation';
  const TOKEN_ENDPOINT = config.tokenEndpoint || '/connect/token';
  const SUBJECTS_ENDPOINT = config.subjectsEndpoint || config.tenantsEndpoint || '/api/tenants?MaxResultCount=1000&Sorting=Name%20ASC';
  const SEARCH_PARAMETER = config.searchParameter || null;
  const ITEMS_PATH = config.itemsPath || 'items';
  const ID_FIELD = config.idField || 'id';
  const NAME_FIELD = config.nameField || 'name';
  const DETAIL_FIELD = config.detailField || null;
  const HEADER_NAME = config.headerName || 'X-Impersonate';
  const API_PREFIXES = (config.apiPathPrefixes && config.apiPathPrefixes.length ? config.apiPathPrefixes : ['/api/'])
    .map((p) => String(p).toLowerCase());
  const GRANT_TYPE = config.grantType || 'tenant_impersonation';
  const CLIENT_ID = config.clientId || 'scalar';
  const SUBJECT_PARAMETER = config.subjectParameter || 'tenant_id';
  const HOST_CLAIM = config.hostClaim || null;
  const IMPERSONATING_CLAIM = config.impersonatingClaim || 'is_impersonating';

  const state = {
    bearer: null,
    allowed: false,
    probedBearer: null,
    subject: null,
    token: null,
    subjects: null,
    loading: false,
    switching: false,
    error: null,
    search: '',
    open: false,
  };

  const originalFetch = window.fetch.bind(window);

  function decodeJwt(token) {
    try {
      const part = String(token).split('.')[1];
      const normalized = part.replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized + '==='.slice((normalized.length + 3) % 4);
      return JSON.parse(decodeURIComponent(escape(atob(padded))));
    } catch (_) {
      return null;
    }
  }

  function truthy(v) {
    return v === true || v === 'true' || v === 1 || v === '1';
  }

  function eligibleBearer(token) {
    if (!token || token === state.token) return false;
    const payload = decodeJwt(token);
    if (payload && truthy(payload[IMPERSONATING_CLAIM])) return false;
    if (HOST_CLAIM && !(payload && truthy(payload[HOST_CLAIM]))) return false;
    if (payload && payload.exp && payload.exp * 1000 < Date.now()) return false;
    return true;
  }

  function read(item, path) {
    let value = item;
    for (const segment of String(path).split('.')) {
      if (value == null) return undefined;
      value = value[segment] !== undefined ? value[segment]
        : value[segment.charAt(0).toUpperCase() + segment.slice(1)];
    }
    return value;
  }

  try {
    const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null');
    if (saved && saved.subject) {
      state.subject = saved.subject;
      state.token = saved.token || null;
    }
  } catch (_) {}

  function persist() {
    try {
      if (state.subject) {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ subject: state.subject, token: state.token }));
      } else {
        sessionStorage.removeItem(STORAGE_KEY);
      }
    } catch (_) {}
  }

  function stop() {
    state.subject = null;
    state.token = null;
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

  function isApiRequest(url) {
    if (!url || url.origin !== window.location.origin) return false;
    const path = url.pathname.toLowerCase();
    if (path === new URL(TOKEN_ENDPOINT, window.location.origin).pathname.toLowerCase()) return false;
    return API_PREFIXES.some((prefix) => path.startsWith(prefix));
  }

  function headersOf(input, init) {
    return new Headers((init && init.headers) || (input instanceof Request ? input.headers : undefined));
  }

  function observe(token) {
    if (!eligibleBearer(token) || token === state.bearer) return;
    state.bearer = token;
    probe();
  }

  window.fetch = async function impersonationFetch(input, init) {
    const url = urlOf(input);
    const headers = headersOf(input, init);
    const auth = headers.get('Authorization');
    if (auth && /^Bearer\s+/i.test(auth)) {
      observe(auth.replace(/^Bearer\s+/i, '').trim());
    }

    if (state.subject && isApiRequest(url)) {
      if (MODE === 'header') {
        headers.set(HEADER_NAME, state.subject.id);
      } else if (state.token) {
        headers.set('Authorization', 'Bearer ' + state.token);
      }
      const next = init ? { ...init, headers } : { headers };
      if (input instanceof Request) input = new Request(input, { headers });
      return originalFetch(input, next);
    }

    return originalFetch(input, init);
  };

  function currentBearer() {
    const fromDocsAuth = window.__eksenDocsAuth && window.__eksenDocsAuth.getToken();
    if (fromDocsAuth && eligibleBearer(fromDocsAuth)) return fromDocsAuth;
    return state.bearer;
  }

  function subjectsUrl(query) {
    const url = new URL(SUBJECTS_ENDPOINT, window.location.origin);
    if (SEARCH_PARAMETER) {
      if (query) url.searchParams.set(SEARCH_PARAMETER, query);
      else url.searchParams.delete(SEARCH_PARAMETER);
    }
    return url;
  }

  async function requestSubjects(query) {
    const bearer = currentBearer();
    if (!bearer) return { status: 0, items: [] };
    const response = await originalFetch(subjectsUrl(query), {
      headers: { Accept: 'application/json', Authorization: 'Bearer ' + bearer },
    });
    if (!response.ok) return { status: response.status, items: [] };
    const json = await response.json();
    const list = Array.isArray(json) ? json : (read(json, ITEMS_PATH) || []);
    return {
      status: response.status,
      items: list
        .map((item) => ({
          id: String(read(item, ID_FIELD) ?? ''),
          name: String(read(item, NAME_FIELD) ?? ''),
          detail: DETAIL_FIELD ? String(read(item, DETAIL_FIELD) ?? '') : '',
        }))
        .filter((item) => item.id && item.name),
    };
  }

  async function probe() {
    const bearer = currentBearer();
    if (!bearer || bearer === state.probedBearer) {
      render();
      return;
    }
    state.probedBearer = bearer;
    try {
      const result = await requestSubjects('');
      state.allowed = result.status === 200;
      if (state.allowed) state.subjects = result.items;
    } catch (_) {
      state.allowed = false;
    }
    if (!state.allowed && state.subject) stop();
    render();
  }

  let searchTimer = null;

  async function loadSubjects() {
    state.loading = true;
    state.error = null;
    renderPanel();
    try {
      const result = await requestSubjects(state.search.trim());
      if (result.status !== 200) throw new Error('HTTP ' + result.status);
      state.subjects = result.items;
    } catch (err) {
      state.error = (err && err.message) ? err.message : String(err);
    } finally {
      state.loading = false;
      renderPanel();
    }
  }

  function visibleSubjects() {
    const list = state.subjects || [];
    if (SEARCH_PARAMETER) return list;
    const q = state.search.trim().toLocaleLowerCase();
    if (!q) return list;
    return list.filter((s) => (s.name + ' ' + s.detail + ' ' + s.id).toLocaleLowerCase().includes(q));
  }

  async function choose(subject) {
    if (MODE === 'header') {
      state.subject = subject;
      state.token = null;
      persist();
      state.open = false;
      render();
      return;
    }

    state.switching = true;
    renderPanel();
    try {
      const bearer = currentBearer();
      const body = new URLSearchParams();
      body.set('grant_type', GRANT_TYPE);
      const payload = decodeJwt(bearer) || {};
      body.set('client_id', payload.client_id || payload.azp || CLIENT_ID);
      body.set(SUBJECT_PARAMETER, subject.id);
      const response = await originalFetch(TOKEN_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          Accept: 'application/json',
          Authorization: 'Bearer ' + bearer,
        },
        body: body.toString(),
      });
      if (!response.ok) throw new Error('HTTP ' + response.status + ' ' + (await response.text().catch(() => '')));
      const json = await response.json();
      if (!json || !json.access_token) throw new Error('access_token is missing');
      state.subject = subject;
      state.token = json.access_token;
      persist();
      state.open = false;
    } catch (err) {
      state.error = (err && err.message) ? err.message : String(err);
    } finally {
      state.switching = false;
      render();
    }
  }

  function escapeHtml(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  let panel = null;

  function renderDock() {
    const dock = window.__eksenScalarDock && window.__eksenScalarDock.slot('impersonation', 20);
    if (!dock) return;
    if (!state.allowed && !state.subject) {
      dock.innerHTML = '';
      return;
    }
    if (state.subject) {
      dock.innerHTML =
        '<div class="eks-chip" data-tone="warning" role="status"><span class="eks-chip-label"><small>' + escapeHtml(text.active) + '</small>' +
        escapeHtml(state.subject.name) + '</span>' +
        '<button type="button" class="eks-link" data-action="stop">' + escapeHtml(text.stop) + '</button></div>';
      dock.querySelector('[data-action="stop"]').addEventListener('click', stop);
      return;
    }
    dock.innerHTML = '<button type="button" class="eks-chip" data-action="open" aria-haspopup="dialog" aria-expanded="' +
      (state.open ? 'true' : 'false') + '">' + escapeHtml(text.open) + '</button>';
    dock.querySelector('[data-action="open"]').addEventListener('click', (event) => {
      event.stopPropagation();
      state.open = !state.open;
      render();
      if (state.open) {
        loadSubjects();
        const input = panel && panel.querySelector('[data-input="search"]');
        if (input) input.focus();
      }
    });
  }

  function renderList() {
    const wrap = panel && panel.querySelector('[data-role="list"]');
    if (!wrap) return;
    let html;
    if (state.switching) {
      html = '<p class="eks-status">' + escapeHtml(text.switching) + '</p>';
    } else if (state.error) {
      html = '<p class="eks-status" data-kind="error" role="alert">' + escapeHtml(state.error) + '</p>';
    } else if (state.loading && !state.subjects) {
      html = '<p class="eks-status">' + escapeHtml(text.loading) + '</p>';
    } else {
      const list = visibleSubjects();
      html = list.length === 0
        ? '<p class="eks-status">' + escapeHtml(text.empty) + '</p>'
        : '<ul class="eks-list" role="list">' + list.map((s) =>
          '<li><button type="button" class="eks-item" data-id="' + escapeHtml(s.id) + '">' +
          '<span class="eks-item-name">' + escapeHtml(s.name) + '</span>' +
          (s.detail ? '<span class="eks-item-detail">' + escapeHtml(s.detail) + '</span>' : '') +
          '</button></li>').join('') + '</ul>';
    }
    wrap.setAttribute('aria-busy', state.loading ? 'true' : 'false');
    wrap.innerHTML = html;
  }

  function renderPanel() {
    if (!panel) return;
    panel.hidden = !state.open || !state.allowed || !!state.subject;
    if (panel.hidden) return;
    if (!panel.dataset.ready) {
      panel.dataset.ready = 'true';
      panel.innerHTML =
        '<div class="eks-head"><h2 class="eks-title" id="eks-imp-title">' + escapeHtml(text.title) + '</h2>' +
        '<button type="button" class="eks-close" data-action="close" aria-label="' + escapeHtml(text.close) + '">×</button></div>' +
        '<input type="search" class="eks-input" data-input="search" placeholder="' + escapeHtml(text.search) + '" aria-label="' +
        escapeHtml(text.search) + '" autocomplete="off" spellcheck="false">' +
        '<div class="eks-list-wrap" data-role="list" aria-live="polite"></div>';
      panel.querySelector('[data-input="search"]').addEventListener('input', (event) => {
        state.search = event.target.value;
        if (SEARCH_PARAMETER) {
          window.clearTimeout(searchTimer);
          searchTimer = window.setTimeout(loadSubjects, 250);
        } else {
          renderList();
        }
      });
      panel.querySelector('[data-action="close"]').addEventListener('click', () => {
        state.open = false;
        render();
      });
      panel.addEventListener('click', (event) => {
        const item = event.target instanceof Element ? event.target.closest('[data-id]') : null;
        if (!item) return;
        const subject = (state.subjects || []).find((s) => s.id === item.dataset.id);
        if (subject) choose(subject);
      });
    }
    renderList();
  }

  function render() {
    renderDock();
    renderPanel();
  }

  function mount() {
    if (document.getElementById('eks-impersonate-panel')) return;
    const style = document.createElement('style');
    style.textContent =
      '#eks-impersonate-panel{position:fixed;right:16px;bottom:64px;z-index:2147483647;display:flex;flex-direction:column;gap:12px;' +
      'width:min(380px,calc(100vw - 32px));max-height:min(70vh,560px);box-sizing:border-box;padding:16px;overflow:hidden;' +
      'background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#212121);border:1px solid var(--scalar-border-color,#e0e0e0);' +
      'border-radius:var(--scalar-radius-xl,var(--scalar-radius-lg,12px));box-shadow:var(--eks-overlay-shadow,var(--scalar-shadow-2,none));' +
      'font:var(--scalar-paragraph,15px)/1.4 var(--scalar-font,system-ui,-apple-system,sans-serif);}' +
      '#eks-impersonate-panel[hidden]{display:none;}' +
      '#eks-impersonate-panel .eks-head{display:flex;align-items:center;justify-content:space-between;gap:8px;}' +
      '#eks-impersonate-panel .eks-title{margin:0;font:var(--scalar-bold,700) 20px/1.2 var(--eks-display-font,var(--scalar-font,system-ui,sans-serif));}' +
      '#eks-impersonate-panel .eks-close{width:36px;height:36px;border:1px solid transparent;border-radius:var(--scalar-radius,6px);background:transparent;' +
      'color:var(--scalar-color-2,#616161);font-size:22px;line-height:1;cursor:pointer;}' +
      '#eks-impersonate-panel .eks-close:hover{background:var(--scalar-background-2,#f5f5f5);color:var(--scalar-color-1,#212121);}' +
      '#eks-impersonate-panel .eks-input{min-height:40px;box-sizing:border-box;padding:0 12px;border:1px solid var(--eks-control-line,var(--scalar-border-color,#757575));' +
      'border-radius:var(--scalar-radius,6px);background:var(--scalar-background-1,#fff);color:var(--scalar-color-1,#212121);font:inherit;}' +
      '#eks-impersonate-panel .eks-input:focus{outline:2px solid var(--scalar-color-accent,#5e35b1);outline-offset:1px;border-color:var(--scalar-color-accent,#5e35b1);}' +
      '#eks-impersonate-panel .eks-list-wrap{min-height:80px;overflow-y:auto;}' +
      '#eks-impersonate-panel .eks-list{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;border-top:1px solid var(--scalar-border-color,#e0e0e0);}' +
      '#eks-impersonate-panel .eks-list li{border-bottom:1px solid var(--scalar-border-color,#e0e0e0);}' +
      '#eks-impersonate-panel .eks-item{display:flex;flex-direction:column;align-items:flex-start;gap:2px;width:100%;min-height:44px;box-sizing:border-box;' +
      'padding:8px 10px;border:0;background:transparent;color:inherit;text-align:left;cursor:pointer;font:inherit;}' +
      '#eks-impersonate-panel .eks-item:hover,#eks-impersonate-panel .eks-item:focus-visible{background:var(--scalar-background-accent,#ede7f6);outline:none;}' +
      '#eks-impersonate-panel .eks-item-name{font-weight:var(--scalar-semibold,600);overflow-wrap:anywhere;}' +
      '#eks-impersonate-panel .eks-item-detail{font-size:var(--scalar-small,13px);color:var(--scalar-color-2,#616161);overflow-wrap:anywhere;}' +
      '#eks-impersonate-panel .eks-status{margin:0;padding:12px 0;font-size:var(--scalar-small,13px);color:var(--scalar-color-2,#616161);}' +
      '#eks-impersonate-panel .eks-status[data-kind="error"]{color:var(--scalar-color-red,#b71c1c);}' +
      '@media (max-width:640px){#eks-impersonate-panel{left:12px;right:12px;bottom:68px;width:auto;}}';
    document.head.appendChild(style);

    panel = document.createElement('div');
    panel.id = 'eks-impersonate-panel';
    panel.hidden = true;
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-labelledby', 'eks-imp-title');
    document.body.appendChild(panel);

    document.addEventListener('click', (event) => {
      if (!state.open || !(event.target instanceof Node)) return;
      if (panel.contains(event.target)) return;
      const dock = document.getElementById('eks-dock');
      if (dock && dock.contains(event.target)) return;
      state.open = false;
      render();
    });
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && state.open) {
        state.open = false;
        render();
      }
    });
    window.addEventListener('eksen-docs-auth-change', probe);

    render();
    probe();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mount);
  } else {
    mount();
  }
})();

export default {};
