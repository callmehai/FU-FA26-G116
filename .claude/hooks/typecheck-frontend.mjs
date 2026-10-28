// Stop hook: type-check the frontend once Claude finishes a turn that changed frontend/src.
// oxlint runs on every edit but does not type-check; CI runs `tsc -b` as part of `npm run build`.
// Exit code 2 feeds the errors back to Claude so it keeps working instead of stopping.
import { readFileSync, existsSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  process.exit(0);
}

// Already continuing because of this hook: do not loop forever.
if (input.stop_hook_active) process.exit(0);

const root = input.cwd ?? process.cwd();
const frontend = resolve(root, "frontend");
if (!existsSync(resolve(frontend, "node_modules"))) process.exit(0); // not installed yet: stay silent

// Uncommitted or untracked changes under frontend/src only.
const status = spawnSync("git", ["status", "--porcelain", "--", "frontend/src"], { cwd: root, encoding: "utf8" });
if (status.status !== 0 || !status.stdout.trim()) process.exit(0);

const run = spawnSync("npx", ["--no-install", "tsc", "-b"], {
  cwd: frontend,
  encoding: "utf8",
  shell: true,
});

if (run.status !== 0) {
  console.error(`tsc -b found type errors in frontend (CI's npm run build would fail):\n${run.stdout}${run.stderr}`);
  process.exit(2);
}
process.exit(0);
