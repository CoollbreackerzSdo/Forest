/* ==========================================================================
   MAREA · ocean engine
   --------------------------------------------------------------------------
   Paints the ocean scene in a fixed canvas and drives every movement from
   the *scroll* position of the document (no pointer tracking).

   Every scene lives in its own folder and is a stack of 16:9 layers:
     · the last file is the flattened, full resolution photograph (2304x1296)
     · the previous ones are the same framing with alpha (haze, light, foam)

   The scene is chosen at random every time the site is loaded and it stays
   stable while you browse. The palette of the whole UI is sampled from the
   picture that is currently on screen.
   ========================================================================== */

const SCENES = [
    { id: 'Ocean_1', base: 'Ocean_1/4.png', layers: ['Ocean_1/2.png', 'Ocean_1/3.png'] },
    { id: 'Ocean_2', base: 'Ocean_2/5.png', layers: ['Ocean_2/3.png', 'Ocean_2/4.png'] },
    { id: 'Ocean_5', base: 'Ocean_5/5.png', layers: ['Ocean_5/3.png', 'Ocean_5/4.png'] },
    { id: 'Ocean_7', base: 'Ocean_7/6.png', layers: ['Ocean_7/2.png', 'Ocean_7/3.png', 'Ocean_7/4.png', 'Ocean_7/5.png'] },
    { id: 'Ocean_8', base: 'Ocean_8/6.png', layers: ['Ocean_8/2.png', 'Ocean_8/3.png', 'Ocean_8/4.png'] },
];

const ZOOM = 1.16;          // extra room so the layers can travel without gaps
const BASE_TRAVEL = 0.016;  // how much the deepest layer drifts (viewport heights)
const STEP_TRAVEL = 0.012;  // extra drift for every layer closer to the camera

const state = {
    canvas: null,
    ctx: null,
    w: 0,
    h: 0,
    dpr: 1,
    rect: { x: 0, y: 0, w: 0, h: 0 },
    scene: null,
    base: null,
    overlays: [],
    particles: [],
    target: 0,
    progress: 0,
    reduced: false,
    raf: 0,
    bound: false,
    observer: null,
    mutation: null,
    colors: { top: '#06131f', bottom: '#02080e' },
};

/* ---------------------------------------------------------------- helpers */

function loadImage(src) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.decoding = 'async';
        img.onload = () => (img.decode ? img.decode().then(() => resolve(img), () => resolve(img)) : resolve(img));
        img.onerror = () => reject(new Error(`No se pudo cargar ${src}`));
        img.src = src;
    });
}

function rgbToHsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const max = Math.max(r, g, b);
    const min = Math.min(r, g, b);
    const l = (max + min) / 2;
    let h = 0;
    let s = 0;
    if (max !== min) {
        const d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if (max === r) h = ((g - b) / d + (g < b ? 6 : 0));
        else if (max === g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        h *= 60;
    }
    return [h, s, l];
}

function hsl(h, s, l) {
    const hue = Number.isFinite(h) ? h.toFixed(1) : '200';
    const sat = (Number.isFinite(s) ? s * 100 : 60).toFixed(1);
    const lig = (Number.isFinite(l) ? l * 100 : 50).toFixed(1);
    return `hsl(${hue} ${sat}% ${lig}%)`;
}

/* --------------------------------------------------------------- palette */

/** Samples the scene and repaints every CSS variable of the design system. */
function applyPalette(image) {
    const probe = document.createElement('canvas');
    probe.width = 56;
    probe.height = 32;
    const pctx = probe.getContext('2d', { willReadFrequently: true });
    if (!pctx) return;

    pctx.drawImage(image, 0, 0, probe.width, probe.height);

    let data;
    try {
        data = pctx.getImageData(0, 0, probe.width, probe.height).data;
    } catch {
        return; // tainted canvas — keep the default palette
    }

    let r = 0, g = 0, b = 0, n = 0;
    let bestScore = -1, vivid = [200, 0.5, 0.5];

    for (let i = 0; i < data.length; i += 4) {
        if (data[i + 3] < 200) continue;
        r += data[i]; g += data[i + 1]; b += data[i + 2]; n++;

        const [h, s, l] = rgbToHsl(data[i], data[i + 1], data[i + 2]);
        // vivid but not blown out highlights
        const score = s * (1 - Math.abs(l - 0.55) * 1.1);
        if (score > bestScore) {
            bestScore = score;
            vivid = [h, s, l];
        }
    }

    if (n === 0) return;

    r /= n; g /= n; b /= n;
    const [sceneH, sceneS] = rgbToHsl(r, g, b);

    const accentH = vivid[1] > 0.12 ? vivid[0] : sceneH;
    const accentS = Math.max(0.55, Math.min(0.92, vivid[1] * 1.35 + 0.25));
    const accentL = Math.max(0.52, Math.min(0.68, vivid[2] * 0.55 + 0.4));
    const secondH = (accentH + 42) % 360;

    const root = document.documentElement.style;
    root.setProperty('--accent', hsl(accentH, accentS, accentL));
    root.setProperty('--accent-2', hsl(secondH, Math.max(0.5, accentS - 0.08), accentL - 0.08));
    root.setProperty('--accent-ink', hsl(accentH, 0.7, 0.08));
    root.setProperty('--abyss', hsl(sceneH, 0.52, 0.05));
    root.setProperty('--scene-glow', hsl(sceneH, Math.min(0.75, sceneS + 0.2), 0.22));
    root.setProperty('--line', `hsl(${sceneH.toFixed(1)} 40% 92% / 0.12)`);
    root.setProperty('--hull', `hsl(${sceneH.toFixed(1)} 40% 92% / 0.055)`);
    root.setProperty('--shadow-soft', '0 30px 70px -35px rgb(0 0 0 / 0.9)');

    state.colors = {
        top: hsl(sceneH, 0.45, 0.09),
        bottom: hsl(sceneH, 0.55, 0.04),
    };
}

