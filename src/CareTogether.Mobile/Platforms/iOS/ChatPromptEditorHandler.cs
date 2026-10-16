using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace CareTogether.Mobile;

public sealed class ChatPromptEditorHandler : EditorHandler
{
    protected override MauiTextView CreatePlatformView() => new ChatPromptTextView();

    protected override void ConnectHandler(MauiTextView platformView)
    {
        base.ConnectHandler(platformView);
        var native = (ChatPromptTextView)platformView;
        native.Owner = (ChatPromptEditor)VirtualView;
        native.ShouldChangeText = (_, _, replacement) =>
        {
            if (replacement != "\n") return true;
            if (native.AllowNextNewline) { native.AllowNextNewline = false; return true; }
            native.Owner?.RequestSend();
            return false;
        };
    }

    protected override void DisconnectHandler(MauiTextView platformView)
    {
        var native = (ChatPromptTextView)platformView;
        native.ShouldChangeText = null;
        native.Owner = null;
        base.DisconnectHandler(platformView);
    }
}

sealed class ChatPromptTextView : MauiTextView
{
    internal ChatPromptEditor? Owner { get; set; }
    internal bool AllowNextNewline { get; set; }

    public ChatPromptTextView() : base(CGRect.Empty) { }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        var key = presses.AnyObject is UIPress press ? press.Key : null;
        if (key?.KeyCode == UIKeyboardHidUsage.KeyboardReturnOrEnter)
        {
            if (key.ModifierFlags.HasFlag(UIKeyModifierFlags.Shift))
            {
                AllowNextNewline = true;
                InsertText("\n");
            }
            else Owner?.RequestSend();
            return;
        }
        base.PressesBegan(presses, evt);
    }
}
