using System.Text;
using System.Text.Json;

namespace RedAI.Application;

/// <summary>Supported deterministic social post layouts (1080x1350).</summary>
public static class CreativeTemplates
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "editorial-bold", "minimal-center", "split-image", "statement", "educational", "promotional"
    };

    public static bool IsSupported(string template) => All.Contains(template);
}

public sealed record CreativeBrief(
    string Purpose,
    string Template,
    bool ImageRequired,
    string ImageDirection,
    string Composition,
    IReadOnlyList<string> Mood,
    IReadOnlyList<string> PaletteRecommendation,
    IReadOnlyList<string> Hierarchy,
    string LogoPlacement,
    IReadOnlyList<string> Avoid,
    string VisualMode,
    bool RequiresAuthenticAsset,
    string? AuthenticAssetReason,
    string VisualDensity,
    decimal NegativeSpaceTarget,
    int MaxVisualElements)
{
    private static readonly IReadOnlySet<string> VisualModes = new HashSet<string>(StringComparer.Ordinal)
    { "TYPOGRAPHIC", "ABSTRACT", "PRODUCT", "GENERIC_LIFESTYLE", "AUTHENTIC_ASSET_REQUIRED" };
    private static readonly IReadOnlySet<string> VisualDensities = new HashSet<string>(StringComparer.Ordinal)
    { "LOW", "MEDIUM", "HIGH" };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Purpose)) throw new ArgumentException("Creative brief purpose is required.");
        if (!CreativeTemplates.IsSupported(Template)) throw new ArgumentException($"Unsupported creative template '{Template}'.");
        if (string.IsNullOrWhiteSpace(ImageDirection) || string.IsNullOrWhiteSpace(Composition)) throw new ArgumentException("Image direction and composition are required.");
        if (Mood.Count == 0 || Hierarchy.Count == 0 || Avoid.Count == 0) throw new ArgumentException("Mood, hierarchy and avoid must not be empty.");
        if (!VisualModes.Contains(VisualMode)) throw new ArgumentException($"Unsupported visual mode '{VisualMode}'.");
        if (!VisualDensities.Contains(VisualDensity)) throw new ArgumentException($"Unsupported visual density '{VisualDensity}'.");
        if (NegativeSpaceTarget is < 0 or > 0.8m) throw new ArgumentOutOfRangeException(nameof(NegativeSpaceTarget), "Negative space target must be between 0 and 0.8.");
        if (MaxVisualElements is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(MaxVisualElements), "Max visual elements must be between 1 and 6.");
        if (RequiresAuthenticAsset && string.IsNullOrWhiteSpace(AuthenticAssetReason)) throw new ArgumentException("An authentic asset reason is required when an authentic asset is required.");
    }
}

public sealed record CreativeSourceDescriptor(string Type, string? Filename, string? MimeType)
{
    public bool IsExplicitAuthenticAsset => Type is "location" or "team" or "product" or "facility";
}

public sealed record CreativeAuthenticityMetadata(bool AuthenticAssetRecommended, string? Reason, string OriginalVisualMode);
public sealed record CreativeAuthenticityDecision(CreativeBrief Brief, CreativeAuthenticityMetadata? Metadata);

/// <summary>
/// Keeps visual concepts from presenting an invented business, team, facility or
/// product as if it were evidence supplied by the client.
/// </summary>
public sealed class CreativeAuthenticityGuard
{
    public CreativeAuthenticityDecision Apply(CreativeBrief brief, IEnumerable<CreativeSourceDescriptor> sources)
    {
        brief.Validate();
        var needsAuthenticAsset = brief.RequiresAuthenticAsset || brief.VisualMode == "AUTHENTIC_ASSET_REQUIRED";
        if (!needsAuthenticAsset) return new CreativeAuthenticityDecision(brief, null);

        // The current image-generation integration does not pass a client image
        // into an edit request. Even an explicitly classified asset must therefore
        // not be silently replaced by an invented scene.
        var hasExplicitAsset = sources.Any(source => source.IsExplicitAuthenticAsset);
        var reason = brief.AuthenticAssetReason
            ?? "A imagem proposta representa uma evidência real da marca e requer um asset autêntico.";
        if (hasExplicitAsset)
            reason = $"{reason} O asset autêntico foi identificado, mas ainda não é enviado ao gerador de imagem final.";

        var fallback = brief with
        {
            VisualMode = "TYPOGRAPHIC",
            RequiresAuthenticAsset = false,
            AuthenticAssetReason = reason,
            ImageRequired = false,
            ImageDirection = "Typography-led editorial composition. No people, location, facility, product or literal scene.",
            Composition = "One dominant headline, restrained brand structure and generous negative space.",
            VisualDensity = "LOW",
            NegativeSpaceTarget = 0.4m,
            MaxVisualElements = 3
        };
        fallback.Validate();
        return new CreativeAuthenticityDecision(fallback, new CreativeAuthenticityMetadata(true, reason, brief.VisualMode));
    }
}

