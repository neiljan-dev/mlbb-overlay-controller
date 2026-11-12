// Turns a design (JSON from the editor) into HTML, and fills placeholders.
// Used by the overlay page (OBS) and by the editor, so both look identical.
(() => {
  'use strict';

  const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  const num = (value, fallback) => (typeof value === 'number' && isFinite(value) ? value : fallback);

  function rgba(hex, alpha) {
    const m = /^#?([0-9a-f]{6})$/i.exec(hex || '');
    if (!m) return hex || 'transparent';
    const n = parseInt(m[1], 16);
    return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`;
  }

  // Position, size and look of one element
  function boxStyle(el) {
    const s = [
      `left:${num(el.x, 0)}px`, `top:${num(el.y, 0)}px`,
      `width:${num(el.w, 100)}px`, `height:${num(el.h, 100)}px`,
      `opacity:${num(el.opacity, 1)}`
    ];
    if (el.rotation) s.push(`transform:rotate(${num(el.rotation, 0)}deg)`);
    if (el.type === 'rect') s.push(`background:${esc(rgba(el.fill || '#1f2430', num(el.fillAlpha, 1)))}`);
    if (el.radius) s.push(`border-radius:${num(el.radius, 0)}px`);
    if (el.borderWidth) s.push(`border:${num(el.borderWidth, 0)}px solid ${esc(el.borderColor || '#ffffff')}`);
    return s.join(';');
  }

  function textHtml(el, showNames) {
    const align = el.align || 'center';
    const valign = el.valign || 'middle';
    const justify = { left: 'flex-start', center: 'center', right: 'flex-end' }[align] || 'center';
    const items = { top: 'flex-start', middle: 'center', bottom: 'flex-end' }[valign] || 'center';

    const style = [
      `font-family:${esc(el.fontFamily || 'Segoe UI')}, Arial, sans-serif`,
      `font-size:${num(el.fontSize, 40)}px`,
      `color:${esc(el.color || '#ffffff')}`,
      `font-weight:${el.bold ? 800 : 400}`,
      `font-style:${el.italic ? 'italic' : 'normal'}`,
      `text-align:${esc(align)}`,
      `justify-content:${justify}`,
      `align-items:${items}`,
      `letter-spacing:${num(el.letterSpacing, 0)}px`
    ];
    if (el.uppercase) style.push('text-transform:uppercase');
    if (el.shadow) style.push('text-shadow:0 2px 10px rgba(0,0,0,0.75)');

    // data-tpl = a text with {placeholders}; applyData() fills it
    const tpl = showNames ? '' : ` data-tpl="${esc(el.text ?? '')}"`;
    const content = showNames ? esc(el.text ?? '') : '';
    return `<div class="dz-text"${tpl} style="${style.join(';')}">${content}</div>`;
  }

  function elementHtml(el, opts) {
    const showNames = !!(opts && opts.showNames);
    switch (el.type) {
      case 'image':
        return `<img src="${esc(el.src)}" alt="" draggable="false" style="object-fit:${esc(el.fit || 'contain')}">`;
      case 'placeholderImage':
        return showNames
          ? `<div class="dz-ph">${esc(el.bind)}</div>`
          : `<img data-src="${esc(el.bind)}" alt="" draggable="false" style="object-fit:${esc(el.fit || 'cover')}">`;
      case 'text':
        return textHtml(el, showNames);
      default:
        return '';   // "rect" is drawn by the box itself
    }
  }

  // The whole design as HTML. Later elements are in front of earlier ones.
  function designToHtml(design, opts) {
    const items = ((design && design.elements) || []).map(el =>
      `<div class="de de-${esc(el.type)}" data-id="${esc(el.id)}" style="${boxStyle(el)}">${elementHtml(el, opts)}</div>`);
    return `<div class="dz">${items.join('')}</div>`;
  }

  // ---- Placeholders ----
  //   data-bind="blue.name"                    -> text
  //   data-tpl="GAME {series.game}"            -> text with several placeholders
  //   data-src="blue.logo"                     -> image (hidden when empty, fades in when it changes)
  //   data-show="ban.blue.slot4.show"          -> element shown only if the value is not empty or "0"
  //   data-attr="side=event.player.team.side"  -> sets data-side="blue" (several: "a=x;b=y")
  //   data-countdown="timer.end"               -> mm:ss counting down to that end time
  function applyData(root, data) {
    root.querySelectorAll('[data-bind]').forEach(el => {
      el.textContent = data[el.dataset.bind] ?? '';
    });

    root.querySelectorAll('[data-tpl]').forEach(el => {
      el.textContent = el.dataset.tpl.replace(/\{([^{}]+)\}/g, (m, key) => data[key.trim()] ?? '');
    });

    root.querySelectorAll('[data-src]').forEach(el => {
      const value = data[el.dataset.src] || '';
      if (value) {
        el.onerror = () => { el.style.visibility = 'hidden'; };
        if (el.getAttribute('src') !== value) {
          el.src = value;
          el.classList.remove('changed');     // restart the fade-in
          void el.offsetWidth;
          el.classList.add('changed');
        }
        el.style.visibility = 'visible';
      } else {
        el.removeAttribute('src');
        el.style.visibility = 'hidden';
      }
    });

    root.querySelectorAll('[data-show]').forEach(el => {
      const value = data[el.dataset.show];
      el.style.display = value && value !== '0' ? '' : 'none';
    });

    root.querySelectorAll('[data-attr]').forEach(el => {
      el.dataset.attr.split(';').forEach(pair => {
        const [name, key] = pair.split('=');
        if (name && key) el.setAttribute('data-' + name.trim(), data[key.trim()] ?? '');
      });
    });

    root.querySelectorAll('[data-countdown]').forEach(el => {
      el.dataset.endMs = data[el.dataset.countdown] ?? '';
    });
    updateCountdowns();
  }

  // ---- Countdown timers (ads) ----
  function formatRemaining(ms) {
    const total = Math.max(0, Math.ceil(ms / 1000));
    const minutes = Math.floor(total / 60);
    const seconds = total % 60;
    return String(minutes).padStart(2, '0') + ':' + String(seconds).padStart(2, '0');
  }

  function updateCountdowns() {
    const now = Date.now();
    document.querySelectorAll('[data-countdown]').forEach(el => {
      const end = Number(el.dataset.endMs);
      el.textContent = end ? formatRemaining(end - now) : '';
    });
  }
  setInterval(updateCountdowns, 250);

  window.OverlayRenderer = { designToHtml, applyData, updateCountdowns };
})();