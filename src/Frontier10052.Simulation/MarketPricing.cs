using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public static class MarketPricing
{
    public static Credits CalculateUnitPrice(Credits basePrice, Tonnes stock, Tonnes targetStock)
    {
        if (targetStock.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetStock), "Target stock must be positive.");
        }

        decimal pressure = (targetStock.Value - stock.Value) / (decimal)targetStock.Value;
        decimal multiplier = Math.Clamp(1m + (pressure * 0.35m), 0.65m, 1.65m);
        long price = checked((long)Math.Round(basePrice.Value * multiplier, MidpointRounding.ToEven));
        return new Credits(price);
    }
}
