namespace Atlas.Domain;

/// <summary>
/// Persistence for applications. Async with an explicit <see cref="Save"/> even where the
/// implementation needs neither, so a database-backed store is a drop-in rather than a change
/// at every call site.
/// </summary>
public interface IApplicationStore
{
    Task<Application?> Get(Guid id, CancellationToken ct);

    Task Save(Application application, CancellationToken ct);

    Task<IReadOnlyList<Application>> FindByState(ApplicationState state, CancellationToken ct);
}
