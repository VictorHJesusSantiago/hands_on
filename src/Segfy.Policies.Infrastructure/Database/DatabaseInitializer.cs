using Microsoft.Data.Sqlite;

namespace Segfy.Policies.Infrastructure.Database;

public sealed class DatabaseInitializer(DatabaseOptions options)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDatabaseDirectory(options.ConnectionString);

        await using var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS policies (
                id TEXT NOT NULL PRIMARY KEY,
                policy_number TEXT NOT NULL UNIQUE,
                insured_document TEXT NOT NULL,
                vehicle_plate TEXT NOT NULL,
                monthly_premium NUMERIC NOT NULL CHECK (monthly_premium > 0),
                coverage_start_date TEXT NOT NULL,
                coverage_end_date TEXT NOT NULL,
                status INTEGER NOT NULL CHECK (status IN (1, 2, 3)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                CHECK (coverage_end_date > coverage_start_date)
            );

            CREATE TABLE IF NOT EXISTS policy_sequences (
                year INTEGER NOT NULL PRIMARY KEY,
                last_value INTEGER NOT NULL CHECK (last_value > 0)
            );

            CREATE INDEX IF NOT EXISTS ix_policies_coverage_end_date
                ON policies (coverage_end_date);
            CREATE INDEX IF NOT EXISTS ix_policies_status
                ON policies (status);
            CREATE INDEX IF NOT EXISTS ix_policies_insured_document
                ON policies (insured_document);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void EnsureDatabaseDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
            return;

        var fullPath = Path.GetFullPath(builder.DataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}
