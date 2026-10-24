using Dapper;
using MapleRemit.Api.Storage;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MapleRemit.Tests.Storage;

// One Postgres container per test class (needs Docker).
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

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
    private readonly PostgresStore _store;

    public PostgresStoreTests(PostgresFixture db)
    {
        db.Reset();
        _store = new PostgresStore(db.DataSource);
    }

    protected override IQuoteStore Quotes => _store;
    protected override ITransferStore Transfers => _store;

    // A DB round trip makes the race window wide, so fewer rounds are enough.
    protected override int ConcurrencyRounds => 5;
}
