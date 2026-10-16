using Microsoft.Maui.Controls.Shapes;

namespace CareTogether.Mobile;

public static class CareTheme
{
    public static readonly Color Blue = Color.FromArgb("#0866E8");
    public static Color Ink => AppearanceSettings.IsDark?Colors.White:AppearanceSettings.IncreasedContrast?Colors.Black:Color.FromArgb("#101828");
    public static Color Muted => AppearanceSettings.IsDark?Color.FromArgb("#D0D5DD"):AppearanceSettings.IncreasedContrast?Color.FromArgb("#344054"):Color.FromArgb("#667085");
    public static Color Surface => AppearanceSettings.IsDark?Color.FromArgb("#101828"):Color.FromArgb("#F7F9FC");
    public static Color Line => AppearanceSettings.IsDark?Color.FromArgb("#667085"):AppearanceSettings.IncreasedContrast?Color.FromArgb("#667085"):Color.FromArgb("#E4E9F0");
    public static Color CardSurface => AppearanceSettings.IsDark?Color.FromArgb("#1D2939"):Colors.White;
    public static readonly Color Red = Color.FromArgb("#E5484D");

    public static Label Text(string text, double size = 14, bool bold = false, Color? color = null) => new()
    { Text = text, FontSize = size*AppearanceSettings.TextScale, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, TextColor = color ?? Ink, VerticalTextAlignment = TextAlignment.Center };

    public static Border Card(View content, Color? background = null) => new()
    { Content = content, Padding = AppearanceSettings.CardPadding, BackgroundColor = background ?? CardSurface, Stroke = Line, StrokeThickness = AppearanceSettings.IncreasedContrast?1.5:1, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Margin = new Thickness(0, AppearanceSettings.Density==1?1:3) };

    public static Button Primary(string text) => new()
    { Text = text, BackgroundColor = Blue, TextColor = Colors.White, CornerRadius = 10, HeightRequest = 48, FontAttributes = FontAttributes.Bold };

    public static Button Small(string text) => new()
    { Text = text, BackgroundColor = Blue, TextColor = Colors.White, CornerRadius = 8, HeightRequest = 36, Padding = new Thickness(12, 4), FontSize = 13 };

    public static Button AddAction(string text = "Add")
    {
        var button=new Button{Text=text,ImageSource="add_document_blue.svg",BackgroundColor=Colors.Transparent,TextColor=Muted,FontSize=10,FontAttributes=FontAttributes.Bold,HeightRequest=48,MinimumWidthRequest=44,Padding=new Thickness(2,1),ContentLayout=new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top,0),AutomationId=$"{text} item"};
        ToolTipProperties.SetText(button,$"{text} item");SemanticProperties.SetDescription(button,$"{text} item");
        return button;
    }

    public static ImageButton PlusAction(string description)
    {
        var button=new ImageButton
        {
            Source=new FontImageSource{Glyph="＋",Color=Blue,Size=21},
            BackgroundColor=Color.FromArgb("#E8F1FF"),CornerRadius=18,HeightRequest=36,WidthRequest=36,Padding=7,AutomationId=description
        };
        ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);return button;
    }

    public static Grid Row(string icon, string title, string detail, Color? tint = null, bool chevron = true)
    {
        var g = new Grid { ColumnDefinitions = [new(46), new(GridLength.Star), new(18)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 8 };
        var avatar = new Border { WidthRequest = 42, HeightRequest = 42, BackgroundColor = tint ?? Color.FromArgb("#E8F1FF"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 21 }, Content = new Label { Text = icon, FontSize = 18, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center } };
        Grid.SetRowSpan(avatar, 2); g.Add(avatar, 0, 0); g.Add(Text(title, 14, true), 1, 0); g.Add(Text(detail, 12, false, Muted), 1, 1);
        if (chevron) { var c = Text("›", 24, false, Muted); Grid.SetRowSpan(c, 2); g.Add(c, 2, 0); }
        return g;
    }

    public static TapGestureRecognizer Tap(Func<Task> action) { var tap = new TapGestureRecognizer(); tap.Tapped += async (_, _) => await action(); return tap; }
}

