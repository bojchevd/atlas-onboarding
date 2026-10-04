using Atlas.Domain;
using Atlas.Onboarding;

namespace Atlas.Tests;

public class VerificationProcessorTests
{
    [Fact]
    public async Task Possible_sanctions_match_leaves_the_application_in_PendingManualReview_even_when_processed_again()
    {
        var store = new InMemoryApplicationStore();
        var stateMachine = new ApplicationStateMachine(TimeProvider.System);
        var processor = new VerificationProcessor(store, new PossibleMatchScreening(), stateMachine);

        var application = new Application
        {
            Market = MarketCode.MA,
            CreatedAt = DateTimeOffset.UtcNow,
            Identity = new IdentityDocument(IdentityDocumentType.NationalIdentifier, "1234567890123"),
        };
        Assert.True(stateMachine.TryApplyAutomated(application, ApplicationState.Submitted));
        await store.Save(application, CancellationToken.None);

        await processor.Process(application.Id, CancellationToken.None);
        await processor.Process(application.Id, CancellationToken.None);

        var stored = await store.Get(application.Id, CancellationToken.None);
        Assert.Equal(ApplicationState.PendingManualReview, stored!.State);
    }

    private sealed class PossibleMatchScreening : IScreeningProvider
    {
        public Task<ScreeningResult> Screen(Application application, CancellationToken ct) =>
            Task.FromResult(ScreeningResult.PossibleMatch);
    }
}
