using System.Net;
using Microsoft.Playwright;
using RedAI.Application;

namespace RedAI.Infrastructure;

/// <summary>Deterministic 1080x1350 social artwork rendered from local HTML/CSS in Chromium.</summary>
public sealed class PlaywrightCreativeRenderer(IAssetStorage storage) : IDeterministicCreativeRenderer
{
    private static readonly SemaphoreSlim BrowserGate = new(1, 1);
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    public async Task<RenderedCreative> RenderPngAsync(DeterministicCreativeRenderRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        var browser = await GetBrowserAsync();
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1080, Height = 1350 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        await page.SetContentAsync(await BuildHtmlAsync(request.Layout, cancellationToken), new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var png = await page.Locator("#creative").ScreenshotAsync(new LocatorScreenshotOptions { Type = ScreenshotType.Png });
        await using var input = new MemoryStream(png, writable: false);
        await storage.PutAsync(input, request.StorageKey, "image/png", cancellationToken);
        return new RenderedCreative(request.StorageKey, "image/png", png);
    }

    private async Task<string> BuildHtmlAsync(CreativeLayout layout, CancellationToken ct)
    {
        var palette = EnsureReadablePalette(layout.Palette);
        var background = string.Empty;
        if (!string.IsNullOrWhiteSpace(layout.BackgroundAssetKey))
        {
            await using var stream = await storage.OpenReadAsync(layout.BackgroundAssetKey, ct);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, ct);
            background = $"linear-gradient(135deg,rgba(0,0,0,.18),rgba(0,0,0,.58)),url('data:{ImageMime(layout.BackgroundAssetKey)};base64,{Convert.ToBase64String(bytes.ToArray())}')";
        }
        var html = """
<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><style>
*{box-sizing:border-box}html,body{margin:0;width:1080px;height:1350px;overflow:hidden}body{font-family:Inter,"Helvetica Neue",Arial,sans-serif;text-rendering:geometricPrecision}.creative{--bg:__BG__;--primary:__PRIMARY__;--accent:__ACCENT__;width:1080px;height:1350px;position:relative;overflow:hidden;color:var(--primary);background:var(--bg);background-image:__BACKGROUND__;background-size:cover;background-position:center}.creative:before{content:"";position:absolute;inset:0;background:radial-gradient(circle at 88% 8%,color-mix(in srgb,var(--accent) 36%,transparent),transparent 30%)}.frame{position:absolute;inset:58px;border:1px solid color-mix(in srgb,var(--primary) 30%,transparent)}.content{position:absolute;inset:0;padding:112px 100px;display:flex;flex-direction:column;z-index:1}.eyebrow{font-size:20px;font-weight:800;letter-spacing:.16em;text-transform:uppercase;margin:0 0 38px}.headline{margin:0;font-size:__SIZE__px;line-height:.92;letter-spacing:-.065em;font-weight:850;max-width:850px;overflow-wrap:anywhere}.accent{color:var(--accent)}.support{font-size:29px;line-height:1.25;max-width:650px;margin:34px 0 0;letter-spacing:-.02em}.cta{font-size:19px;font-weight:800;letter-spacing:.06em;text-transform:uppercase;margin:auto 0 0;padding:18px 22px;border:2px solid currentColor;width:max-content;max-width:680px}.logo{position:absolute;__LOGO__;width:54px;height:54px;display:grid;grid-template-columns:1fr 1fr;gap:6px;z-index:2}.logo i{display:block;background:currentColor;border-radius:50%}.logo i:last-child{background:var(--accent)}.editorial-bold .headline{font-size:112px}.editorial-bold .support{margin-top:auto;max-width:440px}.editorial-bold:after{content:"";position:absolute;width:280px;height:280px;border-radius:50%;background:var(--accent);right:-110px;bottom:210px;opacity:.9}.minimal-center .content,.statement .content{align-items:center;text-align:center}.minimal-center .headline{max-width:800px;font-size:94px}.minimal-center .support{max-width:590px}.minimal-center .cta{margin-top:70px}.statement{background:var(--primary);color:var(--bg)}.statement .frame{border-color:color-mix(in srgb,var(--bg) 38%,transparent)}.statement .headline{font-size:124px;max-width:850px}.split-image{background-size:58% 100%;background-repeat:no-repeat;background-position:right}.split-image:after{content:"";position:absolute;right:0;top:0;width:49%;height:100%;background:linear-gradient(90deg,transparent,rgba(0,0,0,.25))}.split-image .content{width:62%;background:var(--bg);clip-path:polygon(0 0,91% 0,100% 100%,0 100%)}.split-image .headline{font-size:86px;max-width:520px}.educational .eyebrow{color:var(--accent)}.educational .headline{font-size:84px;max-width:720px}.educational .support{border-left:8px solid var(--accent);padding-left:22px;max-width:620px}.promotional .content{padding:90px}.promotional .headline{font-size:98px;max-width:790px;background:var(--accent);color:var(--bg);padding:28px 32px 36px}.promotional .support{margin-top:38px;max-width:500px}.promotional .cta{background:var(--primary);color:var(--bg);border:0;border-radius:999px;padding:19px 28px}
</style></head><body><main id="creative" class="creative __TEMPLATE__"><div class="frame"></div><div class="content"><p class="eyebrow">Comunicação de marca</p><h1 class="headline">__HEADLINE__</h1>__SUPPORT____CTA__</div><div class="logo" aria-label="Marca"><i></i><i></i></div></main></body></html>
""";
        return html.Replace("__BG__", Color(palette.Background)).Replace("__PRIMARY__", Color(palette.Primary)).Replace("__ACCENT__", Color(palette.Accent)).Replace("__BACKGROUND__", background.Length == 0 ? "none" : background).Replace("__SIZE__", HeadlineSize(layout.Headline.Size).ToString()).Replace("__LOGO__", LogoPosition(layout.Logo.Position)).Replace("__TEMPLATE__", Text(layout.Template)).Replace("__HEADLINE__", Highlight(layout.Headline)).Replace("__SUPPORT__", string.IsNullOrWhiteSpace(layout.SupportingText) ? string.Empty : $"<p class=\"support\">{Text(Truncate(layout.SupportingText, 180))}</p>").Replace("__CTA__", string.IsNullOrWhiteSpace(layout.Cta) ? string.Empty : $"<p class=\"cta\">{Text(Truncate(layout.Cta, 48))}</p>");
    }

