using System.Text.Json;

namespace Runtime.Agent.Tests;

public class PayloadNormalizationTests
{
    [Fact]
    public void Normalize_NullPayload_ReturnsNull()
    {
        Assert.Null(PayloadNormalization.Normalize(null));
    }

    [Fact]
    public void Normalize_JsonElementStringValue_ReturnsPlainString()
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"RepositoryName\":\"Ferret\"}")!;

        var normalized = PayloadNormalization.Normalize(payload);

        Assert.IsType<string>(normalized!["RepositoryName"]);
        Assert.Equal("Ferret", normalized["RepositoryName"]);
    }

    [Fact]
    public void Normalize_JsonElementNumberValue_ReturnsPlainNumericPrimitive()
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Count\":5}")!;

        var normalized = PayloadNormalization.Normalize(payload);

        Assert.IsNotType<JsonElement>(normalized!["Count"]);
        Assert.Equal(5d, Convert.ToDouble(normalized["Count"]));
    }

    [Fact]
    public void Normalize_JsonElementBooleanValues_ReturnPlainBool()
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"A\":true,\"B\":false}")!;

        var normalized = PayloadNormalization.Normalize(payload);

        Assert.Equal(true, normalized!["A"]);
        Assert.Equal(false, normalized["B"]);
    }

    [Fact]
    public void Normalize_JsonElementNullValue_ReturnsNull()
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"A\":null}")!;

        var normalized = PayloadNormalization.Normalize(payload);

        Assert.Null(normalized!["A"]);
    }

    [Fact]
    public void Normalize_JsonElementArrayValue_ReturnsListOfNormalizedValues()
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Items\":[\"a\",\"b\"]}")!;

        var normalized = PayloadNormalization.Normalize(payload);

        var items = Assert.IsAssignableFrom<IEnumerable<object>>(normalized!["Items"]).ToList();
        Assert.Equal(new object[] { "a", "b" }, items);
    }

    [Fact]
    public void Normalize_AlreadyCanonicalClrValues_PassThroughUnchanged()
    {
        var payload = new Dictionary<string, object> { ["RepositoryName"] = "Ferret", ["Count"] = 5, ["Flag"] = true };

        var normalized = PayloadNormalization.Normalize(payload);

        Assert.Equal("Ferret", normalized!["RepositoryName"]);
        Assert.Equal(5, normalized["Count"]);
        Assert.Equal(true, normalized["Flag"]);
    }
}
