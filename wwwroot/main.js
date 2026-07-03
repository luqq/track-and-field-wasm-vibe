// Track & Field (1983) — .NET 10 WASM host.
// JS owns the rAF loop and input capture; all game logic and rendering live in C#.
// The framebuffer is read straight out of WASM linear memory: zero serialization.
import { dotnet } from './_framework/dotnet.js';

const runtime = await dotnet.create();

// ---- Web Audio (monophonic square wave, 1983-style) -------------------------
let actx = null;
function ensureAudio() {
    if (!actx) actx = new (window.AudioContext || window.webkitAudioContext)();
    if (actx.state === 'suspended') actx.resume();
}
function tone(freq, ms, vol, when = 0) {
    if (!actx) return;
    const t = actx.currentTime + when;
    const osc = actx.createOscillator();
    const gain = actx.createGain();
    osc.type = 'square';
    osc.frequency.value = freq;
    gain.gain.setValueAtTime(vol, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + ms / 1000);
    osc.connect(gain).connect(actx.destination);
    osc.start(t);
    osc.stop(t + ms / 1000 + 0.02);
}
const JINGLES = [
    [[523, 90], [659, 90], [784, 90], [1047, 180]],                  // 0 title/start
    [[784, 100], [784, 80], [880, 100], [1047, 250]],                // 1 qualify fanfare
    [[330, 150], [262, 150], [196, 300]],                            // 2 fail
    [[1047, 70], [1319, 70], [1568, 70], [1319, 70], [2093, 200]],   // 3 easter egg
    [[1800, 60]],                                                    // 4 starting gun
    [[880, 80], [1109, 80], [1319, 200]],                            // 5 extra life
    [[523, 130], [523, 70], [659, 130], [523, 70], [784, 200], [659, 100], [1047, 380]], // 6 pre-event fanfare
];
function jingle(id) {
    const seq = JINGLES[id] ?? [];
    let at = 0;
    for (const [f, ms] of seq) { tone(f, ms, 0.18, at); at += ms / 1000 * 0.9; }
}
// referee whistle: square carrier warbled by a fast LFO (the rolling pea)
function whistle(ms) {
    if (!actx) return;
    const t = actx.currentTime, dur = ms / 1000;
    const osc = actx.createOscillator();
    const gain = actx.createGain();
    osc.type = 'square';
    osc.frequency.value = 2150;
    const lfo = actx.createOscillator();
    const lfoGain = actx.createGain();
    lfo.type = 'sine';
    lfo.frequency.value = 55;
    lfoGain.gain.value = 170;
    lfo.connect(lfoGain).connect(osc.frequency);
    gain.gain.setValueAtTime(0.22, t);
    gain.gain.setValueAtTime(0.22, t + dur - 0.04);
    gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(gain).connect(actx.destination);
    osc.start(t); lfo.start(t);
    osc.stop(t + dur + 0.02); lfo.stop(t + dur + 0.02);
}

// local text-to-speech announcer (Web Speech API)
function say(text, lang) {
    if (!('speechSynthesis' in window)) return;
    const u = new SpeechSynthesisUtterance(text);
    u.lang = lang;
    u.rate = 1.05;
    speechSynthesis.cancel(); // monophonic, like everything else in 1983
    speechSynthesis.speak(u);
}

runtime.setModuleImports('main.js', {
    audio: { tone: (f, ms, v) => tone(f, ms, v), jingle, whistle },
    speech: { say },
    storage: {
        get: k => { try { return localStorage.getItem(k); } catch { return null; } },
        set: (k, v) => { try { localStorage.setItem(k, v); } catch { } },
    },
});

const config = runtime.getConfig();
const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
const Engine = exports.Engine;

// ---- Canvas: native 256x224, CSS-scaled with pixelated sampling -------------
const W = Engine.GetWidth(), H = Engine.GetHeight();
const canvas = document.getElementById('screen');
canvas.width = W;
canvas.height = H;
const ctx2d = canvas.getContext('2d', { alpha: false });
const fbPtr = Engine.GetFrameBufferAddress();

function blit() {
    // Re-wrap every frame: memory growth can detach the underlying ArrayBuffer.
    const view = new Uint8ClampedArray(runtime.localHeapViewU8().buffer, fbPtr, W * H * 4);
    ctx2d.putImageData(new ImageData(view, W, H), 0, 0);
}

// ---- Input: every raw code goes to C#; the binding table lives there ---------
// Keys that must never scroll/act on the page while playing:
const SWALLOW = new Set(['Space', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Tab', 'Enter']);
addEventListener('keydown', e => {
    ensureAudio();
    if (!e.repeat) Engine.OnKey(e.code, 1);
    if (SWALLOW.has(e.code)) e.preventDefault();
});
addEventListener('keyup', e => {
    Engine.OnKey(e.code, 0);
    if (SWALLOW.has(e.code)) e.preventDefault();
});

// Gamepad buttons surface as raw codes "PAD0".."PAD15", remappable like any key.
const padPrev = new Array(16).fill(false);
function pollGamepad() {
    const gp = navigator.getGamepads?.()[0];
    if (!gp) return;
    const n = Math.min(16, gp.buttons.length);
    for (let i = 0; i < n; i++) {
        const down = gp.buttons[i]?.pressed ?? false;
        if (down !== padPrev[i]) {
            if (down) ensureAudio();
            Engine.OnKey('PAD' + i, down ? 1 : 0);
            padPrev[i] = down;
        }
    }
}

// ---- Main loop: V-sync via requestAnimationFrame -----------------------------
function frame(ts) {
    try {
        pollGamepad();
        Engine.Update(ts);
        blit();
    } catch (e) {
        // keep the loop alive and surface the failure instead of dying silently
        window.__frameErr = String(e?.stack ?? e);
        console.error('frame error:', e);
    }
    requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

await runtime.runMain();
