using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Application.Mapping;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Categories;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> ListActiveAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<CategoryResponse>>> ListAllAsync(CancellationToken ct = default);
    Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<CategoryResponse>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<Result> ActivateAsync(long id, CancellationToken ct = default);
    Task<Result> DeactivateAsync(long id, CancellationToken ct = default);
}

public sealed class CategoryService(IAppDbContext db, ICurrentUser currentUser) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> ListActiveAsync(CancellationToken ct = default)
    {
        var items = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return items.Select(c => c.ToResponse()).ToList();
    }

    public async Task<Result<IReadOnlyList<CategoryResponse>>> ListAllAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<IReadOnlyList<CategoryResponse>>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var items = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return Result<IReadOnlyList<CategoryResponse>>.Success(items.Select(c => c.ToResponse()).ToList());
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<CategoryResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var name = request.Name.Trim();
        if (await NameExistsAsync(name, null, ct))
            return Result<CategoryResponse>.Conflict(Msg.T("Já existe uma categoria com esse nome.", "A category with this name already exists."));

        var now = DateTime.UtcNow;
        var category = new TicketCategory
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(category.ToResponse());
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<CategoryResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return Result<CategoryResponse>.NotFound(Msg.T("Categoria não encontrada.", "Category not found."));

        var name = request.Name.Trim();
        if (await NameExistsAsync(name, id, ct))
            return Result<CategoryResponse>.Conflict(Msg.T("Já existe uma categoria com esse nome.", "A category with this name already exists."));

        category.Name = name;
        category.Description = request.Description?.Trim();
        category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(category.ToResponse());
    }

    public Task<Result> ActivateAsync(long id, CancellationToken ct = default) => SetActiveAsync(id, true, ct);
    public Task<Result> DeactivateAsync(long id, CancellationToken ct = default) => SetActiveAsync(id, false, ct);

    private async Task<Result> SetActiveAsync(long id, bool active, CancellationToken ct)
    {
        if (!currentUser.IsAdmin())
            return Result.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return Result.NotFound(Msg.T("Categoria não encontrada.", "Category not found."));

        category.IsActive = active;
        category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> NameExistsAsync(string name, long? excludeId, CancellationToken ct)
    {
        var lower = name.ToLowerInvariant();
        return await db.Categories.AnyAsync(
            c => c.Name.ToLower() == lower && (excludeId == null || c.Id != excludeId), ct);
    }
}
