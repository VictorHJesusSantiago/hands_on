namespace Segfy.Policies.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = "Data Source=data/policies.db";
}
