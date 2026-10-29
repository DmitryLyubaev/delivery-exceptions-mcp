using System.Diagnostics;
using Xunit;
using ModelContextProtocol.Client;
using DeliveryExceptions.Client;

namespace DeliveryExceptions.Tests.Support;

public sealed class DemoStack : IAsyncLifetime
{
    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(5) };
    private Process? service;
    private readonly System.Text.StringBuilder serviceOutput = new();
    public string ServiceOutput { get { lock (serviceOutput) return serviceOutput.ToString(); } }
    private readonly Dictionary<string, (McpClient Client, DateTimeOffset Created)> clients = [];

    public async Task<McpClient> GetClientAsync(string user = "a-operator", bool review = true)
    {
        var key = $"{user}:{review}";
        if (clients.TryGetValue(key, out var existing))
        {
            if (DateTimeOffset.UtcNow - existing.Created < TimeSpan.FromSeconds(80)) return existing.Client;
            await existing.Client.DisposeAsync();
            clients.Remove(key);
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var browser = new BrowserLogin(user);
        var client = await new OAuthLogin(browser.OpenAsync).ConnectAsync(new("http://127.0.0.1:5180/mcp"), review, deadline.Token);
        clients.Add(key, (client, DateTimeOffset.UtcNow));
        return client;
    }
    public int ServiceProcessId => service?.Id ?? throw new InvalidOperationException("No owned service");
    public async Task RestartAsync()
    {
        await StopServiceAsync();
        await InitializeAsync();
    }
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing");
    }

    public async Task InitializeAsync()
    {
        await using (var source = DemoDatabase.Source())
        {
            var database = new DeliveryExceptions.Service.Storage.Database(source);
            await database.MigrateAsync(CancellationToken.None);
            await database.SeedAsync(Path.Combine(Root, "fixtures"), CancellationToken.None);
        }
        var start = new ProcessStartInfo(Path.Combine(Root, ".local", "dotnet", "dotnet.exe"))
        {
            WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        start.ArgumentList.Add(Path.Combine(Root, "src", "DeliveryExceptions.Service", "bin", configuration, "net10.0", "DeliveryExceptions.Service.dll"));
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5180";
        start.Environment["DELIVERY_DB_CONNECTION"] = DemoDatabase.ConnectionString;
        service = Process.Start(start) ?? throw new InvalidOperationException("Service did not start");
        service.OutputDataReceived += (_, line) => { if (line.Data is not null) lock (serviceOutput) serviceOutput.AppendLine(line.Data); };
        service.ErrorDataReceived += (_, line) => { if (line.Data is not null) lock (serviceOutput) serviceOutput.AppendLine(line.Data); };
        service.BeginOutputReadLine();
        service.BeginErrorReadLine();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (service.HasExited) throw new InvalidOperationException("Owned service exited during startup");
            try { using var response = await Http.GetAsync("http://127.0.0.1:5180/mcp", HttpCompletionOption.ResponseHeadersRead); return; }
            catch (HttpRequestException) { await Task.Delay(100); }
        }
        throw new TimeoutException("Owned service readiness timed out");
    }

    private async Task StopServiceAsync()
    {
        foreach (var entry in clients.Values) await entry.Client.DisposeAsync();
        clients.Clear();
        if (service is { HasExited: false }) { service.Kill(entireProcessTree: true); await service.WaitForExitAsync(); }
        service?.Dispose();
        service = null;
    }

    public async Task DisposeAsync()
    {
        await StopServiceAsync();
        Http.Dispose();
    }
}
