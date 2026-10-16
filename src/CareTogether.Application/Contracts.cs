using CareTogether.Domain;

namespace CareTogether.Application;

public record RegisterRequest(string Email, string Password, string DisplayName, MemberRole Role);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, Guid UserId, string DisplayName, MemberRole Role);
public record CreateCareCircleRequest(string RecipientName, DateOnly? DateOfBirth, string? Relationship);
public record CreateCareInvitationRequest(string Email, MemberRole Role, string? Message, string DeliveryChannel = "Email", string? Phone = null);
public record CreateTaskRequest(Guid CareCircleId, string Title, string? Details, DateTimeOffset DueAt, Guid? AssignedToUserId, CareTaskStatus Status = CareTaskStatus.Open, bool AssignToMe = false, Guid? AppointmentId = null);
public record UpdateTaskRequest(Guid CareCircleId, string Title, string? Details, DateTimeOffset DueAt, Guid? AssignedToUserId, CareTaskStatus Status, bool AssignToMe = false, string? PreviousDetails = null);
public record UpdateTaskStatusRequest(CareTaskStatus Status);
public record CreateMedicationRequest(Guid CareCircleId, string Name, string Dosage, string Schedule, string? Instructions, MedicationStatus Status = MedicationStatus.Active, MedicationScheduleType ScheduleType = MedicationScheduleType.Daily, DateTimeOffset? OnceAt = null, int? Occurrences = null, string? TimesOfDay = null, DateOnly? StartDate = null, DateOnly? EndDate = null, DayOfWeek? WeeklyDay = null, int? MonthlyDay = null, int ScheduleUtcOffsetMinutes = 0);
public record MedicationLogRequest(DateTimeOffset ScheduledAt, bool Taken, string? Note);
public record MedicationOccurrenceRequest(DateTimeOffset ScheduledAt);
public record CreateAppointmentRequest(Guid CareCircleId, string Title, string Provider, DateTimeOffset StartsAt, string? Location, string? Notes, string? PreviousNotes = null);
public record CreateJournalEntryRequest(Guid CareCircleId, string Body, string? Category);
public record CreateHealthReadingRequest(Guid CareCircleId, string Type, decimal Value, decimal? SecondaryValue, int? Pulse, DateTimeOffset RecordedAt);
public record CreateDocumentRequest(Guid CareCircleId, string Name, string Category, string? Notes, Guid? TaskId = null, Guid? AppointmentId = null, Guid? MedicationId = null, string? ContentType = null, string? ContentBase64 = null);
public record CreateEmergencyContactRequest(Guid CareCircleId, string Name, string Relationship, string Phone, bool IsPrimary);
public record CreateConversationRequest(Guid CareCircleId, string Title);
public record SendMessageRequest(Guid ConversationId, string Body);
public record CreateHandoffRequest(Guid CareCircleId, Guid ToUserId, string Summary, string? Risks, string? NextSteps, bool Send = true);
public record UpdateHandoffRequest(Guid ToUserId, string Summary, string? Risks, string? NextSteps, bool Send = true);
public record AiSummaryRequest(Guid CareCircleId, string Context);
public record AiSummaryResponse(string Summary, IReadOnlyList<string> SuggestedActions, string Disclaimer);
public record ChatQuestionRequest(Guid? CareCircleId, string Question, DateTimeOffset? ClientNow = null);
public record ChatPendingAction(string Type, Guid CareCircleId, string RecipientName, string Title, DateTimeOffset ScheduledAt, string? Provider, string? Location, string? Details, bool AssignToMe, string Confirmation,
 string? Dosage = null, MedicationScheduleType? ScheduleType = null, int? Occurrences = null, string? TimesOfDay = null, DateOnly? StartDate = null, DateOnly? EndDate = null, DayOfWeek? WeeklyDay = null, int? MonthlyDay = null,
 string? Category = null, string? ReadingType = null, decimal? Value = null, decimal? SecondaryValue = null, int? Pulse = null);
public record ChatAnswerResponse(string Answer, string Intent, DateTimeOffset GeneratedAt, IReadOnlyList<string> Sources, IReadOnlyList<string> Suggestions, ChatPendingAction? PendingAction = null);

public interface ICurrentUser { Guid UserId { get; } }
public interface ITokenService { AuthResponse Issue(User user); string Hash(string value); }
