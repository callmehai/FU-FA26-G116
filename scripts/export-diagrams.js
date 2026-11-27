import { chromium } from 'playwright';
import { spawnSync } from 'child_process';
import fs from 'fs';
import path from 'path';

function getRepoRoot(defaultRoot) {
  return path.resolve(defaultRoot, '..', '..');
}

function parseOptions(args) {
  // Tách cờ --originals-dir= / --no-originals / --no-compress khỏi danh sách target.
  const options = { originalsDir: null, noOriginals: false, noCompress: false, scale: 1 };
  const targets = [];
  for (const arg of args) {
    if (arg.startsWith('--originals-dir=')) {
      options.originalsDir = arg.slice('--originals-dir='.length);
    } else if (arg === '--no-originals') {
      options.noOriginals = true;
    } else if (arg.startsWith('--scale=')) {
      options.scale = Number(arg.slice('--scale='.length));
      if (!(options.scale >= 1)) throw new Error(`--scale must be a number >= 1: ${arg}`);
    } else if (arg === '--no-compress') {
      options.noCompress = true;
    } else {
      targets.push(arg);
    }
  }
  return { options, targets };
}

function getDefaultRoot() {
  const candidate1 = path.resolve('docs/06-sequencediagram');
  if (fs.existsSync(candidate1)) return candidate1;
  const candidate2 = path.resolve('../docs/06-sequencediagram');
  if (fs.existsSync(candidate2)) return candidate2;
  return candidate1;
}

/**
 * Recursively find all .txt files matching criteria
 */
function findAllTxtFiles(targetPath) {
  if (!fs.existsSync(targetPath)) return [];
  const stats = fs.statSync(targetPath);
  if (stats.isFile()) {
    return targetPath.endsWith('.txt') ? [targetPath] : [];
  }

  const results = [];
  const entries = fs.readdirSync(targetPath, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(targetPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...findAllTxtFiles(fullPath));
    } else if (entry.isFile() && entry.name.endsWith('.txt')) {
      results.push(fullPath);
    }
  }
  return results;
}

function resolveTargets(args, defaultRoot) {
  const allDiagrams = findAllTxtFiles(defaultRoot);
  const matchedFiles = new Set();

  for (const arg of args) {
    if (arg === '--all') {
      allDiagrams.forEach((f) => matchedFiles.add(f));
      continue;
    }

    const directPath = path.resolve(arg);
    if (fs.existsSync(directPath)) {
      const stats = fs.statSync(directPath);
      if (stats.isFile() && directPath.endsWith('.txt')) {
        matchedFiles.add(directPath);
      } else if (stats.isDirectory()) {
        findAllTxtFiles(directPath).forEach((f) => matchedFiles.add(f));
      }
      continue;
    }

    // Try relative to defaultRoot
    const inDocs = path.join(defaultRoot, arg);
    if (fs.existsSync(inDocs)) {
      findAllTxtFiles(inDocs).forEach((f) => matchedFiles.add(f));
      continue;
    }

    // Fuzzy / substring match across defaultRoot
    const normalizedArg = arg.toLowerCase().replace(/\.txt$/, '');
    const found = allDiagrams.filter((file) => {
      const base = path.basename(file, '.txt').toLowerCase();
      return base === normalizedArg || base.includes(normalizedArg) || file.toLowerCase().includes(normalizedArg);
    });

    if (found.length > 0) {
      found.forEach((f) => matchedFiles.add(f));
    } else {
      console.warn(`⚠️ Warning: No matching file found for argument '${arg}'`);
    }
  }

  return Array.from(matchedFiles);
}

