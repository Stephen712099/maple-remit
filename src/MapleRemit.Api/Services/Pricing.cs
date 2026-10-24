namespace MapleRemit.Api.Services;

public static class Pricing
{
    public const decimal MinAmountCad = 10.00m;
    public const decimal MaxAmountCad = 3000.00m;
    public const decimal FeeRate = 0.015m;
    public const decimal MinFeeCad = 2.99m;

    // Half-up; Math.Round defaults to banker's (3.465 → 3.46).
    // + 0.00m forces 2 decimals (100 → 100.00).
    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m;

    public static bool IsValidAmount(decimal amountCad) =>
        amountCad >= MinAmountCad
        && amountCad <= MaxAmountCad
        && amountCad == Math.Round(amountCad, 2);

    public static decimal Fee(decimal amountCad) =>
        Math.Max(RoundMoney(amountCad * FeeRate), MinFeeCad);

    public static decimal AmountThb(decimal amountCad, decimal thbPerCad) =>
        RoundMoney(amountCad * thbPerCad);
}
