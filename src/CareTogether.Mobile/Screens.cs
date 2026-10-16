namespace CareTogether.Mobile;

public sealed class WelcomePage : ContentPage
{
    public WelcomePage(ApiClient api)
    {
        BackgroundColor = AppearanceSettings.IsDark?CareTheme.Surface:Color.FromArgb("#EEF6FF"); Shell.SetNavBarIsVisible(this, false);
        var start = CareTheme.Primary("Get Started"); var signIn = new Button { Text = "Sign In", BackgroundColor = CareTheme.CardSurface, TextColor = CareTheme.Ink, BorderColor = CareTheme.Line, BorderWidth = AppearanceSettings.IncreasedContrast?2:1, CornerRadius = 10, HeightRequest = 48, FontAttributes=FontAttributes.Bold };
        start.Clicked += async (_, _) => await Navigation.PushAsync(new RegistrationPage(api)); signIn.Clicked += async (_, _) => await Navigation.PushAsync(new LoginPage(api));
        var careCircleArtwork = new Image
        {
            Source = "connected_care_circle.png",
            Aspect = Aspect.AspectFit,
            HeightRequest = 270,
            MaximumWidthRequest = 360,
            HorizontalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetDescription(careCircleArtwork,"A care recipient supported by family and a healthcare professional");
        var hero = new VerticalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center, Children = { CareTheme.Text("CareTogether", 31, true), CareTheme.Text("Care for the people\nwho matter most", 17, false, Color.FromArgb("#274C77")), careCircleArtwork } };
        foreach (var label in hero.Children.OfType<Label>()) label.HorizontalTextAlignment = TextAlignment.Center;
        var footer = new VerticalStackLayout { Spacing = 10, Children = { start, signIn, new Label { Text = "Care. Coordinate. Stay Connected.", FontSize = 11, TextColor = Color.FromArgb("#274C77"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10) } } };
        var grid = new Grid { Padding = new Thickness(24, 28, 24, 20), RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] }; grid.Add(hero); grid.Add(footer, 0, 1); Content = grid;
    }
}

public sealed class RegistrationPage : CarePage
{
    public RegistrationPage(ApiClient api):base(api,"Create Account")
    {
        Body.Add(new ProgressBar{Progress=.15,ProgressColor=CareTheme.Blue,Margin=new Thickness(20,0)});Body.Add(CareTheme.Text("Let’s create your account",25,true));Body.Add(CareTheme.Text("Your account keeps every Care Circle private and synchronized.",13,false,CareTheme.Muted));var name=Field("Full name");var email=Field("Email address");email.Keyboard=Keyboard.Email;email.IsTextPredictionEnabled=false;email.IsSpellCheckEnabled=false;var password=Field("Password");password.IsPassword=true;var confirm=Field("Confirm password");confirm.IsPassword=true;var accept=new CheckBox();Body.Add(name);Body.Add(email);Body.Add(password);Body.Add(confirm);Body.Add(new HorizontalStackLayout{Spacing=8,Children={accept,CareTheme.Text("I agree to the privacy policy and terms",12)}});var next=CareTheme.Primary("Create Account & Continue");Body.Add(next);
        next.Clicked+=async(_,_)=> {var displayName=(name.Text??"").Trim();var address=(email.Text??"").Trim();var secret=password.Text??"";if(string.IsNullOrWhiteSpace(displayName)||!address.Contains('@')){await DisplayAlertAsync("Account information required","Enter your full name and a valid email address.","OK");return;}if(secret.Length<8){await DisplayAlertAsync("Password too short","Use at least eight characters.","OK");return;}if(secret!=confirm.Text){await DisplayAlertAsync("Passwords do not match","Enter the same password in both fields.","OK");return;}if(!accept.IsChecked){await DisplayAlertAsync("Agreement required","Accept the privacy policy and terms to continue.","OK");return;}try{next.IsEnabled=false;next.Text="Creating account…";await Api.RegisterAsync(displayName,address,secret);try{await SecureStorage.Default.SetAsync("caretogether.last-login.email",address);await SecureStorage.Default.SetAsync("caretogether.last-login.password",secret);}catch{}await Navigation.PushAsync(new OnboardingPage(Api));}catch(Exception ex){await DisplayAlertAsync("Could not create account",ex.Message.Contains("already",StringComparison.OrdinalIgnoreCase)?"This email already has an account. Use Sign In instead.":ex.Message,"OK");}finally{next.IsEnabled=true;next.Text="Create Account & Continue";}};
    }
}

public sealed class LoginPage : CarePage
{
    const string LastLoginEmailKey="caretogether.last-login.email";
    const string LastLoginPasswordKey="caretogether.last-login.password";
    bool credentialsLoaded;

