// Checks the Play listing lengths in ../PLAY-LISTING.md: name ≤ 30, short ≤ 80, full ≤ 4000, notes ≤ 500.
//   node store-assets/_src/count.mjs
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const md = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'PLAY-LISTING.md'), 'utf8');
const block = (heading) => {
  const at = md.indexOf(heading);
  if (at < 0) throw new Error(`no "${heading}"`);
  const start = md.indexOf('```', at);
  const body = md.indexOf('\n', start) + 1;
  return md.slice(body, md.indexOf('```', body)).replace(/\n$/, '');
};

const checks = [
  ['App name', block('### App name'), 30],
  ['Short description', block('### Short description'), 80],
  ['Full description', block('### Full description'), 4000],
  ['Release notes', block('4. **Release notes**').replace(/<\/?en-US>\n?/g, '').trim(), 500],
];

for (const [name, text, max] of checks) {
  const n = [...text].length;
  console.log(`${n <= max ? 'ok  ' : 'OVER'} ${name}: ${n} / ${max}`);
  if (n > max) process.exitCode = 1;
}
