namespace CareTogether.Mobile;

public static class AppearanceSettings
{
    const string ThemeKey="appearance.theme",TextKey="appearance.text",DensityKey="appearance.density",ContrastKey="appearance.contrast",MotionKey="appearance.reduce-motion",RangeKey="appearance.today-range";
    public static int Theme { get=>Preferences.Default.Get(ThemeKey,0); set=>Preferences.Default.Set(ThemeKey,value); }
    public static int TextSize { get=>Preferences.Default.Get(TextKey,1); set=>Preferences.Default.Set(TextKey,value); }
    public static int Density { get=>Preferences.Default.Get(DensityKey,0); set=>Preferences.Default.Set(DensityKey,value); }
    public static bool IncreasedContrast { get=>Preferences.Default.Get(ContrastKey,false); set=>Preferences.Default.Set(ContrastKey,value); }
    public static bool ReduceMotion { get=>Preferences.Default.Get(MotionKey,false); set=>Preferences.Default.Set(MotionKey,value); }
    public static int TodayRangeIndex { get=>Math.Clamp(Preferences.Default.Get(RangeKey,0),0,3); set=>Preferences.Default.Set(RangeKey,Math.Clamp(value,0,3)); }
    public static double TextScale=>TextSize switch{0=>.9,2=>1.15,3=>1.3,_=>1};
    public static double CardPadding=>Density switch{1=>10,2=>18,_=>14};
    public static double BodySpacing=>Density switch{1=>7,2=>14,_=>10};
    public static bool IsDark=>Theme==2||(Theme==0&&Application.Current?.RequestedTheme==AppTheme.Dark);
    public static void Apply(){if(Application.Current is null)return;Application.Current.UserAppTheme=Theme switch{1=>AppTheme.Light,2=>AppTheme.Dark,_=>AppTheme.Unspecified};}
}

public sealed class AppearancePage : CarePage
{
    readonly Picker theme=new(){ItemsSource=new[]{"System Default","Light","Dark"},BackgroundColor=Colors.White};readonly Picker textSize=new(){ItemsSource=new[]{"Small","Standard","Large","Extra Large"},BackgroundColor=Colors.White};readonly Picker density=new(){ItemsSource=new[]{"Comfortable","Compact","Extra Comfortable"},BackgroundColor=Colors.White};readonly Picker defaultRange=new(){ItemsSource=new[]{"Today","Next 7 days","Next 15 days","Next 30 days"},BackgroundColor=Colors.White};readonly Switch contrast=new();readonly Switch reduceMotion=new();readonly VerticalStackLayout preview=new(){Spacing=6};
    public AppearancePage(ApiClient api):base(api,"Appearance")
    {
        theme.SelectedIndex=AppearanceSettings.Theme;textSize.SelectedIndex=AppearanceSettings.TextSize;density.SelectedIndex=AppearanceSettings.Density;defaultRange.SelectedIndex=AppearanceSettings.TodayRangeIndex;contrast.IsToggled=AppearanceSettings.IncreasedContrast;reduceMotion.IsToggled=AppearanceSettings.ReduceMotion;
        AddPicker("Theme",theme);AddPicker("Text size",textSize);AddPicker("Display density",density);AddSwitch("Increase contrast",contrast,"Strengthens text and card boundaries.");AddSwitch("Reduce motion",reduceMotion,"Avoids nonessential interface animation.");AddPicker("Default Today range",defaultRange);Body.Add(CareTheme.Text("Preview",14,true));Body.Add(preview);var save=CareTheme.Primary("Save Appearance");save.Clicked+=Save;Body.Add(save);theme.SelectedIndexChanged+=(_,_)=>UpdatePreview();textSize.SelectedIndexChanged+=(_,_)=>UpdatePreview();density.SelectedIndexChanged+=(_,_)=>UpdatePreview();contrast.Toggled+=(_,_)=>UpdatePreview();UpdatePreview();
    }
    void AddPicker(string label,Picker picker){Body.Add(CareTheme.Text(label,12,false,CareTheme.Muted));Body.Add(picker);}
    void AddSwitch(string label,Switch value,string detail){var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};grid.Add(new VerticalStackLayout{Spacing=1,Children={CareTheme.Text(label,13,true),CareTheme.Text(detail,11,false,CareTheme.Muted)}},0,0);grid.Add(value,1,0);Body.Add(CareTheme.Card(grid));}
    void UpdatePreview(){preview.Clear();var scale=textSize.SelectedIndex switch{0=>.9,2=>1.15,3=>1.3,_=>1};var padding=density.SelectedIndex switch{1=>10,2=>18,_=>14};var title=CareTheme.Text("Medication reminder",14*scale,true,contrast.IsToggled?Colors.Black:CareTheme.Ink);var detail=CareTheme.Text("Maria • Metoprolol 25 mg at 8:00 AM",12*scale,false,contrast.IsToggled?Color.FromArgb("#344054"):CareTheme.Muted);var card=CareTheme.Card(new VerticalStackLayout{Spacing=4,Children={title,detail}});card.Padding=padding;preview.Add(card);}
    async void Save(object? sender,EventArgs e){AppearanceSettings.Theme=Math.Max(0,theme.SelectedIndex);AppearanceSettings.TextSize=Math.Max(0,textSize.SelectedIndex);AppearanceSettings.Density=Math.Max(0,density.SelectedIndex);AppearanceSettings.IncreasedContrast=contrast.IsToggled;AppearanceSettings.ReduceMotion=reduceMotion.IsToggled;AppearanceSettings.TodayRangeIndex=Math.Max(0,defaultRange.SelectedIndex);AppearanceSettings.Apply();await DisplayAlertAsync("Appearance saved","The settings apply to newly opened screens. Reopen the current page to refresh its layout.","OK");await Navigation.PopAsync();}
}
