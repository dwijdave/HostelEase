using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace HostelEase.Data;

/// <summary>
/// Creates (or upgrades) the HostelEase database at startup by running the embedded
/// Database/01_Create_HostelEase.sql script. The script is safe to run on every start.
/// </summary>
public class DbInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DbInitializer> _log;

    public DbInitializer(IConfiguration configuration, ILogger<DbInitializer> log)
    {
        _connectionString = configuration.GetConnectionString("HostelEase");
        _log = log;
    }

    public void Initialize()
    {
        // Connect to 'master' because the HostelEase database may not exist yet; the script does "USE HostelEase".
        var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        string script = ReadScript();

        using var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();

        // SSMS understands "GO" but SQL Server itself does not, so split the script into batches.
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 60 };
            cmd.ExecuteNonQuery();
        }
        _log.LogInformation("HostelEase database is ready.");
    }

    private static string ReadScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HostelEase.schema.sql")
            ?? throw new InvalidOperationException("Embedded schema script not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
