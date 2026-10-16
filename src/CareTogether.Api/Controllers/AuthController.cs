using CareTogether.Application; using CareTogether.Domain; using CareTogether.Infrastructure; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace CareTogether.Api.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(CareDbContext db, PasswordService passwords, ITokenService tokens) : ControllerBase
{
 [HttpPost("register")] public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request) { var email=request.Email.Trim().ToLowerInvariant(); if(await db.Users.AnyAsync(x=>x.Email==email)) return Conflict("Email already registered."); var user=new User{Email=email,DisplayName=request.DisplayName.Trim(),Role=request.Role,PasswordHash="pending"}; user.PasswordHash=passwords.Hash(user,request.Password); db.Users.Add(user); await ActivateInvitations(user); var response=tokens.Issue(user); db.RefreshTokens.Add(new(){UserId=user.Id,TokenHash=tokens.Hash(response.RefreshToken),ExpiresAt=DateTimeOffset.UtcNow.AddDays(30)}); await db.SaveChangesAsync(); return Ok(response); }
 [HttpPost("login")] public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) { var email=request.Email.Trim().ToLowerInvariant(); var user=await db.Users.SingleOrDefaultAsync(x=>x.Email==email); if(user is null||!passwords.Verify(user,user.PasswordHash,request.Password)) return Unauthorized(); await ActivateInvitations(user); var response=tokens.Issue(user); db.RefreshTokens.Add(new(){UserId=user.Id,TokenHash=tokens.Hash(response.RefreshToken),ExpiresAt=DateTimeOffset.UtcNow.AddDays(30)}); await db.SaveChangesAsync(); return Ok(response); }
 [HttpPost("refresh")] public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request) { var hash=tokens.Hash(request.RefreshToken); var stored=await db.RefreshTokens.SingleOrDefaultAsync(x=>x.TokenHash==hash&&x.RevokedAt==null&&x.ExpiresAt>DateTimeOffset.UtcNow); if(stored is null) return Unauthorized(); stored.RevokedAt=DateTimeOffset.UtcNow; var user=await db.Users.FindAsync(stored.UserId); if(user is null)return Unauthorized(); var response=tokens.Issue(user); db.RefreshTokens.Add(new(){UserId=user.Id,TokenHash=tokens.Hash(response.RefreshToken),ExpiresAt=DateTimeOffset.UtcNow.AddDays(30)}); await db.SaveChangesAsync(); return Ok(response); }
 async Task ActivateInvitations(User user)
 {
  var invitations=await db.CareInvitations.Where(x=>x.Email==user.Email&&(x.Status==InvitationStatus.Pending||x.Status==InvitationStatus.Accepted)&&x.ExpiresAt>DateTimeOffset.UtcNow).ToListAsync();
  if(invitations.Count==0)return;
  var existingCircleIds=await db.CareTeamMembers.Where(x=>x.UserId==user.Id).Select(x=>x.CareCircleId).ToListAsync();
  foreach(var invitation in invitations)
  {
   if(!existingCircleIds.Contains(invitation.CareCircleId)){db.CareTeamMembers.Add(new CareTeamMember{CareCircleId=invitation.CareCircleId,User=user,Role=invitation.Role});existingCircleIds.Add(invitation.CareCircleId);}
   invitation.Status=InvitationStatus.Accepted;
  }
 }
}
