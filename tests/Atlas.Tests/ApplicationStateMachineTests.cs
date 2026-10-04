using Atlas.Domain;

namespace Atlas.Tests;

public class ApplicationStateMachineTests
{
    private readonly ApplicationStateMachine _stateMachine = new(TimeProvider.System);

    [Fact]
    public void Automated_code_cannot_move_an_application_out_of_PendingManualReview_to_any_state()
    {
        Assert.All(Enum.GetValues<ApplicationState>(), target =>
        {
            var application = NewApplicationIn(MarketCode.MA, ApplicationState.PendingManualReview);

            var moved = _stateMachine.TryApplyAutomated(application, target);

            Assert.False(moved, $"automated transition PendingManualReview -> {target} was allowed");
            Assert.Equal(ApplicationState.PendingManualReview, application.State);
        });
    }

    [Fact]
    public void Approved_application_in_a_wet_signature_market_goes_to_PendingBranchVisit_not_Activated()
    {
        var application = NewApplicationIn(MarketCode.MD, ApplicationState.Approved);

        var activated = _stateMachine.TryApplyAutomated(application, ApplicationState.Activated);

        Assert.False(activated);
        Assert.Equal(ApplicationState.Approved, application.State);

        var sentToBranch = _stateMachine.TryApplyAutomated(application, ApplicationState.PendingBranchVisit);

        Assert.True(sentToBranch);
        Assert.Equal(ApplicationState.PendingBranchVisit, application.State);
    }

    // Reaches the starting state through the same automated transitions production code uses,
    // because State cannot be assigned from outside Atlas.Domain.
    private Application NewApplicationIn(MarketCode market, ApplicationState verifyingOutcome)
    {
        var application = new Application { Market = market, CreatedAt = DateTimeOffset.UtcNow };

        foreach (var step in new[] { ApplicationState.Submitted, ApplicationState.Verifying, verifyingOutcome })
        {
            Assert.True(_stateMachine.TryApplyAutomated(application, step), $"setup could not reach {step}");
        }

        return application;
    }
}