public sealed record CreativePalette(string Background, string Primary, string Accent);
public sealed record CreativeHeadline(string Text, string Alignment, string Size, IReadOnlyList<string> Emphasis);
public sealed record CreativeLogo(string Position);
public sealed record CreativeLayout(
    string Template,
    CreativePalette Palette,
    CreativeHeadline Headline,
    CreativeLogo Logo,
    string? SupportingText = null,
    string? Cta = null,
    string? BackgroundAssetKey = null,
    string VisualMode = "TYPOGRAPHIC",
    string VisualDensity = "LOW",
    decimal NegativeSpaceTarget = 0.4m,
    int MaxVisualElements = 3,
    CreativeAuthenticityMetadata? Authenticity = null);

public static class CreativeImagePromptBuilder
{
    public static string Build(CreativeLayout layout, string? visualDirection, string headline, string? supportingText, string? cta, string? revisionInstruction = null)
    {
        var revisionContext = string.IsNullOrWhiteSpace(revisionInstruction) ? string.Empty : $"\nApply this visual revision faithfully: {revisionInstruction}\n";
        var negativeSpacePercent = (layout.NegativeSpaceTarget * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        var modeRules = layout.VisualMode switch
        {
            "TYPOGRAPHIC" => "TYPOGRAPHIC MODE: Photography is prohibited. Use typography, color, spacing and subtle graphic structure only. No people, locations or literal objects. Treat it as a premium editorial poster, never an infographic.",
            "ABSTRACT" => "ABSTRACT MODE: Do not depict a literal place. Use restrained light, texture, shape, depth or conceptual graphics. Keep abstraction secondary to the communication hierarchy; avoid futuristic AI aesthetics unless the brand clearly requires it.",
            "GENERIC_LIFESTYLE" => "GENERIC LIFESTYLE MODE: The scene represents a generic concept only, never the client's actual location, staff or customers. Use a neutral non-identifiable environment with no third-party branding or signage.",
            "PRODUCT" => "PRODUCT MODE: Do not invent a client product. Use only an authentic supplied product asset; otherwise keep the composition typography-led and abstract.",
            _ => "AUTHENTIC ASSET REQUIRED: Do not depict a real-world location, facility, team, product or customer. Use a typography-led abstract fallback."
        };

        return $"""
Create one polished final 4:5 portrait Instagram feed PNG for a Brazilian professional brand. This is a finished commercial creative, not a wireframe or background.

STYLE
Modern editorial advertising. Premium, minimal, sophisticated, intentional and commercially usable. Strong hierarchy, clean Portuguese accent rendering, correct line wrapping and safe margins.

VISUAL DENSITY
{layout.VisualDensity}. Reserve approximately {negativeSpacePercent}% intentional negative space. Use one dominant visual idea and no more than {layout.MaxVisualElements} major visual elements, including the headline. Empty space is intentional; never fill it with decoration.

STRICTLY AVOID
Collages, multiple panels, floating cards, stickers, badges, unnecessary icons, arrows, random ornaments, fake UI, dashboards, fake statistics, extra labels, excessive gradients, glow, shadows, multiple photographs, noisy backgrounds, watermarks, third-party branding and RED AI branding.

AUTHENTICITY
Never invent the client's physical establishment, interior, staff, customers, facilities, equipment or branded product. If there is no authentic asset, use typography, abstraction or neutral conceptual imagery instead.

{modeRules}

Template intent: {layout.Template}
Palette: one dominant color {layout.Palette.Background}; supporting color {layout.Palette.Primary}; accent {layout.Palette.Accent}
Art direction: {visualDirection ?? "Use the approved brand visual language."}
{revisionContext}
Render only this exact Portuguese copy. Do not add words, claims, labels, slogans or CTAs:
HEADLINE: {headline}
SUPPORTING TEXT: {supportingText ?? "(omit)"}
CTA: {cta ?? "(omit)"}
""";
    }
}

public sealed record CreativeRevisionAction(string Type, string Instruction, JsonElement? Changes = null);
public sealed record CreativeRevisionPlan(string Summary, IReadOnlyList<CreativeRevisionAction> Actions)
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    { "CHANGE_COPY", "CHANGE_LAYOUT", "REGENERATE_IMAGE", "CHANGE_COLORS", "CHANGE_TYPOGRAPHY", "CHANGE_ASSET", "NO_CHANGE" };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary) || Actions.Count == 0) throw new ArgumentException("A revision plan needs summary and actions.");
        if (Actions.Any(a => !Allowed.Contains(a.Type) || string.IsNullOrWhiteSpace(a.Instruction))) throw new ArgumentException("Revision plan contains an invalid action.");
    }
}

public sealed record RenderedCreative(string StorageKey, string ContentType, byte[] Bytes);
public interface ICreativeRenderer { Task<RenderedCreative> RenderAsync(CreativeLayout layout, string storageKey, CancellationToken cancellationToken); }

public sealed record CreativeVersionRecord(
    Guid Id, Guid ProjectId, Guid ContentItemId, int Version, Guid SourceContentRevisionId,
    CreativeLayout Layout, string ImageStorageKey, string? ThumbnailStorageKey,
    string? RevisionInstruction, bool IsSelected, DateTimeOffset CreatedAt);

