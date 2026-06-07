using DeskCore.Api.Authorization;
using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeskCore.Api.Controllers;

[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categories) : ApiControllerBase
{
    /// <summary>Categorias ativas (todos os usuários). Com <c>?all=true</c> e perfil Admin, inclui inativas.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List([FromQuery] bool all = false, CancellationToken ct = default)
    {
        if (all)
            return Resolve(await categories.ListAllAsync(ct));

        return Ok(await categories.ListActiveAsync(ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken ct)
        => Resolve(await categories.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<CategoryResponse>> Update(long id, UpdateCategoryRequest request, CancellationToken ct)
        => Resolve(await categories.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/activate")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
        => Resolve(await categories.ActivateAsync(id, ct));

    [HttpPost("{id:long}/deactivate")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
        => Resolve(await categories.DeactivateAsync(id, ct));
}
