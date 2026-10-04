using System.Collections.Concurrent;
using Atlas.Domain;

namespace Atlas.Onboarding;

/// <summary>
/// Process-local store for tonight's slice. Holds object references, so a change is visible
/// before <see cref="Save"/> is called; callers must still call it, because the SQL store will not
/// be so forgiving.
/// </summary>
public sealed class InMemoryApplicationStore : IApplicationStore
{
    private readonly ConcurrentDictionary<Guid, Application> _applications = new();

    public Task<Application?> Get(Guid id, CancellationToken ct) =>
        Task.FromResult(_applications.GetValueOrDefault(id));

    public Task Save(Application application, CancellationToken ct)
    {
        _applications[application.Id] = application;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Application>> FindByState(ApplicationState state, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Application>>(
            _applications.Values.Where(application => application.State == state).ToList());
}
