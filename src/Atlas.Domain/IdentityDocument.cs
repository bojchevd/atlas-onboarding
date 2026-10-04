namespace Atlas.Domain;

/// <summary>
/// The document a customer identifies with: a national identifier or a passport.
/// Not every customer has a national identifier.
/// </summary>
public sealed record IdentityDocument(IdentityDocumentType Type, string Value);
