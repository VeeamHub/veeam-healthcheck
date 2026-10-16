// report-smoke: open generated VHC report HTML files in a real browser and fail on
// console errors, broken nav anchors, dead section toggles and empty core sections.
//
//   bun smoke.ts [--out <dir>] [--profile auto|vbr|none] <report.html | dir> [...]
//
// Env: REPORT_SMOKE_CHANNEL=chrome uses the system Chrome instead of Playwright's chromium.
import { chromium, type Page } from "playwright";
import { existsSync, mkdirSync, readdirSync, statSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

// Core VBR sections: id in the report -> label. Every one must exist and hold >= 1 body row
// (a table data row, or a status tile for Security Summary, which is a grid without a table when no malware settings exist).
export const VBR_CORE: Record<string, string> = {
  license: "License Summary",
  vbrserver: "Backup Server & Config DB Info",
  secsummary: "Security Summary",
  proxies: "Proxy Info",
  repos: "Standalone Repository Details",
  jobsummary: "Job Summary",
};

export interface Finding {
  check: "console" | "nav" | "toggle" | "core";
  detail: string;
}
export interface ReportResult {
  file: string;
  profile: string;
  navLinks: number;
  toggles: number;
  skippedToggles: number;
  coreChecked: number;
  screenshot: string;
  findings: Finding[];
}

function collectHtml(arg: string): string[] {
  const p = resolve(arg);
  if (!existsSync(p)) throw new Error(`no such path: ${arg}`);
  if (statSync(p).isFile()) return [p];
  const out: string[] = [];
  const walk = (d: string) => {
    for (const e of readdirSync(d, { withFileTypes: true })) {
      const f = join(d, e.name);
      if (e.isDirectory()) walk(f);
      else if (e.name.toLowerCase().endsWith(".html")) out.push(f);
    }
  };
  walk(p);
  return out.sort();
}

async function checkNav(page: Page, findings: Finding[]): Promise<number> {
  const hrefs: string[] = await page.$$eval('a[href^="#"]', (as) =>
    [...new Set(as.map((a) => a.getAttribute("href") || ""))].filter((h) => h.length > 1),
  );
  for (const href of hrefs) {
    const id = href.slice(1);
    const target = page.locator(`[id="${id}"]`);
    if ((await target.count()) === 0) {
      findings.push({ check: "nav", detail: `${href}: no element with that id` });
      continue;
    }
    await page.locator(`a[href="${href}"]`).first().click({ timeout: 5000 }).catch(async () => {
      // link itself not clickable (hidden); fall back to navigating the hash
      await page.evaluate((h) => { location.hash = h; }, href);
    });
    await page.waitForTimeout(40);
    if (!(await target.first().isVisible())) {
      findings.push({ check: "nav", detail: `${href}: target not visible after click` });
    }
  }
  return hrefs.length;
}

async function checkToggles(page: Page, findings: Finding[]): Promise<{ n: number; skipped: number }> {
  const headers = page.locator('.section-header[onclick*="toggleSection"]');
  const n = await headers.count();
  let skipped = 0;
  for (let i = 0; i < n; i++) {
    const h = headers.nth(i);
    const card = h.locator("xpath=ancestor::*[contains(concat(' ',normalize-space(@class),' '),' section-card ')][1]");
    const label = ((await h.innerText().catch(() => "")) || `#${i}`).trim().replace(/\s+/g, " ").slice(0, 50);
    if (!(await h.isVisible())) { skipped++; continue; }
    const body = card.locator("> .section-body");
    const state = async () => ({
      open: await card.evaluate((c) => c.classList.contains("open")),
      shown: (await body.count()) ? await body.first().isVisible() : null,
    });
    const before = await state();
    await h.click({ timeout: 5000 });
    const mid = await state();
    await h.click({ timeout: 5000 });
    const after = await state();
    if (mid.open === before.open) findings.push({ check: "toggle", detail: `"${label}": click did not change open state` });
    else if (mid.shown !== null && before.shown !== null && mid.shown === before.shown) {
      findings.push({ check: "toggle", detail: `"${label}": open class flipped but body visibility did not` });
    }
    if (after.open !== before.open) findings.push({ check: "toggle", detail: `"${label}": second click did not restore state` });
  }
  return { n, skipped };
}

async function checkCore(page: Page, findings: Finding[]): Promise<number> {
  for (const [id, label] of Object.entries(VBR_CORE)) {
    const card = page.locator(`[id="${id}"]`);
    if ((await card.count()) === 0) {
      findings.push({ check: "core", detail: `${id} (${label}): section missing` });
      continue;
    }
    const rows = await card.first().locator(".section-body tr:has(td), .section-body .security-item").count();
    if (rows === 0) findings.push({ check: "core", detail: `${id} (${label}): zero body rows` });
  }
  return Object.keys(VBR_CORE).length;
}

export async function smokeReport(file: string, outDir: string, profileArg: string, browser: import("playwright").Browser): Promise<ReportResult> {
  const findings: Finding[] = [];
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();
  page.on("console", (m) => { if (m.type() === "error") findings.push({ check: "console", detail: `console.error: ${m.text()}` }); });
  page.on("pageerror", (e) => findings.push({ check: "console", detail: `pageerror: ${e.message}` }));
  page.on("requestfailed", (r) => findings.push({ check: "console", detail: `request failed: ${r.url().slice(0, 120)}` }));
  await page.goto(pathToFileURL(file).href, { waitUntil: "load" });
  await page.waitForTimeout(200);

  const meta = (await page.locator(".page-header .meta").first().innerText().catch(() => "")) || "";
  const profile = profileArg === "auto" ? (/Backup & Replication/i.test(meta) ? "vbr" : "none") : profileArg;

  const navLinks = await checkNav(page, findings);
  const { n: toggles, skipped } = await checkToggles(page, findings);
  const coreChecked = profile === "vbr" ? await checkCore(page, findings) : 0;

  mkdirSync(outDir, { recursive: true });
  const shot = join(outDir, basename(file).replace(/[^A-Za-z0-9._-]+/g, "_").replace(/\.html$/i, "") + ".png");
  await page.screenshot({ path: shot, fullPage: true });
  await ctx.close();
  return { file, profile, navLinks, toggles, skippedToggles: skipped, coreChecked, screenshot: shot, findings };
}

export async function launch() {
  const channel = process.env.REPORT_SMOKE_CHANNEL;
  return chromium.launch(channel ? { channel } : {});
}

async function main() {
  const args = process.argv.slice(2);
  let outDir = "report-smoke-out";
  let profile = "auto";
  const inputs: string[] = [];
  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--out") outDir = args[++i];
    else if (args[i] === "--profile") profile = args[++i];
    else inputs.push(args[i]);
  }
  if (inputs.length === 0) { console.error("usage: bun smoke.ts [--out dir] [--profile auto|vbr|none] <report.html|dir> ..."); process.exit(2); }
  const files = inputs.flatMap(collectHtml);
  if (files.length === 0) { console.error("no .html reports found"); process.exit(2); }

  const browser = await launch();
  let failed = 0;
  for (const f of files) {
    const r = await smokeReport(f, resolve(outDir), profile, browser);
    const verdict = r.findings.length === 0 ? "PASS" : "FAIL";
    console.log(`${verdict} ${basename(f)} [profile=${r.profile}] nav=${r.navLinks} toggles=${r.toggles}(skipped ${r.skippedToggles}) core=${r.coreChecked}`);
    console.log(`     screenshot: ${r.screenshot}`);
    for (const x of r.findings) console.log(`     - [${x.check}] ${x.detail}`);
    if (r.findings.length) failed++;
  }
  await browser.close();
  console.log(`report-smoke: ${files.length - failed}/${files.length} reports clean`);
  process.exit(failed ? 1 : 0);
}

if (import.meta.main) await main();
