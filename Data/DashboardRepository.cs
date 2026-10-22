using HostelEase.Models;
using static HostelEase.Data.DbConnection;

namespace HostelEase.Data;

public class DashboardRepository
{
    private readonly DbConnection _db;
    private readonly StudentRepository _students;
    private readonly AllocationRepository _allocations;
    private readonly ComplaintRepository _complaints;

    public DashboardRepository(DbConnection db, StudentRepository students, AllocationRepository allocations, ComplaintRepository complaints)
    {
        _db = db; _students = students; _allocations = allocations; _complaints = complaints;
    }

    public AdminDashboardViewModel GetAdminSummary()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalRooms = _db.Count("SELECT COUNT(*) FROM Rooms WHERE Status <> 'Inactive'"),
            OccupiedRooms = _db.Count("SELECT COUNT(DISTINCT RoomID) FROM Allocations WHERE Status = 'Active'"),
            PendingRequests = _db.Count("SELECT COUNT(*) FROM AllocationRequests WHERE Status = 'Pending'"),
            TotalBeds = _db.Count("SELECT ISNULL(SUM(Capacity), 0) FROM Rooms WHERE Status <> 'Inactive'"),
            OccupiedBeds = _db.Count("SELECT COUNT(*) FROM Allocations WHERE Status = 'Active'"),
            TotalStudents = _students.CountActive(),
            OpenComplaints = _complaints.CountOpen(),
            RecentRequests = _allocations.GetRecentRequests(5),
            RecentStudents = _students.Recent(5)
        };
        vm.VacantRooms = Math.Max(vm.TotalRooms - vm.OccupiedRooms, 0);

        vm.Occupancy = _db.Query(@"
            SELECT h.Name, ISNULL(SUM(r.Capacity), 0) AS TotalBeds,
                   (SELECT COUNT(*) FROM Allocations a JOIN Rooms r2 ON r2.RoomID = a.RoomID JOIN Blocks b2 ON b2.BlockID = r2.BlockID
                     WHERE b2.HostelID = h.HostelID AND a.Status = 'Active') AS OccupiedBeds
              FROM Hostels h
              LEFT JOIN Blocks b ON b.HostelID = h.HostelID
              LEFT JOIN Rooms r ON r.BlockID = b.BlockID AND r.Status <> 'Inactive'
             WHERE h.IsActive = 1
             GROUP BY h.HostelID, h.Name ORDER BY h.Name",
            r => new HostelOccupancy { Hostel = r.Str("Name"), TotalBeds = r.Int("TotalBeds"), OccupiedBeds = r.Int("OccupiedBeds") });
        return vm;
    }

    public StudentDashboardViewModel GetStudentSummary(int studentId)
    {
        var requests = _allocations.GetRequestsForStudent(studentId);
        return new StudentDashboardViewModel
        {
            Student = _students.GetById(studentId),
            Current = _allocations.GetActiveForStudent(studentId),
            TotalRequests = requests.Count,
            PendingRequests = requests.Count(r => r.Status == "Pending"),
            LatestRequest = requests.FirstOrDefault(),
            OpenComplaints = _complaints.CountOpenForStudent(studentId)
        };
    }

    /// <summary>Runs a "SELECT label, COUNT(*) ... GROUP BY label" query into a dictionary.</summary>
    private Dictionary<string, int> Counts(string sql) =>
        _db.Query(sql, r => (Key: r.Str("Label"), Value: r.Int("Total"))).ToDictionary(x => x.Key, x => x.Value);

    public ReportsViewModel GetReports()
    {
        var statuses = new[] { "Pending", "Approved", "Rejected", "Cancelled" };
        var reqCounts = Counts("SELECT Status AS Label, COUNT(*) AS Total FROM AllocationRequests GROUP BY Status");
        var cmpCounts = Counts("SELECT ResolutionStatus AS Label, COUNT(*) AS Total FROM Complaints GROUP BY ResolutionStatus");
        foreach (var s in statuses) reqCounts.TryAdd(s, 0);
        foreach (var s in Complaint.Statuses) cmpCounts.TryAdd(s, 0);

        return new ReportsViewModel
        {
            Occupancy = GetAdminSummary().Occupancy,
            RequestCounts = statuses.ToDictionary(s => s, s => reqCounts[s]),
            ComplaintCounts = Complaint.Statuses.ToDictionary(s => s, s => cmpCounts[s]),
            ComplaintCategories = Counts("SELECT Category AS Label, COUNT(*) AS Total FROM Complaints GROUP BY Category ORDER BY COUNT(*) DESC"),
            TotalStudents = _students.CountActive(),
            ActiveAllocations = _db.Count("SELECT COUNT(*) FROM Allocations WHERE Status = 'Active'"),
            EndedAllocations = _db.Count("SELECT COUNT(*) FROM Allocations WHERE Status = 'Ended'")
        };
    }
}
