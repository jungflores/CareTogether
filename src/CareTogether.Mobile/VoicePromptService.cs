namespace CareTogether.Mobile;

public interface IVoicePromptService
{
    Task<string?> ListenAsync();
}
