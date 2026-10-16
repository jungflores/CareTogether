namespace CareTogether.Mobile;

public sealed class AppShell : Shell
{
    public AppShell(ApiClient api)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetTabBarBackgroundColor(this, Colors.White); Shell.SetTabBarForegroundColor(this, CareTheme.Blue); Shell.SetTabBarUnselectedColor(this, CareTheme.Muted);
        var tabs = new TabBar();
        tabs.Items.Add(Item("Today", "🏠", new LiveDashboardPage(api))); tabs.Items.Add(Item("Tasks", "✅", new LiveTasksPage(api)));
        tabs.Items.Add(Item("Medications", "💊", new LiveMedicationsPage(api)));tabs.Items.Add(Item("Appointments", "📅", new LiveAppointmentsPage(api)));tabs.Items.Add(Item("More", "•••", new MorePage(api))); Items.Add(tabs);
    }
    static ShellContent Item(string title, string icon, Page page) => new() { Title = title, Icon = new FontImageSource { Glyph = icon, Color = CareTheme.Blue, Size = 20 }, Content = page };
}

public sealed class MorePage : CarePage
{
    public MorePage(ApiClient api) : base(api, "More")
    {
        AddLink("📓", "Care Journal", "Notes and observations", new LiveJournalPage(api));
        AddLink("❤️", "Health Tracking", "Blood pressure, weight and glucose", new LiveHealthPage(api)); AddLink("📁", "Documents", "Medical, insurance and legal", new LiveDocumentsPage(api));
        AddLink("💬", "Messages", "Care Circle conversations", new LiveConversationsPage(api)); AddLink("🔄", "Care Handoff", "Transfer care responsibility", new LiveHandoffsPage(api));
        AddLink("🆘", "Emergency", "Contacts, doctor and hospital", new LiveEmergencyPage(api), Color.FromArgb("#FFE0E1")); AddLink("⚙️", "Settings", "Preferences and privacy", new ProfilePage(api));
    }
}
