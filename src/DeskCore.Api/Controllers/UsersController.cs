using DeskCore.Api.Authorization;
using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Common;
using DeskCore.Shared.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeskCore.Api.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
[EnableRateLimiting("admin")]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Resolve(await users.ListAsync(page, pageSize, ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> Get(string id, CancellationToken ct)
        => Resolve(await users.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
        => Resolve(await users.CreateAsync(request, ct));

    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponse>> Update(string id, UpdateUserRequest request, CancellationToken ct)
        => Resolve(await users.UpdateAsync(id, request, ct));

    [HttpPost("{id}/roles")]
    public async Task<ActionResult<UserResponse>> SetRoles(string id, SetRolesRequest request, CancellationToken ct)
        => Resolve(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id}/set-password")]
    public async Task<IActionResult> SetPassword(string id, SetUserPasswordRequest request, CancellationToken ct)
        => Resolve(await users.SetPasswordAsync(id, request, ct));

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(string id, CancellationToken ct)
        => Resolve(await users.ActivateAsync(id, ct));

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
        => Resolve(await users.DeactivateAsync(id, ct));
}
