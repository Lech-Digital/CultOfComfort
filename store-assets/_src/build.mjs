// Generates the Play Store feature graphic and phone screenshots from HTML via headless Chrome.
//   node store-assets/_src/build.mjs
// Screens are real captures from the phone (shots/, see ../PLAY-LISTING.md); a screen whose capture is missing is
// skipped. Every headline must be true of the build being published.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = join(HERE, '..');
const REPO = join(ROOT, '..');
const HTML = join(HERE, 'html');
mkdirSync(HTML, { recursive: true });
mkdirSync(join(ROOT, 'phone'), { recursive: true });
const CHROME = process.env.CHROME ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

// The game's own fonts and parchment palette (client/…/Resources/UI/Hud.uss).
const FONTS = join(REPO, 'client/CultOfComfort.Unity/Assets/Resources/UI/Fonts');
const font = (name, file) => `@font-face{font-family:'${name}';src:url('${pathToFileURL(join(FONTS, file))}');}`;
const T = { parchment: '#f6e7c4', ink: '#2b2215', wood: '#3b2614', brown: '#6d4c2f', red: '#7a1f1a', glow: '#ffd98a', night: '#1c1a2b' };

const doc = (w, h, body) => `<!doctype html><html><head><meta charset="utf-8"><style>
${font('Marker', 'PermanentMarker.ttf')}${font('Fell', 'ImFellEnglish.ttf')}${font('FellSC', 'ImFellEnglishSC.ttf')}
*{box-sizing:border-box;margin:0;padding:0}
html,body{width:${w}px;height:${h}px;overflow:hidden}
body{position:relative;font-family:'Fell',Georgia,serif;color:${T.parchment};
  background:radial-gradient(120% 90% at 50% 18%, #5a3d1e 0%, ${T.wood} 45%, #1d140b 100%)}
.marker{font-family:'Marker',cursive}
.sc{font-family:'FellSC',Georgia,serif}
</style></head><body>${body}</body></html>`;

const png = (path) => `data:image/png;base64,${readFileSync(path).toString('base64')}`;

// ---------- feature graphic (1024×500): a close Night, the Hearth's glow and the fire (spec 2026-11-17 fun first, F5) ----------
const nightShot = join(HERE, 'shots', 'night.png');
const feature = doc(1024, 500, `
<img src="${png(nightShot)}" style="position:absolute;inset:0;width:1024px;height:500px;object-fit:cover;object-position:50% 40%;">
<div style="position:absolute;inset:0;background:linear-gradient(90deg, rgba(20,16,30,.96) 0%, rgba(20,16,30,.85) 38%, rgba(20,16,30,.15) 66%, transparent 100%);"></div>
<div style="position:absolute;left:56px;top:0;bottom:0;width:520px;display:flex;flex-direction:column;justify-content:center;">
  <div class="sc" style="font-size:26px;letter-spacing:.14em;color:${T.glow};">Cult of Comfort</div>
  <div class="marker" style="font-size:64px;line-height:1.05;margin:14px 0 22px;color:${T.parchment};text-shadow:0 3px 0 rgba(0,0,0,.35);">
    Build a spa.<br><span style="color:${T.glow};">Hold the night.</span>
  </div>
  <div style="font-size:26px;line-height:1.35;color:#efe2c0;max-width:480px;">
    A cozy medieval village builder, with tower defense when the raiders come.
  </div>
</div>`);

// ---------- phone screenshots (1080×1920): a headline on parchment over a real capture ----------
const PW = 1080;
const PH = 1920;
const phone = (top, accent, sub, capture) => doc(PW, PH, `
<div style="position:absolute;left:0;right:0;top:0;height:430px;padding:92px 70px 0;text-align:center;">
  <div class="marker" style="font-size:78px;line-height:1.08;color:${T.parchment};text-shadow:0 4px 0 rgba(0,0,0,.35);">
    ${top}<br><span style="color:${T.glow};">${accent}</span></div>
  <div style="font-size:36px;line-height:1.3;margin-top:22px;color:#efe2c0;">${sub}</div>
</div>
<div style="position:absolute;left:50%;top:470px;transform:translateX(-50%);width:760px;height:1706px;border-radius:56px;
  padding:18px;background:#120c06;box-shadow:0 0 0 6px ${T.brown}, 0 0 80px rgba(255,190,90,.35);">
  <img src="${capture}" style="width:100%;height:100%;object-fit:cover;object-position:top;border-radius:40px;display:block;">
</div>`);

// Order: the hook (village and Night), then the depth (towers, warning, world, Prophet), then the come-back
// features. Captures are 1080×2424 from the Pixel 10, portrait.
const SCREENS = [
  ['1-village', 'Build a spa', 'out of junk.', 'A medieval village that runs on comfort — and grows while you are away.', 'village'],
  ['2-night', 'When the raiders come,', 'hold the Hearth.', 'Live tower defense at night: throw Hot Stones, and drive every raider off.', 'night'],
  ['3-boons', 'Catch your breath,', 'pick a boon.', 'Between waves: a boon for the next one, or call it early for more Scrap.', 'boons'],
  ['4-towers', 'Place, turn and', 'upgrade towers.', 'Anywhere in the village, not just by the road.', 'towers'],
  ['5-warning', 'Every raid', 'is announced.', 'Tension rises, a warning goes up, and you choose when to light the lamps.', 'warning'],
  ['6-world', 'A world shared', 'with other cults.', 'Send preachers, win over the valley, keep the lord calm.', 'world'],
  ['7-people', 'Villagers with', 'a talent each.', 'Agnes the Axe, Sister Ember, Wet Willy — give them the job they love.', 'prophet'],
  ['8-festival', 'Seven themed days', 'every week.', 'The Comfort Festival: three rewards a day for coming back.', 'festival'],
];

const jobs = [['feature', feature, 1024, 500, 'feature-1024x500.png']];
for (const [name, top, accent, sub, capture] of SCREENS) {
  const file = join(HERE, 'shots', `${capture}.png`);
  if (!existsSync(file)) {
    console.log(`skip phone-${name}: no shots/${capture}.png yet`);
    continue;
  }

  jobs.push([name, phone(top, accent, sub, png(file)), PW, PH, `phone/phone-${name}.png`]);
}

for (const [name, html, w, h, out] of jobs) {
  const file = join(HTML, `${name}.html`);
  writeFileSync(file, html);
  const r = spawnSync(CHROME, [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--allow-file-access-from-files',
    `--screenshot=${join(ROOT, out)}`, `--window-size=${w},${h}`, '--virtual-time-budget=6000', `file://${file}`,
  ], { encoding: 'utf8' });
  if (r.status !== 0) {
    console.error(`FAILED ${out}`, r.stderr?.slice(0, 300));
    process.exitCode = 1;
  } else console.log(`wrote ${out} (${w}x${h})`);
}
