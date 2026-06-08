(function () {
  if (window.__eksenScalarTokenBodyLoaded) return;
  window.__eksenScalarTokenBodyLoaded = true;

  const config = (window.__eksenScalarTokenBodyConfig && typeof window.__eksenScalarTokenBodyConfig === 'object')
    ? window.__eksenScalarTokenBodyConfig
    : {};
  const TOKEN_ENDPOINT = config.tokenEndpoint || '/connect/token';

  function isTokenEndpoint(url) {
    try {
      const parsed = new URL(url, window.location.origin);
      return parsed.pathname.toLowerCase().endsWith(TOKEN_ENDPOINT.toLowerCase());
    } catch (_) {
      return false;
    }
  }

  function decodeBasic(header) {
    const match = /^Basic\s+(.+)$/i.exec(header || '');
    if (!match) return null;
    try {
      const decoded = atob(match[1].trim());
      const sep = decoded.indexOf(':');
      if (sep < 0) return null;
      return {
        clientId: decoded.substring(0, sep),
        clientSecret: decoded.substring(sep + 1),
      };
    } catch (_) {
      return null;
    }
  }

  async function rewriteRequest(input, init) {
    let url;
    let method = (init && init.method) || 'GET';
    let headers;
    let body;

    if (input instanceof Request) {
      url = input.url;
      method = (init && init.method) || input.method;
      headers = new Headers((init && init.headers) || input.headers);
      body = init && 'body' in init ? init.body : await input.clone().text();
    } else {
      url = typeof input === 'string' ? input : input.toString();
      headers = new Headers((init && init.headers) || {});
      body = init && init.body;
    }

    if (!isTokenEndpoint(url)) return null;
    if (String(method).toUpperCase() !== 'POST') return null;

    const authHeader = headers.get('Authorization') || headers.get('authorization');
    const basic = decodeBasic(authHeader);
    if (!basic) return null;

    let params;
    if (typeof body === 'string') {
      params = new URLSearchParams(body);
    } else if (body instanceof URLSearchParams) {
      params = new URLSearchParams(body.toString());
    } else if (body instanceof FormData) {
      params = new URLSearchParams();
      for (const [k, v] of body.entries()) {
        params.append(k, String(v));
      }
    } else {
      params = new URLSearchParams();
    }

    if (!params.has('client_id')) {
      params.set('client_id', basic.clientId);
    }
    if (!params.has('client_secret') && basic.clientSecret) {
      params.set('client_secret', basic.clientSecret);
    }

    headers.delete('Authorization');
    headers.delete('authorization');
    headers.set('Content-Type', 'application/x-www-form-urlencoded');

    const nextInit = {
      method: 'POST',
      headers: headers,
      body: params.toString(),
    };

    if (init) {
      if (init.credentials) nextInit.credentials = init.credentials;
      if (init.mode) nextInit.mode = init.mode;
      if (init.cache) nextInit.cache = init.cache;
      if (init.redirect) nextInit.redirect = init.redirect;
      if (init.referrer) nextInit.referrer = init.referrer;
      if (init.integrity) nextInit.integrity = init.integrity;
      if (init.signal) nextInit.signal = init.signal;
    }

    return { input: url, init: nextInit };
  }

  const originalFetch = window.fetch.bind(window);

  window.fetch = async function tokenBodyFetch(input, init) {
    const rewritten = await rewriteRequest(input, init).catch(() => null);
    if (rewritten) {
      return originalFetch(rewritten.input, rewritten.init);
    }
    return originalFetch(input, init);
  };

  const OriginalXhr = window.XMLHttpRequest;
  if (OriginalXhr) {
    const proto = OriginalXhr.prototype;
    const origOpen = proto.open;
    const origSetHeader = proto.setRequestHeader;
    const origSend = proto.send;

    proto.open = function (method, url) {
      this.__eksToken = isTokenEndpoint(url) && String(method).toUpperCase() === 'POST';
      this.__eksUrl = url;
      this.__eksMethod = method;
      this.__eksBasic = null;
      this.__eksHeaders = [];
      return origOpen.apply(this, arguments);
    };

    proto.setRequestHeader = function (name, value) {
      if (this.__eksToken && /^authorization$/i.test(name)) {
        const basic = decodeBasic(value);
        if (basic) {
          this.__eksBasic = basic;
          return;
        }
      }
      this.__eksHeaders.push([name, value]);
      return origSetHeader.apply(this, arguments);
    };

    proto.send = function (body) {
      if (this.__eksToken && this.__eksBasic) {
        let params;
        if (typeof body === 'string') {
          params = new URLSearchParams(body);
        } else if (body instanceof URLSearchParams) {
          params = new URLSearchParams(body.toString());
        } else if (body instanceof FormData) {
          params = new URLSearchParams();
          for (const [k, v] of body.entries()) {
            params.append(k, String(v));
          }
        } else {
          params = new URLSearchParams();
        }
        if (!params.has('client_id')) {
          params.set('client_id', this.__eksBasic.clientId);
        }
        if (!params.has('client_secret') && this.__eksBasic.clientSecret) {
          params.set('client_secret', this.__eksBasic.clientSecret);
        }
        origSetHeader.call(this, 'Content-Type', 'application/x-www-form-urlencoded');
        return origSend.call(this, params.toString());
      }
      return origSend.apply(this, arguments);
    };
  }
})();

export default {};
