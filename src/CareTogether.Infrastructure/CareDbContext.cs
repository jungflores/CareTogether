using CareTogether.Domain;
using Microsoft.EntityFrameworkCore;

namespace CareTogether.Infrastructure;

public sealed class CareDbContext(DbContextOptions<CareDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>(); public DbSet<CareCircle> CareCircles => Set<CareCircle>(); public DbSet<CareTeamMember> CareTeamMembers => Set<CareTeamMember>();
    public DbSet<CareInvitation> CareInvitations => Set<CareInvitation>();
    public DbSet<CareTask> CareTasks => Set<CareTask>(); public DbSet<Medication> Medications => Set<Medication>(); public DbSet<MedicationOccurrence> MedicationOccurrences => Set<MedicationOccurrence>(); public DbSet<MedicationLog> MedicationLogs => Set<MedicationLog>();
    public DbSet<Appointment> Appointments => Set<Appointment>(); public DbSet<Conversation> Conversations => Set<Conversation>(); public DbSet<Message> Messages => Set<Message>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>(); public DbSet<HealthReading> HealthReadings => Set<HealthReading>(); public DbSet<CareDocument> CareDocuments => Set<CareDocument>(); public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<CareHandoff> CareHandoffs => Set<CareHandoff>(); public DbSet<UserNotification> UserNotifications => Set<UserNotification>(); public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GroceryItem> GroceryItems => Set<GroceryItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<CareTeamMember>().HasIndex(x => new { x.CareCircleId, x.UserId }).IsUnique();
        b.Entity<CareInvitation>().HasIndex(x => new { x.CareCircleId, x.Email, x.Status });
        b.Entity<MedicationOccurrence>().HasIndex(x => new { x.MedicationId, x.ScheduledAt }).IsUnique();
        b.Entity<UserNotification>().HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
        b.Entity<GroceryItem>().HasIndex(x => new { x.CareCircleId, x.IsPurchased, x.CreatedAt });
        b.Entity<CareHandoff>().HasIndex(x => new { x.CareCircleId, x.Status, x.CoverageStartsAt });
        b.Entity<HealthReading>().Property(x => x.Value).HasPrecision(18, 2);
        b.Entity<HealthReading>().Property(x => x.SecondaryValue).HasPrecision(18, 2);
        b.Entity<CareTeamMember>().HasOne(x => x.User).WithMany(x => x.Teams).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CareTeamMember>().HasOne(x => x.CareCircle).WithMany(x => x.Members).HasForeignKey(x => x.CareCircleId);
        b.Entity<Message>().HasOne<Conversation>().WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId);
        foreach (var entity in b.Model.GetEntityTypes()) entity.FindProperty(nameof(Entity.CreatedAt))?.SetDefaultValueSql("SYSUTCDATETIME()");
    }
}
