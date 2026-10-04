using Atlas.Domain;

namespace Atlas.Onboarding;

/// <summary>
/// Deterministic stand-in for World-Check so a reviewer can trigger both paths: an identifier
/// starting with 9 is a possible match, anything else is clear. The delay makes the 202 visibly
/// precede the outcome.
/// </summary>
public sealed class StubScreeningProvider : IScreeningProvider
{
    public async Task<ScreeningResult> Screen(Application application, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        return application.Identity?.Value.StartsWith('9') == true
            ? ScreeningResult.PossibleMatch
            : ScreeningResult.Clear;
    }
}
