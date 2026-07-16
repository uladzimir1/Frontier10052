namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class AssemblyCompositionTests
{
    [TestMethod]
    public void ProductionBoundariesCanBeComposedTogether()
    {
        string?[] actualAssemblyNames =
        [
            typeof(Domain.AssemblyMarker).Assembly.GetName().Name,
            typeof(Simulation.AssemblyMarker).Assembly.GetName().Name,
            typeof(Gameplay.AssemblyMarker).Assembly.GetName().Name,
            typeof(Content.AssemblyMarker).Assembly.GetName().Name,
            typeof(Infrastructure.AssemblyMarker).Assembly.GetName().Name,
        ];

        string[] expectedAssemblyNames =
        [
            "Frontier10052.Domain",
            "Frontier10052.Simulation",
            "Frontier10052.Gameplay",
            "Frontier10052.Content",
            "Frontier10052.Infrastructure",
        ];

        CollectionAssert.AreEqual(expectedAssemblyNames, actualAssemblyNames);
    }
}
