using HostelEase.Helpers;
using HostelEase.Models;

namespace HostelEase.Data;

/// <summary>
/// Runs once at startup: creates the first administrator (and one demo student) if they do not exist.
/// Passwords are hashed here in C#, which is why the SQL script cannot insert them.
/// </summary>
public class DbSeeder
{
    public const string AdminEmail = "admin@hostelease.com";
    public const string AdminPassword = "Admin@123";
    public const string StudentEmail = "student@hostelease.com";
    public const string StudentPassword = "Student@123";

    private readonly UserRepository _users;
    private readonly ILogger<DbSeeder> _log;
    public DbSeeder(UserRepository users, ILogger<DbSeeder> log) { _users = users; _log = log; }

    public void Seed()
    {
        try
        {
            if (!_users.AdminExists())
            {
                _users.CreateAdmin(AdminEmail, PasswordHasher.Hash(AdminPassword));
                _log.LogInformation("Default administrator created: {Email}", AdminEmail);
            }

            if (!_users.EmailExists(StudentEmail) && !_users.StudentNumberExists("DEMO001"))
            {
                _users.CreateStudent(new RegisterViewModel
                {
                    FullName = "Demo Student", StudentNumber = "DEMO001", Email = StudentEmail,
                    Course = "B.Tech Computer Engineering", Semester = 4, Contact = "9999999999"
                }, PasswordHasher.Hash(StudentPassword));
                _log.LogInformation("Demo student created: {Email}", StudentEmail);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Could not reach the HostelEase database. Did you run Database/01_Create_HostelEase.sql in SSMS and is the " +
                "connection string in appsettings.json correct?");
        }
    }
}
