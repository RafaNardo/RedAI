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

    [Fact]
    public void Guard_keeps_a_gym_educational_post_typographic_without_a_real_asset()
    {
        var decision = new CreativeAuthenticityGuard().Apply(CreateBrief() with
        {
            VisualMode = "TYPOGRAPHIC",
            ImageDirection = "Fitness editorial typography"
        }, []);

        Assert.Equal("TYPOGRAPHIC", decision.Brief.VisualMode);
        Assert.Null(decision.Metadata);
    }

    [Fact]
    public void Guard_converts_a_missing_gym_location_asset_to_a_safe_fallback()
    {
        var decision = new CreativeAuthenticityGuard().Apply(CreateBrief() with
        {
            VisualMode = "AUTHENTIC_ASSET_REQUIRED",
            RequiresAuthenticAsset = true,
            AuthenticAssetReason = "The post asks to show the gym facility."
        }, []);

        Assert.Equal("TYPOGRAPHIC", decision.Brief.VisualMode);
        Assert.False(decision.Brief.RequiresAuthenticAsset);
        Assert.True(decision.Metadata!.AuthenticAssetRecommended);
        Assert.Contains("gym facility", decision.Metadata.Reason);
    }

    [Fact]
    public void Guard_allows_generic_lifestyle_when_it_does_not_claim_client_evidence()
    {
        var decision = new CreativeAuthenticityGuard().Apply(CreateBrief() with
        {
            VisualMode = "GENERIC_LIFESTYLE",
            ImageRequired = true,
            ImageDirection = "A neutral family at home, without signs or brands."
        }, []);

        Assert.Equal("GENERIC_LIFESTYLE", decision.Brief.VisualMode);
        Assert.Null(decision.Metadata);
    }

    [Fact]
    public void Final_image_prompt_includes_typographic_mode_and_density_constraints()
    {
        var layout = new CreativeLayout("editorial-bold", new CreativePalette("#111111", "#F6F6F3", "#FF3D1F"), new CreativeHeadline("Você treina. Mas não evolui?", "left", "2xl", []), new CreativeLogo("footer"), VisualMode: "TYPOGRAPHIC", VisualDensity: "LOW", NegativeSpaceTarget: 0.4m, MaxVisualElements: 3);

        var prompt = CreativeImagePromptBuilder.Build(layout, "Bold fitness editorial", layout.Headline.Text, null, null);

        Assert.Contains("TYPOGRAPHIC MODE", prompt);
        Assert.Contains("40%", prompt);
        Assert.Contains("no more than 3", prompt);
        Assert.Contains("Never invent the client's physical establishment", prompt);
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
