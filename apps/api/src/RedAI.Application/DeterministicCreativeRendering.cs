namespace RedAI.Application;

/// <summary>
/// Stable service boundary for post composition. AI may supply only BackgroundAssetKey;
/// headline, support copy, CTA and logo are always painted by this renderer.
/// </summary>
public sealed record DeterministicCreativeRenderRequest(CreativeLayout Layout, string StorageKey)
{
    public void Validate()
    {
        if (!CreativeTemplates.IsSupported(Layout.Template)) throw new ArgumentException("Unsupported layout template.");
        if (!StorageKey.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Creative output must be a PNG storage key.");
        if (string.IsNullOrWhiteSpace(Layout.Headline.Text)) throw new ArgumentException("A creative headline is required.");
    }
}

public interface IDeterministicCreativeRenderer
{
    Task<RenderedCreative> RenderPngAsync(DeterministicCreativeRenderRequest request, CancellationToken cancellationToken = default);
}
