using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Segfy.Policies.Application.Abstractions;
using Segfy.Policies.Domain;
using Segfy.Policies.Infrastructure.Database;

namespace Segfy.Policies.Infrastructure.Repositories;

public sealed class SqlitePolicyRepository(DatabaseOptions options) : IPolicyRepository
{
    public async Task<InsurancePolicy> AddAsync(
        string insuredDocument,
        string vehiclePlate,
        decimal monthlyPremium,
        DateOnly coverageStartDate,
        DateOnly coverageEndDate,
        PolicyStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        var sequence = await GetNextSequenceAsync(connection, transaction, now.Year, cancellationToken);
        if (sequence > 9999)
            throw new InvalidOperationException($"O limite anual de apólices para {now.Year} foi atingido.");

        var policy = new InsurancePolicy(
            Guid.NewGuid(),
            $"SEG-{now.Year}-{sequence:0000}",
            insuredDocument,
            vehiclePlate,
            monthlyPremium,
            coverageStartDate,
            coverageEndDate,
            status,
            now,
            now);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO policies (
                id, policy_number, insured_document, vehicle_plate, monthly_premium,
                coverage_start_date, coverage_end_date, status, created_at, updated_at
            ) VALUES (
                $id, $number, $document, $plate, $premium,
                $start, $end, $status, $created, $updated
            );
            """;
        AddPolicyParameters(command, policy);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return policy;
    }

    public async Task<InsurancePolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM policies WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPolicy(reader) : null;
    }

    public async Task<(IReadOnlyList<InsurancePolicy> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        PolicyStatus? status,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var where = BuildWhere(search, status);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM policies {where};";
        AddFilterParameters(countCommand, search, status);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM policies
            {where}
            ORDER BY coverage_end_date ASC, created_at DESC
            LIMIT $limit OFFSET $offset;
            """;
        AddFilterParameters(command, search, status);
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        var items = await ReadListAsync(command, cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<InsurancePolicy>> GetExpiringAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // Consulta SQL solicitada no desafio: apólices ativas que vencem no período.
        command.CommandText = $"""
            SELECT {Columns}
            FROM policies
            WHERE status = $active_status
              AND date(coverage_end_date) BETWEEN date($start_date) AND date($end_date)
            ORDER BY date(coverage_end_date) ASC, policy_number ASC;
            """;
        command.Parameters.AddWithValue("$active_status", (int)PolicyStatus.Ativa);
        command.Parameters.AddWithValue("$start_date", FormatDate(startDate));
        command.Parameters.AddWithValue("$end_date", FormatDate(endDate));
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task UpdateAsync(InsurancePolicy policy, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE policies SET
                insured_document = $document,
                vehicle_plate = $plate,
                monthly_premium = $premium,
                coverage_start_date = $start,
                coverage_end_date = $end,
                status = $status,
                updated_at = $updated
            WHERE id = $id;
            """;
        AddPolicyParameters(command, policy);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM policies WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<int> MarkExpiredAsync(
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE policies
            SET status = $expired, updated_at = $updated
            WHERE status = $active
              AND date(coverage_end_date) < date($today);
            """;
        command.Parameters.AddWithValue("$expired", (int)PolicyStatus.Expirada);
        command.Parameters.AddWithValue("$active", (int)PolicyStatus.Ativa);
        command.Parameters.AddWithValue("$updated", FormatTimestamp(now));
        command.Parameters.AddWithValue("$today", FormatDate(today));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<int> GetNextSequenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO policy_sequences (year, last_value)
            VALUES ($year, 1)
            ON CONFLICT(year) DO UPDATE SET last_value = last_value + 1
            RETURNING last_value;
            """;
        command.Parameters.AddWithValue("$year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<InsurancePolicy>> ReadListAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<InsurancePolicy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadPolicy(reader));
        return items;
    }

    private static InsurancePolicy ReadPolicy(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetDecimal(4),
        DateOnly.ParseExact(reader.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly.ParseExact(reader.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        (PolicyStatus)reader.GetInt32(7),
        DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static string BuildWhere(string? search, PolicyStatus? status)
    {
        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            clauses.Add("(policy_number LIKE $search OR insured_document LIKE $search OR vehicle_plate LIKE $search)");
        if (status is not null)
            clauses.Add("status = $status");
        return clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";
    }

    private static void AddFilterParameters(SqliteCommand command, string? search, PolicyStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
            command.Parameters.AddWithValue("$search", $"%{search}%");
        if (status is not null)
            command.Parameters.AddWithValue("$status", (int)status.Value);
    }

    private static void AddPolicyParameters(SqliteCommand command, InsurancePolicy policy)
    {
        command.Parameters.AddWithValue("$id", policy.Id.ToString());
        command.Parameters.AddWithValue("$number", policy.PolicyNumber);
        command.Parameters.AddWithValue("$document", policy.InsuredDocument);
        command.Parameters.AddWithValue("$plate", policy.VehiclePlate);
        command.Parameters.AddWithValue("$premium", policy.MonthlyPremium);
        command.Parameters.AddWithValue("$start", FormatDate(policy.CoverageStartDate));
        command.Parameters.AddWithValue("$end", FormatDate(policy.CoverageEndDate));
        command.Parameters.AddWithValue("$status", (int)policy.Status);
        command.Parameters.AddWithValue("$created", FormatTimestamp(policy.CreatedAt));
        command.Parameters.AddWithValue("$updated", FormatTimestamp(policy.UpdatedAt));
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("O", CultureInfo.InvariantCulture);

    private const string Columns = """
        id, policy_number, insured_document, vehicle_plate, monthly_premium,
        coverage_start_date, coverage_end_date, status, created_at, updated_at
        """;
}
