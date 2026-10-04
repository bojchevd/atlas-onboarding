using Atlas.Domain;

namespace Atlas.Tests;

public class MarketConfigurationTests
{
    [Fact]
    public void MA_national_identifier_must_be_exactly_13_digits_so_12_is_rejected_and_13_accepted()
    {
        var rule = MarketConfiguration.For(MarketCode.MA).IdentifierRule;

        var twelveDigits = rule.Validate(new IdentityDocument(IdentityDocumentType.NationalIdentifier, "123456789012"));
        var thirteenDigits = rule.Validate(new IdentityDocument(IdentityDocumentType.NationalIdentifier, "1234567890123"));

        Assert.False(twelveDigits.IsValid);
        Assert.Equal("National identifier must be exactly 13 digits.", twelveDigits.Failure);
        Assert.True(thirteenDigits.IsValid);
        Assert.Null(thirteenDigits.Failure);
    }

    [Fact]
    public void MF_accepts_a_passport_number_from_a_customer_with_no_national_identifier()
    {
        var rule = MarketConfiguration.For(MarketCode.MF).IdentifierRule;

        var result = rule.Validate(new IdentityDocument(IdentityDocumentType.Passport, "X1234567"));

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
    }
}
