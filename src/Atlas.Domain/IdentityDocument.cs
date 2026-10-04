namespace Atlas.Domain;

/// <summary>
/// The document a customer identifies with: a national identifier or a passport.
/// Not every customer has a national identifier.
/// </summary>
public sealed record IdentityDocument(IdentityDocumentType Type, string Value)
{
    // Value is fully redacted, never masked: logs go to Seq, which has no access
    // control, retention or residency guarantees, so an identifier there is personal
    // data outside the compliance controls. Correlate by the application's surrogate Id.
    public override string ToString() => $"IdentityDocument {{ Type = {Type} }}";
}
