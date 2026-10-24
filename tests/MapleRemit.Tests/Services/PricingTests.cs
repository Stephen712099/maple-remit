using System.Globalization;
using MapleRemit.Api.Services;

namespace MapleRemit.Tests.Services;

public class PricingTests
{
    // InlineData cannot hold decimals, so cases are strings.
    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("231.00", "3.47")]   // 3.465 rounds half up
    [InlineData("10.00", "2.99")]    // minimum fee
    [InlineData("199.66", "2.99")]   // 2.9949 rounds to 2.99, minimum applies
    [InlineData("199.67", "3.00")]   // 2.99505 rounds to 3.00, percentage applies
    [InlineData("3000.00", "45.00")]
    public void Fee_is_1_5_percent_with_minimum_rounded_half_up(string amountCad, string expectedFee)
    {
        Assert.Equal(D(expectedFee), Pricing.Fee(D(amountCad)));
    }

    [Theory]
    [InlineData("231.00", "24.50", "5659.50")]
    [InlineData("10.01", "24.50", "245.25")] // 245.245 rounds half up
    public void AmountThb_is_amount_times_rate_rounded_half_up(string amountCad, string rate, string expectedThb)
    {
        Assert.Equal(D(expectedThb), Pricing.AmountThb(D(amountCad), D(rate)));
    }

    [Theory]
    [InlineData("10.00")]
    [InlineData("3000.00")]
    public void Amounts_at_the_limits_are_valid(string amountCad)
    {
        Assert.True(Pricing.IsValidAmount(D(amountCad)));
    }

    [Theory]
    [InlineData("9.99")]
    [InlineData("3000.01")]
    [InlineData("10.001")] // more than 2 decimal places
    public void Amounts_outside_the_limits_or_with_sub_cent_precision_are_invalid(string amountCad)
    {
        Assert.False(Pricing.IsValidAmount(D(amountCad)));
    }
}
