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

  // Scale the 1920x1080 canvas to the window (scale = 1 in a 1920x1080 OBS source)
  function fit() {
    const scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    stage.style.transform = `scale(${scale})`;
  }
  window.addEventListener('resize', fit);
  fit();

  const escapeHtml = s => String(s).replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // Fill placeholders:
  //   data-bind="blue.name"        -> text
  //   data-src="blue.logo"         -> image
  //   data-countdown="timer.end"   -> mm:ss counting down to that end time (see the timer below)
  function applyData(root, data) {
    root.querySelectorAll('[data-bind]').forEach(el => {
      el.textContent = data[el.dataset.bind] ?? '';
    });
    root.querySelectorAll('[data-src]').forEach(el => {
      const value = data[el.dataset.src];
      if (value) {
        el.onerror = () => { el.style.visibility = 'hidden'; };
        el.src = value;
        el.style.visibility = 'visible';
      } else {
        el.removeAttribute('src');
        el.style.visibility = 'hidden';
      }
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

  // ---- Overlay files ----
  async function loadTemplate(key) {
    const response = await fetch(`/handcoded/${encodeURIComponent(key)}.html`, { cache: 'no-store' });
    if (!response.ok) {
      const error = new Error(`No overlay file found for "${key}"`);
      error.missing = true;
      throw error;
    }
    return response.text();
  }

  // Temporary card shown while an overlay has no file yet (so you can test every button)
  function fallbackHtml(msg) {
    const data = msg.data || {};
    const prefixes = ['event.', 'mvp.', 'result.', 'score.', 'timer.'];
    const rows = Object.entries(data)
      .filter(([key, value]) => value && prefixes.some(p => key.startsWith(p)))
      .slice(0, 10)
      .map(([key, value]) => `<div>${escapeHtml(key)}: ${escapeHtml(value)}</div>`)
      .join('');
    const timer = data['timer.end'] ? '<div class="fb-timer" data-countdown="timer.end"></div>' : '';
    const layerClass = msg.layer === 'base' ? 'fb-base' : 'fb-event';

    return `<div class="fb ${layerClass}">
      <div class="fb-key">${escapeHtml(msg.key)}</div>
      ${timer}
      ${rows}
      <div class="fb-note">placeholder card: no file wwwroot/handcoded/${escapeHtml(msg.key)}.html yet</div>
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
    applyData(el, msg.data || {});
    (layers[msg.layer] || layers.event).appendChild(el);
    shown.set(msg.id, { el, msg });
  }

  function remove(id, instant) {
    const item = shown.get(id);
    if (!item) { cancelled.add(id); return; }
    shown.delete(id);
    if (instant) { item.el.remove(); return; }
    item.el.classList.add('leaving');
    setTimeout(() => item.el.remove(), 450);
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