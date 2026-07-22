using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Gatherstead.Data.Tests;

/// <summary>
/// Guards the convention documented in docs/ARCHITECTURE.md: raw SQL in a migration must be
/// EXEC-wrapped. An --idempotent script emits raw SQL verbatim inside its
/// IF NOT EXISTS(... __EFMigrationsHistory ...) guard, and SQL Server binds column names when it
/// compiles the batch even when the guard is false — so a data migration that reads a column its own
/// migration later drops stops the whole script parsing once it has been applied.
/// </summary>
public class MigrationConventionsTests
{
    private static IEnumerable<Migration> AllMigrations()
        => typeof(GathersteadDbContext).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t =>
            {
                var migration = (Migration)Activator.CreateInstance(t)!;
                migration.ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer";
                return migration;
            });

    public static TheoryData<string> MigrationNames()
    {
        var data = new TheoryData<string>();
        foreach (var migration in AllMigrations())
        {
            data.Add(migration.GetType().Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MigrationNames))]
    public void RawSql_IsExecWrapped(string migrationName)
    {
        var migration = AllMigrations().Single(m => m.GetType().Name == migrationName);

        var rawSql = migration.UpOperations.Concat(migration.DownOperations)
            .OfType<SqlOperation>()
            .Select(op => op.Sql.Trim())
            .Where(sql => !sql.StartsWith("EXEC(", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            rawSql.Count == 0,
            $"{migrationName} calls migrationBuilder.Sql with SQL that is not EXEC-wrapped. Wrap it in "
                + $"EXEC(N'...') (doubling any single quotes) so --idempotent scripts stay parseable:{Environment.NewLine}"
                + string.Join(Environment.NewLine + "---" + Environment.NewLine, rawSql));
    }

    [Fact]
    public void MigrationsAssembly_ContainsMigrations()
        => Assert.NotEmpty(AllMigrations());
}
