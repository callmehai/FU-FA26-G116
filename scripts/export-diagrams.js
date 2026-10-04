import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

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
  const args = process.argv.slice(2);
  const defaultRoot = getDefaultRoot();

  if (args.length === 0) {
    console.log(`ℹ️ Usage:
  node export-diagrams.js <path-or-file-or-pattern> [...]
  node export-diagrams.js --all

Examples:
  node export-diagrams.js UC17_ViewMeetingDetails.txt
  node export-diagrams.js docs/06-sequencediagram/meeting-management/UC17_ViewMeetingDetails.txt
  node export-diagrams.js meeting-management
  node export-diagrams.js --all
`);
    return;
  }

  const files = resolveTargets(args, defaultRoot);
  if (files.length === 0) {
    console.log(`ℹ️ No .txt diagram files found for arguments: ${args.join(', ')}`);
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
      const pngDataUrl = await page.evaluate(async (text) => {
        return new Promise((resolve) => {
          window.SEQ.api.generatePngDataUrl(text, (dataUrl) => {
            resolve(dataUrl);
          });
        });
      }, sourceText);

      if (!pngDataUrl || !pngDataUrl.startsWith('data:image/png;base64,')) {
        throw new Error('Invalid PNG data returned from engine.');
      }

      const base64Data = pngDataUrl.replace(/^data:image\/png;base64,/, '');
      const buffer = Buffer.from(base64Data, 'base64');
      fs.writeFileSync(pngFile, buffer);

      console.log(`[${i + 1}/${files.length}] ✅ ${relTxt} -> ${relPng} (${(buffer.length / 1024).toFixed(1)} KB)`);
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
