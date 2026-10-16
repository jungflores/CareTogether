namespace CareTogether.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if IOS
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("CareTogetherTextColors",(handler,view)=>
        {
            handler.PlatformView.TextColor=UIKit.UIColor.FromRGB(16,24,40);
            handler.PlatformView.TintColor=UIKit.UIColor.FromRGB(8,102,232);
            handler.PlatformView.OverrideUserInterfaceStyle=UIKit.UIUserInterfaceStyle.Light;
        });
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("CareTogetherTextColors",(handler,view)=>
        {
            handler.PlatformView.TextColor=UIKit.UIColor.FromRGB(16,24,40);
            handler.PlatformView.TintColor=UIKit.UIColor.FromRGB(8,102,232);
            handler.PlatformView.OverrideUserInterfaceStyle=UIKit.UIUserInterfaceStyle.Light;
        });
#endif
        var builder=MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.ConfigureMauiHandlers(handlers=>handlers.AddHandler<ChatPromptEditor,ChatPromptEditorHandler>());
        var virtualDevice=DeviceInfo.DeviceType==DeviceType.Virtual;
        var apiBaseUrl=DeviceInfo.Platform==DevicePlatform.Android&&virtualDevice
            ?"http://10.0.2.2:8080"
            :DeviceInfo.Platform==DevicePlatform.iOS&&virtualDevice
                ?"http://127.0.0.1:8080"
                :"http://192.168.1.184:8080";
        builder.Services.AddSingleton(new ApiClient(apiBaseUrl));
        return builder.Build();
    }
}
