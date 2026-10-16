using Android.Views;
using Android.Views.InputMethods;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace CareTogether.Mobile;

public sealed class ChatPromptEditorHandler : EditorHandler
{
    protected override void ConnectHandler(MauiAppCompatEditText platformView)
    {
        base.ConnectHandler(platformView);
        platformView.KeyPress += OnKeyPress;
        platformView.EditorAction += OnEditorAction;
    }

    protected override void DisconnectHandler(MauiAppCompatEditText platformView)
    {
        platformView.KeyPress -= OnKeyPress;
        platformView.EditorAction -= OnEditorAction;
        base.DisconnectHandler(platformView);
    }

    void OnKeyPress(object? sender, Android.Views.View.KeyEventArgs e)
    {
        if (e.Event?.KeyCode != Keycode.Enter || e.Event.Action != KeyEventActions.Down) return;
        if (e.Event.IsShiftPressed) { e.Handled = false; return; }
        ((ChatPromptEditor)VirtualView).RequestSend();
        e.Handled = true;
    }

    void OnEditorAction(object? sender, Android.Widget.TextView.EditorActionEventArgs e)
    {
        if (e.ActionId is ImeAction.Done or ImeAction.Send or ImeAction.Go)
        {
            ((ChatPromptEditor)VirtualView).RequestSend();
            e.Handled = true;
        }
    }
}
