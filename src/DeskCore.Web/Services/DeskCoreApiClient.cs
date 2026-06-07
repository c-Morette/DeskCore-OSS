using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeskCore.Shared.Contracts.Account;
using DeskCore.Shared.Contracts.Attachments;
using DeskCore.Shared.Contracts.Audit;
using DeskCore.Shared.Contracts.Auth;
using DeskCore.Shared.Contracts.Categories;
using DeskCore.Shared.Contracts.Comments;
using DeskCore.Shared.Contracts.Metrics;
using DeskCore.Shared.Contracts.Common;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Contracts.Users;
using DeskCore.Shared.Enums;

namespace DeskCore.Web.Services;

/// <summary>Resultado do login: dados do usuário + cookie de autenticação da API.</summary>
public sealed record LoginOutcome(bool Success, CurrentUserResponse? User, string? AuthCookie, string? Error);

/// <summary>Resultado de um POST anônimo simples (cadastro / confirmação).</summary>
public sealed record OperationOutcome(bool Success, string? Error);

/// <summary>Cliente tipado da DeskCore API. As chamadas autenticadas reusam o cookie via <c>ApiAuthCookieHandler</c>.</summary>
public sealed class DeskCoreApiClient(HttpClient http, IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- Auth ----
    public async Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var baseUri = http.BaseAddress ?? throw new InvalidOperationException("ApiSettings:BaseUrl não configurada.");

        // Cliente descartável com CookieContainer próprio: captura o cookie de auth
        // da API de forma confiável e isolada (sem passar pelo ApiAuthCookieHandler,
        // que exige escopo de componente Razor; e sem vazar cookie entre usuários).
        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies };
        if (env.IsDevelopment())
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var client = new HttpClient(handler) { BaseAddress = baseUri };
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.Name);

        using var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = password }, Json, ct);
        if (!response.IsSuccessStatusCode)
            return new LoginOutcome(false, null, null, "E-mail ou senha inválidos.");

        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(Json, ct);

        var apiCookie = cookies.GetCookies(baseUri)["DeskCore.Auth"];
        var authCookie = apiCookie is null ? null : $"{apiCookie.Name}={apiCookie.Value}";

        if (user is null || authCookie is null)
            return new LoginOutcome(false, null, null, "Falha inesperada no login.");

        return new LoginOutcome(true, user, authCookie, null);
    }

    public Task<OperationOutcome> RegisterAsync(string email, string fullName, string password, bool acceptedPrivacy, CancellationToken ct = default) =>
        PostAnonymous("api/auth/register", new RegisterRequest { Email = email, FullName = fullName, Password = password, AcceptedPrivacyPolicy = acceptedPrivacy }, ct);

    // ---- Account / LGPD (self-service do titular) ----
    public Task<AccountDataExport> ExportMyDataAsync(CancellationToken ct = default) =>
        Get<AccountDataExport>("api/account/export", ct);

    public Task UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default) =>
        PutNoContent("api/account/profile", request, ct);

    public Task DeleteMyAccountAsync(CancellationToken ct = default) =>
        PostNoContent("api/account/delete", ct);

    public Task<OperationOutcome> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default) =>
        PostAnonymous("api/auth/confirm-email", new ConfirmEmailRequest { UserId = userId, Token = token }, ct);

    public Task<OperationOutcome> ForgotPasswordAsync(string email, CancellationToken ct = default) =>
        PostAnonymous("api/auth/forgot-password", new ForgotPasswordRequest { Email = email }, ct);

    public Task<OperationOutcome> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default) =>
        PostAnonymous("api/auth/reset-password", new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = newPassword }, ct);

    /// <summary>POST anônimo num cliente descartável próprio (sem o handler de cookie, que exige escopo de componente).</summary>
    private async Task<OperationOutcome> PostAnonymous(string url, object body, CancellationToken ct)
    {
        var baseUri = http.BaseAddress ?? throw new InvalidOperationException("ApiSettings:BaseUrl não configurada.");
        using var handler = new HttpClientHandler();
        if (env.IsDevelopment())
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var client = new HttpClient(handler) { BaseAddress = baseUri };
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.Name);

        using var response = await client.PostAsJsonAsync(url, body, Json, ct);
        if (response.IsSuccessStatusCode)
            return new OperationOutcome(true, null);

        return new OperationOutcome(false, await ExtractMessage(response, ct));
    }

    public Task LogoutAsync(CancellationToken ct = default) => http.PostAsync("api/auth/logout", null, ct);

    public Task<CurrentUserResponse?> GetMeAsync(CancellationToken ct = default) =>
        GetOrNull<CurrentUserResponse>("api/auth/me", ct);

    // ---- Tickets ----
    public async Task<PagedResult<TicketListItemResponse>> ListTicketsAsync(
        TicketStatus? status, int page, int pageSize,
        string? number = null, string? title = null, string? description = null, string? requester = null,
        bool unseen = false,
        CancellationToken ct = default)
    {
        var url = $"api/tickets?page={page}&pageSize={pageSize}";
        if (status is not null) url += $"&status={status}";
        if (!string.IsNullOrWhiteSpace(number)) url += $"&number={Uri.EscapeDataString(number)}";
        if (!string.IsNullOrWhiteSpace(title)) url += $"&title={Uri.EscapeDataString(title)}";
        if (!string.IsNullOrWhiteSpace(description)) url += $"&description={Uri.EscapeDataString(description)}";
        if (!string.IsNullOrWhiteSpace(requester)) url += $"&requester={Uri.EscapeDataString(requester)}";
        if (unseen) url += "&unseen=true";
        return await Get<PagedResult<TicketListItemResponse>>(url, ct);
    }

    public Task<int> GetUnseenCountAsync(CancellationToken ct = default) =>
        Get<int>("api/tickets/unseen-count", ct);

    public Task<TicketResponse> GetTicketAsync(long id, CancellationToken ct = default) =>
        Get<TicketResponse>($"api/tickets/{id}", ct);

    public Task<TicketResponse> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default) =>
        Post<TicketResponse>("api/tickets", request, ct);

    public Task<TicketResponse> UpdateTicketAsync(long id, UpdateTicketRequest request, CancellationToken ct = default) =>
        Put<TicketResponse>($"api/tickets/{id}", request, ct);

    public Task<TicketResponse> AssignTicketAsync(long id, string assignedToUserId, CancellationToken ct = default) =>
        Post<TicketResponse>($"api/tickets/{id}/assign", new AssignTicketRequest { AssignedToUserId = assignedToUserId }, ct);

    public Task<TicketResponse> ChangeStatusAsync(long id, TicketStatus status, CancellationToken ct = default) =>
        Post<TicketResponse>($"api/tickets/{id}/status", new ChangeStatusRequest { Status = status }, ct);

    public Task<TicketResponse> ChangePriorityAsync(long id, TicketPriority priority, CancellationToken ct = default) =>
        Post<TicketResponse>($"api/tickets/{id}/priority", new ChangePriorityRequest { Priority = priority }, ct);

    public Task<TicketResponse> CloseTicketAsync(long id, CancellationToken ct = default) =>
        Post<TicketResponse>($"api/tickets/{id}/close", null, ct);

    public Task<TicketResponse> CancelTicketAsync(long id, CancellationToken ct = default) =>
        Post<TicketResponse>($"api/tickets/{id}/cancel", null, ct);

    // ---- Comments ----
    public Task<IReadOnlyList<CommentResponse>> ListCommentsAsync(long ticketId, CancellationToken ct = default) =>
        Get<IReadOnlyList<CommentResponse>>($"api/tickets/{ticketId}/comments", ct);

    public Task<CommentResponse> AddCommentAsync(long ticketId, CreateCommentRequest request, CancellationToken ct = default) =>
        Post<CommentResponse>($"api/tickets/{ticketId}/comments", request, ct);

    // ---- Attachments ----
    public Task<IReadOnlyList<AttachmentResponse>> ListAttachmentsAsync(long ticketId, CancellationToken ct = default) =>
        Get<IReadOnlyList<AttachmentResponse>>($"api/tickets/{ticketId}/attachments", ct);

    public async Task<AttachmentResponse> UploadAttachmentAsync(long ticketId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var response = await http.PostAsync($"api/tickets/{ticketId}/attachments", form, ct);
        return await ReadOrThrow<AttachmentResponse>(response, ct);
    }

    public async Task<(Stream Content, string ContentType, string FileName)?> DownloadAttachmentAsync(long ticketId, long attachmentId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/tickets/{ticketId}/attachments/{attachmentId}/download", HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"anexo-{attachmentId}";
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return (stream, contentType, fileName);
    }

    public Task DeleteAttachmentAsync(long ticketId, long attachmentId, CancellationToken ct = default) =>
        Delete($"api/tickets/{ticketId}/attachments/{attachmentId}", ct);

    // ---- Categories ----
    public Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(bool all = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<CategoryResponse>>($"api/categories?all={all.ToString().ToLowerInvariant()}", ct);

    public Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default) =>
        Post<CategoryResponse>("api/categories", request, ct);

    public Task<CategoryResponse> UpdateCategoryAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default) =>
        Put<CategoryResponse>($"api/categories/{id}", request, ct);

    public Task ActivateCategoryAsync(long id, CancellationToken ct = default) =>
        PostNoContent($"api/categories/{id}/activate", ct);

    public Task DeactivateCategoryAsync(long id, CancellationToken ct = default) =>
        PostNoContent($"api/categories/{id}/deactivate", ct);

    // ---- Users ----
    public Task<PagedResult<UserResponse>> ListUsersAsync(int page, int pageSize, CancellationToken ct = default) =>
        Get<PagedResult<UserResponse>>($"api/users?page={page}&pageSize={pageSize}", ct);

    public Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        Post<UserResponse>("api/users", request, ct);

    public Task<UserResponse> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken ct = default) =>
        Put<UserResponse>($"api/users/{id}", request, ct);

    public Task<UserResponse> SetUserRolesAsync(string id, SetRolesRequest request, CancellationToken ct = default) =>
        Post<UserResponse>($"api/users/{id}/roles", request, ct);

    public Task SetUserPasswordAsync(string id, SetUserPasswordRequest request, CancellationToken ct = default) =>
        PostNoContent($"api/users/{id}/set-password", request, ct);

    public Task ActivateUserAsync(string id, CancellationToken ct = default) =>
        PostNoContent($"api/users/{id}/activate", ct);

    public Task DeactivateUserAsync(string id, CancellationToken ct = default) =>
        PostNoContent($"api/users/{id}/deactivate", ct);

    // ---- Audit ----
    public Task<IReadOnlyList<TicketAuditLogResponse>> GetTicketAuditAsync(long ticketId, CancellationToken ct = default) =>
        Get<IReadOnlyList<TicketAuditLogResponse>>($"api/tickets/{ticketId}/audit", ct);

    public Task<IReadOnlyList<RecentActivityResponse>> GetRecentActivityAsync(int take = 10, CancellationToken ct = default) =>
        Get<IReadOnlyList<RecentActivityResponse>>($"api/audit/recent?take={take}", ct);

    // ---- Metrics ----
    public Task<MetricsOverviewResponse> GetMetricsOverviewAsync(CancellationToken ct = default) =>
        Get<MetricsOverviewResponse>("api/metrics/overview", ct);

    // ---- Helpers ----
    private async Task<T> Get<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        return await ReadOrThrow<T>(response, ct);
    }

    private async Task<T?> GetOrNull<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return default;
        return await ReadOrThrow<T>(response, ct);
    }

    private async Task<T> Post<T>(string url, object? body, CancellationToken ct)
    {
        using var response = body is null
            ? await http.PostAsync(url, null, ct)
            : await http.PostAsJsonAsync(url, body, Json, ct);
        return await ReadOrThrow<T>(response, ct);
    }

    private async Task<T> Put<T>(string url, object body, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync(url, body, Json, ct);
        return await ReadOrThrow<T>(response, ct);
    }

    private async Task PostNoContent(string url, CancellationToken ct)
    {
        using var response = await http.PostAsync(url, null, ct);
        await EnsureOrThrow(response, ct);
    }

    private async Task PostNoContent(string url, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, body, Json, ct);
        await EnsureOrThrow(response, ct);
    }

    private async Task PutNoContent(string url, object body, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync(url, body, Json, ct);
        await EnsureOrThrow(response, ct);
    }

    private async Task Delete(string url, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(url, ct);
        await EnsureOrThrow(response, ct);
    }

    private static async Task<T> ReadOrThrow<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureOrThrow(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new ApiException((int)response.StatusCode, "Resposta vazia da API.");
    }

    private static async Task EnsureOrThrow(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = await ExtractMessage(response, ct);
        throw new ApiException((int)response.StatusCode, message);
    }

    private static async Task<string> ExtractMessage(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString()!;
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString()!;
        }
        catch
        {
            // resposta sem corpo JSON
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sessão expirada. Faça login novamente.",
            HttpStatusCode.Forbidden => "Você não tem permissão para esta ação.",
            HttpStatusCode.NotFound => "Recurso não encontrado.",
            HttpStatusCode.TooManyRequests => "Muitas requisições. Aguarde um momento.",
            _ => "Ocorreu um erro ao comunicar com o servidor."
        };
    }
}