    public LoginPage(ApiClient api) : base(api, "Sign In")
    {
        Body.Add(CareTheme.Text("Welcome back", 27, true)); Body.Add(CareTheme.Text("Coordinate care with everyone who matters.", 14, false, CareTheme.Muted));
        var email = Field("Email", "alex@caretogether.local"); email.Keyboard=Keyboard.Email; email.IsTextPredictionEnabled=false; email.IsSpellCheckEnabled=false;
        var password = Field("Password", "CareTogether123!"); password.IsPassword = true; var button = CareTheme.Primary("Sign In");
        Appearing+=async(_,_)=>
        {
            if(credentialsLoaded)return;credentialsLoaded=true;
            try
            {
                var savedEmail=await SecureStorage.Default.GetAsync(LastLoginEmailKey);var savedPassword=await SecureStorage.Default.GetAsync(LastLoginPasswordKey);
                if(!string.IsNullOrWhiteSpace(savedEmail))email.Text=savedEmail;
                if(savedPassword is not null)password.Text=savedPassword;
            }
            catch { /* Keep the development defaults if secure storage is unavailable. */ }
        };
        button.Clicked += async (_, _) =>
        {
            var loginEmail=(email.Text??"").Trim();var loginPassword=password.Text??"";
            try
            {
                button.IsEnabled=false;
                if(!await Api.LoginAsync(loginEmail,loginPassword)){await DisplayAlertAsync("Sign in failed","Check your email and password.","OK");return;}
                try{await SecureStorage.Default.SetAsync(LastLoginEmailKey,loginEmail);await SecureStorage.Default.SetAsync(LastLoginPasswordKey,loginPassword);}catch{/* Login still succeeds if the device keychain is unavailable. */}
                Application.Current!.Windows[0].Page=new AppShell(Api);
            }
            catch(Exception ex){await DisplayAlertAsync("API unavailable",$"Could not connect to {Api.BaseUrl}.\n\n{ex.Message}","OK");}
            finally{button.IsEnabled=true;}
        };
        Body.Add(email); Body.Add(password); Body.Add(button);
    }

    protected override Task NavigateBackAsync()
    {
        Application.Current!.Windows[0].Page=new NavigationPage(new WelcomePage(Api))
        {
            BarBackgroundColor=CareTheme.Surface,
            BarTextColor=CareTheme.Ink
        };
        return Task.CompletedTask;
    }

    protected override bool OnBackButtonPressed()
    {
        _=NavigateBackAsync();
        return true;
    }
}

public sealed class OnboardingPage : CarePage
{
    public OnboardingPage(ApiClient api) : base(api, "")
    {
        Body.Add(new ProgressBar { Progress = .34, ProgressColor = CareTheme.Blue, Margin = new Thickness(35, 0) }); var prompt = CareTheme.Text("Who are you setting\nup care for?", 24, true); prompt.HorizontalTextAlignment = TextAlignment.Center; prompt.Margin = new Thickness(0, 25); Body.Add(prompt);
        Body.Add(Choice("👤", "A family member", "(e.g. parent, spouse, relative)", () => Navigation.PushAsync(new AddRecipientPage(Api,true,false)))); Body.Add(Choice("👤", "Myself", "", () => Navigation.PushAsync(new AddRecipientPage(Api,true,true))));
    }
    static Border Choice(string icon, string title, string detail, Func<Task> action) { var stack = new VerticalStackLayout { Spacing = 7, Padding = new Thickness(10, 18), Children = { CareTheme.Text(icon, 42), CareTheme.Text(title, 17, true), CareTheme.Text(detail, 12, false, CareTheme.Muted) } }; foreach (var l in stack.Children.OfType<Label>()) l.HorizontalTextAlignment = TextAlignment.Center; var card = CareTheme.Card(stack); card.GestureRecognizers.Add(CareTheme.Tap(action)); return card; }
}

public sealed class AddRecipientPage : CarePage
{
    readonly bool guided;
    public AddRecipientPage(ApiClient api,bool guided=false,bool self=false) : base(api, "Add Care Recipient")
    {
        this.guided=guided;
        var portrait = CareTheme.Card(CareTheme.Text("👵🏻", 56), Color.FromArgb("#E6E9EE")); portrait.WidthRequest = portrait.HeightRequest = 96; portrait.HorizontalOptions = LayoutOptions.Center; Body.Add(portrait);
        Body.Add(new ProgressBar{Progress=.5,ProgressColor=CareTheme.Blue,IsVisible=guided});var parts=self?(Api.DisplayName??"").Split(' ',2,StringSplitOptions.RemoveEmptyEntries):[];Body.Add(CareTheme.Text("First Name", 12, false, CareTheme.Muted)); var firstName=Field("First Name",parts.Length>0?parts[0]:null); Body.Add(firstName);
        Body.Add(CareTheme.Text("Last Name", 12, false, CareTheme.Muted)); var lastName=Field("Last Name",parts.Length>1?parts[1]:null); Body.Add(lastName);
        Body.Add(CareTheme.Text("Date of Birth", 12, false, CareTheme.Muted)); var birthDate=new DatePicker { MaximumDate=DateTime.Today, Date=DateTime.Today.AddYears(-70), BackgroundColor=Colors.White }; Body.Add(birthDate);
        Body.Add(CareTheme.Text("Relationship", 12, false, CareTheme.Muted)); var relationship=new Picker { ItemsSource = new[] { "Mother", "Father", "Spouse", "Relative", "Friend", "Myself" }, SelectedIndex = self?5:0, IsEnabled=!self, BackgroundColor = Colors.White }; Body.Add(relationship);
        var next = CareTheme.Primary("Save & Continue");
        next.Clicked += async (_, _) =>
        {
            var fullName=$"{firstName.Text} {lastName.Text}".Trim();
            if(string.IsNullOrWhiteSpace(firstName.Text)||string.IsNullOrWhiteSpace(lastName.Text)) { await DisplayAlertAsync("Name required","Enter the care recipient's first and last name.","OK"); return; }
            if(Api.Token is null) { await DisplayAlertAsync("Sign in required","Sign in before adding a care recipient so the profile can be saved securely.","OK"); await Navigation.PushModalAsync(new NavigationPage(new LoginPage(Api))); return; }
            next.IsEnabled=false; next.Text="Saving…";
            try
            {
                var created=await Api.PostAsync("api/care-circles",new{recipientName=fullName,dateOfBirth=birthDate.Date?.ToString("yyyy-MM-dd"),relationship=relationship.SelectedItem?.ToString()});
                var id=created.GetProperty("id").GetGuid();await DisplayAlertAsync("Care recipient added",$"{fullName}'s Care Circle is ready.","Continue");
                await Navigation.PushAsync(guided?new GuidedSetupPage(Api,id,fullName):new CareCirclePage(Api,fullName,id));
            }
            catch(Exception ex) { await DisplayAlertAsync("Could not save",ex.Message,"OK"); }
            finally { next.IsEnabled=true; next.Text="Save & Continue"; }
        };
        Body.Add(next);
    }
}

