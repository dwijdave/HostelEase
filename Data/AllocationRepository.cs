using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class AllocationRepository
{
    private readonly DbConnection _db;
    public AllocationRepository(DbConnection db) => _db = db;

    // ---------------- Requests ----------------
    private const string RequestSql = @"
        SELECT q.RequestID, q.StudentID, s.FullName AS StudentName, s.StudentNumber,
               q.PreferredHostelID, ph.Name AS PreferredHostelName, q.PreferredRoomID,
               CASE WHEN pr.RoomID IS NULL THEN NULL ELSE pbh.Name + ' / ' + pb.BlockName + ' / Room ' + pr.RoomNumber END AS PreferredRoomLabel,
               q.RequestDate, q.Status, q.AdminRemarks, q.ReviewedDate
          FROM AllocationRequests q
          JOIN Students s ON s.StudentID = q.StudentID
          LEFT JOIN Hostels ph ON ph.HostelID = q.PreferredHostelID
          LEFT JOIN Rooms pr ON pr.RoomID = q.PreferredRoomID
          LEFT JOIN Blocks pb ON pb.BlockID = pr.BlockID
          LEFT JOIN Hostels pbh ON pbh.HostelID = pb.HostelID ";

    private static AllocationRequest MapRequest(SqlDataReader r) => new()
    {
        RequestID = r.Int("RequestID"), StudentID = r.Int("StudentID"), StudentName = r.Str("StudentName"),
        StudentNumber = r.Str("StudentNumber"), PreferredHostelID = r.IntN("PreferredHostelID"),
        PreferredHostelName = r.Str("PreferredHostelName"), PreferredRoomID = r.IntN("PreferredRoomID"),
        PreferredRoomLabel = r.Str("PreferredRoomLabel"), RequestDate = r.Date("RequestDate"),
        Status = r.Str("Status"), AdminRemarks = r.Str("AdminRemarks"), ReviewedDate = r.DateN("ReviewedDate")
    };

    public List<AllocationRequest> GetRequests(string status, string q)
    {
        string like = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
        return _db.Query(RequestSql + @"
            WHERE (@st IS NULL OR q.Status = @st)
              AND (@like IS NULL OR s.FullName LIKE @like OR s.StudentNumber LIKE @like)
            ORDER BY CASE q.Status WHEN 'Pending' THEN 0 ELSE 1 END, q.RequestDate DESC",
            MapRequest, P("@st", string.IsNullOrEmpty(status) ? null : status), P("@like", like));
    }

    public List<AllocationRequest> GetRequestsForStudent(int studentId) =>
        _db.Query(RequestSql + "WHERE q.StudentID = @s ORDER BY q.RequestDate DESC", MapRequest, P("@s", studentId));

    public List<AllocationRequest> GetRecentRequests(int count) =>
        _db.Query(RequestSql.Replace("SELECT q.RequestID", $"SELECT TOP ({count}) q.RequestID") + "ORDER BY q.RequestDate DESC", MapRequest);

    public AllocationRequest GetRequest(int id) => _db.QueryOne(RequestSql + "WHERE q.RequestID = @id", MapRequest, P("@id", id));

    public bool HasPendingRequest(int studentId) =>
        _db.Count("SELECT COUNT(*) FROM AllocationRequests WHERE StudentID=@s AND Status='Pending'", P("@s", studentId)) > 0;

    public void CreateRequest(int studentId, int? hostelId, int? roomId) =>
        _db.Execute("INSERT INTO AllocationRequests (StudentID, PreferredHostelID, PreferredRoomID) VALUES (@s, @h, @r)",
            P("@s", studentId), P("@h", hostelId), P("@r", roomId));

    public bool Cancel(int requestId, int studentId) =>
        _db.Execute(@"UPDATE AllocationRequests SET Status='Cancelled', ReviewedDate=SYSDATETIME()
                      WHERE RequestID=@id AND StudentID=@s AND Status='Pending'", P("@id", requestId), P("@s", studentId)) > 0;

    public bool Reject(int requestId, string remarks) =>
        _db.Execute(@"UPDATE AllocationRequests SET Status='Rejected', AdminRemarks=@rem, ReviewedDate=SYSDATETIME()
                      WHERE RequestID=@id AND Status='Pending'",
            P("@id", requestId), P("@rem", string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim())) > 0;

    /// <summary>
    /// Approves a request and assigns a room - ALL inside one transaction.
    /// The room row is locked (UPDLOCK) before counting occupants, so two admins approving at the same moment
    /// are forced to queue up and only ONE of them can take the last free bed.
    /// </summary>
    public (bool Success, string Message) Approve(int requestId, int roomId, string remarks)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        object Scalar(string sql, params SqlParameter[] ps)
        {
            using var c = new SqlCommand(sql, conn, tx);
            c.Parameters.AddRange(ps);
            return c.ExecuteScalar();
        }

        try
        {
            // 1. lock the request and make sure it is still pending
            var status = Scalar("SELECT Status FROM AllocationRequests WITH (UPDLOCK, ROWLOCK) WHERE RequestID=@id", P("@id", requestId)) as string;
            if (status == null) return (false, "Request not found.");
            if (status != "Pending") return (false, $"This request was already {status.ToLower()}.");
            int studentId = (int)Scalar("SELECT StudentID FROM AllocationRequests WHERE RequestID=@id", P("@id", requestId));

            // 2. lock the room row; it must be bookable
            var capacityObj = Scalar(@"SELECT r.Capacity FROM Rooms r WITH (UPDLOCK, ROWLOCK)
                                        JOIN Blocks b ON b.BlockID = r.BlockID JOIN Hostels h ON h.HostelID = b.HostelID
                                       WHERE r.RoomID=@r AND r.Status='Available' AND b.IsActive=1 AND h.IsActive=1", P("@r", roomId));
            if (capacityObj == null) return (false, "That room is not available for allocation.");

            // 3. capacity check (safe, because we hold the room lock)
            int occupied = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Allocations WHERE RoomID=@r AND Status='Active'", P("@r", roomId)));
            if (occupied >= (int)capacityObj) return (false, "That room is already full. Choose another room.");

            // 4. the student must be active and must not already hold a room
            int studentActive = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Students WHERE StudentID=@s AND IsActive=1", P("@s", studentId)));
            if (studentActive == 0) return (false, "This student account is deactivated.");
            int hasRoom = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Allocations WHERE StudentID=@s AND Status='Active'", P("@s", studentId)));
            if (hasRoom > 0) return (false, "This student already has an active room allocation.");

            // 5. create the allocation and close the request
            Scalar("INSERT INTO Allocations (StudentID, RoomID, RequestID) VALUES (@s, @r, @q)", P("@s", studentId), P("@r", roomId), P("@q", requestId));
            Scalar(@"UPDATE AllocationRequests SET Status='Approved', AdminRemarks=@rem, ReviewedDate=SYSDATETIME() WHERE RequestID=@id",
                P("@rem", string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim()), P("@id", requestId));

            tx.Commit();
            return (true, "Request approved and room allocated.");
        }
        catch (SqlException)
        {
            tx.Rollback();
            return (false, "The room could not be allocated because of a database conflict. Please try again.");
        }
    }

    // ---------------- Allocations ----------------
    private const string AllocationSql = @"
        SELECT a.AllocationID, a.StudentID, s.FullName AS StudentName, s.StudentNumber, a.RoomID,
               h.Name AS HostelName, b.BlockName, r.RoomNumber, a.AllocationDate, a.EndDate, a.Status
          FROM Allocations a
          JOIN Students s ON s.StudentID = a.StudentID
          JOIN Rooms r ON r.RoomID = a.RoomID
          JOIN Blocks b ON b.BlockID = r.BlockID
          JOIN Hostels h ON h.HostelID = b.HostelID ";

    private static Allocation MapAllocation(SqlDataReader r) => new()
    {
        AllocationID = r.Int("AllocationID"), StudentID = r.Int("StudentID"), StudentName = r.Str("StudentName"),
        StudentNumber = r.Str("StudentNumber"), RoomID = r.Int("RoomID"), HostelName = r.Str("HostelName"),
        BlockName = r.Str("BlockName"), RoomNumber = r.Str("RoomNumber"), AllocationDate = r.Date("AllocationDate"),
        EndDate = r.DateN("EndDate"), Status = r.Str("Status")
    };

    public List<Allocation> GetAllocations(string status, string q)
    {
        string like = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
        return _db.Query(AllocationSql + @"
            WHERE (@st IS NULL OR a.Status = @st)
              AND (@like IS NULL OR s.FullName LIKE @like OR s.StudentNumber LIKE @like OR r.RoomNumber LIKE @like OR h.Name LIKE @like)
            ORDER BY a.AllocationDate DESC, a.AllocationID DESC",
            MapAllocation, P("@st", string.IsNullOrEmpty(status) ? null : status), P("@like", like));
    }

    public Allocation GetActiveForStudent(int studentId) =>
        _db.QueryOne(AllocationSql + "WHERE a.StudentID=@s AND a.Status='Active'", MapAllocation, P("@s", studentId));

    /// <summary>Check-out: ends the allocation, which frees the bed automatically.</summary>
    public bool Vacate(int allocationId) =>
        _db.Execute("UPDATE Allocations SET Status='Ended', EndDate=CAST(GETDATE() AS DATE) WHERE AllocationID=@id AND Status='Active'",
            P("@id", allocationId)) > 0;
}
