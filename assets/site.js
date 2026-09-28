// A small browser re-creation of Carnac's overlay, following the rules in
// Carnac.Logic (KeyProvider.ToInputs, KeyPress.GetTextParts, Message merging).
(function () {
  'use strict';

  var KEYMAPS = window.CARNAC_KEYMAPS || {};
  var FADE_DELAY = 5000;     // PopupSettings.ItemFadeDelay default (seconds * 1000)
  var MERGE_WINDOW = 1000;   // Message.ShouldCreateNewMessage uses one second
  var CHORD_WINDOW = 1500;
  var MAX_MESSAGES = 6;
  var KONAMI = ['up', 'up', 'down', 'down', 'left', 'right', 'left', 'right', 'b', 'a'];

  // Labels that Carnac draws as keycaps (KeyShowView.xaml DataTriggers).
  var CAPS = ['Return', 'Back', 'Escape', 'Alt', 'Ctrl', 'Shift', 'Win', 'Home', 'End', 'Insert',
    'Delete', 'PageUp', 'PageDown', 'Tab'];
  for (var f = 1; f <= 12; f++) CAPS.push('F' + f);

  // Keys whose text changes with Shift (ReplaceKey.ShiftReplacements).
  var SHIFTED = {
    '0': ')', '1': '!', '2': '@', '3': '#', '4': '$', '5': '%', '6': '^', '7': '&', '8': '*', '9': '(',
    '[': '{', ']': '}', '-': '_', '=': '+', '/': '?', '.': '>', ',': '<', ';': ':', "'": '"', '`': '~'
  };
  SHIFTED[String.fromCharCode(92)] = '|';

  var LABELS = {
    space: ' ', enter: 'Return', backspace: 'Back', escape: 'Escape', tab: 'Tab', delete: 'Delete',
    insert: 'Insert', home: 'Home', end: 'End', pageup: 'PageUp', pagedown: 'PageDown',
    up: '↑', down: '↓', left: '←', right: '→',
    'num/': ' / ', 'num*': ' * ', 'num-': ' - ', 'num+': ' + '
  };

  var CODES = {
    Backquote: '`', Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']', Semicolon: ';', Quote: "'",
    Comma: ',', Period: '.', Slash: '/', Space: 'space', Enter: 'enter', NumpadEnter: 'enter', Tab: 'tab',
    Escape: 'escape', Backspace: 'backspace', Delete: 'delete', Insert: 'insert', Home: 'home', End: 'end',
    PageUp: 'pageup', PageDown: 'pagedown', ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right',
    NumpadDecimal: '.', NumpadDivide: 'num/', NumpadMultiply: 'num*', NumpadSubtract: 'num-', NumpadAdd: 'num+'
  };
  CODES.Backslash = CODES.IntlBackslash = String.fromCharCode(92);
  var MODIFIER_CODE = /^(Control|Shift|Alt|Meta|OS)(Left|Right)?$/;

  var overlay = document.getElementById('overlay');
  if (!overlay) return;

  var store = {
    get: function (k) { try { return window.localStorage.getItem('carnac.' + k); } catch (e) { return null; } },
    set: function (k, v) { try { window.localStorage.setItem('carnac.' + k, v); } catch (e) { /* ignore */ } }
  };

  var state = {
    keymap: KEYMAPS[store.get('keymap')] ? store.get('keymap') : 'vscode',
    messages: [],
    pending: null,
    silent: false,
    recent: []
  };

  // ---------- Key presses ----------

  function tokenFromEvent(e) {
    var c = e.code || '';
    if (MODIFIER_CODE.test(c)) return null;
    if (/^Key[A-Z]$/.test(c)) return c.slice(3).toLowerCase();
    if (/^Digit[0-9]$/.test(c)) return c.slice(5);
    if (/^Numpad[0-9]$/.test(c)) return c.slice(6);
    if (/^F([1-9]|1[0-2])$/.test(c)) return c.toLowerCase();
    if (CODES[c]) return CODES[c];
    var k = e.key || '';
    if (k.length === 1 && /[a-z0-9]/i.test(k)) return k.toLowerCase();
    return null;
  }

  function sanitise(token) {
    if (LABELS.hasOwnProperty(token)) return LABELS[token];
    if (/^f\d+$/.test(token)) return token.toUpperCase();
    if (/^[a-z]$/.test(token)) return token.toUpperCase();
    return token;
  }

  // Mirrors KeyProvider.ToInputs + KeyPress.Format.
  function inputsFor(token, ctrl, alt, shift, win) {
    var out = [];
    if (ctrl) out.push('Ctrl');
    if (alt) out.push('Alt');
    if (win) out.push('Win');
    var isLetter = /^[a-z]$/.test(token);
    if (ctrl || alt) {
      if (shift) out.push('Shift');
      out.push(sanitise(token));
    } else {
      var shiftable = SHIFTED.hasOwnProperty(token);
      if (!isLetter && !shiftable && shift) out.push('Shift');
      if (shift && shiftable) out.push(SHIFTED[token]);
      else if (isLetter && !shift) out.push(token);
      else out.push(sanitise(token));
    }
    var hasModifier = ctrl || alt || win;
    return out.map(function (x) { return x === ' ' && hasModifier ? 'Space' : x; });
  }

  function keyPress(token, ctrl, alt, shift, win) {
    var mods = [];
    if (ctrl) mods.push('ctrl');
    if (alt) mods.push('alt');
    if (shift) mods.push('shift');
    if (win) mods.push('win');
    var inputs = inputsFor(token, ctrl, alt, shift, win);
    return {
      token: token,
      id: mods.concat(token).join('+'),
      hasModifier: ctrl || alt || win,
      bare: !ctrl && !alt && !shift && !win,
      text: inputs.join('\u0001 + \u0001').split('\u0001')
    };
  }

  function parseCombo(combo) {
    var parts = combo.split('+');
    var token = parts.pop();
    return keyPress(token, parts.indexOf('ctrl') > -1, parts.indexOf('alt') > -1,
      parts.indexOf('shift') > -1, parts.indexOf('win') > -1);
  }

  // ---------- Keymap lookups ----------

  function shortcutsFor(id) {
    var km = KEYMAPS[id];
    return km ? km.shortcuts : [];
  }

  function exactName(keymap, steps) {
    var list = shortcutsFor(keymap);
    var key = steps.join(', ');
    for (var i = 0; i < list.length; i++) if (list[i][0].join(', ') === key) return list[i][1];
    return null;
  }

  function startsChord(keymap, step) {
    var list = shortcutsFor(keymap);
    for (var i = 0; i < list.length; i++) if (list[i][0].length > 1 && list[i][0][0] === step) return true;
    return false;
  }

  // ---------- Messages ----------

  // Mirrors Message.CreateTextSequence: repeats become " x N", and a key after a
  // modifier combo is separated with ", ".
  function textSequence(keys) {
    var groups = [];
    keys.forEach(function (k) {
      var last = groups[groups.length - 1];
      if (last && last.key.text.join('') === k.text.join('')) last.count++;
      else groups.push({ key: k, count: 1, prefix: !!last && last.key.hasModifier });
    });
    var parts = [];
    groups.forEach(function (g) {
      if (g.prefix) parts.push(', ');
      parts.push.apply(parts, g.key.text);
      if (g.count > 1) parts.push(' x ' + g.count + ' ');
    });
    return parts;
  }

  function render(msg) {
    var el = msg.el;
    el.textContent = '';
    textSequence(msg.keys).forEach(function (part) {
      if (CAPS.indexOf(part) > -1) {
        var cap = document.createElement('span');
        cap.className = 'ov-cap';
        cap.textContent = part;
        el.appendChild(cap);
      } else {
        el.appendChild(document.createTextNode(part));
      }
    });
    if (msg.name) {
      var name = document.createElement('span');
      name.className = 'ov-name';
      name.textContent = ' [' + msg.name + ']';
      el.appendChild(name);
    }
  }

  function scheduleFade(msg) {
    clearTimeout(msg.timer);
    msg.timer = setTimeout(function () { removeMessage(msg); }, FADE_DELAY);
  }

  function removeMessage(msg) {
    clearTimeout(msg.timer);
    msg.fading = true;
    msg.el.classList.add('out');
    setTimeout(function () {
      if (msg.el.parentNode) msg.el.parentNode.removeChild(msg.el);
    }, 260);
    var i = state.messages.indexOf(msg);
    if (i > -1) state.messages.splice(i, 1);
    if (state.pending && state.pending.msg === msg) state.pending = null;
  }

  function addMessage(msg) {
    msg.el = document.createElement('div');
    msg.el.className = 'ov-msg' + (msg.status ? ' ov-status' : '');
    if (msg.status) msg.el.textContent = msg.status;
    else render(msg);
    overlay.appendChild(msg.el);
    state.messages.push(msg);
    while (state.messages.length > MAX_MESSAGES) removeMessage(state.messages[0]);
    scheduleFade(msg);
    return msg;
  }

  function status(text) {
    addMessage({ status: text, keys: [] });
  }

  function handle(kp, keymap) {
    var now = Date.now();

    // Ctrl+Alt+P toggles silent mode; the toggle itself is never shown (PasswordModeService).
    if (kp.id === 'ctrl+alt+p') {
      state.silent = !state.silent;
      state.pending = null;
      status(state.silent ? 'Silent mode on — keys are hidden' : 'Silent mode off');
      return;
    }
    if (state.silent) return;

    state.recent.push(kp.bare ? kp.token : '');
    if (state.recent.length > KONAMI.length) state.recent.shift();

    var pending = state.pending;
    state.pending = null;
    if (pending && now - pending.time < CHORD_WINDOW && !pending.msg.fading) {
      var chordName = exactName(keymap, pending.steps.concat(kp.id));
      if (chordName) {
        pending.msg.keys.push(kp);
        pending.msg.name = chordName;
        pending.msg.last = now;
        render(pending.msg);
        scheduleFade(pending.msg);
        return;
      }
    }

    var name = exactName(keymap, [kp.id]);
    // A lone space would be invisible next to its name, so draw it the way
    // Carnac's "Show Space as ␣" option does.
    if (name && kp.token === 'space' && !kp.hasModifier) kp.text = ['␣'];
    var chord = !name && kp.hasModifier && startsChord(keymap, kp.id);
    var mergeable = !kp.hasModifier && !name;
    var last = state.messages[state.messages.length - 1];

    if (last && last.mergeable && mergeable && !last.fading && now - last.last <= MERGE_WINDOW) {
      last.keys.push(kp);
      last.last = now;
    } else {
      last = addMessage({ keys: [kp], name: name, mergeable: mergeable, last: now });
      if (chord) state.pending = { steps: [kp.id], msg: last, time: now };
    }

    if (state.recent.join(',') === KONAMI.join(',')) {
      last.name = 'Konami!!!';
      last.mergeable = false;
      state.recent = [];
      document.body.classList.add('konami');
      setTimeout(function () { document.body.classList.remove('konami'); }, 3600);
    }
    render(last);
    scheduleFade(last);
  }

  // ---------- Real keyboard ----------

  document.addEventListener('keydown', function (e) {
    if (e.isComposing || e.keyCode === 229) return;
    var t = e.target;
    if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
    var token = tokenFromEvent(e);
    if (!token) return; // Carnac ignores modifier-only key presses
    autoplay.stop();
    handle(keyPress(token, e.ctrlKey, e.altKey, e.shiftKey, e.metaKey), state.keymap);
  });

  // ---------- Autoplay demo (VS Code keymap) ----------

  var SCRIPT = [
    { text: 'this is CARNAC' }, { wait: 1100 },
    { combo: 'ctrl+shift+k' }, { wait: 1200 },
    { combo: 'alt+up' }, { wait: 1200 },
    { combo: 'ctrl+k' }, { wait: 380 }, { combo: 'ctrl+c' }, { wait: 1300 },
    { combo: 'win+e' }, { wait: 1200 },
    { combo: 'ctrl+/' }, { wait: 7000 }
  ];

  var autoplay = (function () {
    var timer = null, index = 0, stopped = false, visible = true, queue = [];

    function expand(step) {
      if (step.text) {
        return step.text.split('').map(function (ch) {
          var lower = ch.toLowerCase();
          var token = ch === ' ' ? 'space' : lower;
          return { kp: keyPress(token, false, false, ch !== lower, false), wait: 90 };
        });
      }
      if (step.combo) return [{ kp: parseCombo(step.combo), wait: 0 }];
      return [{ wait: step.wait }];
    }

    function tick() {
      timer = null;
      if (stopped || !visible) return;
      if (!queue.length) {
        queue = expand(SCRIPT[index]);
        index = (index + 1) % SCRIPT.length;
      }
      var item = queue.shift();
      if (item.kp) handle(item.kp, 'vscode');
      timer = setTimeout(tick, item.wait || 0);
    }

    function run() {
      if (!timer && !stopped && visible) timer = setTimeout(tick, 600);
    }

    return {
      start: run,
      setVisible: function (v) {
        visible = v;
        if (v) run();
        else { clearTimeout(timer); timer = null; }
      },
      stop: function () {
        if (stopped) return;
        stopped = true;
        clearTimeout(timer);
        timer = null;
        state.messages.slice().forEach(removeMessage);
        state.pending = null;
      }
    };
  })();

  var hero = document.querySelector('.hero');
  if (hero && 'IntersectionObserver' in window) {
    new IntersectionObserver(function (entries) {
      autoplay.setVisible(entries[0].isIntersecting);
    }, { threshold: 0.25 }).observe(hero);
  }
  autoplay.start();

  // ---------- Keymap picker ----------

  // Browser-safe examples from each keymap (no tab-closing or reload shortcuts).
  var HINTS = {
    vscode: [['Ctrl', 'Shift', 'K'], ['Alt', '↑'], ['Ctrl', '/']],
    vs: [['Ctrl', 'Alt', 'L'], ['Ctrl', 'Alt', 'B'], ['Ctrl', 'Alt', 'E']],
    kdenlive: [['J'], ['L'], ['Shift', 'R']]
  };
  var hint = document.getElementById('try-hint');

  function renderHint() {
    if (!hint) return;
    hint.textContent = 'Try ';
    var combos = HINTS[state.keymap] || [];
    combos.forEach(function (combo, i) {
      if (i > 0) hint.appendChild(document.createTextNode(i === combos.length - 1 ? ' or ' : ', '));
      combo.forEach(function (k, j) {
        if (j > 0) hint.appendChild(document.createTextNode(' + '));
        var kbd = document.createElement('kbd');
        kbd.className = 'inline-kbd';
        kbd.textContent = k;
        hint.appendChild(kbd);
      });
    });
    hint.appendChild(document.createTextNode(state.keymap === 'kdenlive' ? '.' : ', or just type.'));
  }

  document.querySelectorAll('[data-keymap]').forEach(function (btn) {
    btn.setAttribute('aria-pressed', String(btn.getAttribute('data-keymap') === state.keymap));
    btn.addEventListener('click', function () {
      autoplay.stop();
      state.keymap = btn.getAttribute('data-keymap');
      store.set('keymap', state.keymap);
      document.querySelectorAll('[data-keymap]').forEach(function (b) {
        b.setAttribute('aria-pressed', String(b === btn));
      });
      renderHint();
    });
  });
  renderHint();

  // ---------- Placement picker ----------

  var PLACE_NAMES = { 'top-left': 'top left', 'top-right': 'top right', 'bottom-left': 'bottom left', 'bottom-right': 'bottom right' };

  function place(where, announce) {
    if (!PLACE_NAMES[where]) return;
    overlay.setAttribute('data-place', where);
    store.set('place', where);
    document.querySelectorAll('[data-place]').forEach(function (b) {
      if (b !== overlay) b.setAttribute('aria-pressed', String(b.getAttribute('data-place') === where));
    });
    if (announce) {
      autoplay.stop();
      status('Overlay moved to the ' + PLACE_NAMES[where]);
    }
  }

  document.querySelectorAll('button[data-place]').forEach(function (btn) {
    btn.addEventListener('click', function () { place(btn.getAttribute('data-place'), true); });
  });
  place(store.get('place') || 'bottom-right', false);

  // ---------- Copy buttons ----------

  function copyText(text) {
    if (navigator.clipboard && window.isSecureContext) return navigator.clipboard.writeText(text);
    return new Promise(function (resolve, reject) {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      try { document.execCommand('copy') ? resolve() : reject(); } catch (err) { reject(err); }
      document.body.removeChild(ta);
    });
  }

  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var target = document.getElementById(btn.getAttribute('data-copy'));
      if (!target) return;
      copyText(target.textContent.trim()).then(function () {
        btn.classList.add('done');
        btn.setAttribute('aria-label', 'Copied');
        setTimeout(function () {
          btn.classList.remove('done');
          btn.setAttribute('aria-label', 'Copy to clipboard');
        }, 1600);
      }, function () { /* clipboard unavailable */ });
    });
  });

  // Touch-only visitors can't press keys, so point them at the autoplay demo instead.
  if (window.matchMedia && window.matchMedia('(hover: none) and (pointer: coarse)').matches) {
    document.documentElement.classList.add('touch');
  }
})();
