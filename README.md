# HostelEase - Smart Hostel Allocation & Management System

Course 01CE15230 - .NET Technologies (Mini Project)
Built with **ASP.NET Core MVC (.NET 8) + C# + ADO.NET (Microsoft.Data.SqlClient) + SQL Server Express + Bootstrap 5**.

## What is included

### Student (client) side
- Register / sign in / sign out, show-hide password, change password
- Dashboard: allocation status, request counts, allocated room details, quick actions
- Browse rooms that have free beds (search + hostel filter) and request one in a click
- Submit a room request (preferred hostel and/or room), track it with a status timeline, cancel while pending
- See the administrator's remarks on approved/rejected requests
- Submit complaints and read the administrator's responses
- Edit own profile (name, course, semester, contact)

### Administrator side
- Dashboard: total / occupied / vacant rooms, pending requests, bed-occupancy bars per hostel, recent requests and registrations, quick links
- Students: search, add, edit, deactivate / reactivate (history is kept), CSV export
- Hostels and Blocks: add, edit, deactivate (blocked while students are still allocated)
- Rooms: add, edit, capacity, status (Available / Maintenance / Inactive), filters by hostel / availability / status
- Requests: filter by status, review screen showing student details + free rooms, **approve & allocate** or **reject** with remarks
- Allocations: list, search, vacate (check-out), CSV export
- Complaints: filter, set status (Open / In Progress / Resolved), reply to the student
- Reports: occupancy per hostel, requests and complaints summaries, CSV downloads
- Create additional administrator accounts (public admin registration is deliberately not offered)

### Rules enforced by the application and the database
- Room capacity is checked inside a **database transaction with a row lock** (`UPDLOCK`), so two administrators can never both take the last bed.
- A student can hold only one active room and have only one pending request (unique filtered indexes + application checks).
- Occupancy is always *counted* from active allocations, never stored, so it cannot drift out of sync.
- Passwords are hashed (PBKDF2-SHA256 with a random salt); all SQL is parameterized; every POST is anti-forgery protected; role-based authorization on every controller action.

## Requirements
- Windows, **Visual Studio 2022 (17.8+) or 2026** with the *ASP.NET and web development* workload
- **.NET 8 SDK** (if you only have the .NET 10 SDK, change `net8.0` to `net10.0` in `HostelEase.csproj`)
- SQL Server Express instance `.\SQLEXPRESS` (SSMS optional, for looking at the data)
- Internet access (Bootstrap, icons and jQuery validation load from CDNs)

## Run it
1. Extract the ZIP and open **`HostelEase.sln`** in Visual Studio.
2. Let NuGet restore, then press **F5** (profile `http` or `https`).
3. On first start the app **creates the `HostelEase` database, all tables and sample hostels/rooms automatically**
   (it runs `Database/01_Create_HostelEase.sql`) and creates the default accounts below.

Prefer the terminal? `dotnet run --launch-profile http` or double-click `run.bat`, then open http://localhost:5080.

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@hostelease.com` | `Admin@123` |
| Student (demo) | `student@hostelease.com` | `Student@123` |

**Change the administrator password** (sidebar -> Change password) before showing the project to anyone.

### Different SQL Server instance?
Edit `ConnectionStrings:HostelEase` in `appsettings.json` (the `Server=` part).

### Running the SQL script yourself (optional)
Open `Database/01_Create_HostelEase.sql` in SSMS and press F5. It is safe to run repeatedly.

### Upgrading from the first starter version
The first starter used a different table layout. When the new app starts and finds those tables, it **renames them to `*_Legacy`** (for example `Students_Legacy`) instead of deleting them, then creates the new structure. Your old rows stay in the database for reference; they are not migrated into the new tables automatically.

## Project structure
```
HostelEase.sln / HostelEase.csproj
Program.cs                 startup: services, cookie auth, routing, DB initialisation
appsettings.json           connection string
Controllers/               Account, Dashboard, Student, Hostel (+blocks), Room, Allocation, Complaint, Reports
Models/                    Entities.cs (database records), ViewModels.cs (forms + dashboards)
Data/                      DbConnection (ADO.NET helper), one repository per area, DbInitializer, DbSeeder
Helpers/                   PasswordHasher, ClaimsExtensions
Views/                     Razor pages per controller + Shared (_Layout, _AuthLayout, _StatusBadge)
wwwroot/                   css/site.css, js/site.js
Database/                  01_Create_HostelEase.sql (schema + sample data)
```
Request flow: Browser -> Controller -> Repository (ADO.NET, parameterized SQL) -> SQL Server -> back to a Razor View.

## Testing checklist (demo scenarios)
1. Register a new student -> sign in -> dashboard shows "Not allocated".
2. As the student: *Browse rooms* -> *Request this room* -> *My requests* shows **Pending**.
3. Try requesting again -> blocked (one pending request at a time).
4. Sign in as admin -> dashboard shows 1 pending request -> *Review* -> choose room -> **Approve & allocate**.
5. Student dashboard now shows the allocated hostel / block / room; admin dashboard occupancy bars move.
6. Capacity: register 3 students, request a 2-bed room for each, approve all three -> the third approval is refused ("room is already full").
7. Admin -> Allocations -> **Vacate** -> the bed becomes free again and the student can request a new room.
8. Student submits a complaint -> admin responds and marks it Resolved -> student sees the reply.
9. Try opening `/Student` while signed in as a student -> *Access denied*.
10. Try deactivating a student who holds a room -> refused until the room is vacated.
11. Admin -> Reports -> download the two CSV files.

## Troubleshooting
| Symptom | Fix |
|---|---|
| "Database setup failed" in the console | Start the *SQL Server (SQLEXPRESS)* service; check `Server=` in `appsettings.json`. |
| Login error about certificates | Keep `TrustServerCertificate=True` in the connection string (local development). |
| Page has no styling / icons | You are offline; Bootstrap loads from a CDN. |
| Port already in use | Change `applicationUrl` in `Properties/launchSettings.json`. |
| Build error about `net8.0` | Install the .NET 8 SDK, or switch the target to `net10.0`. |

## Notes
Demo credentials are shown on the login page only in the Development environment. For real use, change the seeded passwords and the connection string, and run over HTTPS.
