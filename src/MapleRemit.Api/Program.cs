using System.Text.Json.Serialization;
using MapleRemit.Api.Endpoints;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;
using Npgsql;

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

// Storage:Provider is "Postgres" or "InMemory" (default); anything else fails startup.
var provider = builder.Configuration["Storage:Provider"] ?? "InMemory";
var usePostgres = string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase);
if (!usePostgres && !string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Storage:Provider must be \"Postgres\" or \"InMemory\", not \"{provider}\".");
if (usePostgres)
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required when Storage:Provider is Postgres.");
    // Factory, so DI disposes it.
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
    AddStore<PostgresStore>(builder.Services);
}
else
{
    AddStore<InMemoryStore>(builder.Services);
}

builder.Services.AddSingleton<QuoteService>();
builder.Services.AddSingleton<TransferService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SchemaFilter<SwaggerExamples>();
    o.OperationFilter<SwaggerExamples>();
});

var app = builder.Build();

if (usePostgres)
    app.Services.GetRequiredService<PostgresStore>().EnsureSchema();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseStatusCodePages(StatusCodeErrors.WriteAsync);
// On in every environment so the live demo can be tried in a browser.
app.UseSwagger();
app.UseSwaggerUI(o => o.UseRequestInterceptor(SwaggerExamples.RequestInterceptor));

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

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
