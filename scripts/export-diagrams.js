import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

/**
 * Recursively find all .txt files matching criteria
 */
function findTxtFiles(targetPath) {
  const stats = fs.statSync(targetPath);
  if (stats.isFile()) {
    return targetPath.endsWith('.txt') ? [targetPath] : [];
  }

  const results = [];
  const entries = fs.readdirSync(targetPath, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(targetPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...findTxtFiles(fullPath));
    } else if (entry.isFile() && entry.name.endsWith('.txt')) {
      results.push(fullPath);
    }
  }
  return results;
}

async function main() {
  const argPath = process.argv[2] || path.resolve('../docs/06-sequencediagram');
  const targetFullPath = path.resolve(argPath);

  if (!fs.existsSync(targetFullPath)) {
    console.error(`❌ Path not found: ${targetFullPath}`);
    process.exit(1);
  }

  const files = findTxtFiles(targetFullPath);
  if (files.length === 0) {
    console.log(`ℹ️ No .txt diagram files found in: ${targetFullPath}`);
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
