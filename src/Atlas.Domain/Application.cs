namespace Atlas.Domain;

/// <summary>
/// A customer onboarding application. Identified only by its server-generated
/// surrogate <see cref="Id"/>; the national identifier is never a key.
/// </summary>
public sealed class Application
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required MarketCode Market { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // internal set: only Atlas.Domain (in practice, ApplicationStateMachine) can change
    // these. Code in the services can read state but has no way to assign it.
    public ApplicationState State { get; internal set; } = ApplicationState.Draft;

    /// <summary>Null until the customer provides it.</summary>
    public IdentityDocument? Identity { get; set; }

    public DateTimeOffset? SubmittedAt { get; internal set; }

    /// <summary>The compliance officer who resolved a manual review, if one was needed.</summary>
    public string? ReviewedBy { get; internal set; }

    public DateTimeOffset? ReviewedAt { get; internal set; }

    public List<string> DocumentReferences { get; } = [];
}
