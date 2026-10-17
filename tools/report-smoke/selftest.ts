// Proves report-smoke can fail: takes one clean report, makes broken copies, expects exit 1 for each
// and exit 0 for the clean one. Usage: bun selftest.ts <clean-report.html>
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const src = process.argv[2];
if (!src) { console.error("usage: bun selftest.ts <clean-report.html>"); process.exit(2); }
const html = readFileSync(src, "utf8");
const dir = mkdtempSync(join(tmpdir(), "report-smoke-selftest-"));

const mutate = {
  "clean.html": html,
  "js-error.html": html.replace("</body>", "<script>throw new Error('x')</script></body>"),
  // drop the whole Proxy Info card and its nav link target
  "missing-core-section.html": html.replace(/<div class="section-card[^"]*" id="proxies">[\s\S]*?(?=<div class="section-card[^"]*" id="repos")/, ""),
  "broken-nav-target.html": html.replace(/<div class="section-card[^"]*" id="license"/, '<div class="section-card open" id="license-renamed"'),
  "dead-toggle.html": html.replace("function toggleSection(header) {", "function toggleSection(header) { return;"),
} as Record<string, string>;

const expect: Record<string, number> = { "clean.html": 0, "js-error.html": 1, "missing-core-section.html": 1, "broken-nav-target.html": 1, "dead-toggle.html": 1 };
let bad = 0;
for (const [name, body] of Object.entries(mutate)) {
  if (name !== "clean.html" && body === html) { console.log(`SELFTEST-ERROR ${name}: mutation did not apply`); bad++; continue; }
  const p = join(dir, name);
  writeFileSync(p, body);
  const r = Bun.spawnSync(["bun", join(import.meta.dir, "smoke.ts"), "--out", join(dir, "shots"), p], { stdout: "pipe", stderr: "pipe" });
  const ok = r.exitCode === expect[name];
  console.log(`${ok ? "OK  " : "BAD "} ${name}: exit ${r.exitCode} (want ${expect[name]})`);
  console.log(new TextDecoder().decode(r.stdout).split("\n").filter((l) => l.includes("- [") || l.startsWith("PASS") || l.startsWith("FAIL")).map((l) => "     " + l.trim()).join("\n"));
  if (!ok) bad++;
}
console.log(bad === 0 ? "selftest: all mutations behaved" : `selftest: ${bad} unexpected result(s)`);
process.exit(bad ? 1 : 0);
