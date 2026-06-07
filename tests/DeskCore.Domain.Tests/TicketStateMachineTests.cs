using DeskCore.Domain.Tickets;
using DeskCore.Shared.Enums;
using Shouldly;
using Xunit;

namespace DeskCore.Domain.Tests;

public class TicketStateMachineTests
{
    [Fact]
    public void InitialStatus_IsWaitingAgent()
    {
        TicketStateMachine.InitialStatus.ShouldBe(TicketStatus.WaitingAgent);
    }

    [Theory]
    // Open
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Open, TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.Open, TicketStatus.Canceled)]
    // WaitingAgent
    [InlineData(TicketStatus.WaitingAgent, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingAgent, TicketStatus.Canceled)]
    // InProgress
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingUser)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Canceled)]
    // WaitingUser
    [InlineData(TicketStatus.WaitingUser, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingUser, TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.WaitingUser, TicketStatus.Resolved)]
    [InlineData(TicketStatus.WaitingUser, TicketStatus.Closed)]
    [InlineData(TicketStatus.WaitingUser, TicketStatus.Canceled)]
    // Resolved
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Canceled)]
    public void CanTransition_AllowsValidTransitions(TicketStatus from, TicketStatus to)
    {
        TicketStateMachine.CanTransition(from, to).ShouldBeTrue();
    }

    [Theory]
    // Terminais não transicionam para nada
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.Canceled, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Canceled, TicketStatus.Open)]
    // Saltos inválidos
    [InlineData(TicketStatus.WaitingAgent, TicketStatus.Resolved)]
    [InlineData(TicketStatus.WaitingAgent, TicketStatus.Closed)]
    [InlineData(TicketStatus.WaitingAgent, TicketStatus.WaitingUser)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.Resolved, TicketStatus.WaitingUser)]
    public void CanTransition_RejectsInvalidTransitions(TicketStatus from, TicketStatus to)
    {
        TicketStateMachine.CanTransition(from, to).ShouldBeFalse();
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingUser)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void CanTransition_RejectsTransitionToSameStatus(TicketStatus status)
    {
        TicketStateMachine.CanTransition(status, status).ShouldBeFalse();
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void IsTerminal_TrueForClosedAndCanceled(TicketStatus status)
    {
        TicketStateMachine.IsTerminal(status).ShouldBeTrue();
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.WaitingAgent)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingUser)]
    [InlineData(TicketStatus.Resolved)]
    public void IsTerminal_FalseForNonTerminalStatuses(TicketStatus status)
    {
        TicketStateMachine.IsTerminal(status).ShouldBeFalse();
    }

    [Fact]
    public void AllowedNext_ReturnsExpectedSetForInProgress()
    {
        TicketStateMachine.AllowedNext(TicketStatus.InProgress)
            .ShouldBe(new[]
            {
                TicketStatus.WaitingUser,
                TicketStatus.WaitingAgent,
                TicketStatus.Resolved,
                TicketStatus.Closed,
                TicketStatus.Canceled
            }, ignoreOrder: true);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void AllowedNext_IsEmptyForTerminalStatuses(TicketStatus status)
    {
        TicketStateMachine.AllowedNext(status).ShouldBeEmpty();
    }

    [Fact]
    public void AllowedNext_NeverContainsTheStatusItself()
    {
        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
            TicketStateMachine.AllowedNext(status).ShouldNotContain(status);
    }

    [Fact]
    public void UserClosableStatuses_AreResolvedAndWaitingUser()
    {
        TicketStateMachine.UserClosableStatuses
            .ShouldBe(new[] { TicketStatus.Resolved, TicketStatus.WaitingUser }, ignoreOrder: true);
    }
}
