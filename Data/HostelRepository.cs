using HostelEase.Models;
using Microsoft.Data.SqlClient;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

/// <summary>Hostels and their Blocks.</summary>
public class HostelRepository
{
    private readonly DbConnection _db;
    public HostelRepository(DbConnection db) => _db = db;

    // ---------------- Hostels ----------------
    private const string HostelSql = @"
        SELECT h.HostelID, h.Name, h.Location, h.Type, h.IsActive,
               (SELECT COUNT(*) FROM Rooms r JOIN Blocks b ON b.BlockID = r.BlockID
                 WHERE b.HostelID = h.HostelID AND r.Status <> 'Inactive') AS TotalRooms,
               (SELECT ISNULL(SUM(r.Capacity), 0) FROM Rooms r JOIN Blocks b ON b.BlockID = r.BlockID
                 WHERE b.HostelID = h.HostelID AND r.Status <> 'Inactive') AS TotalBeds,
               (SELECT COUNT(*) FROM Allocations a JOIN Rooms r ON r.RoomID = a.RoomID JOIN Blocks b ON b.BlockID = r.BlockID
                 WHERE b.HostelID = h.HostelID AND a.Status = 'Active') AS OccupiedBeds
          FROM Hostels h ";

    private static Hostel MapHostel(SqlDataReader r) => new()
    {
        HostelID = r.Int("HostelID"), Name = r.Str("Name"), Location = r.Str("Location"), Type = r.Str("Type"),
        IsActive = r.Bool("IsActive"), TotalRooms = r.Int("TotalRooms"), TotalBeds = r.Int("TotalBeds"),
        OccupiedBeds = r.Int("OccupiedBeds")
    };

    public List<Hostel> GetHostels(bool activeOnly = false) =>
        _db.Query(HostelSql + (activeOnly ? "WHERE h.IsActive = 1 " : "") + "ORDER BY h.Name", MapHostel);

    public Hostel GetHostel(int id) => _db.QueryOne(HostelSql + "WHERE h.HostelID = @id", MapHostel, P("@id", id));

    public bool HostelNameExists(string name, int excludeId) =>
        _db.Count("SELECT COUNT(*) FROM Hostels WHERE Name = @n AND HostelID <> @id", P("@n", name), P("@id", excludeId)) > 0;

    public void CreateHostel(Hostel h) =>
        _db.Execute("INSERT INTO Hostels (Name, Location, Type) VALUES (@n, @l, @t)",
            P("@n", h.Name.Trim()), P("@l", h.Location.Trim()), P("@t", h.Type));

    public void UpdateHostel(Hostel h) =>
        _db.Execute("UPDATE Hostels SET Name=@n, Location=@l, Type=@t WHERE HostelID=@id",
            P("@n", h.Name.Trim()), P("@l", h.Location.Trim()), P("@t", h.Type), P("@id", h.HostelID));

    public void SetHostelActive(int id, bool active) =>
        _db.Execute("UPDATE Hostels SET IsActive=@a WHERE HostelID=@id", P("@a", active), P("@id", id));

    public int ActiveAllocationsInHostel(int hostelId) =>
        _db.Count(@"SELECT COUNT(*) FROM Allocations a JOIN Rooms r ON r.RoomID=a.RoomID JOIN Blocks b ON b.BlockID=r.BlockID
                    WHERE b.HostelID=@id AND a.Status='Active'", P("@id", hostelId));

    // ---------------- Blocks ----------------
    private const string BlockSql = @"
        SELECT b.BlockID, b.HostelID, b.BlockName, b.IsActive, h.Name AS HostelName,
               (SELECT COUNT(*) FROM Rooms r WHERE r.BlockID = b.BlockID AND r.Status <> 'Inactive') AS RoomCount
          FROM Blocks b JOIN Hostels h ON h.HostelID = b.HostelID ";

    private static Block MapBlock(SqlDataReader r) => new()
    {
        BlockID = r.Int("BlockID"), HostelID = r.Int("HostelID"), BlockName = r.Str("BlockName"),
        IsActive = r.Bool("IsActive"), HostelName = r.Str("HostelName"), RoomCount = r.Int("RoomCount")
    };

    public List<Block> GetBlocks(int? hostelId = null, bool activeOnly = false) =>
        _db.Query(BlockSql + @"WHERE (@h IS NULL OR b.HostelID = @h) AND (@all = 0 OR (b.IsActive = 1 AND h.IsActive = 1))
                               ORDER BY h.Name, b.BlockName",
            MapBlock, P("@h", hostelId), P("@all", activeOnly ? 1 : 0));

    public Block GetBlock(int id) => _db.QueryOne(BlockSql + "WHERE b.BlockID = @id", MapBlock, P("@id", id));

    public bool BlockNameExists(int hostelId, string name, int excludeId) =>
        _db.Count("SELECT COUNT(*) FROM Blocks WHERE HostelID=@h AND BlockName=@n AND BlockID<>@id",
            P("@h", hostelId), P("@n", name), P("@id", excludeId)) > 0;

    public void CreateBlock(Block b) =>
        _db.Execute("INSERT INTO Blocks (HostelID, BlockName) VALUES (@h, @n)", P("@h", b.HostelID), P("@n", b.BlockName.Trim()));

    public void UpdateBlock(Block b) =>
        _db.Execute("UPDATE Blocks SET HostelID=@h, BlockName=@n WHERE BlockID=@id",
            P("@h", b.HostelID), P("@n", b.BlockName.Trim()), P("@id", b.BlockID));

    public void SetBlockActive(int id, bool active) =>
        _db.Execute("UPDATE Blocks SET IsActive=@a WHERE BlockID=@id", P("@a", active), P("@id", id));

    public int ActiveAllocationsInBlock(int blockId) =>
        _db.Count(@"SELECT COUNT(*) FROM Allocations a JOIN Rooms r ON r.RoomID=a.RoomID
                    WHERE r.BlockID=@id AND a.Status='Active'", P("@id", blockId));
}
