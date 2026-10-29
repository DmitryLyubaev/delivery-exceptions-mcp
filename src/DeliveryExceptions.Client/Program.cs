using DeliveryExceptions.Client;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, signal) => { signal.Cancel = true; cancellation.Cancel(); };
return await new ClientCommands(new OAuthLogin()).RunAsync(args, cancellation.Token);
