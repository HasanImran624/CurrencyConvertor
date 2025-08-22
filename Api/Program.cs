using System.Diagnostics;
using System.Threading.RateLimiting;
using Application.Abstraction;
using Application.Contracts;
using Application.Options;
using Application.Services;
using Infrastructure.Cache;
using Infrastructure.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Polly;
using Serilog;
using Serilog.Context;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using Application.Contracts.Auth;
using Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateLogger();
builder.Host.UseSerilog();


// OpenTelemetry 
var resource = ResourceBuilder.CreateDefault().AddService("CurrencyConverter");
builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(resource);
        t.AddAspNetCoreInstrumentation();
        t.AddHttpClientInstrumentation();
        t.AddConsoleExporter(o =>
        {
            o.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug;
        });

    })
    .WithMetrics(m =>
    {
        m.SetResourceBuilder(resource);
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddConsoleExporter(o =>
        {
            o.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug;
        });
    });
// Configs
builder.Services.Configure<FrankfurterOptions>(builder.Configuration.GetSection("Frankfurter"));
builder.Services.Configure<CachingOptions>(builder.Configuration.GetSection("Caching"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // .NET 8 supports DateOnly natively, nothing special required here.
    });

// Swagger + JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Currency Converter API", Version = "v1" });
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] {} }
    });
});

// Rate Limiting (fixed window)
builder.Services.AddRateLimiter(options =>
{

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // 429
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers["Retry-After"] = "1";
        await ctx.HttpContext.Response.WriteAsync("Too many requests. Try again shortly.", ct);
    };
    options.AddFixedWindowLimiter("fixed", o =>
    {
        o.Window = TimeSpan.FromSeconds(1);
        o.PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 20;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});


// JWT Auth 
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
        options.TokenValidationParameters = jwt.ToTokenValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx => Task.CompletedTask
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("User", policy => policy.RequireRole("User", "Admin"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

//// HttpClientFactory + Polly for Frankfurter
builder.Services.AddHttpClient("Frankfurter", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<FrankfurterOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddPolicyHandler(Policy<HttpResponseMessage>
    .Handle<HttpRequestException>()
    .OrResult(r => (int)r.StatusCode >= 500)
    .WaitAndRetryAsync(3, retry => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retry))))
.AddPolicyHandler(Policy<HttpResponseMessage>
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddScoped<IRatesService, RatesService>();
builder.Services.AddScoped<IExchangeRateProvider, FrankfurterProvider>();
builder.Services.AddMemoryCache();                          
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Correlation + enriched logging
app.Use(async (ctx, next) =>
{
    var existing = ctx.Request.Headers["x-correlation-id"].FirstOrDefault();
    var corrId = existing ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["x-correlation-id"] = corrId;
    using var _ = LogContext.PushProperty("CorrelationId", corrId);
    using var __ = LogContext.PushProperty("ClientIp", ctx.Connection.RemoteIpAddress?.ToString());
    using var ___ = LogContext.PushProperty("ClientId", ctx.User.FindFirst("client_id")?.Value);
    var sw = Stopwatch.StartNew();
    await next();
    Log.Information("{Method} {Path} -> {Status} in {Elapsed}ms",
        ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redirect root to Swagger in Development
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

// heartbeat 
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "CurrencyConverter API",
    timestamp = DateTime.UtcNow
}));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");

app.Run();
