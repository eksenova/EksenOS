(function () {
  if (window.__eksenScalarAutofillLoaded) return;
  window.__eksenScalarAutofillLoaded = true;

  const USERNAME_HINT = /user|email|login|mail|kullan/i;
  const CLIENT_ID_HINT = /client[\s_-]?id/i;
  const CLIENT_SECRET_HINT = /client[\s_-]?secret|^secret$|api[\s_-]?secret/i;

  function fieldIdentifiers(input) {
    return [
      input.name || '',
      input.id || '',
      input.placeholder || '',
      inputLabel(input),
      input.getAttribute('aria-label') || '',
    ].join(' ');
  }

  function isClientIdField(input) {
    return CLIENT_ID_HINT.test(fieldIdentifiers(input));
  }

  function isClientSecretField(input) {
    return CLIENT_SECRET_HINT.test(fieldIdentifiers(input));
  }

  function disableAutofill(input, passwordLike) {
    if (input.__eksenAutofillDisabled) return;
    input.__eksenAutofillDisabled = true;
    input.setAttribute('autocomplete', passwordLike ? 'new-password' : 'off');
    input.setAttribute('autocorrect', 'off');
    input.setAttribute('autocapitalize', 'off');
    input.setAttribute('spellcheck', 'false');
    input.setAttribute('data-lpignore', 'true');
    input.setAttribute('data-1p-ignore', 'true');
    input.setAttribute('data-form-type', 'other');
  }

  function inputLabel(input) {
    const id = input.id;
    if (id) {
      const lbl = document.querySelector('label[for="' + CSS.escape(id) + '"]');
      if (lbl && lbl.textContent) return lbl.textContent;
    }
    const parentLabel = input.closest('label');
    if (parentLabel && parentLabel.textContent) return parentLabel.textContent;
    const aria = input.getAttribute('aria-label');
    if (aria) return aria;
    return input.placeholder || input.name || '';
  }

  function containerOf(input) {
    return input.closest('form, fieldset, section, div');
  }

  function applyAutocomplete(input, value) {
    if (input.getAttribute('autocomplete') === value) return;
    input.setAttribute('autocomplete', value);
    if (value === 'username' || value === 'email') {
      if (!input.name) input.name = 'username';
    }
    if (value === 'current-password' || value === 'new-password') {
      if (!input.name) input.name = 'password';
    }
  }

  function findSiblingInput(input, predicate) {
    const scope = containerOf(input) || input.getRootNode();
    const inputs = scope.querySelectorAll('input');
    for (const el of inputs) {
      if (el === input) continue;
      if (predicate(el)) return el;
    }
    return null;
  }

  function fixPasswordInput(pw) {
    if (pw.__eksenAutofillApplied || pw.__eksenAutofillDisabled) return;

    if (pw.hasAttribute('data-no-autofill') || isClientSecretField(pw)) {
      disableAutofill(pw, true);
      return;
    }

    const sibling = findSiblingInput(pw, (el) => {
      if (el.type === 'password') return false;
      if (el.type === 'hidden' || el.type === 'submit' || el.type === 'button') return false;
      const ids = fieldIdentifiers(el);
      if (CLIENT_ID_HINT.test(ids) || CLIENT_SECRET_HINT.test(ids)) return false;
      return USERNAME_HINT.test(ids) || ['text', 'email', ''].includes(el.type);
    });

    if (sibling) {
      const siblingLabel = inputLabel(sibling);
      applyAutocomplete(sibling, /mail/i.test(siblingLabel) ? 'email' : 'username');
      sibling.__eksenAutofillApplied = true;
    }

    applyAutocomplete(pw, 'current-password');
    pw.__eksenAutofillApplied = true;

    const form = pw.closest('form');
    if (form && !form.hasAttribute('autocomplete')) {
      form.setAttribute('autocomplete', 'on');
    }
  }

  function scan(root) {
    const scope = root && root.querySelectorAll ? root : document;
    const passwords = scope.querySelectorAll('input[type="password"]');
    for (const pw of passwords) {
      fixPasswordInput(pw);
    }
    const textish = scope.querySelectorAll('input:not([type]), input[type="text"], input[type="email"], input[type="search"], input[data-no-autofill]');
    for (const input of textish) {
      if (input.__eksenAutofillDisabled) continue;
      if (input.hasAttribute('data-no-autofill') || isClientIdField(input) || isClientSecretField(input)) {
        disableAutofill(input, false);
      }
    }
  }

  function start() {
    scan(document);

    const observer = new MutationObserver((mutations) => {
      for (const m of mutations) {
        if (m.type === 'childList') {
          for (const node of m.addedNodes) {
            if (node.nodeType !== 1) continue;
            if (node.matches && node.matches('input')) {
              if (node.type === 'password') {
                fixPasswordInput(node);
              } else if (node.hasAttribute('data-no-autofill') || isClientIdField(node) || isClientSecretField(node)) {
                disableAutofill(node, false);
              }
            } else if (node.querySelectorAll) {
              scan(node);
            }
          }
        } else if (m.type === 'attributes' && m.target instanceof HTMLInputElement) {
          if (m.target.type === 'password') {
            fixPasswordInput(m.target);
          }
        }
      }
    });

    observer.observe(document.documentElement, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['type'],
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();

export default {};
