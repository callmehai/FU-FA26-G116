# Vendored skill

This folder is [sloplint](https://github.com/callmehai/sloplint) `v0.3.0`, copied unchanged from
`skills/sloplint` at that tag so every agent (Claude Code, Codex) reads the same instructions and
references offline. Do not edit it here: fix upstream, then copy the new tag over this folder and
bump the version in AGENTS.md and in the commands that pin `#v0.3.0`.

TrustCabinet's own part lives outside this folder: `.slop.json` (sanctioned exceptions) and
`.slop.login.mjs` (signs scans in, opens tabs and panels) at the repository root, and the
"Frontend work" paragraph of AGENTS.md.
