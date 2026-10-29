using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using DeliveryExceptions.Service.Auth;
using DeliveryExceptions.Service.Deliveries;
using DeliveryExceptions.Service.Storage;
using Npgsql;
using System.Net;
using Microsoft.AspNetCore.Authentication;

const string issuer = "http://127.0.0.1:5182/realms/delivery-demo";
const string resource = "http://127.0.0.1:5180/mcp";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 5180));
builder.Logging.ClearProviders();
builder.Configuration["AllowedHosts"] = "127.0.0.1";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = issuer;
    options.RequireHttpsMetadata = false; // Loopback development realm only.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = resource,
        ValidateLifetime = true, RequireExpirationTime = true,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(2), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            if (context.Principal?.FindFirst("typ")?.Value != "Bearer" ||
                string.IsNullOrWhiteSpace(context.Principal.FindFirst("sub")?.Value))
                context.Fail("Access token required");
            return Task.CompletedTask;
        }
    };
})
.AddMcp(options =>
{
    options.ResourceMetadataUri = new("http://127.0.0.1:5180/.well-known/oauth-protected-resource/mcp");
    options.ResourceMetadata = new()
    {
        Resource = resource,
        AuthorizationServers = [issuer],
        ScopesSupported = ["deliveries.read"]
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration["DELIVERY_DB_CONNECTION"] ?? throw new InvalidOperationException("Local database configuration required")));
builder.Services.AddSingleton<AccessPolicy>();
builder.Services.AddSingleton<DeliveryRepository>();
builder.Services.AddSingleton<AuditStore>();
builder.Services.AddSingleton<DeliveryService>();
builder.Services.AddSingleton<DeliveryTools>();
builder.Services.AddMcpServer()
    .WithListToolsHandler((_, _) => ValueTask.FromResult(new ModelContextProtocol.Protocol.ListToolsResult { Tools = DeliveryTools.Catalog.ToList() }))
    .WithCallToolHandler((request, ct) => new ValueTask<ModelContextProtocol.Protocol.CallToolResult>((request.Server.Services ?? throw new InvalidOperationException("Service provider unavailable")).GetRequiredService<DeliveryTools>().CallAsync(request.Params!, ct)))
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless);
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 403; return; }
    await next(context);
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/mcp" && context.User.Identity?.IsAuthenticated == true)
    {
        var actor = Actor.FromPrincipal(context.User);
        if (actor is null) { await context.ChallengeAsync(McpAuthenticationDefaults.AuthenticationScheme); return; }
        if (!actor.Scopes.Contains("deliveries.read"))
        {
            context.Response.StatusCode = 403;
            context.Response.Headers.WWWAuthenticate = "Bearer error=\"insufficient_scope\", scope=\"deliveries.read\", resource_metadata=\"http://127.0.0.1:5180/.well-known/oauth-protected-resource/mcp\"";
            return;
        }
    }
    await next(context);
});
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.Run();
