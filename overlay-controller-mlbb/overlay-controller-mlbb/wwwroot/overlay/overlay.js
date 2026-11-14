(() => {
  'use strict';

  const params = new URLSearchParams(location.search);
  const stage = document.getElementById('stage');
  const debugBox = document.getElementById('debug');
  const layers = {
    base: document.getElementById('layer-base'),
    event: document.getElementById('layer-event')
  };

  const shown = new Map();   // id -> { el, msg }   (everything currently on screen)
  const cancelled = new Set();
  let epoch = 0;             // bumped by "hide all" so overlays still loading are dropped

  // ?bg=1 gives a dark background and ?debug=1 shows the connection status (for testing in a browser)
  if (params.has('bg')) document.body.style.background = '#2b2f3a';
  if (params.has('debug')) debugBox.hidden = false;

  function setDebug(text, force) {
    debugBox.textContent = text;
    if (force) debugBox.hidden = false;
  }

  if (!window.OverlayRenderer) {
    setDebug('renderer.js is missing (see wwwroot/overlay)', true);
    return;
  }
  const { designToHtml, applyData, playOut } = window.OverlayRenderer;

  // Scale the 1920x1080 canvas to the window (scale = 1 in a 1920x1080 OBS source)
  function fit() {
    const scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    stage.style.transform = `scale(${scale})`;
  }
  window.addEventListener('resize', fit);
  fit();

  const escapeHtml = s => String(s).replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // ---- Where an overlay comes from ----
  //   1) a design made in the editor   (/api/overlays/{key})
  //   2) a hand-coded file             (wwwroot/handcoded/{key}.html, kill.N falls back to kill.html)
  //   3) a temporary placeholder card
  async function loadTemplate(key) {
    try {
      const designResponse = await fetch(`/api/overlays/${encodeURIComponent(key)}`, { cache: 'no-store' });
      if (designResponse.ok) return designToHtml(await designResponse.json());
    } catch {
      // no design: try the hand-coded file
    }

    const names = [key];
    if (/^kill\.\d+$/.test(key)) names.push('kill');

    for (const name of names) {
      const response = await fetch(`/handcoded/${encodeURIComponent(name)}.html`, { cache: 'no-store' });
      if (response.ok) return response.text();
    }

    const error = new Error(`No overlay file found for "${key}"`);
    error.missing = true;
    throw error;
  }

  // Temporary card shown while an overlay has no design and no file (so you can test every button)
  function fallbackHtml(msg) {
    const data = msg.data || {};
    const prefixes = ['event.', 'mvp.', 'result.', 'score.', 'timer.'];
    const rows = Object.entries(data)
      .filter(([key, value]) => value && !String(value).startsWith('data:') && prefixes.some(p => key.startsWith(p)))
      .slice(0, 10)
      .map(([key, value]) => `<div>${escapeHtml(key)}: ${escapeHtml(value)}</div>`)
      .join('');
    const timer = data['timer.end'] ? '<div class="fb-timer" data-countdown="timer.end"></div>' : '';
    const layerClass = msg.layer === 'base' ? 'fb-base' : 'fb-event';

    return `<div class="fb ${layerClass}">
      <div class="fb-key">${escapeHtml(msg.key)}</div>
      ${timer}
      ${rows}
      <div class="fb-note">placeholder card: design "${escapeHtml(msg.key)}" in the editor or add wwwroot/handcoded/${escapeHtml(msg.key)}.html</div>
    </div>`;
  }

  async function show(msg) {
    const myEpoch = epoch;
    let html;
    try {
      html = await loadTemplate(msg.key);
    } catch (err) {
      if (err.missing) {
        html = fallbackHtml(msg);
      } else {
        console.error(err);
        setDebug(err.message, true);
        return;
      }
    }
    if (myEpoch !== epoch) return;            // "hide all" arrived while loading
    if (cancelled.delete(msg.id)) return;     // hidden while loading

    // The base layer holds one overlay at a time: replace the old one
    if (msg.layer === 'base') {
      for (const [id, item] of [...shown]) {
        if (item.msg.layer === 'base') remove(id);
      }
    }

    const el = document.createElement('div');
    el.className = 'ov';
    el.innerHTML = html;

    // A design with entrance animations plays its own, so skip the default fade-in of the whole overlay
    if (el.querySelector('.da[style*="dzin-"]')) el.classList.add('ov-plain');

    applyData(el, msg.data || {});
    (layers[msg.layer] || layers.event).appendChild(el);
    shown.set(msg.id, { el, msg });
  }

  function remove(id, instant) {
    const item = shown.get(id);
    if (!item) { cancelled.add(id); return; }
    shown.delete(id);
    if (instant) { item.el.remove(); return; }

    // Exit animations of a design; without them, the default fade of the whole overlay
    const outMs = playOut(item.el);
    if (outMs > 0) {
      setTimeout(() => item.el.remove(), outMs + 80);
    } else {
      item.el.classList.add('leaving');
      setTimeout(() => item.el.remove(), 450);
    }
  }

  function hideAll(instant) {
    epoch++;
    for (const id of [...shown.keys()]) remove(id, instant);
  }

  // ---- Connection to the app ----
  if (typeof signalR === 'undefined') {
    setDebug('signalr.min.js is missing (see wwwroot/lib)', true);
    return;
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hub')
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 2000 })
    .build();

  connection.on('Show', msg => show(msg));
  connection.on('Hide', id => remove(id));
  connection.on('HideAll', () => hideAll(false));

  // The app sends new data (a hero was picked, a name changed...): refresh what is on screen
  connection.on('Update', data => {
    for (const item of shown.values()) {
      Object.assign(item.msg.data, data);
      applyData(item.el, item.msg.data);
    }
  });

  // After (re)connecting, ask the app what should be on screen
  async function syncSnapshot() {
    try {
      const snapshot = await connection.invoke('RequestSnapshot');
      hideAll(true);
      if (snapshot && snapshot.base) await show(snapshot.base);
      setDebug('connected');
    } catch (err) {
      setDebug('snapshot failed: ' + err);
    }
  }

  async function start() {
    try {
      await connection.start();
      await syncSnapshot();
    } catch (err) {
      setDebug('waiting for the app...');
      setTimeout(start, 2000);
    }
  }

  connection.onreconnecting(() => setDebug('reconnecting...'));
  connection.onreconnected(syncSnapshot);
  connection.onclose(() => { setDebug('disconnected'); setTimeout(start, 2000); });

  setDebug('connecting...');
  start();
})();