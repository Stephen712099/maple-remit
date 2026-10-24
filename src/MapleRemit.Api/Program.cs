using System.Text.Json.Serialization;
using MapleRemit.Api.Endpoints;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
// Malformed JSON → middleware error format, in every environment.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
// Reject numbers sent as strings ("100").
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.AddOptions<ExchangeRateOptions>()
    .Bind(builder.Configuration.GetSection(ExchangeRateOptions.Section))
    .Validate(o => o.ThbPerCad > 0, "ExchangeRate:ThbPerCad must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton<IQuoteStore>(sp => sp.GetRequiredService<InMemoryStore>());
builder.Services.AddSingleton<QuoteService>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapQuoteEndpoints();

app.Run();

// Lets WebApplicationFactory reference the entry point.
public partial class Program;
