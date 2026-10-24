using Dapper;
using MapleRemit.Api.Models;
using MapleRemit.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MapleRemit.Api.Storage;

public sealed class PostgresStore(NpgsqlDataSource dataSource, ILogger<PostgresStore>? logger = null)
    : IQuoteStore, ITransferStore, IStoreHealth
{
    private readonly ILogger _logger = logger ?? NullLogger<PostgresStore>.Instance;

    static PostgresStore() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    // Separate lock namespaces for schema and customers.
    private const int SchemaLock = 1;
    private const int CustomerLock = 2;

    private const string QuoteColumns =
        "id, amount_cad, fee_cad, total_cad, exchange_rate, amount_thb, created_at, expires_at";

    private const string TransferColumns =
        "id, customer_id, quote_id, amount_cad, fee_cad, total_cad, exchange_rate, amount_thb, created_at";

    // seq breaks ties on equal created_at.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS quotes (
            id            uuid PRIMARY KEY,
            amount_cad    numeric(12, 2) NOT NULL,
            fee_cad       numeric(12, 2) NOT NULL,
            total_cad     numeric(12, 2) NOT NULL,
            exchange_rate numeric NOT NULL,
            amount_thb    numeric(14, 2) NOT NULL,
            created_at    timestamptz NOT NULL,
            expires_at    timestamptz NOT NULL
        );

        CREATE TABLE IF NOT EXISTS transfers (
            seq             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            id              uuid NOT NULL UNIQUE,
            customer_id     text NOT NULL,
            quote_id        uuid NOT NULL UNIQUE REFERENCES quotes (id),
            idempotency_key text NOT NULL,
            amount_cad      numeric(12, 2) NOT NULL,
            fee_cad         numeric(12, 2) NOT NULL,
            total_cad       numeric(12, 2) NOT NULL,
            exchange_rate   numeric NOT NULL,
            amount_thb      numeric(14, 2) NOT NULL,
            created_at      timestamptz NOT NULL,
            UNIQUE (customer_id, idempotency_key)
        );
        """;

    public void EnsureSchema()
    {
        using var connection = dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        // Instances starting together wait instead of racing.
        connection.Execute("SELECT pg_advisory_xact_lock(@SchemaLock, 0)", new { SchemaLock }, transaction);
        connection.Execute(Schema, transaction: transaction);
        transaction.Commit();
    }

    public void AddQuote(Quote quote)
    {
        using var connection = dataSource.OpenConnection();
        connection.Execute(
            $"""
            INSERT INTO quotes ({QuoteColumns})
            VALUES (@Id, @AmountCad, @FeeCad, @TotalCad, @ExchangeRate, @AmountThb, @CreatedAt, @ExpiresAt)
            """,
            quote);
    }

    public Quote? GetQuote(Guid id)
    {
        using var connection = dataSource.OpenConnection();
        return connection.QuerySingleOrDefault<QuoteRow>(
            $"SELECT {QuoteColumns} FROM quotes WHERE id = @id", new { id })?.ToModel();
    }

    public TransferResult CreateTransfer(string customerId, Guid quoteId, string idempotencyKey, DateTimeOffset now)
    {
        using var connection = dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();

        // Serialize per customer (key check + daily limit).
        connection.Execute("SELECT pg_advisory_xact_lock(@CustomerLock, hashtext(@customerId))",
            new { CustomerLock, customerId }, transaction);

        // Key first: replays work after the quote expires.
        var existing = connection.QuerySingleOrDefault<TransferRow>(
            $"SELECT {TransferColumns} FROM transfers WHERE customer_id = @customerId AND idempotency_key = @idempotencyKey",
            new { customerId, idempotencyKey }, transaction);
        if (existing is not null)
        {
            if (existing.QuoteId != quoteId)
                throw StoreErrors.IdempotencyKeyReused();
            return new TransferResult(existing.ToModel(), Replayed: true);
        }

        // Stops two customers using one quote at once.
        var quote = connection.QuerySingleOrDefault<QuoteRow>(
                $"SELECT {QuoteColumns} FROM quotes WHERE id = @quoteId FOR UPDATE", new { quoteId }, transaction)
            ?.ToModel() ?? throw StoreErrors.QuoteNotFound();

        var quoteUsed = connection.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM transfers WHERE quote_id = @quoteId)", new { quoteId }, transaction);

        var sentInWindowCad = connection.ExecuteScalar<decimal>(
            "SELECT COALESCE(SUM(amount_cad), 0) FROM transfers WHERE customer_id = @customerId AND created_at > @windowStart",
            new { customerId, windowStart = now - TransferRules.DailyWindow }, transaction);

        TransferRules.EnsureAllowed(quote, quoteUsed, sentInWindowCad, now);

        var transfer = new Transfer(Guid.NewGuid(), customerId, quote.Id, quote.AmountCad, quote.FeeCad,
            quote.TotalCad, quote.ExchangeRate, quote.AmountThb, now);

        connection.Execute(
            $"""
            INSERT INTO transfers ({TransferColumns}, idempotency_key)
            VALUES (@Id, @CustomerId, @QuoteId, @AmountCad, @FeeCad, @TotalCad, @ExchangeRate, @AmountThb, @CreatedAt, @IdempotencyKey)
            """,
            new
            {
                transfer.Id, transfer.CustomerId, transfer.QuoteId, transfer.AmountCad, transfer.FeeCad,
                transfer.TotalCad, transfer.ExchangeRate, transfer.AmountThb, transfer.CreatedAt,
                IdempotencyKey = idempotencyKey,
            },
            transaction);

        transaction.Commit();
        return new TransferResult(transfer, Replayed: false);
    }

    public TransferPage ListTransfers(string customerId, int skip, int take)
    {
        using var connection = dataSource.OpenConnection();
        // One snapshot, so totalCount matches items.
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        var items = connection.Query<TransferRow>(
                $"""
                SELECT {TransferColumns} FROM transfers WHERE customer_id = @customerId
                ORDER BY created_at DESC, seq DESC
                LIMIT @take OFFSET @skip
                """,
                new { customerId, skip, take }, transaction)
            .Select(row => row.ToModel())
            .ToList();
        var total = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM transfers WHERE customer_id = @customerId", new { customerId }, transaction);
        transaction.Commit();
        return new TransferPage(items, total);
    }

    public bool IsReady()
    {
        try
        {
            using var connection = dataSource.OpenConnection();
            connection.ExecuteScalar<int>("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            // Warning, not Error: /health may be polled often during an outage.
            _logger.LogWarning(ex, "Database health check failed");
            return false;
        }
    }

    // timestamptz reads as UTC DateTime; convert to DateTimeOffset.
    private sealed class QuoteRow
    {
        public Guid Id { get; set; }
        public decimal AmountCad { get; set; }
        public decimal FeeCad { get; set; }
        public decimal TotalCad { get; set; }
        public decimal ExchangeRate { get; set; }
        public decimal AmountThb { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public Quote ToModel() => new(Id, AmountCad, FeeCad, TotalCad, ExchangeRate, AmountThb,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero), new DateTimeOffset(ExpiresAt, TimeSpan.Zero));
    }

    private sealed class TransferRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public Guid QuoteId { get; set; }
        public decimal AmountCad { get; set; }
        public decimal FeeCad { get; set; }
        public decimal TotalCad { get; set; }
        public decimal ExchangeRate { get; set; }
        public decimal AmountThb { get; set; }
        public DateTime CreatedAt { get; set; }

        public Transfer ToModel() => new(Id, CustomerId, QuoteId, AmountCad, FeeCad, TotalCad, ExchangeRate,
            AmountThb, new DateTimeOffset(CreatedAt, TimeSpan.Zero));
    }
}
