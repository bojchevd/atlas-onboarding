namespace Atlas.Domain;

/// <summary>
/// The only code that changes <see cref="Application.State"/>. There are two routes:
/// <see cref="TryApplyAutomated"/> for every automated caller, which can never leave
/// <see cref="ApplicationState.PendingManualReview"/>, and <see cref="ApplyComplianceDecision"/>,
/// which is the only way out of manual review and requires an attributable officer.
/// </summary>
public sealed class ApplicationStateMachine(TimeProvider clock)
{
    // PendingManualReview has no entry: no automated transition out of it exists to be allowed.
    private static readonly IReadOnlyDictionary<ApplicationState, ApplicationState[]> AutomatedTransitions =
        new Dictionary<ApplicationState, ApplicationState[]>
        {
            [ApplicationState.Draft] = [ApplicationState.Submitted],
            [ApplicationState.Submitted] = [ApplicationState.Verifying],
            [ApplicationState.Verifying] =
                [ApplicationState.Approved, ApplicationState.PendingManualReview, ApplicationState.Rejected],
            [ApplicationState.Approved] = [ApplicationState.Activated, ApplicationState.PendingBranchVisit],
            [ApplicationState.PendingBranchVisit] = [ApplicationState.Activated],
        };

    /// <summary>
    /// Moves <paramref name="application"/> to <paramref name="target"/> if the transition is allowed
    /// for automated code. Returns false and leaves the application unchanged otherwise.
    /// </summary>
    public bool TryApplyAutomated(Application application, ApplicationState target)
    {
        if (!AutomatedTransitions.TryGetValue(application.State, out var allowed) || !allowed.Contains(target))
        {
            return false;
        }

        if (application.State == ApplicationState.Approved && target != ActivationTarget(application.Market))
        {
            return false;
        }

        if (target == ApplicationState.Submitted)
        {
            application.SubmittedAt = clock.GetUtcNow();
        }

        application.State = target;
        return true;
    }

    /// <summary>
    /// Resolves a manual review. The only route out of <see cref="ApplicationState.PendingManualReview"/>.
    /// </summary>
    /// <param name="officerId">The compliance officer making the decision (Compliance §2: every access
    /// must be attributable to the person who made it). Recorded on the application.</param>
    public void ApplyComplianceDecision(Application application, ComplianceDecision decision, string officerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officerId);

        if (application.State != ApplicationState.PendingManualReview)
        {
            throw new InvalidOperationException(
                $"Application {application.Id} is {application.State}, not {ApplicationState.PendingManualReview}.");
        }

        application.State = decision switch
        {
            ComplianceDecision.Approve => ApplicationState.Approved,
            ComplianceDecision.Reject => ApplicationState.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null),
        };
        application.ReviewedBy = officerId;
        application.ReviewedAt = clock.GetUtcNow();
    }

    /// <summary>Where an approved application goes next under its market's activation policy.</summary>
    public static ApplicationState ActivationTarget(MarketCode market) =>
        MarketConfiguration.For(market).ActivationPolicy == ActivationPolicy.InPersonWithWetSignature
            ? ApplicationState.PendingBranchVisit
            : ApplicationState.Activated;
}
