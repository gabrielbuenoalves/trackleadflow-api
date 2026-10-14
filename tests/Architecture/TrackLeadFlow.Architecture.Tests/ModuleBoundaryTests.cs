using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Module_infrastructure_does_not_reference_another_module_infrastructure()
    {
        var root = FindRepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src", "Modules"), "*.Infrastructure.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(projects);

        var violations = new List<string>();
        foreach (var project in projects)
        {
            var text = File.ReadAllText(project);
            foreach (var line in text.Split('\n'))
            {
                if (!line.Contains("ProjectReference", StringComparison.Ordinal) || !line.Contains(".Infrastructure.csproj", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!line.Contains("TrackLeadFlow.Infrastructure.csproj", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetFileName(project) + " -> " + line.Trim());
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Database_has_no_foreign_key_across_module_schemas()
    {
        using var connection = new NpgsqlConnectionFactory().Open();
        using var command = new NpgsqlCommand(
            """
            select nsp.nspname, refnsp.nspname
            from pg_constraint c
            join pg_namespace nsp on nsp.oid = c.connamespace
            join pg_class refrel on refrel.oid = c.confrelid
            join pg_namespace refnsp on refnsp.oid = refrel.relnamespace
            where c.contype = 'f'
              and nsp.nspname in ('identity', 'whatsapp', 'qualification', 'followup', 'attendance', 'payments', 'marketing')
              and nsp.nspname <> refnsp.nspname
            """,
            connection);
        using var reader = command.ExecuteReader();
        Assert.False(reader.Read());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TrackLeadFlow.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
