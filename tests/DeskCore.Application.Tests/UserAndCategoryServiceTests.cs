using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Domain.Constants;
using DeskCore.Shared.Contracts.Categories;
using DeskCore.Shared.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class UserAndCategoryServiceTests(DatabaseFixture fixture)
{
    // ---- UserService ----

    [Fact]
    public async Task User_List_AsNonAdmin_ReturnsForbidden()
    {
        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var result = await service.ListAsync(1, 50);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task User_Deactivate_Self_ReturnsValidation()
    {
        await using var scope = fixture.NewScope(fixture.Admin);
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var result = await service.DeactivateAsync(fixture.Admin.Id);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task User_RemoveOwnAdminRole_ReturnsValidation()
    {
        await using var scope = fixture.NewScope(fixture.Admin);
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var result = await service.SetRolesAsync(fixture.Admin.Id, new SetRolesRequest { Roles = [Roles.Agent] });

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task User_Create_AsAdmin_Succeeds()
    {
        await using var scope = fixture.NewScope(fixture.Admin);
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var email = $"novo-{Guid.NewGuid():N}@deskcore.test";
        var result = await service.CreateAsync(new CreateUserRequest
        {
            Email = email,
            FullName = "Novo Atendente",
            Password = "NovaSenha@123456",
            Role = Roles.Agent
        });

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.Email.ShouldBe(email);
        result.Value.Roles.ShouldContain(Roles.Agent);
    }

    // ---- CategoryService ----

    [Fact]
    public async Task Category_Create_AsNonAdmin_ReturnsForbidden()
    {
        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        var result = await service.CreateAsync(new CreateCategoryRequest { Name = "Tentativa" });

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Category_Create_DuplicateName_ReturnsConflict()
    {
        await using var scope = fixture.NewScope(fixture.Admin);
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        var name = $"Categoria {Guid.NewGuid():N}";
        var first = await service.CreateAsync(new CreateCategoryRequest { Name = name });
        first.Succeeded.ShouldBeTrue(first.Error?.Message);

        var duplicate = await service.CreateAsync(new CreateCategoryRequest { Name = name });

        duplicate.Failed.ShouldBeTrue();
        duplicate.Error!.Type.ShouldBe(ErrorType.Conflict);
    }
}
