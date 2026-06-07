using System.Globalization;
using System.Net;
using System.Security.Claims;
using DeskCore.Web.Auth;
using DeskCore.Web.Authorization;
using DeskCore.Web.Components;
using DeskCore.Web.Display;
using DeskCore.Web.Services;
using DeskCore.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization();

// Atrás do Nginx: ler X-Forwarded-Proto/For para que UseHttpsRedirection e o cookie
// Secure funcionem (sem isto, o Web vê http -> loop de redirect para https).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
            options.KnownIPNetworks.Add(ipNetwork);
});

// Persistir as chaves de DataProtection (cookie do Web) em volume, para sobreviver a
// restart do container. Sem KeysPath (ex.: dev local fora do Docker) usa o padrão.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("DeskCore.Web");
if (!string.IsNullOrWhiteSpace(dpKeysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Autenticação própria do Web (cookie). O cookie da API é guardado como claim.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "DeskCore.Web.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/acesso-negado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AgentOrAdmin, p => p.RequireRole(Roles.Agent, Roles.Admin));
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

// Cliente da API com reenvio do cookie de autenticação.
builder.Services.AddScoped<ApiAuthCookieHandler>();
builder.Services.AddHttpClient<DeskCoreApiClient>(client =>
    {
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("ApiSettings:BaseUrl não configurada.");
        client.BaseAddress = new Uri(baseUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler { UseCookies = false };
        if (builder.Environment.IsDevelopment())
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    })
    .AddHttpMessageHandler<ApiAuthCookieHandler>();

var app = builder.Build();

// Cedo no pipeline: aplica X-Forwarded-* antes de HSTS/HttpsRedirection.
app.UseForwardedHeaders();

// Headers de segurança do app que o browser carrega (clickjacking, MIME-sniffing, etc.).
// A CSP é compatível com Blazor Server (importmap/inline) e com o widget Turnstile do /cadastro;
// 'unsafe-inline' em script/style é necessário pelo Blazor — um endurecimento futuro usaria nonce.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
        "img-src 'self' data:; font-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; " +
        "connect-src 'self' ws: wss: https://challenges.cloudflare.com; " +
        "frame-src https://challenges.cloudflare.com; " +
        "form-action 'self'";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

var supportedCultures = new[] { new CultureInfo("pt-BR"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("pt-BR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Login: feito num endpoint (não em componente) para o SignInAsync rodar antes
// de a resposta começar. Chama a API, guarda o cookie da API como claim.
app.MapPost("/account/login", async (
    HttpContext context,
    DeskCoreApiClient api,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch { return Results.Redirect("/login?error=1"); }

    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var outcome = await api.LoginAsync(email, password);
    if (!outcome.Success)
    {
        var back = string.IsNullOrEmpty(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.Redirect($"/login?error=1{back}");
    }

    var user = outcome.User!;
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.UserId),
        new(ClaimTypes.Name, user.Email),
        new(ClaimTypes.Email, user.Email),
        new(DeskCoreClaims.FullName, user.FullName),
        new(DeskCoreClaims.ApiAuthCookie, outcome.AuthCookie!)
    };
    claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
}).DisableAntiforgery();

// Política de Privacidade (LGPD) — HTML estático, acessível logado ou não.
app.MapGet("/privacidade", () => Results.Content(BuildPrivacyHtml(), "text/html; charset=utf-8"));

// Troca de idioma (PT-BR / EN): grava o cookie de cultura e volta à página informada.
// Usa returnUrl explícito (não o Referer, que pode virar /language e causar loop).
app.MapGet("/language/{culture}", (string culture, string? returnUrl, HttpContext ctx) =>
{
    var allowed = culture is "pt-BR" or "en" ? culture : "pt-BR";
    ctx.Response.Cookies.Append(
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(allowed)),
        new CookieOptions { Path = "/", Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });

    // Só caminho local; nunca volta para /language (evita loop de redirecionamento).
    var back = returnUrl;
    if (string.IsNullOrEmpty(back) || !back.StartsWith('/') || back.StartsWith("//")
        || back.StartsWith("/language", StringComparison.OrdinalIgnoreCase))
        back = "/";
    return Results.LocalRedirect(back);
});

// Página de cadastro servida como HTML estático (fora do router Blazor interativo):
// o Blazor não executa <script> de componente e a reconciliação interativa removeria
// o widget injetado pelo Turnstile. Como HTML puro, o script roda e o token é gerado.
app.MapGet("/cadastro", (
    HttpContext context,
    IConfiguration config,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    // Modo privado: auto-cadastro desligado → aviso, sem formulário (a API também bloqueia).
    if (!config.GetValue("SelfService:Enabled", true))
        return Results.Content(BuildRegisterHtml(null, "", registered: false, error: "", message: "", disabled: true), "text/html; charset=utf-8");

    var tokens = antiforgery.GetAndStoreTokens(context);
    var siteKey = config["Turnstile:SiteKey"];
    var query = context.Request.Query;
    var html = BuildRegisterHtml(
        siteKey,
        tokens.RequestToken!,
        registered: query["registered"] == "1",
        error: query["error"].ToString(),
        message: query["msg"].ToString());
    return Results.Content(html, "text/html; charset=utf-8");
});

// Auto-cadastro: valida antiforgery, verifica o Turnstile (se configurado) e chama a API.
// Não loga o usuário — a conta nasce inativa e precisa confirmar o e-mail.
app.MapPost("/account/register", async (
    HttpContext context,
    DeskCoreApiClient api,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILoggerFactory loggerFactory,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    var logger = loggerFactory.CreateLogger("Cadastro");

    // Modo privado: trava o POST também (defesa em profundidade — a API é a fonte da verdade).
    if (!config.GetValue("SelfService:Enabled", true))
        return Results.Redirect("/cadastro");

    try { await antiforgery.ValidateRequestAsync(context); }
    catch { return Results.Redirect("/cadastro?error=1"); }

    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var fullName = form["fullName"].ToString();
    var password = form["password"].ToString();
    var acceptedPrivacy = form["acceptPrivacy"].ToString() == "true";

    // Consentimento LGPD obrigatório (também validado na API).
    if (!acceptedPrivacy)
        return Results.Redirect("/cadastro?error=privacy");

    // Anti-bot Cloudflare Turnstile — só exigido quando a secret está configurada.
    var secret = config["Turnstile:SecretKey"];
    if (!string.IsNullOrWhiteSpace(secret))
    {
        var captchaToken = form["cf-turnstile-response"].ToString();
        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (!await VerifyTurnstileAsync(httpFactory, secret, captchaToken, ip, logger))
            return Results.Redirect("/cadastro?error=turnstile");
    }

    var outcome = await api.RegisterAsync(email, fullName, password, acceptedPrivacy);
    if (!outcome.Success)
        return Results.Redirect($"/cadastro?error=1&msg={Uri.EscapeDataString(outcome.Error ?? "Não foi possível concluir o cadastro.")}");

    return Results.Redirect("/cadastro?registered=1");
}).DisableAntiforgery();

// Confirmação de e-mail: feita num endpoint GET (executa uma única vez, sem o
// double-render dos componentes interativos que queimaria o token de uso único).
// Redireciona para a página de status /confirmar-email.
app.MapGet("/account/confirm-email", async (
    string? userId,
    string? token,
    DeskCoreApiClient api) =>
{
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        return Results.Redirect("/confirmar-email?status=fail");

    var outcome = await api.ConfirmEmailAsync(userId, token);
    return Results.Redirect($"/confirmar-email?status={(outcome.Success ? "ok" : "fail")}");
});

// Esqueci minha senha: chama a API e SEMPRE mostra a mesma confirmação genérica
// (anti-enumeração de e-mail — o resultado real é ignorado de propósito).
app.MapPost("/account/forgot-password", async (
    HttpContext context,
    DeskCoreApiClient api,
    IConfiguration config,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    // Modo privado: sem auto-serviço de senha (a API também bloqueia).
    if (!config.GetValue("SelfService:Enabled", true))
        return Results.Redirect("/login");

    try { await antiforgery.ValidateRequestAsync(context); }
    catch { return Results.Redirect("/esqueci-senha?error=1"); }

    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString();

    await api.ForgotPasswordAsync(email);
    return Results.Redirect("/esqueci-senha?sent=1");
}).DisableAntiforgery();

// Redefinir senha: confere a confirmação, troca a senha via API e volta ao login.
app.MapPost("/account/reset-password", async (
    HttpContext context,
    DeskCoreApiClient api,
    IConfiguration config,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    // Modo privado: sem auto-serviço de senha (a API também bloqueia).
    if (!config.GetValue("SelfService:Enabled", true))
        return Results.Redirect("/login");

    try { await antiforgery.ValidateRequestAsync(context); }
    catch { return Results.Redirect("/redefinir-senha?error=1"); }

    var form = await context.Request.ReadFormAsync();
    var userId = form["userId"].ToString();
    var token = form["token"].ToString();
    var password = form["password"].ToString();
    var confirm = form["confirmPassword"].ToString();

    var back = $"/redefinir-senha?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";

    if (password != confirm)
        return Results.Redirect($"{back}&error=mismatch");

    var outcome = await api.ResetPasswordAsync(userId, token, password);
    if (!outcome.Success)
        return Results.Redirect($"{back}&error=1&msg={Uri.EscapeDataString(outcome.Error ?? "")}");

    return Results.Redirect("/login?reset=1");
}).DisableAntiforgery();

// Logout: limpa o cookie do Web e volta ao login.
app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery(); // CSRF de logout mitigado pelo cookie SameSite=Lax (não vai em POST cross-site).

// LGPD — exportar meus dados: baixa um JSON com o pacote de dados do titular.
app.MapGet("/account/export", async (DeskCoreApiClient api) =>
{
    var data = await api.ExportMyDataAsync();
    var json = System.Text.Json.JsonSerializer.Serialize(data,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    return Results.File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "meus-dados-deskcore.json");
}).RequireAuthorization();

// LGPD — excluir (anonimizar) a própria conta: anonimiza na API e encerra a sessão.
app.MapPost("/account/delete", async (
    HttpContext context,
    DeskCoreApiClient api,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch { return Results.Redirect("/minha-conta?error=1"); }

    try
    {
        await api.DeleteMyAccountAsync();
    }
    catch (DeskCore.Web.Services.ApiException ex)
    {
        return Results.Redirect($"/minha-conta?error=1&msg={Uri.EscapeDataString(ex.Message)}");
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login?deleted=1");
}).DisableAntiforgery().RequireAuthorization();

// Proxy autenticado de download de anexos (o browser não possui o cookie da API).
app.MapGet("/files/{ticketId:long}/{attachmentId:long}",
    async (long ticketId, long attachmentId, DeskCoreApiClient api) =>
    {
        var file = await api.DownloadAttachmentAsync(ticketId, attachmentId);
        return file is null
            ? Results.NotFound()
            : Results.Stream(file.Value.Content, file.Value.ContentType, file.Value.FileName);
    }).RequireAuthorization();

app.Run();

// Seletor de idioma flutuante (PT|EN) para as páginas estáticas servidas fora do Blazor.
static string LangSwitcher(bool en, string returnUrl)
{
    var ret = Uri.EscapeDataString(returnUrl);
    return $"""<div class="dc-lang dc-lang-float" title="Idioma / Language"><a href="/language/pt-BR?returnUrl={ret}" class="{(en ? "" : "dc-lang-active")}">PT</a><span class="dc-lang-sep">/</span><a href="/language/en?returnUrl={ret}" class="{(en ? "dc-lang-active" : "")}">EN</a></div>""";
}

// Política de Privacidade / Privacy Policy (PT-BR + EN; texto sugerido — revisar com jurídico).
static string BuildPrivacyHtml()
{
    var versao = DeskCore.Domain.Constants.PrivacyPolicy.CurrentVersion;
    var en = Loc.IsEnglish;
    var lang = en ? "en" : "pt-BR";
    var title = en ? "Privacy Policy" : "Política de Privacidade";
    var back = en ? "&larr; Back" : "&larr; Voltar";
    var body = en ? PrivacyBodyEn(versao) : PrivacyBodyPt(versao);

    return $$"""
        <!DOCTYPE html>
        <html lang="{{lang}}" data-bs-theme="dark">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <base href="/" />
            <title>{{title}} — DeskCore</title>
            <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css" />
            <link rel="stylesheet" href="/app.css" />
            <link rel="stylesheet" href="/css/deskcore-theme.css" />
            <link rel="stylesheet" href="/DeskCore.Web.styles.css" />
            <link rel="icon" type="image/svg+xml" href="/favicon.svg?v=3" />
            <style>
                .dc-doc { max-width: 820px; margin: 0 auto; padding: 2.5rem 1.5rem 4rem; }
                .dc-doc h1 { color: var(--dc-gold); }
                .dc-doc h2 { font-size: 1.1rem; margin-top: 2rem; color: var(--dc-text); }
                .dc-doc small { color: var(--dc-text-dim); }
                .dc-doc a { color: var(--dc-gold); }
            </style>
            <script src="/js/cookie-notice.js" defer></script>
        </head>
        <body>
            {{LangSwitcher(en, "/privacidade")}}
            <div class="dc-doc">
                <a href="/login">{{back}}</a>
                <h1 class="mt-3">{{title}}</h1>
                {{body}}
            </div>
        </body>
        </html>
        """;
}

static string PrivacyBodyPt(string versao) => $$"""
        <small>Versão {{versao}} • Em conformidade com a Lei nº 13.709/2018 (LGPD).</small>

        <p class="mt-4"><strong>Aviso:</strong> recomenda-se a revisão deste texto por profissional jurídico antes de considerá-lo definitivo.</p>

        <h2>1. Quem é o controlador</h2>
        <p>O tratamento dos dados é realizado por <strong>[Nome do Controlador]</strong>, responsável por esta instância do sistema de tickets DeskCore.</p>

        <h2>2. Quais dados tratamos</h2>
        <ul>
            <li><strong>Cadastro:</strong> nome completo e e-mail; senha (armazenada apenas como hash).</li>
            <li><strong>Uso e segurança:</strong> endereço IP, navegador (user-agent) e registros de acesso/auditoria.</li>
            <li><strong>Conteúdo:</strong> textos, comentários e anexos que você inclui nos tickets.</li>
        </ul>

        <h2>3. Para que usamos e com qual base legal</h2>
        <ul>
            <li>Prestar o serviço de atendimento/tickets — execução de contrato e procedimentos preliminares (Art. 7º, V).</li>
            <li>Segurança, prevenção a fraude e auditoria (IP/registros) — legítimo interesse (Art. 7º, IX).</li>
            <li>Criação de conta por auto-cadastro — consentimento, registrado no aceite desta política (Art. 7º, I).</li>
        </ul>

        <h2>4. Compartilhamento e operadores</h2>
        <p>Não vendemos dados pessoais. Utilizamos prestadores que atuam como operadores:</p>
        <ul>
            <li><strong>Hetzner</strong> — hospedagem da infraestrutura (servidores na Alemanha).</li>
            <li><strong>Resend</strong> — envio de e-mails transacionais (ex.: confirmação de cadastro).</li>
            <li><strong>Cloudflare</strong> — proteção anti-robô (Turnstile) no cadastro.</li>
        </ul>

        <h2>5. Transferência internacional</h2>
        <p>Os dados podem ser processados fora do Brasil (ex.: servidores na União Europeia), com salvaguardas adequadas, nos termos dos Arts. 33 e 34 da LGPD.</p>

        <h2>6. Por quanto tempo guardamos</h2>
        <p>Mantemos os dados enquanto a conta estiver ativa e pelo prazo necessário às finalidades acima e a obrigações legais. Registros de acesso/auditoria são mantidos por 12 (doze) meses e depois anonimizados ou eliminados.</p>

        <h2>7. Seus direitos (Art. 18)</h2>
        <p>Você pode acessar, corrigir, exportar e solicitar a eliminação dos seus dados. No próprio sistema, em <a href="/minha-conta">Minha conta</a>, é possível corrigir seu nome, exportar seus dados e excluir (anonimizar) a conta. Para outras solicitações, fale com o nosso encarregado.</p>

        <h2>8. Segurança</h2>
        <p>Adotamos medidas técnicas como criptografia em trânsito (HTTPS), senhas com hash, controle de acesso por perfil, limitação de tentativas e registros de auditoria.</p>

        <h2>9. Cookies</h2>
        <p>Usamos apenas cookies estritamente necessários para autenticação e segurança da sessão. Não utilizamos cookies de publicidade.</p>

        <h2>10. Encarregado (DPO) e contato</h2>
        <p>Encarregado pelo tratamento de dados: [Nome do Encarregado] — <a href="mailto:contato@exemplo.com">contato@exemplo.com</a>.</p>

        <h2>11. Alterações</h2>
        <p>Esta política pode ser atualizada. A versão vigente e sua data ficam indicadas no topo desta página.</p>
        """;

static string PrivacyBodyEn(string versao) => $$"""
        <small>Version {{versao}} • In accordance with Brazilian Law No. 13,709/2018 (LGPD).</small>

        <p class="mt-4"><strong>Notice:</strong> we recommend that this text be reviewed by a legal professional before being considered final.</p>

        <h2>1. Who is the controller</h2>
        <p>Data processing is carried out by <strong>[Controller name]</strong>, responsible for this instance of the DeskCore ticket system.</p>

        <h2>2. What data we process</h2>
        <ul>
            <li><strong>Account:</strong> full name and email; password (stored only as a hash).</li>
            <li><strong>Usage and security:</strong> IP address, browser (user-agent) and access/audit logs.</li>
            <li><strong>Content:</strong> text, comments and attachments you add to tickets.</li>
        </ul>

        <h2>3. Why we use it and on what legal basis</h2>
        <ul>
            <li>To provide the support/ticket service — performance of a contract and preliminary procedures.</li>
            <li>Security, fraud prevention and auditing (IP/logs) — legitimate interest.</li>
            <li>Account creation via self-registration — consent, recorded upon acceptance of this policy.</li>
        </ul>

        <h2>4. Sharing and processors</h2>
        <p>We do not sell personal data. We use providers that act as processors:</p>
        <ul>
            <li><strong>Hetzner</strong> — infrastructure hosting (servers in Germany).</li>
            <li><strong>Resend</strong> — transactional email delivery (e.g., registration confirmation).</li>
            <li><strong>Cloudflare</strong> — anti-bot protection (Turnstile) at registration.</li>
        </ul>

        <h2>5. International transfer</h2>
        <p>Data may be processed outside Brazil (e.g., servers in the European Union), with appropriate safeguards, under Articles 33 and 34 of the LGPD.</p>

        <h2>6. How long we keep it</h2>
        <p>We keep data while the account is active and for as long as necessary for the purposes above and legal obligations. Access/audit logs are kept for 12 (twelve) months and then anonymized or deleted.</p>

        <h2>7. Your rights</h2>
        <p>You may access, correct, export and request deletion of your data. Within the system itself, under <a href="/minha-conta">My account</a>, you can correct your name, export your data and delete (anonymize) your account. For other requests, contact our data protection officer.</p>

        <h2>8. Security</h2>
        <p>We adopt technical measures such as encryption in transit (HTTPS), hashed passwords, role-based access control, attempt rate limiting and audit logs.</p>

        <h2>9. Cookies</h2>
        <p>We use only strictly necessary cookies for authentication and session security. We do not use advertising cookies.</p>

        <h2>10. Data Protection Officer (DPO) and contact</h2>
        <p>Officer in charge of data processing: [Officer name] — <a href="mailto:contato@exemplo.com">contato@exemplo.com</a>.</p>

        <h2>11. Changes</h2>
        <p>This policy may be updated. The current version and its date are shown at the top of this page.</p>
        """;

// Monta a página de cadastro (HTML puro, mesmo visual da tela de login via CSS do app).
static string BuildRegisterHtml(string? siteKey, string requestToken, bool registered, string error, string message, bool disabled = false)
{
    static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
    var hasWidget = !string.IsNullOrWhiteSpace(siteKey);
    var en = Loc.IsEnglish;
    var lang = en ? "en" : "pt-BR";

    string body;
    if (disabled)
    {
        body = $"""
            <div class="alert alert-warning py-2">{Loc.T("O auto-cadastro está desabilitado nesta instância. Solicite uma conta ao administrador.", "Self-registration is disabled on this instance. Please request an account from the administrator.")}</div>
            <a class="btn btn-primary w-100" href="/login">{Loc.T("Voltar ao login", "Back to sign in")}</a>
            """;
    }
    else if (registered)
    {
        body = $"""
            <div class="alert alert-success py-2">{Loc.T("Cadastro recebido! Enviamos um link de confirmação para o seu e-mail. Confirme para ativar a conta e poder entrar.", "Registration received! We sent a confirmation link to your email. Confirm it to activate your account and sign in.")}</div>
            <a class="btn btn-primary w-100" href="/login">{Loc.T("Ir para o login", "Go to sign in")}</a>
            """;
    }
    else
    {
        var alert = error switch
        {
            "turnstile" => $"<div class=\"alert alert-danger py-2\">{Loc.T("Verificação anti-robô falhou. Tente novamente.", "Anti-bot verification failed. Please try again.")}</div>",
            "privacy" => $"<div class=\"alert alert-danger py-2\">{Loc.T("É necessário aceitar a Política de Privacidade.", "You must accept the Privacy Policy.")}</div>",
            "1" => $"<div class=\"alert alert-danger py-2\">{Enc(string.IsNullOrWhiteSpace(message) ? Loc.T("Não foi possível concluir o cadastro.", "Could not complete registration.") : message)}</div>",
            _ => ""
        };
        var widget = hasWidget ? $"<div class=\"cf-turnstile mb-3\" data-sitekey=\"{Enc(siteKey)}\"></div>" : "";

        body = $"""
            {alert}
            <form action="/account/register" method="post">
                <input type="hidden" name="__RequestVerificationToken" value="{Enc(requestToken)}" />
                <div class="mb-3">
                    <label class="form-label">{Loc.T("Nome completo", "Full name")}</label>
                    <input name="fullName" type="text" class="form-control" autocomplete="name" required minlength="2" maxlength="200" />
                </div>
                <div class="mb-3">
                    <label class="form-label">{Loc.T("E-mail", "Email")}</label>
                    <input name="email" type="email" class="form-control" autocomplete="username" required />
                </div>
                <div class="mb-2">
                    <label class="form-label">{Loc.T("Senha", "Password")}</label>
                    <input name="password" type="password" class="form-control" autocomplete="new-password" required minlength="12" />
                    <div class="form-text">{Loc.T("Mínimo 12 caracteres, com maiúscula, minúscula, número e símbolo.", "At least 12 characters, with uppercase, lowercase, number and symbol.")}</div>
                </div>
                {widget}
                <div class="form-check mb-3 text-start">
                    <input class="form-check-input" type="checkbox" name="acceptPrivacy" id="acceptPrivacy" value="true" required />
                    <label class="form-check-label small" for="acceptPrivacy">
                        {Loc.T("Li e aceito a", "I have read and accept the")} <a href="/privacidade" target="_blank">{Loc.T("Política de Privacidade", "Privacy Policy")}</a>.
                    </label>
                </div>
                <button type="submit" class="btn btn-primary w-100">{Loc.T("Criar conta", "Create account")}</button>
            </form>
            <div class="text-center mt-3 dc-login-sub">{Loc.T("Já tem conta?", "Already have an account?")} <a href="/login">{Loc.T("Entrar", "Sign in")}</a></div>
            """;
    }

    var turnstileScript = hasWidget
        ? "<script src=\"https://challenges.cloudflare.com/turnstile/v0/api.js\" async defer></script>"
        : "";

    return $$"""
        <!DOCTYPE html>
        <html lang="{{lang}}" data-bs-theme="dark">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <base href="/" />
            <title>{{Loc.T("Criar conta", "Create account")}} — DeskCore</title>
            <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css" />
            <link rel="stylesheet" href="/app.css" />
            <link rel="stylesheet" href="/css/deskcore-theme.css" />
            <link rel="stylesheet" href="/DeskCore.Web.styles.css" />
            <link rel="icon" type="image/svg+xml" href="/favicon.svg?v=3" />
            <style>
                /* Estilos do layout de auth — espelham EmptyLayout.razor.css / Login.razor.css,
                   que são scoped e não alcançam esta página HTML pura. */
                .dc-auth { min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 1.5rem; padding: 1.5rem; background: radial-gradient(900px 500px at 50% -10%, rgba(242,199,68,0.07), transparent 60%), var(--dc-bg); }
                .dc-auth-inner { width: 100%; max-width: 380px; }
                .dc-auth-foot { font-size: 0.66rem; letter-spacing: 0.14em; text-transform: uppercase; color: var(--dc-text-faint); }
                .dc-login-brand { font-size: 1.5rem; font-weight: 700; letter-spacing: 0.12em; color: var(--dc-text); }
                .dc-login-mark { color: var(--dc-gold); text-shadow: 0 0 12px rgba(242,199,68,0.5); }
                .dc-login-accent { color: var(--dc-gold); }
                .dc-login-sub { margin-top: 0.4rem; font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.1em; color: var(--dc-text-dim); }
            </style>
            {{turnstileScript}}
            <script src="/js/cookie-notice.js" defer></script>
        </head>
        <body>
            {{LangSwitcher(en, "/cadastro")}}
            <div class="dc-auth">
                <div class="dc-auth-inner">
                    <div class="card">
                        <div class="card-body p-4">
                            <div class="text-center mb-4">
                                <div class="dc-login-brand"><span class="dc-login-mark">◆</span> DESK<span class="dc-login-accent">CORE</span></div>
                                <div class="dc-login-sub">{{Loc.T("Criar conta de cliente", "Create a client account")}}</div>
                            </div>
                            {{body}}
                        </div>
                    </div>
                </div>
                <div class="dc-auth-foot">DESKCORE // {{Loc.T("SISTEMA DE TICKETS", "TICKET SYSTEM")}}</div>
            </div>
        </body>
        </html>
        """;
}

// Verifica o token do Cloudflare Turnstile no endpoint siteverify. Loga o motivo
// exato da reprovação (token vazio / error-codes / HTTP) para diagnóstico.
static async Task<bool> VerifyTurnstileAsync(IHttpClientFactory httpFactory, string secret, string? token, string? remoteIp, ILogger logger)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        logger.LogWarning("Turnstile reprovado: token 'cf-turnstile-response' ausente no POST (widget não resolveu).");
        return false;
    }

    var fields = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
    if (!string.IsNullOrEmpty(remoteIp))
        fields["remoteip"] = remoteIp;

    try
    {
        var client = httpFactory.CreateClient();
        using var response = await client.PostAsync(
            "https://challenges.cloudflare.com/turnstile/v0/siteverify",
            new FormUrlEncodedContent(fields));
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Turnstile: siteverify retornou HTTP {Status}. Corpo: {Body}", (int)response.StatusCode, payload);
            return false;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(payload);
        var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        if (!success)
            logger.LogWarning("Turnstile reprovado pelo siteverify. Resposta: {Body}", payload);

        return success;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Turnstile: erro ao chamar o siteverify.");
        return false;
    }
}
