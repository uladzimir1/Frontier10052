namespace Frontier10052.Domain.Tests;

[TestClass]
public sealed class AssemblyTests
{
    [TestMethod]
    public void DomainAssemblyHasExpectedIdentity()
    {
        var assemblyName = typeof(Domain.AssemblyMarker).Assembly.GetName().Name;

        Assert.AreEqual("Frontier10052.Domain", assemblyName);
    }
}
