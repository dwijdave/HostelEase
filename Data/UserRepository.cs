using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class UserRepository
{
    private readonly DbConnection _db;
    public UserRepository(DbConnection db) => _db = db;

    private static User Map(SqlDataReader r) => new()
    {
        UserID = r.Int("UserID"), Email = r.Str("Email"), PasswordHash = r.Str("PasswordHash"),
        Role = r.Str("Role"), IsActive = r.Bool("IsActive")
    };

    public User GetByEmail(string email) =>
        _db.QueryOne("SELECT UserID, Email, PasswordHash, Role, IsActive FROM Users WHERE Email = @e", Map, P("@e", email));

    public bool EmailExists(string email) => _db.Count("SELECT COUNT(*) FROM Users WHERE Email = @e", P("@e", email)) > 0;

    public bool StudentNumberExists(string number) =>
        _db.Count("SELECT COUNT(*) FROM Students WHERE StudentNumber = @n", P("@n", number)) > 0;

    public void UpdatePassword(int userId, string passwordHash) =>
        _db.Execute("UPDATE Users SET PasswordHash = @h WHERE UserID = @id", P("@h", passwordHash), P("@id", userId));

    public bool AdminExists() => _db.Count("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'") > 0;

    public int CreateAdmin(string email, string passwordHash) =>
        Convert.ToInt32(_db.Scalar(
            "INSERT INTO Users (Email, PasswordHash, Role) OUTPUT INSERTED.UserID VALUES (@e, @h, 'Admin')",
            P("@e", email), P("@h", passwordHash)));

    /// <summary>Creates the login (Users) and the profile (Students) together in ONE transaction.</summary>
    public int CreateStudent(RegisterViewModel m, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            int userId;
            using (var c = new SqlCommand(
                "INSERT INTO Users (Email, PasswordHash, Role) OUTPUT INSERTED.UserID VALUES (@e, @h, 'Student')", conn, tx))
            {
                c.Parameters.Add(P("@e", m.Email));
                c.Parameters.Add(P("@h", passwordHash));
                userId = (int)c.ExecuteScalar();
            }

            int studentId;
            using (var c = new SqlCommand(
                @"INSERT INTO Students (UserID, FullName, StudentNumber, Course, Semester, Contact)
                  OUTPUT INSERTED.StudentID VALUES (@u, @n, @num, @c, @s, @p)", conn, tx))
            {
                c.Parameters.Add(P("@u", userId));
                c.Parameters.Add(P("@n", m.FullName.Trim()));
                c.Parameters.Add(P("@num", m.StudentNumber.Trim()));
                c.Parameters.Add(P("@c", m.Course.Trim()));
                c.Parameters.Add(P("@s", m.Semester));
                c.Parameters.Add(P("@p", string.IsNullOrWhiteSpace(m.Contact) ? null : m.Contact.Trim()));
                studentId = (int)c.ExecuteScalar();
            }

            tx.Commit();
            return studentId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
