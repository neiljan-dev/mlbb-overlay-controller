(() => {
  'use strict';

  const $ = id => document.getElementById(id);
  const R = window.OverlayRenderer;

  const FONTS = ['Segoe UI', 'Arial', 'Impact', 'Bahnschrift', 'Trebuchet MS', 'Verdana', 'Tahoma', 'Georgia', 'Consolas'];
  const TYPE_LABEL = { rect: 'Rect', image: 'Image', placeholderImage: 'Placeholder', text: 'Text' };
  const CURSORS = { nw: 'nwse-resize', se: 'nwse-resize', ne: 'nesw-resize', sw: 'nesw-resize',
                    n: 'ns-resize', s: 'ns-resize', e: 'ew-resize', w: 'ew-resize' };
  const HANDLES = { nw: [0, 0], n: [50, 0], ne: [100, 0], e: [100, 50], se: [100, 100], s: [50, 100], sw: [0, 100], w: [0, 50] };

  // Animation presets (the names match the keyframes in design.css)
  const IN_OUT_PRESETS = [
    ['fade', 'Fade'], ['slideLeft', 'Slide: left edge'], ['slideRight', 'Slide: right edge'],
    ['slideTop', 'Slide: top edge'], ['slideBottom', 'Slide: bottom edge'],
    ['zoom', 'Zoom'], ['pop', 'Pop'], ['spin', 'Spin'], ['blur', 'Blur'], ['wipe', 'Wipe']
  ];
  const LOOP_PRESETS = [['pulse', 'Pulse'], ['float', 'Float'], ['glow', 'Glow'], ['shake', 'Shake'], ['spin', 'Spin'], ['blink', 'Blink']];
  const EASINGS = [['ease-out', 'Ease out'], ['ease-in', 'Ease in'], ['ease-in-out', 'Ease in-out'],
                   ['ease', 'Ease'], ['linear', 'Linear'], ['bounce', 'Bounce']];

  const key = new URLSearchParams(location.search).get('key') || '';

  let design = { canvas: { w: 1920, h: 1080 }, elements: [] };
  let sample = {};          // placeholder name -> sample value
  let selectedId = null;
  let scale = 1;
  let zoom = 'fit';
  let showNames = false;
  let dirty = false;
  let previewing = false;   // true while "Play animation" runs
  let history = [];         // snapshots, for undo
  const inputs = {};        // property name -> { input, type } of the selected element

  const uid = () => 'e' + Math.random().toString(36).slice(2, 8);
  const current = () => design.elements.find(e => e.id === selectedId) || null;
  const isImageKey = k => /\.(icon|splash|full|logo)$/.test(k);
  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

  // ---------- Status, history ----------
  const setStatus = text => { $('status').textContent = text; };
  const updateStatus = () => setStatus(dirty ? 'Unsaved changes' : 'All changes saved');

  const snapshot = () => JSON.stringify(design);

  function commit() {
    const s = snapshot();
    if (history[history.length - 1] !== s) {
      history.push(s);
      if (history.length > 100) history.shift();
    }
    dirty = true;
    updateStatus();
  }

  function undo() {
    if (history.length < 2) return;
    history.pop();
    design = JSON.parse(history[history.length - 1]);
    if (!current()) selectedId = null;
    dirty = true;
    renderAll();
  }

  // ---------- Canvas ----------
  function renderStage() {
    const stage = $('stage');
    // Animations only run while "Play animation" is on, so the canvas stays still while you edit
    stage.innerHTML = R.designToHtml(design, { showNames, animate: previewing });
    R.applyData(stage, sample);
  }

  // Quick update of the position and size of one element (used while dragging)
  function patch(el) {
    const node = $('stage').querySelector(`.de[data-id="${el.id}"]`);
    if (!node) return;
    node.style.left = el.x + 'px';
    node.style.top = el.y + 'px';
    node.style.width = el.w + 'px';
    node.style.height = el.h + 'px';
  }

  function applyZoom() {
    const vp = $('viewport');
    const w = design.canvas.w, h = design.canvas.h;
    scale = zoom === 'fit'
      ? Math.max(0.1, Math.min((vp.clientWidth - 48) / w, (vp.clientHeight - 48) / h))
      : Number(zoom);
    $('scaler').style.width = w * scale + 'px';
    $('scaler').style.height = h * scale + 'px';
    $('wrap').style.transform = `scale(${scale})`;
    positionSelection();
  }

  function renderAll() {
    renderStage();
    renderLayers();
    renderProps();
    positionSelection();
    updateStatus();
  }

  function select(id) {
    selectedId = id;
    renderLayers();
    renderProps();
    positionSelection();
  }

  // ---------- Selection box and handles ----------
  function positionSelection() {
    const sel = $('sel');
    sel.innerHTML = '';
    const el = current();
    if (!el || previewing) return;

    const box = document.createElement('div');
    box.className = 'selbox';
    box.style.cssText = `left:${el.x}px;top:${el.y}px;width:${el.w}px;height:${el.h}px;` +
      `outline-width:${2 / scale}px;` + (el.rotation ? `transform:rotate(${el.rotation}deg);` : '');

    const size = 12 / scale;
    for (const [name, [px, py]] of Object.entries(HANDLES)) {
      const h = document.createElement('div');
      h.className = 'h';
      h.dataset.h = name;
      h.style.cssText = `left:${px}%;top:${py}%;width:${size}px;height:${size}px;` +
        `margin:${-size / 2}px 0 0 ${-size / 2}px;cursor:${CURSORS[name]};border-width:${1 / scale}px;`;
      box.appendChild(h);
    }
    sel.appendChild(box);
  }

  function resize(el, handle, s, dx, dy, keepRatio) {
    let x = s.ex, y = s.ey, w = s.ew, h = s.eh;
    if (handle.includes('e')) w = s.ew + dx;
    if (handle.includes('s')) h = s.eh + dy;
    if (handle.includes('w')) { w = s.ew - dx; x = s.ex + dx; }
    if (handle.includes('n')) { h = s.eh - dy; y = s.ey + dy; }

    if (keepRatio && handle.length === 2) {          // corner + Shift: keep the proportions
      const ratio = s.ew / s.eh;
      if (Math.abs(w - s.ew) > Math.abs(h - s.eh)) h = w / ratio; else w = h * ratio;
      if (handle.includes('w')) x = s.ex + s.ew - w;
      if (handle.includes('n')) y = s.ey + s.eh - h;
    }

    el.x = Math.round(x);
    el.y = Math.round(y);
    el.w = Math.max(10, Math.round(w));
    el.h = Math.max(10, Math.round(h));
  }

  // mode = "move" or a handle name ("se", "n"...)
  function startDrag(e, mode) {
    const el = current();
    if (!el) return;
    e.preventDefault();

    const s = { x: e.clientX, y: e.clientY, ex: el.x, ey: el.y, ew: el.w, eh: el.h };
    let moved = false;

    const move = ev => {
      const dx = (ev.clientX - s.x) / scale;
      const dy = (ev.clientY - s.y) / scale;
      if (Math.abs(dx) + Math.abs(dy) > 0.5) moved = true;

      if (mode === 'move') {
        el.x = Math.round(s.ex + dx);
        el.y = Math.round(s.ey + dy);
      } else {
        resize(el, mode, s, dx, dy, ev.shiftKey);
      }
      patch(el);
      positionSelection();
      syncInputs();
      dirty = true;
      updateStatus();
    };
    const up = () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      if (moved) commit();
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  }

  $('stage').addEventListener('pointerdown', e => {
    if (previewing) return;
    const node = e.target.closest('.de');
    if (!node) { select(null); return; }
    select(node.dataset.id);
    startDrag(e, 'move');
  });

  $('sel').addEventListener('pointerdown', e => {
    const handle = e.target.dataset.h;
    if (!handle) return;
    e.stopPropagation();
    startDrag(e, handle);
  });

  // ---------- Elements list ----------
  function swap(i, j) {
    const list = design.elements;
    const tmp = list[i];
    list[i] = list[j];
    list[j] = tmp;
  }

  function layerAction(id, action) {
    const list = design.elements;
    const i = list.findIndex(e => e.id === id);
    if (i < 0) return;

    if (action === 'up' && i < list.length - 1) swap(i, i + 1);
    else if (action === 'down' && i > 0) swap(i, i - 1);
    else if (action === 'del') {
      list.splice(i, 1);
      if (selectedId === id) selectedId = null;
    }
    renderAll();
    commit();
  }

  function renderLayers() {
    const box = $('layers');
    box.innerHTML = '';
    if (!design.elements.length) {
      box.innerHTML = '<p class="hint">Nothing yet. Add an element above.</p>';
      return;
    }

    [...design.elements].reverse().forEach(el => {
      const row = document.createElement('div');
      row.className = 'row' + (el.id === selectedId ? ' on' : '');
      row.innerHTML =
        '<span class="nm"></span>' +
        '<button data-a="up" title="Bring forward">&#8593;</button>' +
        '<button data-a="down" title="Send back">&#8595;</button>' +
        '<button data-a="del" title="Delete">&#10005;</button>';

      const label = row.querySelector('.nm');
      const tag = document.createElement('span');
      tag.className = 'tag';
      tag.textContent = TYPE_LABEL[el.type] || el.type;
      label.appendChild(tag);
      label.appendChild(document.createTextNode(el.name || ''));

      row.addEventListener('click', e => {
        const action = e.target.dataset.a;
        if (action) { e.stopPropagation(); layerAction(el.id, action); }
        else select(el.id);
      });
      box.appendChild(row);
    });
  }

  // ---------- Properties ----------
  function setInputValue(input, value, type) {
    if (type === 'checkbox') input.checked = !!value;
    else if (type === 'color') input.value = /^#[0-9a-f]{6}$/i.test(value || '') ? value : '#ffffff';
    else input.value = value ?? '';
  }

  function readInput(input, type) {
    if (type === 'checkbox') return input.checked;
    if (type === 'number') {
      const n = Number(input.value);
      return Number.isFinite(n) ? n : 0;
    }
    return input.value;
  }

  function onChanged(el, prop) {
    if (['x', 'y', 'w', 'h'].includes(prop)) {
      patch(el);
    } else {
      renderStage();
      if (prop === 'name') renderLayers();
    }
    positionSelection();
    dirty = true;
    updateStatus();
  }

  function addField(parent, label, prop, type, opts = {}) {
    const el = current();
    const wrap = document.createElement('label');
    wrap.className = 'field' + (type === 'checkbox' ? ' check' : '');

    const caption = document.createElement('span');
    caption.textContent = label;

    let input;
    if (type === 'select') {
      input = document.createElement('select');
      (opts.options || []).forEach(o => {
        const option = document.createElement('option');
        option.value = o;
        option.textContent = o;
        input.appendChild(option);
      });
    } else if (type === 'textarea') {
      input = document.createElement('textarea');
      input.rows = 3;
    } else {
      input = document.createElement('input');
      input.type = type;
      if (opts.step !== undefined) input.step = opts.step;
      if (opts.min !== undefined) input.min = opts.min;
      if (opts.max !== undefined) input.max = opts.max;
      if (opts.readonly) input.readOnly = true;
    }

    setInputValue(input, el[prop], type);
    input.addEventListener('input', () => {
      el[prop] = readInput(input, type);
      onChanged(el, prop);
    });
    input.addEventListener('change', commit);

    inputs[prop] = { input, type };
    if (type === 'checkbox') { wrap.appendChild(input); wrap.appendChild(caption); }
    else { wrap.appendChild(caption); wrap.appendChild(input); }
    parent.appendChild(wrap);
  }

  function group(parent, cssClass) {
    const div = document.createElement('div');
    div.className = cssClass;
    parent.appendChild(div);
    return div;
  }

  // A caption above any input (used by the animation section)
  function labeled(text, input) {
    const wrap = document.createElement('label');
    wrap.className = 'field';
    const caption = document.createElement('span');
    caption.textContent = text;
    wrap.appendChild(caption);
    wrap.appendChild(input);
    return wrap;
  }

  // Refresh the numbers while dragging (but never the box you are typing in)
  function syncInputs() {
    const el = current();
    if (!el) return;
    for (const [prop, { input, type }] of Object.entries(inputs)) {
      if (document.activeElement !== input) setInputValue(input, el[prop], type);
    }
  }

  // ---------- Animation section ----------
  // One group: Entrance ("in"), Exit ("out") or Loop. Stored in el.anim[name] = { preset, ms, delay, easing }
  function addAnimGroup(box, el, name, title, presets, withDelay, defaultMs, defaultEase) {
    const head = document.createElement('div');
    head.className = 'subhead';
    head.textContent = title;
    box.appendChild(head);

    const saved = (el.anim && el.anim[name]) || null;

    const preset = document.createElement('select');
    [['', 'None'], ...presets].forEach(([value, label]) => {
      const option = document.createElement('option');
      option.value = value;
      option.textContent = label;
      preset.appendChild(option);
    });
    preset.value = saved ? saved.preset : '';

    const ms = document.createElement('input');
    ms.type = 'number';
    ms.min = 50;
    ms.step = 50;
    ms.value = saved && saved.ms !== undefined ? saved.ms : defaultMs;

    let delay = null;
    if (withDelay) {
      delay = document.createElement('input');
      delay.type = 'number';
      delay.min = 0;
      delay.step = 50;
      delay.value = saved && saved.delay !== undefined ? saved.delay : 0;
    }

    let easing = null;
    if (defaultEase) {
      easing = document.createElement('select');
      EASINGS.forEach(([value, label]) => {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = label;
        easing.appendChild(option);
      });
      easing.value = saved && saved.easing ? saved.easing : defaultEase;
    }

    const update = () => {
      if (!preset.value) {
        if (el.anim) {
          delete el.anim[name];
          if (!Object.keys(el.anim).length) delete el.anim;
        }
      } else {
        el.anim = el.anim || {};
        const cfg = { preset: preset.value, ms: Number(ms.value) || defaultMs };
        if (delay) cfg.delay = Number(delay.value) || 0;
        if (easing) cfg.easing = easing.value;
        el.anim[name] = cfg;
      }
      dirty = true;
      updateStatus();
    };

    const grid = group(box, 'grid2');
    grid.appendChild(labeled('Type', preset));
    grid.appendChild(labeled('Duration (ms)', ms));
    if (delay) grid.appendChild(labeled('Delay (ms)', delay));
    if (easing) grid.appendChild(labeled('Easing', easing));

    preset.addEventListener('change', () => { update(); commit(); });
    [ms, delay, easing].filter(Boolean).forEach(input => {
      input.addEventListener('input', update);
      input.addEventListener('change', commit);
    });
  }

  // Copies the animation of this element to all the others, each entrance 80 ms after the previous one
  function staggerAll(source) {
    if (!source.anim) { setStatus('Give this element an animation first.'); return; }

    design.elements.forEach((el, i) => {
      if (el === source) return;
      el.anim = JSON.parse(JSON.stringify(source.anim));
      if (el.anim.in) el.anim.in.delay = i * 80;
    });
    commit();
    setStatus('Animation copied to all elements. Press "Play animation" to see it.');
  }

  function renderAnimation(box, el) {
    const title = document.createElement('h3');
    title.textContent = 'Animation';
    box.appendChild(title);

    const hint = document.createElement('p');
    hint.className = 'hint';
    hint.textContent = 'Preview it with "Play animation" in the toolbar.';
    box.appendChild(hint);

    addAnimGroup(box, el, 'in', 'Entrance', IN_OUT_PRESETS, true, 500, 'ease-out');
    addAnimGroup(box, el, 'out', 'Exit', IN_OUT_PRESETS, true, 400, 'ease-in');
    addAnimGroup(box, el, 'loop', 'Loop (starts after the entrance)', LOOP_PRESETS, false, 1500, null);

    const stagger = document.createElement('button');
    stagger.textContent = 'Copy to all, staggered';
    stagger.title = 'Gives every other element the same animation, each entrance 80 ms later';
    stagger.addEventListener('click', () => staggerAll(el));
    box.appendChild(stagger);
  }

  // Plays entrances and loops, holds a moment, then plays the exits
  async function playAnimation() {
    if (previewing) return;
    previewing = true;
    $('stage').classList.add('previewing');
    $('btn-play').disabled = true;
    positionSelection();           // hides the selection box
    renderStage();                 // the entrances and loops start now

    const intro = Math.max(0, ...design.elements.map(e =>
      e.anim && e.anim.in ? (Number(e.anim.in.ms) || 500) + (Number(e.anim.in.delay) || 0) : 0));
    await wait(intro + 1500);

    const outMs = R.playOut($('stage'));
    await wait(outMs + 500);

    previewing = false;
    $('stage').classList.remove('previewing');
    $('btn-play').disabled = false;
    renderStage();
    positionSelection();
  }

  // ---------- Properties panel ----------
  function renderProps() {
    const box = $('props');
    box.innerHTML = '';
    Object.keys(inputs).forEach(k => delete inputs[k]);

    const el = current();
    if (!el) {
      box.innerHTML = '<p class="hint">Select an element on the canvas or in the list.</p>';
      return;
    }

    addField(box, 'Name', 'name', 'text');

    const g4 = group(box, 'grid4');
    addField(g4, 'X', 'x', 'number');
    addField(g4, 'Y', 'y', 'number');
    addField(g4, 'W', 'w', 'number', { min: 1 });
    addField(g4, 'H', 'h', 'number', { min: 1 });

    const g2 = group(box, 'grid2');
    addField(g2, 'Rotation (deg)', 'rotation', 'number');
    addField(g2, 'Opacity (0 to 1)', 'opacity', 'number', { step: 0.05, min: 0, max: 1 });

    if (el.type === 'rect') {
      const a = group(box, 'grid2');
      addField(a, 'Fill', 'fill', 'color');
      addField(a, 'Fill opacity', 'fillAlpha', 'number', { step: 0.05, min: 0, max: 1 });
      const b = group(box, 'grid2');
      addField(b, 'Corner radius', 'radius', 'number', { min: 0 });
      addField(b, 'Border width', 'borderWidth', 'number', { min: 0 });
      addField(box, 'Border color', 'borderColor', 'color');
    }

    if (el.type === 'image') {
      addField(box, 'Image file', 'src', 'text', { readonly: true });
      const a = group(box, 'grid2');
      addField(a, 'Fit', 'fit', 'select', { options: ['contain', 'cover', 'fill'] });
      addField(a, 'Corner radius', 'radius', 'number', { min: 0 });
    }

    if (el.type === 'placeholderImage') {
      addField(box, 'Placeholder (pick one in the list)', 'bind', 'text');
      const a = group(box, 'grid2');
      addField(a, 'Fit', 'fit', 'select', { options: ['cover', 'contain', 'fill'] });
      addField(a, 'Corner radius', 'radius', 'number', { min: 0 });
      const b = group(box, 'grid2');
      addField(b, 'Border width', 'borderWidth', 'number', { min: 0 });
      addField(b, 'Border color', 'borderColor', 'color');
    }

    if (el.type === 'text') {
      addField(box, 'Text (use {placeholders})', 'text', 'textarea');
      const a = group(box, 'grid2');
      addField(a, 'Font', 'fontFamily', 'select', { options: FONTS });
      addField(a, 'Size (px)', 'fontSize', 'number', { min: 1 });
      const b = group(box, 'grid2');
      addField(b, 'Color', 'color', 'color');
      addField(b, 'Letter spacing', 'letterSpacing', 'number', { step: 0.5 });
      const c = group(box, 'grid2');
      addField(c, 'Align', 'align', 'select', { options: ['left', 'center', 'right'] });
      addField(c, 'Vertical', 'valign', 'select', { options: ['top', 'middle', 'bottom'] });
      const d = group(box, 'grid2');
      addField(d, 'Bold', 'bold', 'checkbox');
      addField(d, 'Italic', 'italic', 'checkbox');
      addField(d, 'UPPERCASE', 'uppercase', 'checkbox');
      addField(d, 'Shadow', 'shadow', 'checkbox');
    }

    renderAnimation(box, el);
  }

  // ---------- Adding elements ----------
  function spot(w, h) {
    const offset = (design.elements.length % 10) * 24;      // so new elements don't hide each other
    return { x: Math.round((design.canvas.w - w) / 2) + offset, y: Math.round((design.canvas.h - h) / 2) + offset };
  }

  function addElement(el) {
    design.elements.push(el);
    renderStage();
    select(el.id);
    commit();
  }

  function newRect() {
    const w = 400, h = 200;
    addElement({ id: uid(), type: 'rect', name: 'Rectangle', ...spot(w, h), w, h, rotation: 0, opacity: 1,
                 fill: '#1f2430', fillAlpha: 0.9, radius: 12, borderColor: '#4c8dff', borderWidth: 0 });
  }

  function newText(text) {
    const w = 520, h = 80;
    addElement({ id: uid(), type: 'text', name: 'Text', ...spot(w, h), w, h, rotation: 0, opacity: 1,
                 text: text || 'Text', fontFamily: 'Segoe UI', fontSize: 48, color: '#ffffff', bold: true,
                 italic: false, uppercase: false, shadow: false, align: 'center', valign: 'middle', letterSpacing: 0 });
  }

  function newPlaceholderImage(bind) {
    const w = 200, h = 200;
    addElement({ id: uid(), type: 'placeholderImage', name: bind, ...spot(w, h), w, h, rotation: 0, opacity: 1,
                 bind, fit: 'cover', radius: 0, borderColor: '#ffffff', borderWidth: 0 });
  }

  function newImage(url, naturalW, naturalH) {
    const k = Math.min(1, 900 / naturalW, 600 / naturalH);
    const w = Math.max(10, Math.round(naturalW * k)), h = Math.max(10, Math.round(naturalH * k));
    const name = decodeURIComponent(url.split('/').pop()).replace(/^[0-9a-f]{6}_/, '');
    addElement({ id: uid(), type: 'image', name, ...spot(w, h), w, h, rotation: 0, opacity: 1,
                 src: url, fit: 'contain', radius: 0 });
  }

  function addImageFromUrl(url) {
    const img = new Image();
    img.onload = () => newImage(url, img.naturalWidth || 200, img.naturalHeight || 200);
    img.onerror = () => setStatus('Could not load that image.');
    img.src = url;
  }

  function deleteSelected() {
    const el = current();
    if (el) layerAction(el.id, 'del');
  }

  function duplicateSelected() {
    const el = current();
    if (!el) return;
    const copy = JSON.parse(JSON.stringify(el));
    copy.id = uid();
    copy.name = (el.name || '') + ' copy';
    copy.x += 24;
    copy.y += 24;
    addElement(copy);
  }

  // ---------- Placeholders ----------
  function usePlaceholder(k) {
    const el = current();

    if (isImageKey(k)) {
      if (el && el.type === 'placeholderImage') {
        el.bind = k;
        el.name = k;
        renderStage();
        renderLayers();
        renderProps();
        commit();
      } else {
        newPlaceholderImage(k);
      }
      return;
    }

    if (el && el.type === 'text') {
      const old = el.text && el.text !== 'Text' ? el.text + ' ' : '';
      el.text = old + '{' + k + '}';
      renderStage();
      renderProps();
      commit();
    } else {
      newText('{' + k + '}');
    }
  }

  function renderPlaceholders() {
    const filter = $('ph-filter').value.trim().toLowerCase();
    const box = $('phs');
    box.innerHTML = '';

    const keys = Object.keys(sample).filter(k => !filter || k.toLowerCase().includes(filter)).sort();
    let lastGroup = '';
    keys.slice(0, 300).forEach(k => {
      const g = k.split('.')[0];
      if (g !== lastGroup) {
        const h = document.createElement('div');
        h.className = 'phg';
        h.textContent = g;
        box.appendChild(h);
        lastGroup = g;
      }
      const b = document.createElement('button');
      b.className = 'ph' + (isImageKey(k) ? ' img' : '');
      b.textContent = k;
      const value = String(sample[k]);
      b.title = value.startsWith('data:') ? '(sample picture)' : value;
      b.addEventListener('click', () => usePlaceholder(k));
      box.appendChild(b);
    });
  }

  // ---------- Images (uploaded files) ----------
  async function loadAssets() {
    try {
      const response = await fetch('/api/assets', { cache: 'no-store' });
      const list = response.ok ? await response.json() : [];
      const box = $('assets');
      box.innerHTML = '';
      list.forEach(a => {
        const img = document.createElement('img');
        img.src = a.url;
        img.title = a.name;
        img.addEventListener('click', () => addImageFromUrl(a.url));
        box.appendChild(img);
      });
    } catch { /* the list stays empty */ }
  }

  async function uploadFiles(files) {
    for (const file of files) {
      if (!/\.(png|jpe?g|webp|gif|svg)$/i.test(file.name)) {
        setStatus('Skipped ' + file.name + ' (not an image).');
        continue;
      }
      try {
        const response = await fetch('/api/assets?name=' + encodeURIComponent(file.name), { method: 'POST', body: file });
        if (!response.ok) throw new Error(await response.text());
        const info = await response.json();
        await loadAssets();
        addImageFromUrl(info.url);
      } catch (err) {
        setStatus('Upload failed: ' + err.message);
      }
    }
  }

  // ---------- Buttons, keyboard, drag and drop ----------
  $('btn-image').addEventListener('click', () => $('file').click());
  $('file').addEventListener('change', e => { uploadFiles([...e.target.files]); e.target.value = ''; });
  $('btn-rect').addEventListener('click', newRect);
  $('btn-text').addEventListener('click', () => newText());
  $('btn-play').addEventListener('click', playAnimation);
  $('ph-filter').addEventListener('input', renderPlaceholders);

  $('zoom').addEventListener('change', e => { zoom = e.target.value; applyZoom(); });
  $('chk-names').addEventListener('change', e => { showNames = e.target.checked; renderStage(); });
  $('chk-guides').addEventListener('change', e => $('wrap').classList.toggle('guides', e.target.checked));
  window.addEventListener('resize', applyZoom);

  $('viewport').addEventListener('dragover', e => e.preventDefault());
  $('viewport').addEventListener('drop', e => {
    e.preventDefault();
    uploadFiles([...e.dataTransfer.files]);
  });

  window.addEventListener('keydown', e => {
    if (previewing) return;

    const tag = (e.target.tagName || '').toLowerCase();
    const typing = tag === 'input' || tag === 'textarea' || tag === 'select';

    if (e.ctrlKey && e.key.toLowerCase() === 'z') {
      if (!typing) { e.preventDefault(); undo(); }
      return;
    }
    if (typing) return;

    if (e.ctrlKey && e.key.toLowerCase() === 'd') { e.preventDefault(); duplicateSelected(); return; }
    if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); deleteSelected(); return; }

    const el = current();
    if (!el || !e.key.startsWith('Arrow')) return;
    e.preventDefault();
    const step = e.shiftKey ? 10 : 1;
    if (e.key === 'ArrowLeft') el.x -= step;
    if (e.key === 'ArrowRight') el.x += step;
    if (e.key === 'ArrowUp') el.y -= step;
    if (e.key === 'ArrowDown') el.y += step;
    patch(el);
    positionSelection();
    syncInputs();
    commit();
  });

  // ---------- What the app (WPF) asks for ----------
  window.editor = {
    getDesign: () => design,
    isDirty: () => dirty,
    markClean: () => { dirty = false; updateStatus(); }
  };

  // ---------- Start ----------
  async function init() {
    try {
      const [designResponse, sampleResponse] = await Promise.all([
        fetch('/api/overlays/' + encodeURIComponent(key), { cache: 'no-store' }),
        fetch('/api/dummydata', { cache: 'no-store' })
      ]);
      if (designResponse.ok) design = await designResponse.json();
      sample = sampleResponse.ok ? await sampleResponse.json() : {};
    } catch (err) {
      setStatus('Could not reach the app: ' + err.message);
    }

    if (!design.canvas) design.canvas = { w: 1920, h: 1080 };
    design.canvas.w = 1920;
    design.canvas.h = 1080;
    if (!Array.isArray(design.elements)) design.elements = [];

    history = [snapshot()];
    dirty = false;

    renderPlaceholders();
    await loadAssets();
    renderAll();
    applyZoom();
  }

  init();
})();