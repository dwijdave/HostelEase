using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class StudentRepository
{
    private readonly DbConnection _db;
    public StudentRepository(DbConnection db) => _db = db;

    private const string BaseSql = @"
        SELECT s.StudentID, s.UserID, s.FullName, s.StudentNumber, u.Email, s.Course, s.Semester, s.Contact,
               s.IsActive, s.CreatedAt,
               (SELECT TOP 1 h.Name + ' / ' + b.BlockName + ' / Room ' + r.RoomNumber
                  FROM Allocations a
                  JOIN Rooms r ON r.RoomID = a.RoomID
                  JOIN Blocks b ON b.BlockID = r.BlockID
                  JOIN Hostels h ON h.HostelID = b.HostelID
                 WHERE a.StudentID = s.StudentID AND a.Status = 'Active') AS CurrentRoom
          FROM Students s JOIN Users u ON u.UserID = s.UserID ";

    private static Student Map(SqlDataReader r) => new()
    {
        StudentID = r.Int("StudentID"), UserID = r.Int("UserID"), FullName = r.Str("FullName"),
        StudentNumber = r.Str("StudentNumber"), Email = r.Str("Email"), Course = r.Str("Course"),
        Semester = r.Int("Semester"), Contact = r.Str("Contact"), IsActive = r.Bool("IsActive"),
        CreatedAt = r.Date("CreatedAt"), CurrentRoom = r.Str("CurrentRoom")
    };

    /// <summary>Search by name / student ID / email; optional active filter.</summary>
    public List<Student> Search(string q, bool? active)
    {
        string like = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
        return _db.Query(BaseSql + @"
            WHERE (@like IS NULL OR s.FullName LIKE @like OR s.StudentNumber LIKE @like OR u.Email LIKE @like)
              AND (@active IS NULL OR s.IsActive = @active)
            ORDER BY s.FullName",
            Map, P("@like", like), P("@active", active));
    }

    public Student GetById(int id) => _db.QueryOne(BaseSql + "WHERE s.StudentID = @id", Map, P("@id", id));
    public Student GetByUserId(int userId) => _db.QueryOne(BaseSql + "WHERE s.UserID = @id", Map, P("@id", userId));

    public List<Student> Recent(int count) =>
        _db.Query($"SELECT TOP ({count}) * FROM ({BaseSql}) x ORDER BY x.CreatedAt DESC", Map);

    public void Update(Student s) =>
        _db.Execute("UPDATE Students SET FullName=@n, Course=@c, Semester=@s, Contact=@p WHERE StudentID=@id",
            P("@n", s.FullName.Trim()), P("@c", s.Course.Trim()), P("@s", s.Semester),
            P("@p", string.IsNullOrWhiteSpace(s.Contact) ? null : s.Contact.Trim()), P("@id", s.StudentID));

    /// <summary>Deactivate / reactivate (students are never hard-deleted so history is kept).</summary>
    public void SetActive(int id, bool active) =>
        _db.Execute(@"UPDATE Students SET IsActive = @a WHERE StudentID = @id;
                      UPDATE Users SET IsActive = @a WHERE UserID = (SELECT UserID FROM Students WHERE StudentID = @id);",
            P("@a", active), P("@id", id));

    public int CountActive() => _db.Count("SELECT COUNT(*) FROM Students WHERE IsActive = 1");
}
