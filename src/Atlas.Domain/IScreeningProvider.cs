namespace Atlas.Domain;

/// <summary>Sanctions and PEP screening of an applicant.</summary>
public interface IScreeningProvider
{
    Task<ScreeningResult> Screen(Application application, CancellationToken ct);
}

public enum ScreeningResult
{
    Clear,

    /// <summary>Needs a compliance officer's manual review (Compliance §3); never decided automatically.</summary>
    PossibleMatch,
}
