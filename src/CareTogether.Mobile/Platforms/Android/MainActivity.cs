using Android.App; using Android.Content; using Android.Content.PM; using Android.OS; using Android.Views;
namespace CareTogether.Mobile; [Activity(Theme="@style/Maui.SplashTheme",MainLauncher=true,WindowSoftInputMode=SoftInput.AdjustResize,ConfigurationChanges=ConfigChanges.ScreenSize|ConfigChanges.Orientation|ConfigChanges.UiMode|ConfigChanges.ScreenLayout|ConfigChanges.SmallestScreenSize|ConfigChanges.Density)] public class MainActivity:MauiAppCompatActivity
{
    internal static event Action<int,Result,Intent?>? ActivityResultReceived;
    protected override void OnActivityResult(int requestCode,Result resultCode,Intent? data){base.OnActivityResult(requestCode,resultCode,data);ActivityResultReceived?.Invoke(requestCode,resultCode,data);}
}
