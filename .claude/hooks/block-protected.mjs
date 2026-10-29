// PreToolUse hook: refuse edits to generated output and to files that must never hold secrets.
// Exit code 2 blocks the tool call and shows stderr to Claude.
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";

let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  process.exit(0);
}

const ti = input.tool_input ?? {};
const file = String(ti.file_path ?? "").replaceAll("\\", "/");
if (!file) process.exit(0);

const base = file.split("/").pop();
const block = (why) => {
  console.error(`Blocked: ${why}`);
  process.exit(2);
};

if (/\/backend\/TrustCabinet\.Web\/wwwroot\//.test(file)) {
  block("wwwroot/ is build output (npm run build) and is gitignored. Edit frontend/src instead.");
}

// A migration that reached main is immutable: teammates have already applied it.
// The model snapshot changes with every new migration, so it stays editable.
const migration = file.match(/\/(backend\/TrustCabinet\.Infrastructure\/Migrations\/[^/]+\.cs)$/);
if (migration && base !== "TrustCabinetDbContextModelSnapshot.cs") {
  const onMain = spawnSync("git", ["cat-file", "-e", `origin/main:${migration[1]}`], {
    cwd: input.cwd ?? process.cwd(),
  });
  if (onMain.status === 0) {
    block(`${base} is already on origin/main, so teammates have applied it. Correct it with a new migration (dotnet dotnet-ef migrations add ...).`);
  }
}

if (base === ".env" || /^\.env\.(?!example$)/.test(base)) {
  block(".env files hold local secrets and are gitignored. Edit .env.example (placeholders only) or ask the user.");
}

// appsettings*.json is committed and the repo is public: no connection-string passwords or secrets.
// ApiKey / AccessKey(Id) are identifiers, not secrets (LiveKit pairs ApiKey with ApiSecret, R2/S3
// pairs AccessKeyId with SecretAccessKey), and appsettings.Development.json carries a dev ApiKey.
if (/^appsettings(\..+)?\.json$/.test(base)) {
  const text = String(ti.content ?? ti.new_string ?? "");
  const secretLike =
    /(Password|Pwd)\s*=\s*[^;"\s]+/i.test(text) ||
    /"(ApiSecret|SecretKey|ClientSecret|SecretAccessKey|Password)"\s*:\s*"(?!\s*")[^"]+"/i.test(text);
  if (secretLike) {
    block("appsettings*.json is committed to a public repo. Use `dotnet user-secrets` locally and environment variables in deployment.");
  }
}

process.exit(0);
