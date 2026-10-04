namespace Atlas.Domain;

/// <summary>
/// Verifies one submitted application: screens it and moves it to its outcome through the
/// state machine. No host dependency, so it runs the same under a polling loop or a message consumer.
/// </summary>
public sealed class VerificationProcessor(
    IApplicationStore store,
    IScreeningProvider screening,
    ApplicationStateMachine stateMachine)
{
    public async Task Process(Guid applicationId, CancellationToken ct)
    {
        var application = await store.Get(applicationId, ct)
            ?? throw new InvalidOperationException($"Application {applicationId} does not exist.");

        // Anything not in Submitted has already been picked up; skipping makes redelivery harmless.
        if (!stateMachine.TryApplyAutomated(application, ApplicationState.Verifying))
        {
            return;
        }

        await store.Save(application, ct);

        var result = await screening.Screen(application, ct);

        if (result == ScreeningResult.PossibleMatch)
        {
            Move(application, ApplicationState.PendingManualReview);
        }
        else
        {
            Move(application, ApplicationState.Approved);
            Move(application, ApplicationStateMachine.ActivationTarget(application.Market));
        }

        await store.Save(application, ct);
    }

    private void Move(Application application, ApplicationState target)
    {
        if (!stateMachine.TryApplyAutomated(application, target))
        {
            throw new InvalidOperationException(
                $"Application {application.Id} cannot move from {application.State} to {target}.");
        }
    }
}