public interface ICreativeVersionStore
{
    Task<int> NextVersionAsync(Guid contentItemId, CancellationToken cancellationToken);
    Task AddAsync(CreativeVersionRecord version, CancellationToken cancellationToken);
    Task<CreativeVersionRecord?> GetCurrentAsync(Guid contentItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CreativeVersionRecord>> ListAsync(Guid contentItemId, CancellationToken cancellationToken);
    Task SelectAsync(Guid contentItemId, Guid versionId, CancellationToken cancellationToken);
}

public sealed class CreativeLayoutSelector
{
    public CreativeLayout Select(CreativeBrief brief, string headline, string? supportingText, string? cta, string? logoPosition = null)
    {
        brief.Validate();
        if (string.IsNullOrWhiteSpace(headline)) throw new ArgumentException("Headline is required.");
        var palette = brief.PaletteRecommendation.Count >= 3
            ? new CreativePalette(brief.PaletteRecommendation[0], brief.PaletteRecommendation[1], brief.PaletteRecommendation[2])
            : new CreativePalette("#0B0D10", "#F5F2EA", "#E82D31");
        var alignment = brief.Template is "minimal-center" or "statement" ? "center" : "left";
        var size = brief.Template is "editorial-bold" or "statement" ? "2xl" : "xl";
        return new CreativeLayout(brief.Template, palette, new CreativeHeadline(headline, alignment, size, []), new CreativeLogo(logoPosition ?? brief.LogoPlacement), supportingText, cta);
    }
}

/// <summary>Coordinates immutable creative versions. A revision always renders and persists V+1.</summary>
public sealed class CreativeEngine(ICreativeVersionStore versions, ICreativeRenderer renderer)
{
    public async Task<CreativeVersionRecord> CreateAsync(Guid projectId, Guid contentItemId, Guid sourceRevisionId, CreativeLayout layout, CancellationToken ct = default)
    {
        ValidateLayout(layout);
        var number = await versions.NextVersionAsync(contentItemId, ct);
        var key = $"projects/{projectId}/content/{contentItemId}/creatives/v{number}/final.svg";
        var rendered = await renderer.RenderAsync(layout, key, ct);
        var item = new CreativeVersionRecord(Guid.NewGuid(), projectId, contentItemId, number, sourceRevisionId, layout, rendered.StorageKey, null, null, false, DateTimeOffset.UtcNow);
        await versions.AddAsync(item, ct);
        return item;
    }

    public async Task<CreativeVersionRecord> ReviseAsync(Guid contentItemId, Guid sourceRevisionId, CreativeRevisionPlan plan, CancellationToken ct = default)
    {
        plan.Validate();
        var current = await versions.GetCurrentAsync(contentItemId, ct) ?? throw new InvalidOperationException("No creative version exists for this content item.");
        var layout = ApplyPlan(current.Layout, plan);
        ValidateLayout(layout);
        var number = await versions.NextVersionAsync(contentItemId, ct);
        var key = $"projects/{current.ProjectId}/content/{contentItemId}/creatives/v{number}/final.svg";
        var rendered = await renderer.RenderAsync(layout, key, ct);
        var next = new CreativeVersionRecord(Guid.NewGuid(), current.ProjectId, contentItemId, number, sourceRevisionId, layout, rendered.StorageKey, null, plan.Summary, false, DateTimeOffset.UtcNow);
        await versions.AddAsync(next, ct);
        return next;
    }

    public Task SelectAsync(Guid contentItemId, Guid versionId, CancellationToken ct = default) => versions.SelectAsync(contentItemId, versionId, ct);

    private static CreativeLayout ApplyPlan(CreativeLayout layout, CreativeRevisionPlan plan)
    {
        foreach (var action in plan.Actions.Where(x => x.Type != "NO_CHANGE"))
        {
            if (action.Changes is not { ValueKind: JsonValueKind.Object } changes) continue;
            if (action.Type == "CHANGE_COPY" && changes.TryGetProperty("headline", out var headline))
                layout = layout with { Headline = layout.Headline with { Text = headline.GetString() ?? layout.Headline.Text } };
            if (action.Type == "CHANGE_LAYOUT" && changes.TryGetProperty("template", out var template))
                layout = layout with { Template = template.GetString() ?? layout.Template };
            if (action.Type == "CHANGE_COLORS" && changes.TryGetProperty("palette", out var palette))
                layout = layout with { Palette = JsonSerializer.Deserialize<CreativePalette>(palette.GetRawText()) ?? layout.Palette };
        }
        return layout;
    }

    private static void ValidateLayout(CreativeLayout layout)
    {
        if (!CreativeTemplates.IsSupported(layout.Template)) throw new ArgumentException("Unsupported layout template.");
        if (string.IsNullOrWhiteSpace(layout.Headline.Text) || !new[] { "left", "center", "right" }.Contains(layout.Headline.Alignment)) throw new ArgumentException("Layout headline is invalid.");
        if (new[] { layout.Palette.Background, layout.Palette.Primary, layout.Palette.Accent }.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Layout palette is invalid.");
    }
}
