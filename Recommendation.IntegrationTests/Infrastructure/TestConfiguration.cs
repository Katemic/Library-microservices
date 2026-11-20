
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Recommendation.IntegrationTests.Infrastructure;

[TestClass]
public sealed class TestConfiguration
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext context)
    {
        await TestInfrastructure.InitializeAsync();
    }

    [AssemblyCleanup]
    public static async Task Cleanup()
    {
        await TestInfrastructure.CleanupAsync();
    }
}