/* ---------------------------------------------------------------- layers */

/** Pre-renders a layer into its own bitmap so the blur is paid only once. */
function prerenderLayer(image, rect) {
    const scale = 0.5;
    const c = document.createElement('canvas');
    c.width = Math.max(1, Math.round(rect.w * scale));
    c.height = Math.max(1, Math.round(rect.h * scale));
    const cx = c.getContext('2d');
    if (!cx) return null;
    cx.filter = 'blur(2.5px)';
    cx.drawImage(image, 0, 0, c.width, c.height);
    cx.filter = 'none';
    return c;
}

/** True when the layer is completely transparent (useless overlay). */
function hasContent(image) {
    const probe = document.createElement('canvas');
    probe.width = 32;
    probe.height = 18;
    const cx = probe.getContext('2d', { willReadFrequently: true });
    if (!cx) return true;
    cx.drawImage(image, 0, 0, probe.width, probe.height);
    try {
        const d = cx.getImageData(0, 0, probe.width, probe.height).data;
        for (let i = 3; i < d.length; i += 4) {
            if (d[i] > 8) return true;
        }
    } catch { /* ignore */ }
    return false;
}

function layout() {
    if (!state.canvas) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    state.dpr = dpr;
    state.w = window.innerWidth;
    state.h = window.innerHeight;
    state.canvas.width = Math.round(state.w * dpr);
    state.canvas.height = Math.round(state.h * dpr);
    state.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    computeRect();
    rebuildOverlays();
}

function computeRect() {
    const img = state.base;
    if (!img) return;
    const iw = img.naturalWidth || img.width;
    const ih = img.naturalHeight || img.height;
    const s = Math.max(state.w / iw, state.h / ih) * ZOOM;
    state.rect = {
        x: (state.w - iw * s) / 2,
        y: (state.h - ih * s) / 2,
        w: iw * s,
        h: ih * s,
    };
}

function rebuildOverlays() {
    state.overlays = state.sources.map((src, index) => ({
        canvas: prerenderLayer(src, state.rect),
        travel: BASE_TRAVEL + (index + 1) * STEP_TRAVEL,
        alpha: 0.62 - index * 0.07,
    })).filter(o => o.canvas);
}

/* -------------------------------------------------------------- particles */

function seedParticles() {
    const list = [];
    for (let i = 0; i < 26; i++) {
        list.push({
            x: Math.random(),
            y: Math.random(),
            r: 1 + Math.random() * 3.2,
            speed: 0.000012 + Math.random() * 0.000028,
            sway: 8 + Math.random() * 26,
            phase: Math.random() * Math.PI * 2,
            depth: 0.25 + Math.random() * 0.75,
        });
    }
    state.particles = list;
}

function drawParticles(now) {
    const { ctx, w, h } = state;
    const p = state.progress;
    ctx.save();
    for (const b of state.particles) {
        const y = (((b.y - now * b.speed) % 1) + 1) % 1;
        const py = y * (h + 60) - 30 - p * 90 * b.depth;
        const px = b.x * w + Math.sin(now * 0.00022 + b.phase) * b.sway * b.depth;
        const r = b.r * (0.6 + b.depth * 0.9);
        ctx.beginPath();
        ctx.arc(px, py, r, 0, Math.PI * 2);
        ctx.strokeStyle = `rgba(255,255,255,${0.05 + b.depth * 0.13})`;
        ctx.lineWidth = 1;
        ctx.stroke();
        ctx.fillStyle = `rgba(255,255,255,${0.02 + b.depth * 0.05})`;
        ctx.fill();
    }
    ctx.restore();
}

/* ------------------------------------------------------------------ loop */

