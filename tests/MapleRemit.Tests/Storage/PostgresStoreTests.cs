using Dapper;
using MapleRemit.Api.Models;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MapleRemit.Tests.Storage;

// One Postgres container per test class (needs Docker).
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
        new PostgresStore(DataSource).EnsureSchema();
    }

    public void Reset()
    {
        using var connection = DataSource.OpenConnection();
        connection.Execute("TRUNCATE transfers, quotes");
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

public class PostgresStoreTests : StoreContractTests, IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _db;
    private readonly PostgresStore _store;

    public PostgresStoreTests(PostgresFixture db)
    {
        _db = db;
        db.Reset();
        _store = new PostgresStore(db.DataSource);
    }

    protected override IQuoteStore Quotes => _store;
    protected override ITransferStore Transfers => _store;

    // A DB round trip makes the race window wide, so fewer rounds are enough.
    protected override int ConcurrencyRounds => 5;

    private Guid AddQuote()
    {
        var quote = new Quote(Guid.NewGuid(), 100m, 2.99m, 102.99m, 24.50m, 2450m, Now, Now.AddMinutes(15));
        _store.AddQuote(quote);
        return quote.Id;
    }

    // Raw insert: skips the store's locks, as a bug or another writer would.
    private void InsertTransfer(Guid quoteId, string customerId, string key)
    {
        using var connection = _db.DataSource.OpenConnection();
        connection.Execute(
            """
            INSERT INTO transfers (id, customer_id, quote_id, idempotency_key, amount_cad, fee_cad, total_cad,
                                   exchange_rate, amount_thb, created_at)
            VALUES (@id, @customerId, @quoteId, @key, 100, 2.99, 102.99, 24.50, 2450, @Now)
            """,
            new { id = Guid.NewGuid(), customerId, quoteId, key, Now });
    }

    [Fact]
    public void Replay_returns_an_identical_transfer_when_the_clock_has_sub_microsecond_ticks()
    {
        var clock = new FakeTimeProvider(Now.AddTicks(1234567));
        var quotes = new QuoteService(_store, clock, Options.Create(new ExchangeRateOptions { ThbPerCad = 24.50m }));
        var transfers = new TransferService(_store, clock);
        var quoteId = quotes.Create(100m).Id.ToString();
        var key = Guid.NewGuid().ToString();

        var first = transfers.Create("cust-1", quoteId, key);
        var replay = transfers.Create("cust-1", quoteId, key);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Transfer, replay.Transfer);
    }

    [Fact]
    public void Schema_rejects_two_transfers_for_one_quote()
    {
        var quoteId = AddQuote();
        InsertTransfer(quoteId, "cust-1", "key-1");

        var ex = Assert.Throws<PostgresException>(() => InsertTransfer(quoteId, "cust-2", "key-2"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public void Schema_rejects_a_reused_idempotency_key_for_one_customer()
    {
        InsertTransfer(AddQuote(), "cust-1", "key-1");

        var ex = Assert.Throws<PostgresException>(() => InsertTransfer(AddQuote(), "cust-1", "key-1"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task Schema_creation_is_safe_when_instances_start_together()
    {
        var database = $"schema_{Guid.NewGuid():N}";
        using (var connection = _db.DataSource.OpenConnection())
            connection.Execute($"CREATE DATABASE {database}");
        var connectionString = new NpgsqlConnectionStringBuilder(_db.ConnectionString) { Database = database }.ToString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        // Throws if any instance fails.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => new PostgresStore(dataSource).EnsureSchema())));
    }

    [Fact]
    public void Is_ready_when_the_database_is_reachable()
    {
        Assert.True(_store.IsReady());
    }

    [Fact]
    public void Is_not_ready_when_the_database_is_unreachable()
    {
        using var unreachable = NpgsqlDataSource.Create("Host=localhost;Port=1;Timeout=1");

        Assert.False(new PostgresStore(unreachable).IsReady());
    }

    [Fact]
    public void Unreachable_database_is_logged_as_a_warning_with_the_cause()
    {
        var logs = new CapturingLoggerProvider();
        var logger = new Logger<PostgresStore>(new LoggerFactory([logs]));
        using var unreachable = NpgsqlDataSource.Create("Host=localhost;Port=1;Timeout=1");

        new PostgresStore(unreachable, logger).IsReady();

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Exception);
    }
}
