using AVFoundation;
using Foundation;
using Speech;

namespace CareTogether.Mobile;

public sealed class VoicePromptService : IVoicePromptService
{
    public async Task<string?> ListenAsync()
    {
        if (DeviceInfo.DeviceType == DeviceType.Virtual)
            throw new InvalidOperationException("Native microphone recording is not reliable in this iOS Simulator runtime. Use the microphone on the simulated keyboard for dictation, or test the in-app microphone button on a connected iPhone.");

        var speechPermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SFSpeechRecognizer.RequestAuthorization(status => speechPermission.TrySetResult(status == SFSpeechRecognizerAuthorizationStatus.Authorized));
        if (!await speechPermission.Task)
            throw new InvalidOperationException("Speech-recognition permission is required to enter a voice prompt.");

        bool microphoneGranted;
        if (OperatingSystem.IsIOSVersionAtLeast(17)) microphoneGranted = await AVAudioApplication.RequestRecordPermissionAsync();
        else
        {
            var microphonePermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AVAudioSession.SharedInstance().RequestRecordPermission(granted => microphonePermission.TrySetResult(granted));
            microphoneGranted = await microphonePermission.Task;
        }
        if (!microphoneGranted)
            throw new InvalidOperationException("Microphone permission is required to enter a voice prompt.");

        var recognizer = new SFSpeechRecognizer(new NSLocale(System.Globalization.CultureInfo.CurrentCulture.Name))
            ?? throw new InvalidOperationException("Speech recognition is not available for the current language.");
        if (!recognizer.Available)
            throw new InvalidOperationException("Speech recognition is temporarily unavailable.");

        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Record, AVAudioSessionCategoryOptions.DuckOthers);
        session.SetActive(true);
        using var engine = new AVAudioEngine();
        using var request = new SFSpeechAudioBufferRecognitionRequest { ShouldReportPartialResults = true };
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? latest = null;
        SFSpeechRecognitionTask? task = null;
        task = recognizer.GetRecognitionTask(request, (result, error) =>
        {
            if (result is not null)
            {
                latest = result.BestTranscription.FormattedString;
                if (result.Final) completion.TrySetResult(latest);
            }
            if (error is not null) completion.TrySetException(new InvalidOperationException(error.LocalizedDescription));
        });

        var input = engine.InputNode;
        var format = input.GetBusOutputFormat(0);
        if (format.SampleRate <= 0 || format.ChannelCount == 0)
        {
            request.EndAudio();
            task.Cancel();
            session.SetActive(false);
            throw new InvalidOperationException("No microphone input is available. In Simulator, select the Mac microphone as its audio input, then try again.");
        }
        var tapInstalled = false;
        try
        {
            if (OperatingSystem.IsIOSVersionAtLeast(27))
            {
                input.InstallTapOnBus(0, 1024, format, out var tapError, (buffer, _) => request.Append(buffer));
                if (tapError is not null) throw new InvalidOperationException(tapError.LocalizedDescription);
            }
            else input.InstallTapOnBus(0, 1024, format, (buffer, _) => request.Append(buffer));
            tapInstalled = true;
            engine.Prepare();
            if (!engine.StartAndReturnError(out var startError))
                throw new InvalidOperationException(startError?.LocalizedDescription ?? "The microphone could not be started.");

            var finished = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(20)));
            if (finished == completion.Task) return await completion.Task;
            return latest;
        }
        finally
        {
            if (engine.Running) engine.Stop();
            if (tapInstalled) input.RemoveTapOnBus(0);
            request.EndAudio();
            task.Cancel();
            session.SetActive(false);
        }
    }
}
