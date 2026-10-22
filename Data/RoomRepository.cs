using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class RoomRepository
{
    private readonly DbConnection _db;
    public RoomRepository(DbConnection db) => _db = db;

    // "Occupied" is always COUNTED from active allocations, so it can never get out of sync.
    private const string RoomSql = @"
        SELECT r.RoomID, r.BlockID, r.RoomNumber, r.Capacity, r.Status,
               b.BlockName, b.IsActive AS BlockActive, h.HostelID, h.Name AS HostelName, h.IsActive AS HostelActive,
               (SELECT COUNT(*) FROM Allocations a WHERE a.RoomID = r.RoomID AND a.Status = 'Active') AS Occupied
          FROM Rooms r
          JOIN Blocks b ON b.BlockID = r.BlockID
          JOIN Hostels h ON h.HostelID = b.HostelID";

    private static Room Map(SqlDataReader r) => new()
    {
        RoomID = r.Int("RoomID"), BlockID = r.Int("BlockID"), RoomNumber = r.Str("RoomNumber"),
        Capacity = r.Int("Capacity"), Status = r.Str("Status"), BlockName = r.Str("BlockName"),
        BlockActive = r.Bool("BlockActive"), HostelID = r.Int("HostelID"), HostelName = r.Str("HostelName"),
        HostelActive = r.Bool("HostelActive"), Occupied = r.Int("Occupied")
    };

    /// <param name="availability">null/"" = all, "available" = has a free bed, "full" = no free bed</param>
    public List<Room> Search(string q, int? hostelId, string availability, string status)
    {
        string like = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
        return _db.Query($@"SELECT * FROM ({RoomSql}) x
            WHERE (@like IS NULL OR x.RoomNumber LIKE @like OR x.BlockName LIKE @like OR x.HostelName LIKE @like)
              AND (@h IS NULL OR x.HostelID = @h)
              AND (@st IS NULL OR x.Status = @st)
              AND (@av IS NULL
                   OR (@av = 'available' AND x.Status = 'Available' AND x.BlockActive = 1 AND x.HostelActive = 1 AND x.Occupied < x.Capacity)
                   OR (@av = 'full' AND x.Occupied >= x.Capacity))
            ORDER BY x.HostelName, x.BlockName, x.RoomNumber",
            Map, P("@like", like), P("@h", hostelId),
            P("@st", string.IsNullOrEmpty(status) ? null : status),
            P("@av", string.IsNullOrEmpty(availability) ? null : availability));
    }

    /// <summary>Rooms a student could be placed in right now.</summary>
    public List<Room> GetAllocatable() => Search(null, null, "available", null);

    public Room GetById(int id) => _db.QueryOne(RoomSql + " WHERE r.RoomID = @id", Map, P("@id", id));

    public bool Exists(int blockId, string number, int excludeId) =>
        _db.Count("SELECT COUNT(*) FROM Rooms WHERE BlockID=@b AND RoomNumber=@n AND RoomID<>@id",
            P("@b", blockId), P("@n", number), P("@id", excludeId)) > 0;

    public void Create(Room r) =>
        _db.Execute("INSERT INTO Rooms (BlockID, RoomNumber, Capacity, Status) VALUES (@b, @n, @c, @s)",
            P("@b", r.BlockID), P("@n", r.RoomNumber.Trim()), P("@c", r.Capacity), P("@s", r.Status));

    public void Update(Room r) =>
        _db.Execute("UPDATE Rooms SET BlockID=@b, RoomNumber=@n, Capacity=@c, Status=@s WHERE RoomID=@id",
            P("@b", r.BlockID), P("@n", r.RoomNumber.Trim()), P("@c", r.Capacity), P("@s", r.Status), P("@id", r.RoomID));

    public void SetStatus(int id, string status) =>
        _db.Execute("UPDATE Rooms SET Status=@s WHERE RoomID=@id", P("@s", status), P("@id", id));

    public int OccupiedCount(int roomId) =>
        _db.Count("SELECT COUNT(*) FROM Allocations WHERE RoomID=@id AND Status='Active'", P("@id", roomId));
}
