using Microsoft.Data.SqlClient;

namespace HostelEase.Data;

/// <summary>
/// Central ADO.NET helper. Every repository uses it to open connections and run
/// PARAMETERIZED queries (parameters prevent SQL injection).
/// </summary>
public class DbConnection
{
    private readonly string _connectionString;

    public DbConnection(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HostelEase")
            ?? throw new InvalidOperationException("Connection string 'HostelEase' is missing in appsettings.json.");
    }

    public SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    /// <summary>Creates a parameter; a C# null becomes SQL NULL.</summary>
    public static SqlParameter P(string name, object value) => new SqlParameter(name, value ?? DBNull.Value);

    /// <summary>Runs a SELECT and maps each row with the supplied function.</summary>
    public List<T> Query<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
    {
        var list = new List<T>();
        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
        conn.Open();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(map(reader));
        return list;
    }

    public T QueryOne<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters) where T : class
        => Query(sql, map, parameters).FirstOrDefault();

    /// <summary>INSERT / UPDATE / DELETE. Returns the number of rows affected.</summary>
    public int Execute(string sql, params SqlParameter[] parameters)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    public object Scalar(string sql, params SqlParameter[] parameters)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters);
        conn.Open();
        return cmd.ExecuteScalar();
    }

    public int Count(string sql, params SqlParameter[] parameters)
    {
        var o = Scalar(sql, parameters);
        return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
    }
}

/// <summary>Small readability helpers: reader.Str("Name") instead of reader.GetString(reader.GetOrdinal("Name")).</summary>
public static class ReaderExtensions
{
    public static string Str(this SqlDataReader r, string col) { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetString(i); }
    public static int Int(this SqlDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));
    public static int? IntN(this SqlDataReader r, string col) { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetInt32(i); }
    public static bool Bool(this SqlDataReader r, string col) => r.GetBoolean(r.GetOrdinal(col));
    public static DateTime Date(this SqlDataReader r, string col) => r.GetDateTime(r.GetOrdinal(col));
    public static DateTime? DateN(this SqlDataReader r, string col) { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetDateTime(i); }
}
