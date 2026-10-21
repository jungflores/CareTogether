using CareTogether.Application;
using CareTogether.Domain;
using CareTogether.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareTogether.Api.Controllers;

[ApiController, Authorize, Route("api/groceries")]
public sealed class GroceryController(CareDbContext db, ICurrentUser current) : ControllerBase
{
    private IQueryable<Guid> CircleIds => db.CareTeamMembers.Where(x => x.UserId == current.UserId && x.IsActive).Select(x => x.CareCircleId);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? careCircleId = null, [FromQuery] bool includePurchased = false)
    {
        var query = db.GroceryItems.Where(x => CircleIds.Contains(x.CareCircleId));
        if (careCircleId is not null) query = query.Where(x => x.CareCircleId == careCircleId);
        if (!includePurchased) query = query.Where(x => !x.IsPurchased);
        query = query.OrderBy(x => x.IsPurchased).ThenByDescending(x => x.CreatedAt);
        return Ok(await View(query).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await View(db.GroceryItems.Where(x => x.Id == id && CircleIds.Contains(x.CareCircleId))).SingleOrDefaultAsync();
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGroceryItemRequest request)
    {
        if (!await IsMember(request.CareCircleId)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Item name is required.");
        if (!await IsValidAssignee(request.CareCircleId, request.AssignedToUserId)) return BadRequest("Choose an active member of this Care Circle.");
        var item = new GroceryItem { CareCircleId = request.CareCircleId, CreatedByUserId = current.UserId, Name = request.Name.Trim(), Quantity = Clean(request.Quantity), Notes = Clean(request.Notes), NeededBy = request.NeededBy, AssignedToUserId = request.AssignedToUserId, AssignmentStatus = request.AssignedToUserId is null ? GroceryAssignmentStatus.Unassigned : GroceryAssignmentStatus.Pending };
        db.GroceryItems.Add(item);
        NotifyAssignee(item, "New grocery assignment", $"You were assigned to get {item.Name}.");
        await db.SaveChangesAsync();
        return Created($"api/groceries/{item.Id}", await View(db.GroceryItems.Where(x => x.Id == item.Id)).SingleAsync());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateGroceryItemRequest request)
    {
        var item = await Find(id); if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Item name is required.");
        if (!await IsMember(request.CareCircleId) || !await IsValidAssignee(request.CareCircleId, request.AssignedToUserId)) return BadRequest("Choose a valid recipient and active assignee.");
        var assignmentChanged = item.AssignedToUserId != request.AssignedToUserId;
        item.CareCircleId = request.CareCircleId; item.Name = request.Name.Trim(); item.Quantity = Clean(request.Quantity); item.Notes = Clean(request.Notes); item.NeededBy = request.NeededBy; item.AssignedToUserId = request.AssignedToUserId;
        if (assignmentChanged) { item.AssignmentStatus = request.AssignedToUserId is null ? GroceryAssignmentStatus.Unassigned : GroceryAssignmentStatus.Pending; NotifyAssignee(item, "Grocery item assigned", $"You were assigned to get {item.Name}."); }
        await db.SaveChangesAsync(); return Ok(await View(db.GroceryItems.Where(x => x.Id == id)).SingleAsync());
    }

    [HttpPut("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id) => await Respond(id, true);

    [HttpPut("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id) => await Respond(id, false);

    [HttpPut("{id:guid}/reassign")]
    public async Task<IActionResult> Reassign(Guid id, ReassignGroceryItemRequest request)
    {
        var item = await Find(id); if (item is null) return NotFound();
        if (!await IsValidAssignee(item.CareCircleId, request.AssignedToUserId)) return BadRequest("Choose an active member of this Care Circle.");
        item.AssignedToUserId = request.AssignedToUserId; item.AssignmentStatus = request.AssignedToUserId is null ? GroceryAssignmentStatus.Unassigned : GroceryAssignmentStatus.Pending;
        NotifyAssignee(item, "Grocery item reassigned", $"You were assigned to get {item.Name}.");
        await db.SaveChangesAsync(); return Ok(await View(db.GroceryItems.Where(x => x.Id == id)).SingleAsync());
    }

    [HttpPut("{id:guid}/toggle-purchased")]
    public async Task<IActionResult> TogglePurchased(Guid id)
    {
        var item = await Find(id); if (item is null) return NotFound();
        item.IsPurchased = !item.IsPurchased; item.PurchasedAt = item.IsPurchased ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(); return Ok(await View(db.GroceryItems.Where(x => x.Id == id)).SingleAsync());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await Find(id); if (item is null) return NotFound();
        db.GroceryItems.Remove(item); await db.SaveChangesAsync(); return Ok(new { Deleted = true, id });
    }

    private async Task<IActionResult> Respond(Guid id, bool accept)
    {
        var item = await db.GroceryItems.SingleOrDefaultAsync(x => x.Id == id && x.AssignedToUserId == current.UserId && CircleIds.Contains(x.CareCircleId));
        if (item is null) return NotFound();
        if (item.AssignmentStatus != GroceryAssignmentStatus.Pending) return Conflict("Only a pending grocery assignment can be accepted or declined.");
        item.AssignmentStatus = accept ? GroceryAssignmentStatus.Accepted : GroceryAssignmentStatus.Declined;
        if (item.CreatedByUserId != current.UserId) db.UserNotifications.Add(new UserNotification { UserId = item.CreatedByUserId, Type = "grocery", Title = accept ? "Grocery assignment accepted" : "Grocery assignment declined", Body = $"{item.Name} was {(accept ? "accepted" : "declined")}.", RelatedId = item.Id });
        await db.SaveChangesAsync(); return Ok(await View(db.GroceryItems.Where(x => x.Id == id)).SingleAsync());
    }

    private IQueryable<object> View(IQueryable<GroceryItem> query) => query.Select(x => new { x.Id, x.CareCircleId, RecipientName = db.CareCircles.Where(c => c.Id == x.CareCircleId).Select(c => c.RecipientName).First(), x.CreatedByUserId, CreatedByName = db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.DisplayName).First(), x.Name, x.Quantity, x.Notes, x.NeededBy, x.AssignedToUserId, AssigneeName = x.AssignedToUserId == null ? null : db.Users.Where(u => u.Id == x.AssignedToUserId).Select(u => u.DisplayName).FirstOrDefault(), AssignmentStatus = (int)x.AssignmentStatus, AssignmentStatusName = x.AssignmentStatus.ToString(), x.IsPurchased, x.PurchasedAt, x.CreatedAt, CanRespond = x.AssignedToUserId == current.UserId && x.AssignmentStatus == GroceryAssignmentStatus.Pending });
    private Task<GroceryItem?> Find(Guid id) => db.GroceryItems.SingleOrDefaultAsync(x => x.Id == id && CircleIds.Contains(x.CareCircleId));
    private Task<bool> IsMember(Guid circleId) => db.CareTeamMembers.AnyAsync(x => x.CareCircleId == circleId && x.UserId == current.UserId && x.IsActive);
    private Task<bool> IsValidAssignee(Guid circleId, Guid? userId) => userId is null ? Task.FromResult(true) : db.CareTeamMembers.AnyAsync(x => x.CareCircleId == circleId && x.UserId == userId && x.IsActive);
    private void NotifyAssignee(GroceryItem item, string title, string body) { if (item.AssignedToUserId is Guid userId && userId != current.UserId) db.UserNotifications.Add(new UserNotification { UserId = userId, Type = "grocery", Title = title, Body = body, RelatedId = item.Id }); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
