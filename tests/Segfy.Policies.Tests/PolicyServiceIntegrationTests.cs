using Segfy.Policies.Application.Abstractions;
using Segfy.Policies.Application.Contracts;
using Segfy.Policies.Application.Exceptions;
using Segfy.Policies.Application.Services;
using Segfy.Policies.Domain;
using Segfy.Policies.Infrastructure.Database;
using Segfy.Policies.Infrastructure.Repositories;

namespace Segfy.Policies.Tests;

public sealed class PolicyServiceIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"segfy-policies-tests-{Guid.NewGuid():N}.db");
    private PolicyService _service = null!;
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        var options = new DatabaseOptions { ConnectionString = $"Data Source={_databasePath}" };
        await new DatabaseInitializer(options).InitializeAsync();
        _service = new PolicyService(new SqlitePolicyRepository(options), _clock);
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_GeneratesSequentialPolicyNumber()
    {
        var first = await CreatePolicyAsync("ABC1D23", new DateOnly(2026, 7, 20));
        var second = await CreatePolicyAsync("DEF2G34", new DateOnly(2026, 8, 20));

        Assert.Equal("SEG-2026-0001", first.PolicyNumber);
        Assert.Equal("SEG-2026-0002", second.PolicyNumber);
    }

    [Fact]
    public async Task Delete_DoesNotReusePolicyNumber()
    {
        var first = await CreatePolicyAsync("ABC1D23", new DateOnly(2026, 7, 20));
        await _service.DeleteAsync(first.Id, CancellationToken.None);

        var second = await CreatePolicyAsync("DEF2G34", new DateOnly(2026, 8, 20));

        Assert.Equal("SEG-2026-0002", second.PolicyNumber);
    }

    [Fact]
    public async Task Expiring_ReturnsOnlyActivePoliciesWithinNextThirtyDays()
    {
        var withinRange = await CreatePolicyAsync("ABC1D23", new DateOnly(2026, 7, 20));
        await CreatePolicyAsync("DEF2G34", new DateOnly(2026, 9, 20));
        var cancelled = await CreatePolicyAsync("GHI3J45", new DateOnly(2026, 7, 15));
        await _service.UpdateAsync(cancelled.Id, new UpdatePolicyRequest(
            cancelled.InsuredDocument, cancelled.VehiclePlate, cancelled.MonthlyPremium,
            cancelled.CoverageStartDate, cancelled.CoverageEndDate, PolicyStatus.Cancelada), CancellationToken.None);

        var result = await _service.GetExpiringAsync(30, CancellationToken.None);

        var policy = Assert.Single(result);
        Assert.Equal(withinRange.Id, policy.Id);
    }

    [Fact]
    public async Task List_MarksPastActivePolicyAsExpired()
    {
        var policy = await CreatePolicyAsync("ABC1D23", new DateOnly(2026, 7, 2));
        _clock.UtcNow = _clock.UtcNow.AddDays(2);

        var result = await _service.GetAsync(policy.Id, CancellationToken.None);

        Assert.Equal(PolicyStatus.Expirada, result.Status);
    }

    [Fact]
    public async Task MissingPolicy_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private Task<PolicyResponse> CreatePolicyAsync(string plate, DateOnly endDate) =>
        _service.CreateAsync(new CreatePolicyRequest(
            "52998224725",
            plate,
            249.90m,
            new DateOnly(2026, 7, 1),
            endDate,
            PolicyStatus.Ativa), CancellationToken.None);

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
