namespace CareTogether.Mobile;

public sealed class ChatPromptEditor : Editor
{
    public event EventHandler? SendRequested;
    internal void RequestSend() => SendRequested?.Invoke(this, EventArgs.Empty);
}
