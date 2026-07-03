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
];
function jingle(id) {
    const seq = JINGLES[id] ?? [];
    let at = 0;
    for (const [f, ms] of seq) { tone(f, ms, 0.18, at); at += ms / 1000 * 0.9; }
}

runtime.setModuleImports('main.js', {
    audio: { tone: (f, ms, v) => tone(f, ms, v), jingle },
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

// ---- Input: keyboard + gamepad. Buttons: 0/1 RUN, 2 ACTION, 3 START ---------
const KEYMAP = new Map([
    ['KeyZ', 0], ['ArrowLeft', 0],
    ['KeyX', 1], ['ArrowRight', 1],
    ['Space', 2], ['ArrowUp', 2],
    ['Enter', 3],
]);
addEventListener('keydown', e => {
    ensureAudio();
    const b = KEYMAP.get(e.code);
    if (b !== undefined && !e.repeat) { Engine.OnButton(b, 1); e.preventDefault(); }
});
addEventListener('keyup', e => {
    const b = KEYMAP.get(e.code);
    if (b !== undefined) { Engine.OnButton(b, 0); e.preventDefault(); }
});

const padPrev = [false, false, false, false];
function pollGamepad() {
    const gp = navigator.getGamepads?.()[0];
    if (!gp) return;
    // A/B mash = run, X/RB = action, Start = start
    const state = [
        gp.buttons[0]?.pressed ?? false,
        gp.buttons[1]?.pressed ?? false,
        (gp.buttons[2]?.pressed || gp.buttons[5]?.pressed) ?? false,
        gp.buttons[9]?.pressed ?? false,
    ];
    for (let i = 0; i < 4; i++) {
        if (state[i] !== padPrev[i]) { Engine.OnButton(i, state[i] ? 1 : 0); padPrev[i] = state[i]; }
    }
}

// ---- Main loop: V-sync via requestAnimationFrame -----------------------------
function frame(ts) {
    pollGamepad();
    Engine.Update(ts);
    blit();
    requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

await runtime.runMain();
