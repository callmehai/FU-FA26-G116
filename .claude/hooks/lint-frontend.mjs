// PostToolUse hook: run oxlint on a frontend source file right after it is edited.
// CI runs `npm run lint`, so catching it here saves a failed pull request.
// Exit code 2 feeds the lint output back to Claude.
import { readFileSync, existsSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { relative, resolve } from "node:path";

let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  process.exit(0);
}

const file = String(input.tool_input?.file_path ?? "");
const norm = file.replaceAll("\\", "/");
if (!/\/frontend\/src\/.+\.(ts|tsx)$/.test(norm) || !existsSync(file)) process.exit(0);

const frontend = resolve(input.cwd ?? process.cwd(), "frontend");
if (!existsSync(resolve(frontend, "node_modules"))) process.exit(0); // not installed yet: stay silent

const rel = relative(frontend, file);
const run = spawnSync("npx", ["--no-install", "oxlint", rel], {
  cwd: frontend,
  encoding: "utf8",
  shell: true,
});

if (run.status !== 0) {
  console.error(`oxlint reported problems in ${rel}:\n${run.stdout}${run.stderr}`);
  process.exit(2);
}
process.exit(0);
