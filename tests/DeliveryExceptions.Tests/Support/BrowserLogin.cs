using System.Text.Json;
using Microsoft.Playwright;

namespace DeliveryExceptions.Tests.Support;

public sealed class BrowserLogin(string username = "a-operator")
{
    public async Task OpenAsync(Uri uri, CancellationToken ct)
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(DemoStack.Root, ".local", "browsers"));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(uri.AbsoluteUri);
        if (page.Url.StartsWith("http://127.0.0.1:5181/callback", StringComparison.Ordinal)) return;
        using var privateConfig = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DemoStack.Root, ".local", "credentials.json"), ct));
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(privateConfig.RootElement.GetProperty("users").GetProperty(username).GetString()!);
        await page.Locator("#kc-login").ClickAsync();
        await page.WaitForURLAsync("http://127.0.0.1:5181/callback**");
    }
}
