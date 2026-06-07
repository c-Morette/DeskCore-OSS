// Captura screenshots do app para o README (uso local/descartável).
// Requer Playwright e o Microsoft Edge instalado (channel msedge).
const { chromium } = require("playwright");
const path = require("path");

const WEB = "https://localhost:5544";
const OUT = process.argv[2] || ".";
const ADMIN = { email: "admin@deskcore.local", password: "ChangeMe@123456" };

async function shoot(page, name, { full = true, wait = 1600 } = {}) {
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.waitForTimeout(wait);
  const file = path.join(OUT, name + ".png");
  await page.screenshot({ path: file, fullPage: full });
  console.log("  saved", file);
}

(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  const ctx = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 2,
    locale: "pt-BR",
  });
  // Dispensa o aviso de cookies (localStorage) para telas mais limpas.
  await ctx.addInitScript(() => {
    try { localStorage.setItem("dc-cookie-notice", "ok"); } catch (e) {}
  });

  const page = await ctx.newPage();

  // 1) Login (sem autenticar) — hero shot
  await page.goto(WEB + "/login", { waitUntil: "domcontentloaded" });
  await shoot(page, "01-login", { full: false, wait: 1200 });

  // autentica
  await page.fill('input[name="email"]', ADMIN.email);
  await page.fill('input[name="password"]', ADMIN.password);
  await Promise.all([
    page.waitForURL((u) => !u.pathname.startsWith("/login"), { timeout: 20000 }).catch(() => {}),
    page.click('button[type="submit"]'),
  ]);
  await page.waitForTimeout(1500);
  console.log("after login:", page.url());

  // 2) Painel / Dashboard
  await page.goto(WEB + "/", { waitUntil: "domcontentloaded" });
  await shoot(page, "02-dashboard");

  // 3) Lista de tickets
  await page.goto(WEB + "/tickets", { waitUntil: "domcontentloaded" });
  await shoot(page, "03-tickets");

  // 4) Detalhe do ticket (com comentários)
  await page.goto(WEB + "/tickets/1", { waitUntil: "domcontentloaded" });
  await shoot(page, "04-ticket-detail");

  // 5) Métricas (gráficos)
  await page.goto(WEB + "/metricas", { waitUntil: "domcontentloaded" });
  await shoot(page, "05-metrics", { wait: 2000 });

  // 6) Admin — usuários
  await page.goto(WEB + "/admin/usuarios", { waitUntil: "domcontentloaded" });
  await shoot(page, "06-admin-users");

  await browser.close();
  console.log("DONE");
})().catch((e) => { console.error("SHOOT ERROR:", e); process.exit(1); });