function draw(now) {
    const { ctx, w, h, rect } = state;
    if (!ctx) return;

    const p = state.reduced ? 0 : state.progress;
    const motion = state.reduced ? 0 : 1;

    ctx.clearRect(0, 0, w, h);

    const g = ctx.createLinearGradient(0, 0, w * 0.4, h);
    g.addColorStop(0, state.colors.top);
    g.addColorStop(1, state.colors.bottom);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    const k = 1 + 0.05 * p;
    const drift = -BASE_TRAVEL * p * h * motion;

    ctx.save();
    ctx.translate(w / 2, h / 2);
    ctx.scale(k, k);
    ctx.translate(-w / 2, -h / 2);

    if (state.base) {
        ctx.drawImage(state.base, rect.x, rect.y + drift, rect.w, rect.h);
    }

    for (let i = 0; i < state.overlays.length; i++) {
        const ov = state.overlays[i];
        const sway = motion ? Math.sin(now * 0.00032 + i * 1.9) * 5 : 0;
        const dy = drift - ov.travel * p * h * motion + sway;
        ctx.globalAlpha = ov.alpha;
        ctx.drawImage(ov.canvas, rect.x, rect.y + dy, rect.w, rect.h);
    }
    ctx.globalAlpha = 1;
    ctx.restore();

    // the deeper you scroll, the deeper the water gets
    const veil = ctx.createLinearGradient(0, h * 0.45, 0, h);
    veil.addColorStop(0, 'rgba(2,8,14,0)');
    veil.addColorStop(1, `rgba(2,8,14,${(0.5 + p * 0.3).toFixed(3)})`);
    ctx.fillStyle = veil;
    ctx.fillRect(0, 0, w, h);

    drawParticles(now);

    state.raf = requestAnimationFrame(draw);
}

/* ---------------------------------------------------------------- scroll */

function readScroll() {
    const doc = document.documentElement;
    const max = Math.max(1, doc.scrollHeight - window.innerHeight);
    const raw = Math.min(1, Math.max(0, window.scrollY / max));
    state.target = raw;
    doc.style.setProperty('--scroll-progress', `${(raw * 100).toFixed(2)}%`);
}

/* ---------------------------------------------------------------- reveal */

function watchReveals() {
    if (!('IntersectionObserver' in window)) {
        document.querySelectorAll('.reveal').forEach(el => el.classList.add('is-visible'));
        return;
    }

    if (!state.observer) {
        state.observer = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    state.observer.unobserve(entry.target);
                }
            }
        }, { threshold: 0.12, rootMargin: '0px 0px -6% 0px' });
    }

    document.querySelectorAll('.reveal:not(.is-visible)').forEach(el => state.observer.observe(el));

    // Blazor swaps the DOM on every navigation — pick up new elements.
    if (!state.mutation) {
        state.mutation = new MutationObserver(() => {
            document.querySelectorAll('.reveal:not(.is-visible)').forEach(el => state.observer.observe(el));
        });
        state.mutation.observe(document.body, { childList: true, subtree: true });
    }
}

/* ----------------------------------------------------------------- scene */

async function loadScene(scene) {
    const base = await loadImage(scene.base);
    const sources = [];
    for (const src of scene.layers) {
        try {
            const img = await loadImage(src);
            if (hasContent(img)) sources.push(img);
        } catch {
            /* skip missing layers */
        }
    }

    state.scene = scene;
    state.base = base;
    state.sources = sources;

    // the picture is published first: the UI can already show it even if the
    // palette extraction fails for some reason. The url is absolute because
    // relative urls inside custom properties resolve against the stylesheet.
    const picture = new URL(scene.base, document.baseURI).href;
    document.documentElement.style.setProperty('--scene-image', `url("${picture}")`);

    try {
        applyPalette(base);
    } catch (err) {
        console.warn('[marea] no se pudo extraer la paleta de la escena', err);
    }

    computeRect();
    rebuildOverlays();
}

/** Every visit gets a different scene — the sea never repeats itself. */
function pickScene() {
    return SCENES[Math.floor(Math.random() * SCENES.length)];
}

/* --------------------------------------------------------------- public api */

export async function initOcean(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    state.canvas = canvas;
    state.ctx = canvas.getContext('2d');
    state.reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    state.sources = [];

    seedParticles();
    layout();

    try {
        await loadScene(pickScene());
    } catch (err) {
        // keep the gradient background if the scene cannot be decoded
        console.warn('[marea] no se pudo cargar la escena', err);
    }

    layout();
    watchReveals();
    readScroll();

    if (!state.bound) {
        state.bound = true;
        window.addEventListener('scroll', readScroll, { passive: true });
        window.addEventListener('resize', () => {
            layout();
            readScroll();
        }, { passive: true });
    }

    if (!state.raf) state.raf = requestAnimationFrame(draw);
}

export function getSceneId() {
    return state.scene ? state.scene.id : SCENES[0].id;
}

export function getSceneImage() {
    return state.scene ? state.scene.base : SCENES[0].base;
}
