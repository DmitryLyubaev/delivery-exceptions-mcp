using System.Text.Json;
using Microsoft.Playwright;

namespace DeliveryExceptions.Tests.Support;

public sealed class BrowserLogin(string username = "a-operator")
{
    public async Task OpenAsync(Uri uri, CancellationToken ct)
    {
        var timing = System.Diagnostics.Stopwatch.StartNew();
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(DemoStack.Root, ".local", "browsers"));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(uri.AbsoluteUri, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        if (ct.IsCancellationRequested)
            throw new TimeoutException($"Browser startup/navigation exceeded test budget after {timing.Elapsed.TotalSeconds:F0}s");
        if (page.Url.StartsWith("http://127.0.0.1:5181/callback", StringComparison.Ordinal)) return;
        using var privateConfig = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DemoStack.Root, ".local", "credentials.json"), ct));
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(privateConfig.RootElement.GetProperty("users").GetProperty(username).GetString()!);
        await page.Locator("#kc-login").ClickAsync();
        // The callback request, not completion of page-resource loading, is the
        // OAuth boundary. The SDK independently checks its code, state and issuer.
        try { await page.WaitForFunctionAsync("() => location.origin === 'http://127.0.0.1:5181' && location.pathname === '/callback'"); }
        catch (TimeoutException)
        {
            var currentPath = new Uri(page.Url).AbsolutePath;
            var problem = await page.Locator("#input-error").CountAsync() > 0 ? await page.Locator("#input-error").InnerTextAsync() : "no-login-error";
            throw new InvalidOperationException($"Synthetic login callback wait failed for {username}; path={currentPath}; login-error={problem.Trim()}");
        }
    }
}
