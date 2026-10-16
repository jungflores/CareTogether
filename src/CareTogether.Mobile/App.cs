namespace CareTogether.Mobile;
public sealed class App : Application
{
    readonly ApiClient api;
    public App(ApiClient api)
    {
        this.api=api;UserAppTheme=AppearanceSettings.Theme switch{1=>AppTheme.Light,2=>AppTheme.Dark,_=>AppTheme.Unspecified};
    }
    protected override Window CreateWindow(IActivationState? activationState) => new(new NavigationPage(new WelcomePage(api))
    {
        BarBackgroundColor = CareTheme.Surface,
        BarTextColor = CareTheme.Ink
    });
}
