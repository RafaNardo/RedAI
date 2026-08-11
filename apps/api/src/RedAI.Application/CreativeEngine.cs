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
    string? BackgroundAssetKey = null);

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
