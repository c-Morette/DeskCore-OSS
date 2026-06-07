using DeskCore.Domain.Tickets;
using Shouldly;
using Xunit;

namespace DeskCore.Domain.Tests;

public class TicketNumberTests
{
    [Theory]
    [InlineData(1, "TK-000001")]
    [InlineData(42, "TK-000042")]
    [InlineData(999999, "TK-999999")]
    public void Format_PadsToSixDigitsWithPrefix(long value, string expected)
    {
        TicketNumber.Format(value).ShouldBe(expected);
    }

    [Fact]
    public void Format_WithMoreThanSixDigits_DoesNotTruncate()
    {
        TicketNumber.Format(1_000_000).ShouldBe("TK-1000000");
    }

    [Fact]
    public void Constants_AreStable()
    {
        TicketNumber.Prefix.ShouldBe("TK-");
        TicketNumber.Digits.ShouldBe(6);
    }
}
