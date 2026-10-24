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

AddStore<InMemoryStore>(builder.Services);
builder.Services.AddSingleton<QuoteService>();
builder.Services.AddSingleton<TransferService>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseStatusCodePages(StatusCodeErrors.WriteAsync);

app.MapGet("/health", (IStoreHealth store) => store.IsReady()
    ? Results.Ok(new { status = "ok" })
    : Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapQuoteEndpoints();
app.MapTransferEndpoints();

app.Run();

// One instance for all interfaces: shared data and locks.
static void AddStore<TStore>(IServiceCollection services)
    where TStore : class, IQuoteStore, ITransferStore, IStoreHealth
{
    services.AddSingleton<TStore>();
    services.AddSingleton<IQuoteStore>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<ITransferStore>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<IStoreHealth>(sp => sp.GetRequiredService<TStore>());
}

// Lets WebApplicationFactory reference the entry point.
public partial class Program;
