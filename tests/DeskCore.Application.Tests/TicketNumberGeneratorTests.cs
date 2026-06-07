using DeskCore.Application.Abstractions.Tickets;
using DeskCore.Application.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class TicketNumberGeneratorTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Next_FormatsWithPrefixAndPadding()
    {
        await using var scope = fixture.NewAnonymousScope();
        var generator = scope.ServiceProvider.GetRequiredService<ITicketNumberGenerator>();

        var number = await generator.NextAsync();

        number.ShouldStartWith("TK-");
        number.Length.ShouldBe("TK-".Length + 6);
    }

    [Fact]
    public async Task Next_UnderConcurrency_ProducesUniqueNumbers()
    {
        const int parallelism = 50;

        // Cada task usa o próprio escopo/DbContext (DbContext não é thread-safe).
        var tasks = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await using var scope = fixture.NewAnonymousScope();
            var generator = scope.ServiceProvider.GetRequiredService<ITicketNumberGenerator>();
            return await generator.NextAsync();
        });

        var numbers = await Task.WhenAll(tasks);

        numbers.Distinct().Count().ShouldBe(parallelism);
        numbers.ShouldAllBe(n => n.StartsWith("TK-"));
    }
}
