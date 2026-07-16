using Frontier10052.Domain;

namespace Frontier10052.Simulation.Tests;

[TestClass]
public sealed class MarketPricingTests
{
    [TestMethod]
    public void PriceRisesBelowTargetAndFallsAboveTarget()
    {
        Credits basePrice = new(1_000);

        Credits scarce = MarketPricing.CalculateUnitPrice(basePrice, new Tonnes(10), new Tonnes(20));
        Credits balanced = MarketPricing.CalculateUnitPrice(basePrice, new Tonnes(20), new Tonnes(20));
        Credits abundant = MarketPricing.CalculateUnitPrice(basePrice, new Tonnes(30), new Tonnes(20));

        Assert.AreEqual(new Credits(1_175), scarce);
        Assert.AreEqual(basePrice, balanced);
        Assert.AreEqual(new Credits(825), abundant);
    }

    [TestMethod]
    public void PriceCalculationRejectsZeroTargetStock()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            MarketPricing.CalculateUnitPrice(new Credits(100), new Tonnes(1), new Tonnes(0)));
    }
}
