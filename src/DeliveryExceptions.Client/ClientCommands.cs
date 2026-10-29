namespace DeliveryExceptions.Client;

public sealed class ClientCommands(OAuthLogin login, TextWriter? output = null)
{
    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var writer = output ?? Console.Out;
        string tool;
        var arguments = new Dictionary<string, object?>();
        var review = args.Length > 0 && args[0] == "review";
        if (args.Length == 1 && args[0] == "list") tool = "list_delayed_deliveries";
        else if ((args.Length == 3 && args[0] == "get" || args.Length == 5 && review) &&
            args[1] == "--id" && Guid.TryParseExact(args[2], "D", out var id))
        {
            arguments["delivery_id"] = id.ToString("D");
            tool = review ? "mark_exception_reviewed" : "get_delivery";
            if (review)
            {
                if (args[3] != "--expected-version" || !long.TryParse(args[4], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var version) || version <= 0)
                    return await UsageAsync(writer);
                arguments["expected_version"] = version;
            }
        }
        else return await UsageAsync(writer);
        try
        {
            await using var client = await login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), review, ct);
            var result = await client.CallToolAsync(tool, arguments, cancellationToken: ct);
            if (result.StructuredContent is not { } payload)
            {
                await writer.WriteLineAsync("MCP returned an unsupported response.");
                return 2;
            }
            await writer.WriteLineAsync(payload.GetRawText());
            if (result.IsError != true) return 0;
            return payload.GetProperty("error").GetString() == "unavailable" ? 2 : 1;
        }
        catch (OperationCanceledException) { await writer.WriteLineAsync("Sign-in or request canceled or timed out."); return 2; }
        catch (Exception) { await writer.WriteLineAsync("Unable to complete the MCP request."); return 2; }
    }

    private static async Task<int> UsageAsync(TextWriter writer)
    {
        await writer.WriteLineAsync("Usage: list | get --id <guid> | review --id <guid> --expected-version <positive integer>");
        return 2;
    }
}
