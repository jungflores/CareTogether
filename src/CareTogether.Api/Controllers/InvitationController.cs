using System.Net;
using System.Text;
using CareTogether.Domain;
using CareTogether.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareTogether.Api.Controllers;

[AllowAnonymous]
public sealed class InvitationController(CareDbContext db):Controller
{
    [HttpGet("invite/{token}")]
    public async Task<IActionResult> ViewInvitation(string token)
    {
        var invitation=await db.CareInvitations.SingleOrDefaultAsync(x=>x.Token==token);if(invitation is null)return Content(Page("Invitation unavailable","This invitation link is invalid.",null),"text/html",Encoding.UTF8);if(invitation.ExpiresAt<=DateTimeOffset.UtcNow)return Content(Page("Invitation expired","Ask the Care Circle administrator to send a new invitation.",null),"text/html",Encoding.UTF8);var recipient=await db.CareCircles.Where(x=>x.Id==invitation.CareCircleId).Select(x=>x.RecipientName).SingleAsync();var actions=invitation.Status==InvitationStatus.Pending?$"<form method='post' action='/invite/{WebUtility.HtmlEncode(token)}/accept'><button class='accept'>Accept invitation</button></form><form method='post' action='/invite/{WebUtility.HtmlEncode(token)}/decline'><button class='decline'>Decline</button></form>":null;return Content(Page("CareTogether invitation",$"You have been invited to join <strong>{WebUtility.HtmlEncode(recipient)}</strong>'s Care Circle as {invitation.Role}.",actions),"text/html",Encoding.UTF8);
    }
    [HttpPost("invite/{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        var invitation=await Pending(token);if(invitation is null)return Content(Page("Invitation unavailable","This invitation is invalid, expired, or already answered.",null),"text/html",Encoding.UTF8);invitation.Status=InvitationStatus.Accepted;var user=await db.Users.SingleOrDefaultAsync(x=>x.Email==invitation.Email);if(user is not null){var member=await db.CareTeamMembers.SingleOrDefaultAsync(x=>x.CareCircleId==invitation.CareCircleId&&x.UserId==user.Id);if(member is null)db.CareTeamMembers.Add(new(){CareCircleId=invitation.CareCircleId,UserId=user.Id,Role=invitation.Role});else{member.IsActive=true;member.DeactivatedAt=null;member.Role=invitation.Role;}}await db.SaveChangesAsync();var message=user is null?"Invitation accepted. Create a CareTogether account using the invited email address to join the Care Circle.":"Invitation accepted. Open CareTogether and sign in to view the Care Circle.";return Content(Page("Invitation accepted",message,null),"text/html",Encoding.UTF8);
    }
    [HttpPost("invite/{token}/decline")]
    public async Task<IActionResult> Decline(string token){var invitation=await Pending(token);if(invitation is null)return Content(Page("Invitation unavailable","This invitation is invalid, expired, or already answered.",null),"text/html",Encoding.UTF8);invitation.Status=InvitationStatus.Revoked;await db.SaveChangesAsync();return Content(Page("Invitation declined","You will not be added to this Care Circle.",null),"text/html",Encoding.UTF8);}
    async Task<CareInvitation?> Pending(string token)=>await db.CareInvitations.SingleOrDefaultAsync(x=>x.Token==token&&x.Status==InvitationStatus.Pending&&x.ExpiresAt>DateTimeOffset.UtcNow);
    static string Page(string title,string message,string? actions)
    {
        var safeTitle=WebUtility.HtmlEncode(title);return "<!doctype html><html><head><meta name='viewport' content='width=device-width,initial-scale=1'><title>"+safeTitle+"</title><style>body{font-family:-apple-system,BlinkMacSystemFont,sans-serif;background:#eef6ff;color:#101828;margin:0;display:grid;place-items:center;min-height:100vh}main{background:white;border:1px solid #e4e9f0;border-radius:18px;padding:28px;max-width:420px;margin:20px;box-shadow:0 12px 35px #16345b22}h1{color:#0866e8}button{width:100%;padding:13px;border:0;border-radius:10px;font-weight:700;font-size:16px;margin-top:10px}.accept{background:#0866e8;color:white}.decline{background:#ffe8e9;color:#e5484d}</style></head><body><main><h1>"+safeTitle+"</h1><p>"+message+"</p>"+(actions??"")+"</main></body></html>";
    }
}
