using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security;
using System.Text;
using RedAI.Application;

namespace RedAI.Infrastructure;

public sealed class InMemoryCreativeVersionStore : ICreativeVersionStore
{
    private readonly ConcurrentDictionary<Guid, List<CreativeVersionRecord>> _versions = new();
    private readonly object _sync = new();
    public Task<int> NextVersionAsync(Guid contentItemId, CancellationToken cancellationToken) { lock (_sync) return Task.FromResult(_versions.TryGetValue(contentItemId, out var all) ? all.Count + 1 : 1); }
    public Task AddAsync(CreativeVersionRecord version, CancellationToken cancellationToken) { lock (_sync) _versions.GetOrAdd(version.ContentItemId, _ => []).Add(version); return Task.CompletedTask; }
    public Task<CreativeVersionRecord?> GetCurrentAsync(Guid contentItemId, CancellationToken cancellationToken) { lock (_sync) return Task.FromResult(_versions.TryGetValue(contentItemId, out var all) ? all.OrderByDescending(x => x.Version).FirstOrDefault() : null); }
    public Task<IReadOnlyList<CreativeVersionRecord>> ListAsync(Guid contentItemId, CancellationToken cancellationToken) { lock (_sync) return Task.FromResult<IReadOnlyList<CreativeVersionRecord>>(_versions.TryGetValue(contentItemId, out var all) ? all.OrderBy(x => x.Version).ToList() : []); }
    public Task SelectAsync(Guid contentItemId, Guid versionId, CancellationToken cancellationToken) { lock (_sync) { if (!_versions.TryGetValue(contentItemId, out var all) || all.All(x => x.Id != versionId)) throw new KeyNotFoundException("Creative version was not found."); var index = all.FindIndex(x => x.Id == versionId); all[index] = all[index] with { IsSelected = true }; for (var i = 0; i < all.Count; i++) if (i != index) all[i] = all[i] with { IsSelected = false }; } return Task.CompletedTask; }
}

/// <summary>Deterministic SVG renderer for development/demo. The SVG can be replaced with Playwright PNG rendering without changing the engine.</summary>
public sealed class SvgCreativeRenderer(IAssetStorage storage) : ICreativeRenderer
{
    public async Task<RenderedCreative> RenderAsync(CreativeLayout layout, string storageKey, CancellationToken cancellationToken)
    {
        var svg = BuildSvg(layout);
        var bytes = Encoding.UTF8.GetBytes(svg);
        await using var source = new MemoryStream(bytes, writable: false);
        await storage.PutAsync(source, storageKey, "image/svg+xml", cancellationToken);
        return new RenderedCreative(storageKey, "image/svg+xml", bytes);
    }

    private static string BuildSvg(CreativeLayout l)
    {
        var headline = Escape(l.Headline.Text); var support = Escape(l.SupportingText); var cta = Escape(l.Cta);
        var left = l.Headline.Alignment == "center" ? "540" : "96"; var anchor = l.Headline.Alignment == "center" ? "middle" : "start";
        var fontSize = l.Headline.Size switch { "md" => 60, "lg" => 82, "xl" => 108, _ => 138 };
        var split = l.Template == "split-image" ? $"<rect x=\"670\" width=\"410\" height=\"1350\" fill=\"{l.Palette.Accent}\" opacity=\".35\"/>" : "";
        var educational = l.Template == "educational" ? $"<line x1=\"96\" x2=\"984\" y1=\"760\" y2=\"760\" stroke=\"{l.Palette.Accent}\" stroke-width=\"8\"/>" : "";
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1080\" height=\"1350\" viewBox=\"0 0 1080 1350\"><rect width=\"1080\" height=\"1350\" fill=\"{l.Palette.Background}\"/>{split}<rect x=\"68\" y=\"68\" width=\"944\" height=\"1214\" fill=\"none\" stroke=\"{l.Palette.Accent}\" opacity=\".45\"/>{educational}<text x=\"{left}\" y=\"260\" fill=\"{l.Palette.Primary}\" text-anchor=\"{anchor}\" font-family=\"Arial, sans-serif\" font-size=\"{fontSize}\" font-weight=\"700\">{headline}</text><text x=\"{left}\" y=\"{(l.Template == "statement" ? 430 : 500)}\" fill=\"{l.Palette.Primary}\" text-anchor=\"{anchor}\" font-family=\"Arial, sans-serif\" font-size=\"38\">{support}</text><text x=\"{left}\" y=\"1150\" fill=\"{l.Palette.Accent}\" text-anchor=\"{anchor}\" font-family=\"Arial, sans-serif\" font-size=\"34\" font-weight=\"700\">{cta}</text><text x=\"96\" y=\"1230\" fill=\"{l.Palette.Primary}\" font-family=\"Arial, sans-serif\" font-size=\"26\">RED AI · {Escape(l.Logo.Position)}</text></svg>";
    }
    private static string Escape(string? text) => SecurityElement.Escape(text ?? string.Empty) ?? string.Empty;
}

public sealed class CreativeExportService(IAssetStorage storage)
{
    public async Task<byte[]> CreateZipAsync(IEnumerable<CreativeVersionRecord> versions, CancellationToken ct = default)
    {
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        foreach (var version in versions.OrderBy(x => x.ContentItemId).ThenBy(x => x.Version))
        {
            var entry = archive.CreateEntry($"content-{version.ContentItemId}/v{version.Version}/final.svg", CompressionLevel.Optimal);
            await using var input = await storage.OpenReadAsync(version.ImageStorageKey, ct);
            await using var destination = entry.Open();
            await input.CopyToAsync(destination, ct);
        }
        return output.ToArray();
    }
}
