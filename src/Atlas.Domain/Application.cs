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

    public ApplicationState State { get; set; } = ApplicationState.Draft;

    /// <summary>Null until the customer provides it.</summary>
    public IdentityDocument? Identity { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public List<string> DocumentReferences { get; } = [];
}