public abstract class CarePage : ContentPage
{
    protected readonly ApiClient Api;
    protected readonly VerticalStackLayout Body = new() { Spacing = 10, Padding = new Thickness(16, 10, 16, 30) };
    protected readonly Button BackButton;
    protected readonly Label HeaderTitle;
    protected readonly Grid Header;
    protected readonly ScrollView BodyScroll;
    protected readonly Grid PageLayout;
    protected CarePage(ApiClient api, string title, View? trailing = null)
    {
        Api = api; Title = title; BackgroundColor = CareTheme.Surface; Shell.SetNavBarIsVisible(this, false);
        // .NET MAUI 10 defaults to edge-to-edge content. Respect the device
        // safe area so pages stay clear of the status, home-indicator and
        // Shell tab-bar regions on iOS (and system bars on Android).
        SafeAreaEdges = SafeAreaEdges.All;
        Header = new Grid { ColumnDefinitions = [new(36), new(GridLength.Star), new(GridLength.Auto),new(GridLength.Auto)], ColumnSpacing=6, Margin = new Thickness(16, 10, 16, 8) };
        BackButton = new Button { Text = "‹", FontSize = 29, BackgroundColor = Colors.Transparent, TextColor = CareTheme.Ink, Padding = 0 };
        BackButton.Clicked += async (_, _) => await NavigateBackAsync();
        HeaderTitle=CareTheme.Text(title,19,true);
        Header.Add(BackButton, 0, 0); Header.Add(HeaderTitle, 1, 0);
        if(Api.Token is not null)
        {
            var actions=new HorizontalStackLayout{Spacing=8};
            if(this is not CareChatPage){var ask=HeaderAction("🪄","Ask","Ask CareTogether");ask.Button.Clicked+=async(_,_)=>await Navigation.PushAsync(new CareChatPage(Api));actions.Add(ask.View);}
            var exit=HeaderAction("🚪","Exit","Sign out");exit.Button.Clicked+=async(_,_)=>await SignOutAsync();actions.Add(exit.View);Header.Add(actions,3,0);
        }
        if (trailing is not null) Header.Add(trailing, 2, 0);
        Body.Spacing=AppearanceSettings.BodySpacing;Body.Padding = new Thickness(16, 2, 16, 30);
        BodyScroll = new ScrollView { Content = Body };
        PageLayout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        PageLayout.Add(Header, 0, 0); PageLayout.Add(BodyScroll, 0, 1);
        // Keep navigation and page actions fixed while only the page body
        // scrolls. The Grid clips the body to its row so content cannot draw
        // over the header or beneath the Shell tab bar.
        Content = PageLayout;
    }
    protected void AddLink(string icon, string title, string detail, Page page, Color? tint = null) { var c = CareTheme.Card(CareTheme.Row(icon, title, detail, tint)); c.GestureRecognizers.Add(CareTheme.Tap(() => Navigation.PushAsync(page))); Body.Add(c); }
    static (VerticalStackLayout View,ImageButton Button) HeaderAction(string glyph,string labelText,string description)
    {
        var button=new ImageButton{Source=new FontImageSource{Glyph=glyph,Size=21},BackgroundColor=Colors.Transparent,Padding=2,HeightRequest=28,WidthRequest=34,Aspect=Aspect.AspectFit,AutomationId=description};ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);var label=CareTheme.Text(labelText,10,true,CareTheme.Muted);label.HorizontalTextAlignment=TextAlignment.Center;return(new VerticalStackLayout{Spacing=0,HorizontalOptions=LayoutOptions.Center,Children={button,label}},button);
    }
    async Task SignOutAsync(){if(!await DisplayAlertAsync("Sign out?","Are you sure you want to sign out of CareTogether?","Sign Out","Cancel"))return;Api.Logout();Application.Current!.Windows[0].Page=new NavigationPage(new WelcomePage(Api)){BarBackgroundColor=CareTheme.Surface,BarTextColor=CareTheme.Ink};}
    protected static Entry Field(string placeholder, string? text = null) => new() { Placeholder = placeholder, Text = text, TextColor=CareTheme.Ink, PlaceholderColor=CareTheme.Muted, BackgroundColor = CareTheme.CardSurface, HeightRequest = 48 };
    protected virtual async Task NavigateBackAsync() { if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); }
}
