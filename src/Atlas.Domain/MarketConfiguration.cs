namespace Atlas.Domain;

/// <summary>
/// Per-market settings. Market differences are data in <see cref="Markets"/>, not branches in code:
/// adding a market is a new entry, not a new <c>case</c>.
/// </summary>
public sealed record MarketConfiguration
{
    public required MarketCode Market { get; init; }

    public required ActivationPolicy ActivationPolicy { get; init; }

    public required IIdentifierRule IdentifierRule { get; init; }

    // MB, MC and ME are deliberately absent until they are specified and signed off (Annex B note 3).
    private static readonly IReadOnlyDictionary<MarketCode, MarketConfiguration> Markets =
        new Dictionary<MarketCode, MarketConfiguration>
        {
            [MarketCode.MA] = new()
            {
                Market = MarketCode.MA,
                ActivationPolicy = ActivationPolicy.Remote,
                IdentifierRule = new NationalIdentifierDigitsRule(13),
            },
            [MarketCode.MD] = new()
            {
                Market = MarketCode.MD,
                ActivationPolicy = ActivationPolicy.InPersonWithWetSignature,
                IdentifierRule = new NationalIdentifierDigitsRule(10),
            },
            [MarketCode.MF] = new()
            {
                Market = MarketCode.MF,
                ActivationPolicy = ActivationPolicy.Remote,
                IdentifierRule = new NationalIdentifierOrPassportRule(),
            },
        };

    public static MarketConfiguration For(MarketCode market) =>
        Markets.TryGetValue(market, out var configuration)
            ? configuration
            : throw new NotSupportedException($"Market {market} is not yet configured.");
}

/// <summary>Checks a customer's identity document against one market's identifier rule.</summary>
public interface IIdentifierRule
{
    IdentifierValidationResult Validate(IdentityDocument? identity);
}

/// <summary>
/// Outcome of an identifier check. <see cref="Failure"/> describes the rule that was broken and
/// never echoes the submitted value, since it may end up in a response or a log.
/// </summary>
public sealed record IdentifierValidationResult(bool IsValid, string? Failure)
{
    public static readonly IdentifierValidationResult Valid = new(true, null);

    public static IdentifierValidationResult Invalid(string failure) => new(false, failure);
}

/// <summary>A national identifier is required and must be exactly <c>digits</c> ASCII digits.</summary>
public sealed class NationalIdentifierDigitsRule(int digits) : IIdentifierRule
{
    public IdentifierValidationResult Validate(IdentityDocument? identity)
    {
        if (identity is not { Type: IdentityDocumentType.NationalIdentifier })
        {
            return IdentifierValidationResult.Invalid("A national identifier is required.");
        }

        return identity.Value.Length == digits && identity.Value.All(char.IsAsciiDigit)
            ? IdentifierValidationResult.Valid
            : IdentifierValidationResult.Invalid($"National identifier must be exactly {digits} digits.");
    }
}

/// <summary>
/// A national identifier, or a passport number for residents who were never issued one:
/// 6 to 12 ASCII letters or digits. Annex B gives no format for the national identifier
/// in this case, so it is only required to be non-blank.
/// </summary>
public sealed class NationalIdentifierOrPassportRule : IIdentifierRule
{
    public IdentifierValidationResult Validate(IdentityDocument? identity) => identity switch
    {
        null => IdentifierValidationResult.Invalid("A national identifier or passport number is required."),

        { Type: IdentityDocumentType.NationalIdentifier } when !string.IsNullOrWhiteSpace(identity.Value) =>
            IdentifierValidationResult.Valid,

        { Type: IdentityDocumentType.Passport } when identity.Value.Length is >= 6 and <= 12
                                                     && identity.Value.All(char.IsAsciiLetterOrDigit) =>
            IdentifierValidationResult.Valid,

        { Type: IdentityDocumentType.Passport } =>
            IdentifierValidationResult.Invalid("Passport number must be 6 to 12 letters or digits."),

        _ => IdentifierValidationResult.Invalid("National identifier must not be blank."),
    };
}
