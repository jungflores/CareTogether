using Android.App;
using Android.Content;
using Android.Speech;

namespace CareTogether.Mobile;

public sealed class VoicePromptService : IVoicePromptService
{
    const int RequestCode = 4821;

    public async Task<string?> ListenAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.Microphone>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Microphone permission is required to enter a voice prompt.");

        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("The voice recognizer is not available right now.");
        if (!SpeechRecognizer.IsRecognitionAvailable(activity))
            throw new InvalidOperationException("Speech recognition is not available on this device.");

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode != RequestCode) return;
            MainActivity.ActivityResultReceived -= Completed;
            var matches = resultCode == Result.Ok ? data?.GetStringArrayListExtra(RecognizerIntent.ExtraResults) : null;
            completion.TrySetResult(matches?.FirstOrDefault());
        }

        MainActivity.ActivityResultReceived += Completed;
        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraPrompt, "Speak your CareTogether prompt");
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
        try { activity.StartActivityForResult(intent, RequestCode); }
        catch
        {
            MainActivity.ActivityResultReceived -= Completed;
            throw;
        }

        var finished = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromMinutes(2)));
        if (finished == completion.Task) return await completion.Task;
        MainActivity.ActivityResultReceived -= Completed;
        throw new TimeoutException("Voice recognition timed out. Please try again.");
    }
}
