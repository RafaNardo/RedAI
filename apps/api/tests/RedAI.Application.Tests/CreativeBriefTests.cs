using RedAI.Application;
using System.Text.Json;
using Xunit;

namespace RedAI.Application.Tests;

public sealed class CreativeBriefTests
{
    [Fact]
    public void Validate_accepts_a_low_density_typographic_brief()
    {
        CreateBrief().Validate();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.81)]
    public void Validate_rejects_negative_space_outside_contract_range(decimal target)
    {
        var brief = CreateBrief() with { NegativeSpaceTarget = target };

        Assert.Throws<ArgumentOutOfRangeException>(brief.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Validate_rejects_an_invalid_maximum_element_count(int count)
    {
        var brief = CreateBrief() with { MaxVisualElements = count };

        Assert.Throws<ArgumentOutOfRangeException>(brief.Validate);
    }

    [Fact]
    public void Validate_requires_a_reason_when_an_authentic_asset_is_required()
    {
        var brief = CreateBrief() with
        {
            VisualMode = "AUTHENTIC_ASSET_REQUIRED",
            RequiresAuthenticAsset = true,
            AuthenticAssetReason = null
        };

        Assert.Throws<ArgumentException>(brief.Validate);
    }

    [Fact]
    public void Contract_exposes_the_art_direction_fields_and_bounds()
    {
        var contracts = Path.Combine(AppContext.BaseDirectory, "contracts");
        var catalog = new FileContractSchemaCatalog(contracts);
        using var schema = catalog.Load("creative-brief");
        var properties = schema.RootElement.GetProperty("properties");
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToHashSet();

        foreach (var field in new[] { "visualMode", "requiresAuthenticAsset", "authenticAssetReason", "visualDensity", "negativeSpaceTarget", "maxVisualElements" })
            Assert.Contains(field, required);

        Assert.Equal(0.8m, properties.GetProperty("negativeSpaceTarget").GetProperty("maximum").GetDecimal());
        Assert.Equal(6, properties.GetProperty("maxVisualElements").GetProperty("maximum").GetInt32());
        var modes = properties.GetProperty("visualMode").GetProperty("enum").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("AUTHENTIC_ASSET_REQUIRED", modes);
    }

    private static CreativeBrief CreateBrief() => new(
        "Educational authority",
        "editorial-bold",
        false,
        "Typography-led composition",
        "Generous whitespace",
        ["confident"],
        ["#111111", "#F6F6F3", "#FF3D1F"],
        ["headline", "supporting text"],
        "footer",
        ["clutter"],
        "TYPOGRAPHIC",
        false,
        null,
        "LOW",
        0.4m,
        3);
}
