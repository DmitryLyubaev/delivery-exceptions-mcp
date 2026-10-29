using System.Text.Json;
using Npgsql;
using DeliveryExceptions.Service.Storage;

namespace DeliveryExceptions.Tests.Support;

public static class DemoDatabase
{
    public static string ConnectionString
    {
        get
        {
            using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(DemoStack.Root, ".local", "credentials.json")));
            return new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", Port = 5183, Database = "delivery_demo", Username = "delivery_demo", Password = config.RootElement.GetProperty("postgresPassword").GetString(), SslMode = SslMode.Disable, Timeout = 5, CommandTimeout = 10 }.ConnectionString;
        }
    }

    public static NpgsqlDataSource Source() => NpgsqlDataSource.Create(ConnectionString);
}
