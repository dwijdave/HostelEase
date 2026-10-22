using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class ComplaintRepository
{
    private readonly DbConnection _db;
    public ComplaintRepository(DbConnection db) => _db = db;

    private const string Sql = @"
        SELECT c.ComplaintID, c.StudentID, s.FullName AS StudentName, c.Category, c.Description,
               c.DateSubmitted, c.ResolutionStatus, c.AdminResponse
          FROM Complaints c JOIN Students s ON s.StudentID = c.StudentID ";

    private static Complaint Map(SqlDataReader r) => new()
    {
        ComplaintID = r.Int("ComplaintID"), StudentID = r.Int("StudentID"), StudentName = r.Str("StudentName"),
        Category = r.Str("Category"), Description = r.Str("Description"), DateSubmitted = r.Date("DateSubmitted"),
        ResolutionStatus = r.Str("ResolutionStatus"), AdminResponse = r.Str("AdminResponse")
    };

    public List<Complaint> GetAll(string status) =>
        _db.Query(Sql + @"WHERE (@st IS NULL OR c.ResolutionStatus = @st)
                          ORDER BY CASE c.ResolutionStatus WHEN 'Open' THEN 0 WHEN 'In Progress' THEN 1 ELSE 2 END, c.DateSubmitted DESC",
            Map, P("@st", string.IsNullOrEmpty(status) ? null : status));

    public List<Complaint> GetForStudent(int studentId) =>
        _db.Query(Sql + "WHERE c.StudentID=@s ORDER BY c.DateSubmitted DESC", Map, P("@s", studentId));

    public Complaint GetById(int id) => _db.QueryOne(Sql + "WHERE c.ComplaintID=@id", Map, P("@id", id));

    public void Create(int studentId, Complaint c) =>
        _db.Execute("INSERT INTO Complaints (StudentID, Category, Description) VALUES (@s, @c, @d)",
            P("@s", studentId), P("@c", c.Category), P("@d", c.Description.Trim()));

    public void Respond(int id, string status, string response) =>
        _db.Execute("UPDATE Complaints SET ResolutionStatus=@st, AdminResponse=@r WHERE ComplaintID=@id",
            P("@st", status), P("@r", string.IsNullOrWhiteSpace(response) ? null : response.Trim()), P("@id", id));

    public int CountOpen() => _db.Count("SELECT COUNT(*) FROM Complaints WHERE ResolutionStatus <> 'Resolved'");
    public int CountOpenForStudent(int studentId) =>
        _db.Count("SELECT COUNT(*) FROM Complaints WHERE StudentID=@s AND ResolutionStatus <> 'Resolved'", P("@s", studentId));
}
