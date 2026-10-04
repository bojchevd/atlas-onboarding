namespace Atlas.Domain;

public sealed record MarketConfiguration
{
    public required MarketCode Market { get; init; }

    public required ActivationPolicy ActivationPolicy { get; init; }

    // TODO: replace with the identifier validation strategy once it is designed.
    public object? IdentifierValidationRule { get; init; }
}
