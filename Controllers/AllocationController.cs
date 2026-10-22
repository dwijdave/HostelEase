using HostelEase.Data;
using HostelEase.Helpers;
using HostelEase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HostelEase.Controllers;

[Authorize]
public class AllocationController : Controller
{
    private readonly AllocationRepository _alloc;
    private readonly RoomRepository _rooms;
    private readonly HostelRepository _hostels;
    private readonly StudentRepository _students;

    public AllocationController(AllocationRepository alloc, RoomRepository rooms, HostelRepository hostels, StudentRepository students)
    { _alloc = alloc; _rooms = rooms; _hostels = hostels; _students = students; }

    // =====================  STUDENT SIDE  =====================

    [HttpGet, Authorize(Roles = "Student")]
    public IActionResult Apply(int? roomId)
    {
        int sid = User.StudentId();
        if (_alloc.GetActiveForStudent(sid) != null) { TempData["Info"] = "You already have a room allocated."; return RedirectToAction(nameof(My)); }
        if (_alloc.HasPendingRequest(sid)) { TempData["Info"] = "You already have a pending request. Cancel it first if you want to change it."; return RedirectToAction(nameof(My)); }

        FillRequestForm();
        return View(new RequestFormViewModel { PreferredRoomId = roomId, PreferredHostelId = roomId.HasValue ? _rooms.GetById(roomId.Value)?.HostelID : null });
    }

    [HttpPost, Authorize(Roles = "Student")]
    public IActionResult Apply(RequestFormViewModel m)
    {
        int sid = User.StudentId();
        if (_alloc.GetActiveForStudent(sid) != null || _alloc.HasPendingRequest(sid))
        {
            TempData["Error"] = "You already have an active room or a pending request.";
            return RedirectToAction(nameof(My));
        }

        int? hostelId = m.PreferredHostelId;
        if (m.PreferredRoomId.HasValue)
        {
            var room = _rooms.GetById(m.PreferredRoomId.Value);
            if (room == null || !room.CanBeAllocated)
            {
                ModelState.AddModelError(nameof(m.PreferredRoomId), "That room is no longer available");
                FillRequestForm(); return View(m);
            }
            hostelId = room.HostelID;      // keep hostel consistent with the chosen room
        }

        try
        {
            _alloc.CreateRequest(sid, hostelId, m.PreferredRoomId);
            TempData["Success"] = "Your request was submitted. You can track its status here.";
        }
        catch (SqlException)
        {
            TempData["Error"] = "You already have a pending request.";
        }
        return RedirectToAction(nameof(My));
    }

    [Authorize(Roles = "Student")]
    public IActionResult My()
    {
        int sid = User.StudentId();
        ViewBag.Current = _alloc.GetActiveForStudent(sid);
        return View(_alloc.GetRequestsForStudent(sid));
    }

    [HttpPost, Authorize(Roles = "Student")]
    public IActionResult Cancel(int id)
    {
        bool ok = _alloc.Cancel(id, User.StudentId());
        TempData[ok ? "Success" : "Error"] = ok ? "Request cancelled." : "Only your own pending requests can be cancelled.";
        return RedirectToAction(nameof(My));
    }

    // =====================  ADMIN SIDE  =====================

    [Authorize(Roles = "Admin")]
    public IActionResult Index(string status, string q)
    {
        ViewBag.Status = status; ViewBag.Q = q;
        return View(_alloc.GetRequests(status, q));
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public IActionResult Review(int id)
    {
        var req = _alloc.GetRequest(id);
        if (req == null) return NotFound();

        // preferred room first, then rooms in the preferred hostel, then the rest
        var rooms = _rooms.GetAllocatable()
            .OrderBy(r => r.RoomID == req.PreferredRoomID ? 0 : r.HostelID == req.PreferredHostelID ? 1 : 2)
            .ThenBy(r => r.HostelName).ThenBy(r => r.BlockName).ThenBy(r => r.RoomNumber).ToList();

        return View(new ReviewViewModel
        {
            Request = req,
            Student = _students.GetById(req.StudentID),
            Rooms = rooms,
            SelectedRoomId = req.PreferredRoomID is int p && rooms.Any(r => r.RoomID == p) ? p : (rooms.FirstOrDefault()?.RoomID ?? 0)
        });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public IActionResult Approve(int id, int roomId, string remarks)
    {
        if (roomId == 0) { TempData["Error"] = "Select a room to allocate."; return RedirectToAction(nameof(Review), new { id }); }
        var (ok, message) = _alloc.Approve(id, roomId, remarks);
        TempData[ok ? "Success" : "Error"] = message;
        return ok ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public IActionResult Reject(int id, string remarks)
    {
        bool ok = _alloc.Reject(id, remarks);
        TempData[ok ? "Success" : "Error"] = ok ? "Request rejected." : "This request is no longer pending.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Allocations(string status = "Active", string q = null)
    {
        ViewBag.Status = status; ViewBag.Q = q;
        return View(_alloc.GetAllocations(status, q));
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public IActionResult Vacate(int id)
    {
        bool ok = _alloc.Vacate(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Room vacated." : "Allocation is already ended.";
        return RedirectToAction(nameof(Allocations));
    }

    // ---------------- helpers ----------------
    private void FillRequestForm()
    {
        ViewBag.Hostels = _hostels.GetHostels(activeOnly: true);
        ViewBag.Rooms = _rooms.GetAllocatable();
    }
}
