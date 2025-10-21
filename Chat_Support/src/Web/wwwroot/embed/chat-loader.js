(function () {
  try {
    var currentScript = document.currentScript || (function () { var s = document.getElementsByTagName('script'); return s[s.length - 1] })();
    var scriptSrc = currentScript.getAttribute('src') || '';
    var baseUrl;
    try { var u = new URL(scriptSrc, window.location.origin); baseUrl = u.origin; } catch { baseUrl = '' }
    // Allow overriding base with data-host
    var hostOverride = currentScript.getAttribute('data-host');
    if (hostOverride) {
      try { baseUrl = new URL(hostOverride).origin; } catch { }
    }
    if (!baseUrl) { console.warn('[ChatSupport] Unable to detect base url from script src.'); return; }

    var regionId = currentScript.getAttribute('data-region-id') || '';
    var color = currentScript.getAttribute('data-color') || '#0d6efd';
    var zIndexBase = parseInt(currentScript.getAttribute('data-z') || '2147483000', 10);

    // Position customization
    var pos = (currentScript.getAttribute('data-position') || 'bottom-right').toLowerCase();
    var offset = parseInt(currentScript.getAttribute('data-offset') || '24', 10);

    function applyPosition(el) {
      el.style.position = 'fixed';
      el.style.zIndex = el === btn ? String(zIndexBase + 1) : String(zIndexBase);
      // reset
      el.style.top = el.style.left = el.style.right = el.style.bottom = '';
      if (pos.includes('bottom')) el.style.bottom = offset + 'px'; else el.style.top = offset + 'px';
      if (pos.includes('right')) el.style.right = offset + 'px'; else el.style.left = offset + 'px';
    }

    // Create launcher button (floating circle)
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.setAttribute('aria-label', 'Chat Support');
    applyPosition(btn);
    btn.style.width = '56px';
    btn.style.height = '56px';
    btn.style.borderRadius = '50%';
    btn.style.border = '0';
    btn.style.cursor = 'pointer';
    btn.style.background = color;
    btn.style.boxShadow = '0 10px 25px rgba(0,0,0,0.2)';
    btn.style.padding = '0';
    btn.style.display = 'inline-flex';
    btn.style.alignItems = 'center';
    btn.style.justifyContent = 'center';
    btn.style.color = '#fff';
    btn.style.transition = 'transform .2s ease';
    btn.onmouseenter = function () { btn.style.transform = 'scale(1.08)'; };
    btn.onmouseleave = function () { btn.style.transform = 'scale(1)'; };
    // Message icon SVG
    btn.innerHTML = '\n      <svg width="26" height="26" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">\n        <path d="M21 12c0 4.418-4.03 8-9 8-1.15 0-2.25-.18-3.26-.51L3 20l1.54-3.69C4.19 15.28 4 14.17 4 13c0-4.418 4.03-8 9-8s8 3.582 8 7z" fill="currentColor"/>\n      </svg>\n    ';

    // Prepare iframe (hidden by default)
    var iframe; var isOpen = false;
    function ensureIframe() {
      if (iframe) return iframe;
      iframe = document.createElement('iframe');
      iframe.src = baseUrl + '/embed/widget.html' +
        '?v=3' +
        (regionId ? ('&regionId=' + encodeURIComponent(regionId)) : '') +
        '&origin=' + encodeURIComponent(window.location.href) +
        '&color=' + encodeURIComponent(color);
      applyPosition(iframe);
      iframe.style.width = '384px';
      iframe.style.height = '600px';
      iframe.style.maxHeight = '80vh';
      iframe.style.border = '0';
      iframe.style.borderRadius = '8px';
      iframe.style.boxShadow = '0 20px 25px -5px rgba(0,0,0,0.1)';
      iframe.style.display = 'none';
      iframe.setAttribute('title', 'Chat Support');
      iframe.setAttribute('aria-label', 'Chat Support');
      (document.body || document.documentElement).appendChild(iframe);
      return iframe;
    }

    function openWidget() { ensureIframe(); iframe.style.display = 'block'; btn.style.display = 'none'; isOpen = true; }
    function closeWidget() { if (!iframe) return; iframe.style.display = 'none'; btn.style.display = 'inline-flex'; isOpen = false; }

    btn.addEventListener('click', function () { if (isOpen) closeWidget(); else openWidget(); });

    (document.body || document.documentElement).appendChild(btn);

    window.addEventListener('message', function (e) {
      if (!e || !e.data || typeof e.data !== 'object') return;
      if (e.data.__chat_support_embed__ !== true) return;
      if (e.data.type === 'resize') {
        ensureIframe();
        if (typeof e.data.width === 'number') iframe.style.width = e.data.width + 'px';
        if (typeof e.data.height === 'number') iframe.style.height = e.data.height + 'px';
      } else if (e.data.type === 'hide') {
        closeWidget();
      } else if (e.data.type === 'open') {
        openWidget();
      }
    });
  } catch (err) {
    console.error('[ChatSupport] loader error:', err);
  }
})();
