namespace Frontier10052.Simulation.Tests;

[TestClass]
public sealed class AssemblyTests
{
    [TestMethod]
    public void SimulationAndDomainRemainSeparateAssemblies()
    {
        var domainAssembly = typeof(Domain.AssemblyMarker).Assembly;
        var simulationAssembly = typeof(Simulation.AssemblyMarker).Assembly;

        Assert.AreNotEqual(domainAssembly, simulationAssembly);
        Assert.AreEqual("Frontier10052.Simulation", simulationAssembly.GetName().Name);
    }
}