async function main() {
  const { options, targets } = parseOptions(process.argv.slice(2));
  const defaultRoot = getDefaultRoot();
  const repoRoot = getRepoRoot(defaultRoot);
  const originalsDir = options.noOriginals
    ? null
    : path.resolve(options.originalsDir || process.env.DIAGRAM_ORIGINALS_DIR || path.join(repoRoot, 'anh-goc'));

  if (targets.length === 0) {
    console.log(`ℹ️ Usage:
  node export-diagrams.js <path-or-file-or-pattern> [...]
  node export-diagrams.js --all

Examples:
  node export-diagrams.js UC17_ViewMeetingDetails.txt
  node export-diagrams.js docs/06-sequencediagram/meeting-management/UC17_ViewMeetingDetails.txt
  node export-diagrams.js meeting-management
  node export-diagrams.js --all
  node export-diagrams.js --all --scale=3     # 3x the engine's actual size (sharper in the report)
`);
    return;
  }

  const files = resolveTargets(targets, defaultRoot);
  if (files.length === 0) {
    console.log(`ℹ️ No .txt diagram files found for arguments: ${targets.join(', ')}`);
    return;
  }

  console.log(`🚀 Found ${files.length} diagram(s) to export.`);

  // Launch browser (msedge or chrome)
  let browser;
  try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
  } catch {
    try {
      browser = await chromium.launch({ channel: 'chrome', headless: true });
    } catch {
      browser = await chromium.launch({ headless: true });
    }
  }

  const page = await browser.newPage();
  console.log('🌐 Loading sequencediagram.org engine...');
  await page.goto('https://sequencediagram.org/', { waitUntil: 'networkidle' });

  // Verify SEQ.api exists
  const hasApi = await page.evaluate(() => typeof window.SEQ?.api?.generatePngDataUrl === 'function');
  if (!hasApi) {
    console.error('❌ sequencediagram.org API not available.');
    await browser.close();
    process.exit(1);
  }

  console.log('✅ Engine loaded successfully. Exporting diagrams...\n');

  let successCount = 0;
  let failCount = 0;

  for (let i = 0; i < files.length; i++) {
    const txtFile = files[i];
    const baseName = path.basename(txtFile, '.txt');
    const dirName = path.dirname(txtFile);
    const pngFile = path.join(dirName, `${baseName}.png`);
    const relTxt = path.relative(process.cwd(), txtFile);
    const relPng = path.relative(process.cwd(), pngFile);

    try {
      const sourceText = fs.readFileSync(txtFile, 'utf-8');
      // generatePngDataUrl only renders at actual size, so a larger image is the
      // engine's own SVG rasterised N times bigger; the canvas side is capped at
      // 16000 px (the browser canvas limit).
      const pngDataUrl = await page.evaluate(async ({ text, scale }) => {
        if (scale === 1) {
          return new Promise((resolve) => window.SEQ.api.generatePngDataUrl(text, resolve));
        }
        const svgUrl = await new Promise((resolve) => window.SEQ.api.generateSvgDataUrl(text, resolve));
        const img = new Image();
        await new Promise((resolve, reject) => { img.onload = resolve; img.onerror = reject; img.src = svgUrl; });
        const s = Math.min(scale, 16000 / img.width, 16000 / img.height);
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(img.width * s);
        canvas.height = Math.round(img.height * s);
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        return canvas.toDataURL('image/png');
      }, { text: sourceText, scale: options.scale });

      if (!pngDataUrl || !pngDataUrl.startsWith('data:image/png;base64,')) {
        throw new Error('Invalid PNG data returned from engine.');
      }

      const base64Data = pngDataUrl.replace(/^data:image\/png;base64,/, '');
      const buffer = Buffer.from(base64Data, 'base64');
      fs.writeFileSync(pngFile, buffer);

      // Bản gốc vào thư mục local (gitignore) để sau này dựng báo cáo chuẩn nộp;
      // bản trong repo nén palette cho nhẹ.
      if (originalsDir) {
        const mirror = path.join(originalsDir, path.relative(repoRoot, pngFile));
        fs.mkdirSync(path.dirname(mirror), { recursive: true });
        fs.copyFileSync(pngFile, mirror);
      }
      if (!options.noCompress) {
        const py = process.env.PYTHON || 'python';
        // A scaled export keeps its size: only the palette is reduced.
        const widthArgs = options.scale > 1 ? ['--max-width', '100000'] : [];
        const res = spawnSync(py, [path.join(repoRoot, 'scripts', 'compress_png.py'), ...widthArgs, pngFile], { encoding: 'utf-8' });
        if (res.status !== 0) {
          console.error(`[${i + 1}/${files.length}] ❌ Compress failed for ${relPng}: ${(res.stderr || res.error?.message || '').trim()}`);
          failCount++;
          continue;
        }
        if (res.stdout.trim()) console.log(`[${i + 1}/${files.length}] ${res.stdout.trim()}`);
      }

      console.log(`[${i + 1}/${files.length}] ✅ ${relTxt} -> ${relPng} (${(fs.statSync(pngFile).size / 1024).toFixed(1)} KB)`);
      successCount++;
    } catch (err) {
      console.error(`[${i + 1}/${files.length}] ❌ Failed to export ${relTxt}:`, err.message);
      failCount++;
    }
  }

  await browser.close();

  console.log(`\n🎉 Export finished: ${successCount} succeeded, ${failCount} failed.`);
}

main().catch((err) => {
  console.error('❌ Fatal error:', err);
  process.exit(1);
});