public sealed class GuidedSetupPage : CarePage
{
    readonly Guid circleId;readonly string recipientName;readonly VerticalStackLayout checklist=new(){Spacing=7};readonly ProgressBar progress=new(){ProgressColor=CareTheme.Blue};
    public GuidedSetupPage(ApiClient api,Guid circleId,string recipientName):base(api,"Finish Setup")
    {
        this.circleId=circleId;this.recipientName=recipientName;BackButton.IsEnabled=false;BackButton.Opacity=.28;Body.Add(progress);Body.Add(CareTheme.Card(CareTheme.Row("👤",recipientName,"Your first Care Circle is ready",Color.FromArgb("#F3E8D9"),false)));Body.Add(CareTheme.Text("Add essential care information",21,true));Body.Add(CareTheme.Text("Complete what you know now. You can add or change everything later.",13,false,CareTheme.Muted));Body.Add(checklist);var finish=CareTheme.Primary("Go to Today");finish.Clicked+=(_,_)=>Application.Current!.Windows[0].Page=new AppShell(Api);Body.Add(finish);Body.Add(CareTheme.Text("Unfinished items remain available from Today and More.",11,false,CareTheme.Muted));Appearing+=async(_,_)=>await Load();
    }
    async Task Load()
    {
        checklist.Clear();try{var circleTask=Api.GetAsync($"api/care-circles/{circleId}");var contactsTask=Api.GetAsync($"api/emergency-contacts?careCircleId={circleId}");var medicationsTask=Api.GetAsync($"api/medications?careCircleId={circleId}");var appointmentsTask=Api.GetAsync($"api/appointments?careCircleId={circleId}");var tasksTask=Api.GetAsync($"api/tasks?careCircleId={circleId}");await Task.WhenAll(circleTask,contactsTask,medicationsTask,appointmentsTask,tasksTask);var circle=await circleTask;var invited=circle.GetProperty("members").GetArrayLength()>1||circle.GetProperty("invitations").GetArrayLength()>0;var contacts=(await contactsTask).GetArrayLength()>0;var medications=(await medicationsTask).GetArrayLength()>0;var appointments=(await appointmentsTask).GetArrayLength()>0;var tasks=(await tasksTask).GetArrayLength()>0;var completed=new[]{invited,contacts,medications,appointments,tasks}.Count(x=>x);progress.Progress=.55+completed*.09;AddItem(invited,"👥","Invite family or a caregiver","Share access to this Care Circle",()=>Navigation.PushAsync(new InviteMemberPage(Api,circleId,recipientName)));AddItem(contacts,"!","Add an emergency contact","Record who should be called first",()=>Navigation.PushAsync(new AddEmergencyContactPage(Api)));AddItem(medications,"💊","Add a medication","Create its schedule and dose cards",()=>Navigation.PushAsync(new MedicationEditorPage(Api)));AddItem(appointments,"📅","Add an appointment","Record the provider, location, date and time",()=>Navigation.PushAsync(new AppointmentEditorPage(Api)));AddItem(tasks,"☑","Add a care task","Assign a care activity and due date",()=>Navigation.PushAsync(new TaskEditorPage(Api)));}catch(Exception ex){checklist.Add(CareTheme.Card(CareTheme.Text($"Could not load setup status.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
    void AddItem(bool complete,string icon,string title,string detail,Func<Task> action){var row=CareTheme.Row(complete?"✓":icon,title,complete?$"Completed • {detail}":detail,complete?Color.FromArgb("#DDF5E8"):Color.FromArgb("#E8F1FF"),false);var card=CareTheme.Card(row,complete?Color.FromArgb("#F4FBF7"):null);card.GestureRecognizers.Add(CareTheme.Tap(action));checklist.Add(card);}
}

public sealed class CareCirclePage : CarePage
{
    readonly VerticalStackLayout people=new(){Spacing=6}; readonly Guid? circleId; readonly string recipientName;
    public CareCirclePage(ApiClient api, string recipientName="Maria Santos", Guid? circleId=null) : base(api, "Care Circle", CareTheme.AddAction("Invite"))
    {
        this.circleId=circleId; this.recipientName=recipientName;
        var invitePage=new InviteMemberPage(Api,circleId,recipientName);
        var header=Header; if(header.Children.LastOrDefault() is Button inviteButton) inviteButton.Clicked+=async(_,_)=>await Navigation.PushAsync(invitePage);
        Body.Add(CareTheme.Card(CareTheme.Row("👵🏻",recipientName,"Care Recipient",Color.FromArgb("#F3E8D9"),false))); Body.Add(people);
        var inviteCard=CareTheme.Card(CareTheme.Row("＋", "Invite a Family Member", "or Caregiver", Color.FromArgb("#E8F1FF"), false)); inviteCard.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new InviteMemberPage(Api,circleId,recipientName)))); Body.Add(inviteCard);
        var done = CareTheme.Primary("Go to Today"); done.Clicked += (_, _) => Application.Current!.Windows[0].Page = new AppShell(Api); Body.Add(done);
        Appearing+=async(_,_)=>await LoadPeople();
    }
    async Task LoadPeople()
    {
        people.Clear();
        if(circleId is null){people.Add(CareTheme.Card(CareTheme.Text("Open this Care Circle from Settings to load its members.",12,false,CareTheme.Muted)));return;}
        people.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try
        {
            var result=await Api.GetAsync($"api/care-circles/{circleId}"); people.Clear();
            foreach(var member in result.GetProperty("members").EnumerateArray())
            {
                var name=member.GetProperty("displayName").GetString()??member.GetProperty("email").GetString()??"Care Circle member";
                var role=member.GetProperty("role").GetString()??"Caregiver";
                var isActive=member.GetProperty("isActive").GetBoolean(); var isCurrentUser=member.GetProperty("isCurrentUser").GetBoolean();
                var canRemove=member.GetProperty("canRemove").GetBoolean(); var hasInteractions=member.GetProperty("hasInteractions").GetBoolean(); var memberId=member.GetProperty("id").GetGuid();
                var detail=isActive?role:$"{role} • Deactivated";
                var row=CareTheme.Row(role=="Clinician"?"⚕":"👤",name,detail,isActive?(role=="Clinician"?Color.FromArgb("#DDF5F2"):Color.FromArgb("#DDF2E7")):Color.FromArgb("#ECEFF3"),false);
                View content=row;
                if(canRemove)
                {
                    var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star),new(44)],ColumnSpacing=6}; wrapper.Add(row,0,0);
                    var trash=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,Padding=10,HeightRequest=40,WidthRequest=40,Aspect=Aspect.AspectFit,AutomationId=$"Remove {name}"};
                    trash.Clicked+=async(_,_)=>await ConfirmRemove(memberId,name); wrapper.Add(trash,1,0); content=wrapper;
                }
                var card=CareTheme.Card(content);
                if(!isCurrentUser&&isActive&&hasInteractions)card.GestureRecognizers.Add(CareTheme.Tap(()=>ManageMember(memberId,name)));
                people.Add(card);
            }
            foreach(var invitation in result.GetProperty("invitations").EnumerateArray())
            {
                var email=invitation.GetProperty("email").GetString()??"Pending invitation"; var role=invitation.GetProperty("role").GetString()??"Caregiver";
                var row=CareTheme.Row("✉",email,$"{role} • Invitation pending",Color.FromArgb("#FFF4D9"),false);View content=row;
                if(invitation.TryGetProperty("canCancel",out var canCancelValue)&&canCancelValue.GetBoolean())
                {
                    var invitationId=invitation.GetProperty("id").GetGuid();var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star),new(44)],ColumnSpacing=6};wrapper.Add(row,0,0);
                    var cancel=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,Padding=10,HeightRequest=40,WidthRequest=40,Aspect=Aspect.AspectFit,AutomationId=$"Cancel invitation for {email}"};
                    cancel.Clicked+=async(_,_)=>await ConfirmCancelInvitation(invitationId,email);wrapper.Add(cancel,1,0);content=wrapper;
                }
                people.Add(CareTheme.Card(content));
            }
            if(people.Count==0) people.Add(CareTheme.Card(CareTheme.Text("No caregivers have been added yet.",12,false,CareTheme.Muted)));
        }
        catch(Exception ex){people.Clear();people.Add(CareTheme.Card(CareTheme.Text($"Could not load Care Circle.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
    async Task ManageMember(Guid memberId,string name)
    {
        if(circleId is null)return;
        try
        {
            var status=await Api.GetAsync($"api/care-circles/{circleId}/members/{memberId}/removal-status");
            var canRemove=status.GetProperty("canRemove").GetBoolean(); var total=status.GetProperty("totalInteractions").GetInt32();
            var action=canRemove?"Remove":"Deactivate";
            var explanation=canRemove?$"{name} has no care interactions and can be permanently removed.":$"{name} has {total} related interaction(s). Their access can be deactivated, but their history must be retained.";
            if(!await DisplayAlertAsync($"{action} member?",explanation,action,"Cancel"))return;
            var result=await Api.DeleteAsync($"api/care-circles/{circleId}/members/{memberId}");
            await DisplayAlertAsync(result.GetProperty("action").GetString()=="removed"?"Member removed":"Member deactivated",result.GetProperty("message").GetString()??"The Care Circle was updated.","OK");
            await LoadPeople();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not update member",ex.Message,"OK");}
    }
    async Task ConfirmRemove(Guid memberId,string name)
    {
        if(circleId is null)return;
        if(!await DisplayAlertAsync("Remove from Care Circle?",$"Are you sure you want to permanently remove {name} from this Care Circle?", "Remove", "Cancel"))return;
        try
        {
            var result=await Api.DeleteAsync($"api/care-circles/{circleId}/members/{memberId}");
            if(result.GetProperty("action").GetString()!="removed"){await DisplayAlertAsync("Member not removed","New care interactions were found, so the member was deactivated to preserve their history.","OK");}
            await LoadPeople();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not remove member",ex.Message,"OK");}
    }
    async Task ConfirmCancelInvitation(Guid invitationId,string email)
    {
        if(circleId is null)return;
        if(!await DisplayAlertAsync("Cancel invitation?",$"Cancel the pending invitation for {email}? The existing accept/decline link will stop working.","Cancel invitation","Keep"))return;
        try
        {
            await Api.DeleteAsync($"api/care-circles/{circleId}/invitations/{invitationId}");
            await LoadPeople();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not cancel invitation",ex.Message,"OK");}
    }
}

public sealed class DashboardPage : CarePage
{
    public DashboardPage(ApiClient api) : base(api, "")
    {
        var greeting=CareTheme.Text("",25,true); Body.Add(greeting); Body.Add(CareTheme.Text("Here’s what’s happening today", 13, false, CareTheme.Muted));
        void UpdateGreeting(){var hour=DateTime.Now.Hour;var salutation=hour<12?"Good morning":hour<17?"Good afternoon":"Good evening";var firstName=Api.DisplayName?.Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"there";greeting.Text=$"{salutation}, {firstName}";}
        UpdateGreeting(); Appearing+=(_,_)=>UpdateGreeting();
        var days = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 7 };
        string[] values = ["Mon\nOct 7", "Tue\nOct 8", "Wed\nOct 9", "Thu\nOct 10"]; for (var i = 0; i < values.Length; i++) { var l = CareTheme.Text(values[i], 12, i == 1, i == 1 ? Colors.White : CareTheme.Ink); l.HorizontalTextAlignment = TextAlignment.Center; var c = CareTheme.Card(l, i == 1 ? CareTheme.Blue : Colors.White); days.Add(c, i, 0); } Body.Add(days);
        Body.Add(CareTheme.Text("🔴  Due Now (1)", 13, true, CareTheme.Red)); AddLink("💊", "Give Medication", "Metoprolol 25 mg  •  8:00 AM", new TaskDetailPage(Api), Color.FromArgb("#F2DDFF"));
        Body.Add(CareTheme.Text("Upcoming (3)", 14, true)); AddLink("🩺", "Blood Pressure", "9:00 AM", new HealthTrackingPage(Api)); AddLink("📅", "Doctor Appointment", "Dr. Reyes Clinic  •  11:00 AM", new AppointmentDetailPage(Api)); Body.Add(CareTheme.Card(CareTheme.Row("🥣", "Lunch", "12:30 PM", Color.FromArgb("#FFF0D2"))));
    }
}

public sealed class TasksPage : CarePage
{
    public TasksPage(ApiClient api) : base(api, "Tasks", CareTheme.AddAction()) { Body.Add(CareTheme.Text("Today", 14, true)); AddLink("💊", "Give Medication", "Metoprolol 25 mg  •  8:00 AM", new TaskDetailPage(api), Color.FromArgb("#F2DDFF")); Body.Add(CareTheme.Card(CareTheme.Row("🩺", "Check Blood Pressure", "9:00 AM"))); Body.Add(CareTheme.Card(CareTheme.Row("🥣", "Prepare Lunch", "12:30 PM", Color.FromArgb("#FFF0D2")))); }
}

public sealed class TaskDetailPage : CarePage
{
    public TaskDetailPage(ApiClient api) : base(api, "Task Details")
    {
        Body.Add(CareTheme.Card(CareTheme.Row("💊", "Give Medication", "Metoprolol\n25 mg (1 tablet)\nToday, 8:00 AM\nRepeat: Daily", Color.FromArgb("#F1D9FF"), false)));
        var states = new VerticalStackLayout { Spacing = 12, Children = { new RadioButton { Content = "Administered                 8:05 AM", IsChecked = true }, new RadioButton { Content = "Skipped" }, new RadioButton { Content = "Rescheduled" } } }; Body.Add(CareTheme.Card(states));
        Body.Add(CareTheme.Text("Notes (optional)", 13, false, CareTheme.Muted)); Body.Add(new Editor { Placeholder = "Add a note…", HeightRequest = 95, BackgroundColor = Colors.White }); var save = CareTheme.Primary("Save"); save.Clicked += async (_, _) => await DisplayAlertAsync("Saved", "Medication task updated.", "OK"); Body.Add(save);
    }
}

public sealed class MedicationsPage : CarePage
{
    public MedicationsPage(ApiClient api) : base(api, "Medications", CareTheme.AddAction())
    {
        Body.Add(new Button { Text = "Active (4)                         Inactive", BackgroundColor = CareTheme.Blue, TextColor = Colors.White, CornerRadius = 14 });
        Med("Metoprolol", "25 mg\n1 tablet • Twice daily\n8:00 AM • 8:00 PM", "#FFE5E6"); Med("Amlodipine", "5 mg\n1 tablet • Daily\n8:00 AM", "#E1E8FF"); Med("Atorvastatin", "20 mg\n1 tablet • Daily\n8:00 PM", "#DCF5E5"); Med("Vitamin D", "1,000 IU\n1 capsule • Daily\n8:00 AM", "#FFF0D5");
    }
    void Med(string name, string detail, string tint) => AddLink("💊", name, detail, new MedicationDetailPage(Api), Color.FromArgb(tint));
}

public sealed class MedicationDetailPage : CarePage
{
    public MedicationDetailPage(ApiClient api) : base(api, "Medication Details") { Body.Add(CareTheme.Card(CareTheme.Row("💊", "Metoprolol 25 mg", "1 tablet • Twice daily\n8:00 AM • 8:00 PM", Color.FromArgb("#FFE5E6"), false))); Body.Add(CareTheme.Text("Instructions", 14, true)); Body.Add(CareTheme.Card(CareTheme.Text("Take with food. Confirm each dose after it is administered."))); }
}

public sealed class AppointmentsPage : CarePage
{
    public AppointmentsPage(ApiClient api) : base(api, "Appointments", CareTheme.AddAction()) { Body.Add(new Button { Text = "Upcoming                              Past", BackgroundColor = CareTheme.Blue, TextColor = Colors.White, CornerRadius = 14 }); Appt("8", "Dr. Reyes", "General Checkup\n11:00 AM\nMakati Medical Center"); Appt("15", "Lab Test", "Blood Work\n9:00 AM\nMakati Medical Center"); Appt("28", "Cardiology Follow-up", "Dr. Lim\n10:00 AM\nSt. Luke’s Medical Center"); }
    void Appt(string day, string title, string detail) => AddLink(day, title, detail, new AppointmentDetailPage(Api), Color.FromArgb("#FFF1F2"));
}

public sealed class AppointmentDetailPage : CarePage
{
    public AppointmentDetailPage(ApiClient api) : base(api, "Appointment Details") { Body.Add(CareTheme.Card(CareTheme.Row("8", "Dr. Reyes", "General Checkup\nOctober 8 at 11:00 AM\nMakati Medical Center", Color.FromArgb("#FFF1F2"), false))); Body.Add(CareTheme.Text("Questions for the doctor", 14, true)); Body.Add(CareTheme.Card(CareTheme.Text("• Review blood-pressure readings\n• Ask about occasional dizziness\n• Confirm medication schedule"))); }
}

public sealed class JournalPage : CarePage
{
    public JournalPage(ApiClient api) : base(api, "Care Journal", CareTheme.AddAction()) { Body.Add(CareTheme.Text("Today, Oct 8, 2024", 13, true)); Note("👨🏻", "Ana Reyes", "Gave Metoprolol 25 mg.\nBP was 128/76. Feeling well.\n8:10 AM", "Medication     Vitals"); Body.Add(CareTheme.Text("Yesterday, Oct 7, 2024", 13, true)); Note("👩🏻", "Rose Martinez", "Helped with bathing and dressing. Good appetite.\n3:20 PM", "Personal Care     General"); }
    void Note(string icon, string person, string note, string tags) => Body.Add(CareTheme.Card(new VerticalStackLayout { Spacing = 7, Children = { CareTheme.Text($"{icon}   {person}", 13, true), CareTheme.Text(note, 13), CareTheme.Text(tags, 11, false, CareTheme.Blue) } }));
}

public sealed class HealthTrackingPage : CarePage
{
    public HealthTrackingPage(ApiClient api) : base(api, "Health Tracking") { Body.Add(new Button { Text = "Blood Pressure        Weight        Glucose", BackgroundColor = CareTheme.Blue, TextColor = Colors.White, CornerRadius = 14 }); Body.Add(CareTheme.Card(new VerticalStackLayout { Spacing = 11, HeightRequest = 205, Children = { CareTheme.Text("160 ─────────────────", 12, false, CareTheme.Muted), CareTheme.Text("140 ──●──●────●────●", 16, false, CareTheme.Blue), CareTheme.Text("120 ─────────────────", 12, false, CareTheme.Muted), CareTheme.Text("100 ──●──●────●────●", 16, false, Color.FromArgb("#35A867")), CareTheme.Text("       Oct 1                 Oct 7", 11, false, CareTheme.Muted), CareTheme.Text("● Systolic       ● Diastolic", 12) } })); Reading("Oct 8, 2024        128 / 76", "♥ 72"); Reading("Oct 7, 2024        132 / 78", "♥ 68"); Reading("Oct 6, 2024        128 / 74", "♥ 70"); }
    void Reading(string value, string pulse) => Body.Add(CareTheme.Card(CareTheme.Row("♡", value, pulse, Colors.White, false)));
}

public sealed class DocumentsPage : CarePage
{
    public DocumentsPage(ApiClient api) : base(api, "Documents", CareTheme.AddAction()) { Body.Add(CareTheme.Text("All     Medical     Insurance     Legal", 13, true, CareTheme.Blue)); Doc("▣", "Insurance Card", "Updated Oct 1, 2024", "#FFE2E3"); Doc("▤", "Advance Directive", "Updated Sep 15, 2024", "#E1E8FF"); Doc("♙", "Lab Results", "Updated Aug 28, 2024", "#DDF5E4"); Doc("⚕", "Medication List", "Updated Aug 1, 2024", "#EEDCFF"); Doc("▰", "Doctor Notes", "Updated Jul 20, 2024", "#FFF0D3"); }
    void Doc(string icon, string title, string date, string tint) => Body.Add(CareTheme.Card(CareTheme.Row(icon, title, date, Color.FromArgb(tint))));
}

public sealed class ConversationsPage : CarePage
{
    public ConversationsPage(ApiClient api) : base(api, "Messages", CareTheme.AddAction("New")) { Message("👥", "Care Circle", "Lisa: I’ll take Mom to her appt…", "9:15 AM"); Message("👨🏻", "Jun Santos", "Thanks, I’ll meet you there.", "8:42 AM"); Message("👩🏻", "Rose Martinez", "She’s doing well today.", "Yesterday"); Message("📅", "Dr. Reyes Clinic", "Your appointment is confirmed.", "Oct 5"); }
    void Message(string icon, string title, string detail, string time) => AddLink(icon, title, $"{detail}       {time}", new ChatPage(Api));
}

public sealed class ChatPage : CarePage
{
    public ChatPage(ApiClient api) : base(api, "Care Circle") { Body.Add(CareTheme.Card(CareTheme.Text("Ana: Mom’s appointment is confirmed for 11 AM."))); Body.Add(CareTheme.Card(CareTheme.Text("Lisa: I’ll take Mom to her appointment."), Color.FromArgb("#E8F1FF"))); Body.Add(new Editor { Placeholder = "Write a message…", HeightRequest = 90, BackgroundColor = Colors.White }); Body.Add(CareTheme.Primary("Send")); }
}

public sealed class HandoffListPage : CarePage
{
    public HandoffListPage(ApiClient api) : base(api, "Care Handoff") { Body.Add(CareTheme.Text("Oct 8 – Oct 9", 13, false, CareTheme.Muted)); Body.Add(CareTheme.Text("Coming On Duty", 13, true)); Body.Add(CareTheme.Card(CareTheme.Row("👩🏻", "Rose Martinez", "10:00 AM – 6:00 PM", Color.FromArgb("#F3E8D9"), false))); Body.Add(CareTheme.Card(new VerticalStackLayout { Spacing = 6, Children = { CareTheme.Text("📋  Notes for Rose", 14, true), CareTheme.Text("• Give afternoon medications\n• Monitor for dizziness\n• Prepare light dinner\n• Check Sunday’s lab results", 13) } }, Color.FromArgb("#FFF4D9"))); Body.Add(CareTheme.Card(CareTheme.Row("✓", "Handover Complete", "Oct 8, 9:45 AM by Ana Reyes", Color.FromArgb("#DDF5E8"), false))); }
}

public sealed class HandoffEditorPage : CarePage
{
    public HandoffEditorPage(ApiClient api) : base(api, "New Handoff") { Body.Add(new Editor { Placeholder = "What does the next caregiver need to know?", HeightRequest = 180, BackgroundColor = Colors.White }); Body.Add(CareTheme.Primary("Review and Send")); }
}

public sealed class EmergencyPage : CarePage
{
    public EmergencyPage(ApiClient api) : base(api, "Emergency Information", new Button { Text = "Edit", BackgroundColor = Colors.Transparent, TextColor = CareTheme.Blue }) { Body.Add(CareTheme.Card(CareTheme.Text("⚠️   In an Emergency\n       Call 911 or your local emergency number", 13, true, CareTheme.Red), Color.FromArgb("#FFE8E9"))); Contact("👤", "Primary Contact", "Ana Reyes\n+1 917 555 1234", "#E9DFFF"); Contact("👤", "Secondary Contact", "Jun Santos\n+1 917 555 5678", "#DFEBFF"); Contact("⚕", "Primary Doctor", "Dr. Maria Reyes\n+63 2 8123 4567", "#DDF5F2"); Contact("✚", "Hospital", "Makati Medical Center\n+63 2 8888 8999", "#FFE0E1"); }
    void Contact(string icon, string title, string detail, string tint) => Body.Add(CareTheme.Card(CareTheme.Row(icon, title, detail, Color.FromArgb(tint), false)));
}

public sealed class ProfilePage : CarePage
{
    readonly VerticalStackLayout recipients=new(){Spacing=6};
    public ProfilePage(ApiClient api) : base(api, "Settings")
    {
        Body.Add(CareTheme.Text("Care Recipients",16,true)); Body.Add(recipients);
        AddLink("＋", "Add Care Recipient", "Create another Care Circle", new AddRecipientPage(api), Color.FromArgb("#E8F1FF"));
        Setting("🔔", "Notifications"); Setting("♧", "Privacy & Sharing"); AddLink("🎨", "Appearance", "Theme, text, spacing and Today defaults", new AppearancePage(api)); Setting("◎", "Language                         English"); Setting("?", "Help & Support");
        AddLink("!", "Emergency Information", "Contacts, doctor and hospital", new LiveEmergencyPage(api), Color.FromArgb("#FFE0E1"));
        var signOut = new Button { Text = "Sign Out", TextColor = CareTheme.Red, BackgroundColor = Colors.Transparent };
        signOut.Clicked += (_, _) => { Api.Logout(); Application.Current!.Windows[0].Page = new NavigationPage(new WelcomePage(Api)); }; Body.Add(signOut);
        Appearing+=async(_,_)=>await LoadRecipients();
    }
    async Task LoadRecipients()
    {
        recipients.Clear(); recipients.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try
        {
            var result=await Api.GetAsync("api/care-circles"); recipients.Clear();
            foreach(var circle in result.EnumerateArray())
            {
                var name=circle.GetProperty("recipientName").GetString()??"Care recipient";
                var card=CareTheme.Card(CareTheme.Row("👵🏻",name,"Care Circle",Color.FromArgb("#F3E8D9")));
                var id=circle.GetProperty("id").GetGuid(); card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new CareCirclePage(Api,name,id)))); recipients.Add(card);
            }
            if(recipients.Count==0) recipients.Add(CareTheme.Card(CareTheme.Text("No care recipients yet. Select Add Care Recipient below.",13,false,CareTheme.Muted)));
        }
        catch(Exception ex) { recipients.Clear(); recipients.Add(CareTheme.Card(CareTheme.Text($"Could not load care recipients.\n{ex.Message}",13,false,CareTheme.Red))); }
    }
    void Setting(string icon, string title) => Body.Add(CareTheme.Card(CareTheme.Row(icon, title, "")));
}

public sealed class InviteMemberPage : CarePage
{
    readonly Guid? initialCircleId; readonly string initialRecipientName; readonly Picker circlePicker=new(){Title="Choose a care recipient",BackgroundColor=Colors.White}; readonly List<Guid> circleIds=[];
    public InviteMemberPage(ApiClient api,Guid? circleId=null,string recipientName="") : base(api,"Invite Family or Caregiver")
    {
        initialCircleId=circleId; initialRecipientName=recipientName;
        Body.Add(CareTheme.Text("Care Circle",12,false,CareTheme.Muted));
        if(circleId is not null) Body.Add(CareTheme.Card(CareTheme.Text(recipientName,14,true))); else Body.Add(circlePicker);
        Body.Add(CareTheme.Text("Send invitation by",12,false,CareTheme.Muted));var channel=new Picker{ItemsSource=new[]{"Email","Text"},SelectedIndex=0,BackgroundColor=Colors.White};Body.Add(channel);
        Body.Add(CareTheme.Text("Email address",12,false,CareTheme.Muted)); var email=Field("name@example.com"); email.Keyboard=Keyboard.Email; Body.Add(email);
        var phoneLabel=CareTheme.Text("Mobile phone",12,false,CareTheme.Muted);var phone=Field("+1 555 555 5555");phone.Keyboard=Keyboard.Telephone;phoneLabel.IsVisible=phone.IsVisible=false;Body.Add(phoneLabel);Body.Add(phone);channel.SelectedIndexChanged+=(_,_)=>phoneLabel.IsVisible=phone.IsVisible=channel.SelectedIndex==1;
        Body.Add(CareTheme.Text("Role",12,false,CareTheme.Muted)); var role=new Picker{ItemsSource=new[]{"Caregiver","Clinician","Administrator"},SelectedIndex=0,BackgroundColor=Colors.White}; Body.Add(role);
        Body.Add(CareTheme.Text("Message (optional)",12,false,CareTheme.Muted)); var message=new Editor{Placeholder="Add a personal invitation message…",HeightRequest=100,BackgroundColor=Colors.White}; Body.Add(message);
        var send=CareTheme.Primary("Send Invitation");
        send.Clicked+=async(_,_)=>
        {
            Guid? selectedId=initialCircleId;
            if(selectedId is null&&circlePicker.SelectedIndex>=0&&circlePicker.SelectedIndex<circleIds.Count) selectedId=circleIds[circlePicker.SelectedIndex];
            if(selectedId is null){await DisplayAlertAsync("Care recipient required","Choose the Care Circle for this invitation.","OK");return;}
            if(string.IsNullOrWhiteSpace(email.Text)||!email.Text.Contains('@')){await DisplayAlertAsync("Email required","Enter the email address associated with the invitee's current or future CareTogether account.","OK");return;}if(channel.SelectedIndex==1&&string.IsNullOrWhiteSpace(phone.Text)){await DisplayAlertAsync("Mobile number required","Enter the phone number that should receive the invitation text.","OK");return;}
            send.IsEnabled=false; send.Text="Sending…";
            try
            {
                var result=await Api.PostAsync($"api/care-circles/{selectedId}/invitations",new{email=email.Text.Trim(),role=role.SelectedIndex+1,message=message.Text,deliveryChannel=channel.SelectedItem?.ToString(),phone=channel.SelectedIndex==1?phone.Text?.Trim():null});var sent=result.GetProperty("deliveryStatus").GetString()=="sent";var deliveryMessage=result.GetProperty("message").GetString()??"The invitation was saved.";var link=result.GetProperty("inviteUrl").GetString()??"";if(!sent&&!string.IsNullOrWhiteSpace(link)){await Clipboard.Default.SetTextAsync(link);deliveryMessage+=" The accept/decline link was copied to the clipboard.";}await DisplayAlertAsync(sent?"Invitation sent":"Invitation saved",$"{deliveryMessage}\n\nThe invitation remains pending until it is accepted and expires in seven days.","Done");
                await Navigation.PopAsync();
            }
            catch(Exception ex){await DisplayAlertAsync("Could not invite",ex.Message,"OK");}
            finally{send.IsEnabled=true;send.Text="Send Invitation";}
        };
        Body.Add(send); Appearing+=async(_,_)=>{if(initialCircleId is null&&circleIds.Count==0)await LoadCircles();};
    }
    async Task LoadCircles()
    {
        try { var circles=await Api.GetAsync("api/care-circles"); foreach(var item in circles.EnumerateArray()){circleIds.Add(item.GetProperty("id").GetGuid());circlePicker.Items.Add(item.GetProperty("recipientName").GetString()??"Care recipient");} if(circlePicker.Items.Count>0)circlePicker.SelectedIndex=0; }
        catch(Exception ex){await DisplayAlertAsync("Could not load Care Circles",ex.Message,"OK");}
    }
}

public sealed class AiAssistantPage : CarePage
{
    public AiAssistantPage(ApiClient api) : base(api, "Ask CareTogether") { Body.Add(CareTheme.Card(CareTheme.Text("AI suggestions use authorized care records and always require human review."), Color.FromArgb("#E8F1FF"))); Body.Add(new Editor { Placeholder = "Ask about Maria’s care…", HeightRequest = 150, BackgroundColor = Colors.White }); Body.Add(CareTheme.Primary("Ask")); }
}
