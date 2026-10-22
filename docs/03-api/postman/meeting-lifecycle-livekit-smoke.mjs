// Manual, media-free LiveKit clients. Tokens travel through stdin only, never stdout.
import { chromium } from '../../../scripts/node_modules/playwright/index.mjs';
import { createInterface } from 'node:readline';
import { resolve } from 'node:path';

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const pages = [];
let tickets;
const lines = createInterface({ input: process.stdin });
try {
  for await (const line of lines) {
    const command = JSON.parse(line);
    try {
      if (command.action === 'connect') {
        tickets = command.tickets;
        for (const ticket of tickets) {
          const page = await browser.newPage();
          await page.addScriptTag({ path: resolve('frontend/node_modules/livekit-client/dist/livekit-client.umd.js') });
          await page.evaluate(async t => {
            window.room = new LivekitClient.Room();
            window.disconnected = false;
            window.room.on(LivekitClient.RoomEvent.Disconnected, () => { window.disconnected = true; });
            await window.room.connect(t.serverUrl, t.token);
          }, ticket);
          pages.push(page);
        }
        console.log(JSON.stringify({ connected: pages.length }));
      } else if (command.action === 'disconnected') {
        await Promise.all(pages.map(page => page.waitForFunction(() => window.disconnected, { timeout: 10000 })));
        console.log(JSON.stringify({ disconnected: pages.length }));
      } else if (command.action === 'reconnect') {
        const accepted = await pages[0].evaluate(async t => {
          window.disconnected = false;
          try { await window.room.connect(t.serverUrl, t.token); return true; }
          catch { return false; }
        }, tickets[0]);
        console.log(JSON.stringify({ accepted }));
      } else if (command.action === 'stop') break;
    } catch (error) {
      console.log(JSON.stringify({ failed: error.name }));
      process.exitCode = 1;
      break;
    }
  }
} finally { lines.close(); process.stdin.destroy(); await browser.close(); }
