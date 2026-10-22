using System.ComponentModel.DataAnnotations;

namespace HostelEase.Models;

public class User
{
    public int UserID { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
}

public class Student
{
    public int StudentID { get; set; }
    public int UserID { get; set; }

    [Required(ErrorMessage = "Full name is required"), StringLength(100, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; }

    [Display(Name = "Student ID")]
    public string StudentNumber { get; set; }

    public string Email { get; set; }

    [Required(ErrorMessage = "Course is required"), StringLength(100)]
    public string Course { get; set; }

    [Range(1, 12, ErrorMessage = "Semester must be between 1 and 12")]
    public int Semester { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number"), StringLength(20)]
    public string Contact { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CurrentRoom { get; set; }   // filled by queries (read-only)
}

public class Hostel
{
    public int HostelID { get; set; }

    [Required(ErrorMessage = "Hostel name is required"), StringLength(100)]
    public string Name { get; set; }

    [Required(ErrorMessage = "Location is required"), StringLength(200)]
    public string Location { get; set; }

    [Required(ErrorMessage = "Select a hostel type")]
    public string Type { get; set; }

    public bool IsActive { get; set; } = true;

    // read-only statistics
    public int TotalRooms { get; set; }
    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int OccupancyPercent => TotalBeds == 0 ? 0 : OccupiedBeds * 100 / TotalBeds;
}

public class Block
{
    public int BlockID { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a hostel")]
    [Display(Name = "Hostel")]
    public int HostelID { get; set; }

    [Required(ErrorMessage = "Block name is required"), StringLength(50), Display(Name = "Block name")]
    public string BlockName { get; set; }

    public bool IsActive { get; set; } = true;
    public string HostelName { get; set; }
    public int RoomCount { get; set; }
}

public class Room
{
    public int RoomID { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a block")]
    [Display(Name = "Block")]
    public int BlockID { get; set; }

    [Required(ErrorMessage = "Room number is required"), StringLength(20), Display(Name = "Room number")]
    public string RoomNumber { get; set; }

    [Range(1, 20, ErrorMessage = "Capacity must be between 1 and 20")]
    public int Capacity { get; set; } = 2;

    [Required]
    public string Status { get; set; } = "Available";

    // read-only, filled by queries
    public int HostelID { get; set; }
    public string HostelName { get; set; }
    public string BlockName { get; set; }
    public bool BlockActive { get; set; }
    public bool HostelActive { get; set; }
    public int Occupied { get; set; }

    public int FreeBeds => Math.Max(Capacity - Occupied, 0);
    public bool CanBeAllocated => Status == "Available" && BlockActive && HostelActive && Occupied < Capacity;
    public string Label => $"{HostelName} / {BlockName} / Room {RoomNumber}";
    public string AvailabilityText => Status != "Available" ? Status : (Occupied >= Capacity ? "Full" : "Available");
}

public class AllocationRequest
{
    public int RequestID { get; set; }
    public int StudentID { get; set; }
    public string StudentName { get; set; }
    public string StudentNumber { get; set; }
    public int? PreferredHostelID { get; set; }
    public string PreferredHostelName { get; set; }
    public int? PreferredRoomID { get; set; }
    public string PreferredRoomLabel { get; set; }
    public DateTime RequestDate { get; set; }
    public string Status { get; set; }
    public string AdminRemarks { get; set; }
    public DateTime? ReviewedDate { get; set; }
}

public class Allocation
{
    public int AllocationID { get; set; }
    public int StudentID { get; set; }
    public string StudentName { get; set; }
    public string StudentNumber { get; set; }
    public int RoomID { get; set; }
    public string HostelName { get; set; }
    public string BlockName { get; set; }
    public string RoomNumber { get; set; }
    public DateTime AllocationDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; }
    public string RoomLabel => $"{HostelName} / {BlockName} / Room {RoomNumber}";
}

public class Complaint
{
    public int ComplaintID { get; set; }
    public int StudentID { get; set; }
    public string StudentName { get; set; }

    [Required(ErrorMessage = "Choose a category")]
    public string Category { get; set; }

    [Required(ErrorMessage = "Please describe the problem"), StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be 10 to 1000 characters")]
    public string Description { get; set; }

    public DateTime DateSubmitted { get; set; }
    public string ResolutionStatus { get; set; }
    public string AdminResponse { get; set; }

    public static readonly string[] Categories =
        { "Maintenance", "Electricity", "Water", "Cleanliness", "Food", "Roommate", "Security", "Other" };
    public static readonly string[] Statuses = { "Open", "In Progress", "Resolved" };
}