    private static async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null) return _browser;
        await BrowserGate.WaitAsync();
        try { if (_browser is null) { _playwright = await Playwright.CreateAsync(); _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }); } return _browser; }
        finally { BrowserGate.Release(); }
    }

    private static string Text(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Truncate(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value ?? string.Empty : $"{value[..(maxLength - 1)].TrimEnd()}…";
    private static string Color(string value) => value.StartsWith('#') && value.Length is 4 or 7 ? value : "#0B0D10";
    private static CreativePalette EnsureReadablePalette(CreativePalette palette)
    {
        var background = Color(palette.Background); var primary = Color(palette.Primary); var accent = Color(palette.Accent);
        if (Contrast(background, primary) < 4.5) primary = Luminance(background) > .45 ? "#101114" : "#F8F7F2";
        if (Contrast(background, accent) < 2.2) accent = Luminance(background) > .45 ? "#B42318" : "#FF6545";
        return new CreativePalette(background, primary, accent);
    }
    private static double Contrast(string first, string second) { var a = Luminance(first); var b = Luminance(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static double Luminance(string color)
    {
        var hex = color.TrimStart('#'); if (hex.Length == 3) hex = string.Concat(hex.Select(character => $"{character}{character}"));
        var components = Enumerable.Range(0, 3).Select(index => Convert.ToInt32(hex.Substring(index * 2, 2), 16) / 255d).Select(value => value <= .03928 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4)).ToArray();
        return .2126 * components[0] + .7152 * components[1] + .0722 * components[2];
    }
    private static int HeadlineSize(string size) => size switch { "sm" => 62, "md" => 76, "lg" => 92, "2xl" => 116, _ => 92 };
    private static string ImageMime(string key) => Path.GetExtension(key).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".svg" => "image/svg+xml", _ => "image/png" };
    private static string LogoPosition(string position) => position.ToLowerInvariant() switch { "top-left" or "superior-esquerdo" => "top:94px;left:96px", "top-right" or "superior-direito" => "top:94px;right:96px", "bottom-left" => "bottom:92px;left:96px", _ => "bottom:92px;right:96px" };
    private static string Highlight(CreativeHeadline headline)
    {
        var text = Text(headline.Text); var emphasis = headline.Emphasis.FirstOrDefault(word => !string.IsNullOrWhiteSpace(word));
        return string.IsNullOrWhiteSpace(emphasis) ? text : text.Replace(Text(emphasis), $"<span class=\"accent\">{Text(emphasis)}</span>", StringComparison.OrdinalIgnoreCase);
    }
}
