using System.Diagnostics;
using Xunit;

namespace DeliveryExceptions.Tests.Support;

public sealed class DemoStack : IAsyncLifetime
{
    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(5) };
    private Process? service;
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
        var start = new ProcessStartInfo(Path.Combine(Root, ".local", "dotnet", "dotnet.exe"))
        {
            WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(Root, "src", "DeliveryExceptions.Service", "bin", "Debug", "net10.0", "DeliveryExceptions.Service.dll"));
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5180";
        service = Process.Start(start) ?? throw new InvalidOperationException("Service did not start");
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

    public async Task DisposeAsync()
    {
        if (service is { HasExited: false }) { service.Kill(entireProcessTree: true); await service.WaitForExitAsync(); }
        service?.Dispose();
        Http.Dispose();
    }
}
