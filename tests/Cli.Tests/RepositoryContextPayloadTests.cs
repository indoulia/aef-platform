using System.Text.Json;

namespace Cli.Tests;

// Regression test for an execution-discovered adapter translation defect
// (GA-3 Capability 5): CLI's JsonSerializer.Deserialize call produced
// payload values as JsonElement, which EngineeringExecutionPipeline's
// `is string name` pattern match never matched - RepositoryName/
// RepositoryRoot silently looked "missing" on every real request. Fixed
// via PayloadNormalization.Normalize (Runtime.Agent). Uses a portable,
// self-created temp directory rather than the external C:\POC\Ferret
// path used for live dogfooding, so this test is safe to run in CI.
public class RepositoryContextPayloadTests
{
    [Fact]
    public void Execute_PayloadWithRepositoryContext_IsRecognizedNotMissing()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("aef-cli-repo-context-test-");
        try
        {
            var payloadJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["RepositoryName"] = "temp-repo",
                ["RepositoryRoot"] = tempDirectory.FullName.Replace("\\", "/"),
            });

            var (_, stdout, _) = CliExecutableFixture.Run(
                "execute", "--user-request", "analyze this repository", "--project-profile-id", "default-profile",
                "--payload-json", payloadJson, "--output", "json");

            Assert.DoesNotContain("No repository context was supplied", stdout);
            Assert.Contains("\"repositoryAnalyzed\":\"temp-repo\"", stdout);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
