using System.ComponentModel.DataAnnotations;

namespace HostelEase.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)]
    public string Password { get; set; }

    public string ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required"), StringLength(100, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Student ID is required"), StringLength(30), Display(Name = "Student ID")]
    [RegularExpression(@"^[A-Za-z0-9\-/]+$", ErrorMessage = "Use letters, digits, '-' or '/' only")]
    public string StudentNumber { get; set; }

    [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Enter a valid email address"), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Course is required"), StringLength(100)]
    public string Course { get; set; }

    [Range(1, 12, ErrorMessage = "Semester must be between 1 and 12")]
    public int Semester { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number"), StringLength(20)]
    public string Contact { get; set; }

    [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password needs at least one letter and one digit")]
    public string Password { get; set; }

    [Required(ErrorMessage = "Please confirm the password"), DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }
}

public class CreateAdminViewModel
{
    [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Enter a valid email address"), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password needs at least one letter and one digit")]
    public string Password { get; set; }

    [Required(ErrorMessage = "Please confirm the password"), DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }
}

public class HostelOccupancy
{
    public string Hostel { get; set; }
    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int Percent => TotalBeds == 0 ? 0 : OccupiedBeds * 100 / TotalBeds;
}

public class AdminDashboardViewModel
{
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int VacantRooms { get; set; }
    public int PendingRequests { get; set; }
    public int TotalStudents { get; set; }
    public int OpenComplaints { get; set; }
    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int OccupancyPercent => TotalBeds == 0 ? 0 : OccupiedBeds * 100 / TotalBeds;
    public List<AllocationRequest> RecentRequests { get; set; } = new();
    public List<Student> RecentStudents { get; set; } = new();
    public List<HostelOccupancy> Occupancy { get; set; } = new();
}

public class StudentDashboardViewModel
{
    public Student Student { get; set; }
    public Allocation Current { get; set; }
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public AllocationRequest LatestRequest { get; set; }
    public int OpenComplaints { get; set; }
}

public class RequestFormViewModel
{
    public int? PreferredHostelId { get; set; }
    public int? PreferredRoomId { get; set; }
}

public class ReviewViewModel
{
    public AllocationRequest Request { get; set; }
    public Student Student { get; set; }
    public List<Room> Rooms { get; set; } = new();
    public int SelectedRoomId { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password"), DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; }

    [Required(ErrorMessage = "Enter a new password"), DataType(DataType.Password), Display(Name = "New password")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password needs at least one letter and one digit")]
    public string NewPassword { get; set; }

    [Required(ErrorMessage = "Please confirm the new password"), DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }
}

public class ReportsViewModel
{
    public List<HostelOccupancy> Occupancy { get; set; } = new();
    public Dictionary<string, int> RequestCounts { get; set; } = new();
    public Dictionary<string, int> ComplaintCounts { get; set; } = new();
    public Dictionary<string, int> ComplaintCategories { get; set; } = new();
    public int TotalStudents { get; set; }
    public int ActiveAllocations { get; set; }
    public int EndedAllocations { get; set; }
}
