using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;

const string issuer = "http://127.0.0.1:5182/realms/delivery-demo";
const string resource = "http://127.0.0.1:5180/mcp";
var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddMcpServer()
    .WithListToolsHandler((_, _) => ValueTask.FromResult(new ModelContextProtocol.Protocol.ListToolsResult { Tools = [] }))
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless);
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 403; return; }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.Run();
