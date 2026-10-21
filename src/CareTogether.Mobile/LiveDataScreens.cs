using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareTogether.Mobile;

public sealed class CirclePickerView : VerticalStackLayout
{
    readonly ApiClient api; readonly Picker picker=new(){Title="Select care recipient",BackgroundColor=Colors.White}; readonly List<Guid> ids=[];
    public Guid? SelectedId=>picker.SelectedIndex>=0&&picker.SelectedIndex<ids.Count?ids[picker.SelectedIndex]:null;
    public string? SelectedName=>picker.SelectedIndex>=0?picker.SelectedItem?.ToString():null;
    public bool SelectionEnabled { set=>picker.IsEnabled=value; }
    public event Func<Guid?,Task>? SelectedIdChanged;
    public CirclePickerView(ApiClient api){this.api=api;Spacing=5;Children.Add(CareTheme.Text("Care recipient",12,false,CareTheme.Muted));Children.Add(picker);picker.SelectedIndexChanged+=async(_,_)=>{if(SelectedIdChanged is not null)await SelectedIdChanged(SelectedId);};}
    public async Task LoadAsync(Guid? selectedId=null){if(ids.Count==0){var data=await api.GetAsync("api/care-circles");foreach(var x in data.EnumerateArray()){ids.Add(x.GetProperty("id").GetGuid());picker.Items.Add(x.GetProperty("recipientName").GetString()??"Care recipient");}}if(ids.Count>0){var index=selectedId is null?0:ids.IndexOf(selectedId.Value);picker.SelectedIndex=index<0?0:index;}}
}

public sealed class CareMemberPickerView : VerticalStackLayout
{
    readonly ApiClient api; readonly Picker picker=new(){Title="Unassigned",BackgroundColor=Colors.White}; readonly List<Guid?> ids=[]; Guid? loadedCircleId;
    public Guid? SelectedUserId=>picker.SelectedIndex>=0&&picker.SelectedIndex<ids.Count?ids[picker.SelectedIndex]:null;
    public bool SelectionEnabled { set=>picker.IsEnabled=value; }
    public CareMemberPickerView(ApiClient api){this.api=api;Spacing=5;Children.Add(CareTheme.Text("Assign To",12,false,CareTheme.Muted));Children.Add(picker);}
    public async Task LoadAsync(Guid? circleId,Guid? selectedUserId=null)
    {
        if(circleId is null){picker.Items.Clear();ids.Clear();loadedCircleId=null;return;}
        if(loadedCircleId!=circleId){picker.Items.Clear();ids.Clear();picker.Items.Add("Unassigned");ids.Add(null);var data=await api.GetAsync($"api/care-circles/{circleId}");foreach(var x in data.GetProperty("members").EnumerateArray().Where(x=>x.GetProperty("isActive").GetBoolean())){ids.Add(x.GetProperty("userId").GetGuid());picker.Items.Add(x.GetProperty("displayName").GetString()??x.GetProperty("email").GetString()??"Care circle member");}loadedCircleId=circleId;}
        var index=selectedUserId is null?0:ids.IndexOf(selectedUserId.Value);picker.SelectedIndex=index<0?0:index;
    }
}

public sealed class CareFilterBar : Grid
{
    readonly ApiClient api; readonly Picker recipients=new(){Title="All recipients",BackgroundColor=Colors.White}; readonly Picker? ownership; readonly List<Guid?> ids=[]; bool loading;
    public event Func<Task>? Changed;
    public CareFilterBar(ApiClient api,string? mineLabel=null){this.api=api;ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)];Children.Add(recipients);if(mineLabel is not null){ownership=new Picker{ItemsSource=new[]{"All",mineLabel},SelectedIndex=0,BackgroundColor=Colors.White};Grid.SetColumn(ownership,1);Children.Add(ownership);ownership.SelectedIndexChanged+=async(_,_)=>{if(!loading&&Changed is not null)await Changed();};}recipients.SelectedIndexChanged+=async(_,_)=>{if(!loading&&Changed is not null)await Changed();};}
    public async Task LoadAsync(){if(ids.Count>0)return;loading=true;recipients.Items.Add("All recipients");ids.Add(null);var data=await api.GetAsync("api/care-circles");foreach(var x in data.EnumerateArray()){recipients.Items.Add(x.GetProperty("recipientName").GetString()??"Care recipient");ids.Add(x.GetProperty("id").GetGuid());}recipients.SelectedIndex=ids.Count==2?1:0;loading=false;}
    public string Apply(string endpoint){var query=new List<string>();if(recipients.SelectedIndex>=0&&recipients.SelectedIndex<ids.Count&&ids[recipients.SelectedIndex] is Guid id)query.Add($"careCircleId={id}");if(ownership?.SelectedIndex==1)query.Add("mine=true");return endpoint+(query.Count==0?"":"?"+string.Join("&",query));}
}

public sealed class CareDateRangeBar : Border
{
    readonly Picker picker=new(){Title="Display range",ItemsSource=new[]{"Today","Next 7 days","Next 15 days","Next 30 days"},SelectedIndex=0,BackgroundColor=Colors.White,WidthRequest=150,HeightRequest=42,HorizontalOptions=LayoutOptions.Start};
    readonly Label dates=CareTheme.Text("",12,false,CareTheme.Blue);
    public int Days=>picker.SelectedIndex switch{1=>7,2=>15,3=>30,_=>1};
    public event Func<Task>? Changed;
    public CareDateRangeBar()
    {
        dates.FontAttributes=FontAttributes.Bold;Padding=new Thickness(12,6);BackgroundColor=Color.FromArgb("#E8F1FF");Stroke=Color.FromArgb("#B8D4FF");StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=14};Content=new HorizontalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.Start,VerticalOptions=LayoutOptions.Center,Children={CareTheme.Text("Show",13,true,CareTheme.Ink),picker,dates}};UpdateDates();picker.SelectedIndexChanged+=async(_,_)=>{UpdateDates();if(Changed is not null)await Changed();};
    }
    public bool Includes(DateTimeOffset value){var date=value.ToLocalTime().Date;return date>=DateTime.Today&&date<=DateTime.Today.AddDays(Days-1);}
    void UpdateDates(){var start=DateTime.Today;var end=start.AddDays(Days-1);dates.Text=Days==1?start.ToString("MMM d, yyyy"):$"{start:MMM d} – {end:MMM d, yyyy}";}
}

public abstract class LiveListPage : CarePage
{
    protected readonly VerticalStackLayout Items=new(){Spacing=7}; readonly string endpoint; readonly string empty; readonly CareFilterBar? filters;
    protected LiveListPage(ApiClient api,string title,string endpoint,string empty,Button? add=null,bool filterByRecipient=false,string? mineLabel=null):base(api,title,add){this.endpoint=endpoint;this.empty=empty;if(filterByRecipient){filters=new CareFilterBar(api,mineLabel);filters.Changed+=Refresh;Body.Add(filters);}Body.Add(Items);Appearing+=async(_,_)=>{if(filters is not null)await filters.LoadAsync();await Refresh();};}
    protected async Task Refresh(){Items.Clear();Items.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});try{var data=await Api.GetAsync(filters?.Apply(endpoint)??endpoint);Items.Clear();foreach(var x in data.EnumerateArray().Where(Include))Render(x);if(Items.Count==0)Items.Add(CareTheme.Card(CareTheme.Text(empty,13,false,CareTheme.Muted)));}catch(Exception ex){Items.Clear();Items.Add(CareTheme.Card(CareTheme.Text($"Could not load data.\n{ex.Message}",12,false,CareTheme.Red)));}}
    protected virtual bool Include(JsonElement item)=>true;
    protected abstract void Render(JsonElement item);
    protected void WireAdd(Page page){var h=Header;if(h.Children.LastOrDefault() is Button b)b.Clicked+=async(_,_)=>await Navigation.PushAsync(page);}
    protected static string S(JsonElement x,string name)=>x.TryGetProperty(name,out var v)&&v.ValueKind!=JsonValueKind.Null?v.GetString()??"":"";
}

public sealed class LiveDashboardPage : CarePage
{
    readonly VerticalStackLayout recipientsData=new(){Spacing=8};
    readonly VerticalStackLayout data=new(){Spacing=8};
    readonly Picker rangePicker=new(){Title="Display range",ItemsSource=new[]{"Today","Next 7 days","Next 15 days","Next 30 days"},SelectedIndex=AppearanceSettings.TodayRangeIndex,BackgroundColor=Colors.White};
    readonly Label rangeDates=CareTheme.Text("",12,false,CareTheme.Muted);
    readonly HashSet<Guid> selectedRecipientIds=[];readonly HashSet<Guid> knownRecipientIds=[];bool missedAlertShown;
    public LiveDashboardPage(ApiClient api):base(api,"")
    {
        BackButton.IsEnabled=false;BackButton.Opacity=.28;ToolTipProperties.SetText(BackButton,"Back is unavailable on the Today screen");SemanticProperties.SetDescription(BackButton,"Back is unavailable on the Today screen");HeaderTitle.FontSize=22;rangePicker.WidthRequest=150;rangePicker.HeightRequest=42;rangePicker.HorizontalOptions=LayoutOptions.Start;rangeDates.TextColor=CareTheme.Blue;rangeDates.FontAttributes=FontAttributes.Bold;var summaryRow=new HorizontalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.Start,VerticalOptions=LayoutOptions.Center,Children={CareTheme.Text("Here’s what’s happening",13,true,CareTheme.Ink),rangePicker,rangeDates}};var summaryHighlight=CareTheme.Card(summaryRow,Color.FromArgb("#E8F1FF"));summaryHighlight.Padding=new Thickness(12,6);summaryHighlight.Stroke=Color.FromArgb("#B8D4FF");Body.Add(summaryHighlight);Body.Add(recipientsData);
        Body.Padding=new Thickness(16,2,16,4);BodyScroll.Content=null;PageLayout.Remove(BodyScroll);var careItemsScroll=new ScrollView{Content=data,Padding=new Thickness(16,2,16,30)};var todayContent=new Grid{RowDefinitions=[new(GridLength.Auto),new(GridLength.Star)],IsClippedToBounds=true};todayContent.Add(Body,0,0);todayContent.Add(careItemsScroll,0,1);PageLayout.Add(todayContent,0,1);rangePicker.SelectedIndexChanged+=async(_,_)=>await Load();
        Appearing+=async(_,_)=>{var h=DateTime.Now.Hour;var sal=h<12?"Good morning":h<17?"Good afternoon":"Good evening";var first=Api.DisplayName?.Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"there";HeaderTitle.Text=$"{sal}, {first}";await Load();await ShowNotifications();};
    }
    async Task ShowNotifications(){try{var notifications=await Api.GetAsync("api/notifications");foreach(var item in notifications.EnumerateArray().Reverse()){await DisplayAlertAsync(item.GetProperty("title").GetString()??"CareTogether",item.GetProperty("body").GetString()??"You have a new notification.","OK");await Api.PutAsync($"api/notifications/{item.GetProperty("id").GetGuid()}/read",new{});}}catch{/* Dashboard data remains available if notification polling is temporarily unavailable. */}}
    Grid Section(string title,Func<Task> add)
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Margin=new Thickness(0,6,0,0)};grid.Add(CareTheme.Text(title,14,true),0,0);var button=CareTheme.PlusAction($"Add {title}");button.Clicked+=async(_,_)=>await add();grid.Add(button,1,0);return grid;
    }
    async Task Load()
    {
        recipientsData.Clear();data.Clear();data.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try
        {
            var days=rangePicker.SelectedIndex switch{1=>7,2=>15,3=>30,_=>1};var localStart=new DateTimeOffset(DateTime.Today);var localEnd=localStart.AddDays(days-1);rangeDates.Text=days==1?localStart.ToString("MMM d, yyyy"):$"{localStart:MMM d} – {localEnd:MMM d, yyyy}";var start=Uri.EscapeDataString(localStart.ToString("O"));var j=await Api.GetAsync($"api/dashboard?startDate={start}&days={days}");data.Clear();var circles=j.GetProperty("circles").EnumerateArray().ToList();var currentRecipientIds=circles.Select(x=>x.GetProperty("id").GetGuid()).ToHashSet();var newRecipientIds=currentRecipientIds.Where(x=>!knownRecipientIds.Contains(x)).ToList();selectedRecipientIds.IntersectWith(currentRecipientIds);selectedRecipientIds.UnionWith(newRecipientIds);knownRecipientIds.IntersectWith(currentRecipientIds);knownRecipientIds.UnionWith(currentRecipientIds);
            recipientsData.Add(Section("Care recipients",()=>Navigation.PushAsync(new AddRecipientPage(Api))));
            foreach(var c in circles){var id=c.GetProperty("id").GetGuid();var name=c.GetProperty("recipientName").GetString()??"Care recipient";var isSelected=selectedRecipientIds.Contains(id);var row=CareTheme.Row("👤",name,"Care Circle",Color.FromArgb("#F3E8D9"),false);row.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new CareCirclePage(Api,name,id))));var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star),new(40),new(40),new(40),new(40),new(40),new(circles.Count>1?40:0)]};wrapper.Add(row,0,0);var info=RecipientInfoButton(name);info.Clicked+=async(_,_)=>await Navigation.PushAsync(new RecipientInfoPage(Api,id,name));wrapper.Add(info,1,0);var ai=RecipientAiButton(name);ai.Clicked+=async(_,_)=>await Navigation.PushAsync(new RecipientAiSummaryPage(Api,id,name));wrapper.Add(ai,2,0);var journal=RecipientShortcutButton("✎",CareTheme.Blue,$"Open {name} care journal");journal.Clicked+=async(_,_)=>await Navigation.PushAsync(new LiveJournalPage(Api,id,name));wrapper.Add(journal,3,0);var health=RecipientShortcutButton("♡",Color.FromArgb("#24A148"),$"Open {name} health tracker");health.Clicked+=async(_,_)=>await Navigation.PushAsync(new LiveHealthPage(Api,id,name));wrapper.Add(health,4,0);var handoffDescription=isSelected?$"Open {name} handoffs":$"Select {name} to open handoffs";var handoff=RecipientShortcutButton("⇄",isSelected?Color.FromArgb("#7F56D9"):Color.FromArgb("#AAB2BF"),handoffDescription);handoff.IsEnabled=isSelected;handoff.Opacity=isSelected?1:.4;if(isSelected)handoff.Clicked+=async(_,_)=>await Navigation.PushAsync(new LiveHandoffsPage(Api,id,name));wrapper.Add(handoff,5,0);var selector=new CheckBox{IsChecked=isSelected,IsVisible=circles.Count>1,AutomationId=$"Show care for {name}"};selector.CheckedChanged+=async(_,e)=>{if(e.Value)selectedRecipientIds.Add(id);else selectedRecipientIds.Remove(id);await Load();};wrapper.Add(selector,6,0);recipientsData.Add(CareTheme.Card(wrapper));}
            if(circles.Count==0)recipientsData.Add(CareTheme.Card(CareTheme.Text("No care recipients yet.",13,false,CareTheme.Muted)));

            data.Add(Section("Tasks",()=>Navigation.PushAsync(new TaskEditorPage(Api))));
            var tasks=j.GetProperty("tasks").EnumerateArray().Where(x=>selectedRecipientIds.Contains(x.GetProperty("careCircleId").GetGuid())).ToList();
            foreach(var x in tasks)
            {
                var id=x.GetProperty("id").GetGuid();var status=x.GetProperty("status").GetInt32();var done=status==3;var isMissed=x.TryGetProperty("isMissed",out var missedTask)&&missedTask.GetBoolean();var assignee=x.TryGetProperty("assigneeName",out var assigned)&&assigned.ValueKind!=JsonValueKind.Null?assigned.GetString():"Unassigned";var instructions=x.TryGetProperty("details",out var taskDetails)&&taskDetails.ValueKind!=JsonValueKind.Null?taskDetails.GetString():null;var recipient=x.GetProperty("recipientName").GetString()??"Care recipient";var taskTitle=x.GetProperty("title").GetString()??"Task";var detail=$"{(isMissed?"MISSED • ":done?"Done • ":"Due ")}{x.GetProperty("dueAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}\nAssigned to {assignee}";var row=CareTheme.Row(isMissed?"!":done?"✓":"☑",$"{taskTitle} • {recipient}",detail,isMissed?Color.FromArgb("#FECACA"):null,false);var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star)]};wrapper.Add(row,0,0);var actionColumn=1;
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var message=MessageButton(!string.IsNullOrWhiteSpace(instructions),"task instructions");if(message.IsEnabled)message.Clicked+=async(_,_)=>await DisplayAlertAsync("Task instructions",instructions!,"Close");wrapper.Add(message,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var hasDocuments=x.TryGetProperty("hasDocuments",out var taskDocuments)&&taskDocuments.GetBoolean();var documents=DocumentButton(hasDocuments,"task");var circleId=x.GetProperty("careCircleId").GetGuid();documents.Clicked+=async(_,_)=>await Navigation.PushAsync(new LinkedDocumentsPage(Api,circleId,id,null,taskTitle,!hasDocuments));wrapper.Add(documents,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var canDelete=status==0;var trash=TrashButton(canDelete,"task");if(canDelete)trash.Clicked+=async(_,_)=>await Delete("task",id,$"api/tasks/{id}");wrapper.Add(trash,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var canToggle=status!=4;var toggle=ToggleButton(done,canToggle,"task");if(canToggle)toggle.Clicked+=async(_,_)=>await Toggle($"api/tasks/{id}/toggle-done");wrapper.Add(toggle,actionColumn,0);var card=CareTheme.Card(wrapper,isMissed?Color.FromArgb("#FFF1F2"):null);card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new TaskEditorPage(Api,id))));data.Add(card);
            }
            if(tasks.Count==0)data.Add(CareTheme.Card(CareTheme.Text("No tasks are due in the selected range.",13,false,CareTheme.Muted)));

            data.Add(Section("Appointments",()=>Navigation.PushAsync(new AppointmentEditorPage(Api))));
            var appts=j.GetProperty("appointments").EnumerateArray().Where(x=>selectedRecipientIds.Contains(x.GetProperty("careCircleId").GetGuid())).ToList();
            foreach(var x in appts)
            {
                var id=x.GetProperty("id").GetGuid();var status=x.GetProperty("status").GetInt32();var done=status==1;var isMissed=x.TryGetProperty("isMissed",out var missedAppointment)&&missedAppointment.GetBoolean();var provider=x.GetProperty("provider").GetString()??"Provider not specified";var location=x.TryGetProperty("location",out var place)&&place.ValueKind!=JsonValueKind.Null&&!string.IsNullOrWhiteSpace(place.GetString())?$" • {place.GetString()}":"";var comments=x.TryGetProperty("notes",out var appointmentNotes)&&appointmentNotes.ValueKind!=JsonValueKind.Null?appointmentNotes.GetString():null;var recipient=x.GetProperty("recipientName").GetString()??"Care recipient";var appointmentTitle=x.GetProperty("title").GetString()??"Appointment";var row=CareTheme.Row(isMissed?"!":done?"✓":"📅",$"{appointmentTitle} • {recipient}",$"{(isMissed?"MISSED • ":done?"Done • ":"")}{x.GetProperty("startsAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}\n{provider}{location}",isMissed?Color.FromArgb("#FECACA"):null,false);var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star)]};wrapper.Add(row,0,0);var actionColumn=1;
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var message=MessageButton(!string.IsNullOrWhiteSpace(comments),"appointment comments");if(message.IsEnabled)message.Clicked+=async(_,_)=>await DisplayAlertAsync("Appointment comments",comments!,"Close");wrapper.Add(message,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var hasDocuments=x.TryGetProperty("hasDocuments",out var appointmentDocuments)&&appointmentDocuments.GetBoolean();var documents=DocumentButton(hasDocuments,"appointment");var circleId=x.GetProperty("careCircleId").GetGuid();documents.Clicked+=async(_,_)=>await Navigation.PushAsync(new LinkedDocumentsPage(Api,circleId,null,id,appointmentTitle,!hasDocuments));wrapper.Add(documents,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var canDelete=status==0;var trash=TrashButton(canDelete,"appointment");if(canDelete)trash.Clicked+=async(_,_)=>await Delete("appointment",id,$"api/appointments/{id}");wrapper.Add(trash,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var canToggle=status!=2;var toggle=ToggleButton(done,canToggle,"appointment");if(canToggle)toggle.Clicked+=async(_,_)=>await Toggle($"api/appointments/{id}/toggle-done");wrapper.Add(toggle,actionColumn,0);var card=CareTheme.Card(wrapper,isMissed?Color.FromArgb("#FFF1F2"):null);card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new AppointmentEditorPage(Api,id))));data.Add(card);
            }
            if(appts.Count==0)data.Add(CareTheme.Card(CareTheme.Text("No appointments are due in the selected range.",13,false,CareTheme.Muted)));

            data.Add(Section("Medications",()=>Navigation.PushAsync(new MedicationEditorPage(Api))));
            var medications=j.GetProperty("medications").EnumerateArray().Where(x=>selectedRecipientIds.Contains(x.GetProperty("careCircleId").GetGuid())).ToList();
            foreach(var x in medications)
            {
                var id=x.GetProperty("id").GetGuid();var occurrenceId=x.GetProperty("occurrenceId").GetGuid();var done=x.TryGetProperty("isTaken",out var taken)&&taken.GetBoolean();var isMissed=x.TryGetProperty("isMissed",out var missedMedication)&&missedMedication.GetBoolean();var instructions=x.TryGetProperty("instructions",out var medicationInstructions)&&medicationInstructions.ValueKind!=JsonValueKind.Null?medicationInstructions.GetString():null;var recipient=x.GetProperty("recipientName").GetString()??"Care recipient";var medicationName=x.GetProperty("name").GetString()??"Medication";var scheduledAt=x.GetProperty("scheduledAt").GetDateTimeOffset().ToLocalTime();var scheduledLabel=scheduledAt.ToString("MMM d, yyyy • h:mm tt");var detail=$"{(isMissed?"MISSED • ":done?"Administered • ":"")}{scheduledLabel} • {x.GetProperty("dosage").GetString()}";var row=CareTheme.Row(isMissed?"!":done?"✓":"●",$"{medicationName} • {recipient}",detail,isMissed?Color.FromArgb("#FECACA"):Color.FromArgb("#F3E8FF"),false);var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star)]};wrapper.Add(row,0,0);var actionColumn=1;
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var cards=MedicationCardsButton();cards.Clicked+=async(_,_)=>await Navigation.PushAsync(new MedicationOccurrencesPage(Api,id,medicationName,recipient));wrapper.Add(cards,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var message=MessageButton(!string.IsNullOrWhiteSpace(instructions),"medication instructions");if(message.IsEnabled)message.Clicked+=async(_,_)=>await DisplayAlertAsync("Medication instructions",instructions!,"Close");wrapper.Add(message,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var hasDocuments=x.TryGetProperty("hasDocuments",out var medicationDocuments)&&medicationDocuments.GetBoolean();var documents=DocumentButton(hasDocuments,"medication");var circleId=x.GetProperty("careCircleId").GetGuid();documents.Clicked+=async(_,_)=>await Navigation.PushAsync(new LinkedDocumentsPage(Api,circleId,null,null,medicationName,!hasDocuments,id));wrapper.Add(documents,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var canDelete=!hasDocuments&&!(x.TryGetProperty("hasLogs",out var medicationLogs)&&medicationLogs.GetBoolean());var trash=TrashButton(canDelete,"medication");if(canDelete)trash.Clicked+=async(_,_)=>await Delete("medication",id,$"api/medications/{id}");wrapper.Add(trash,actionColumn++,0);
                wrapper.ColumnDefinitions.Add(new ColumnDefinition(44));var toggle=ToggleButton(done,true,"medication dose");toggle.Clicked+=async(_,_)=>await Toggle($"api/medication-occurrences/{occurrenceId}/toggle-taken");wrapper.Add(toggle,actionColumn,0);var card=CareTheme.Card(wrapper,isMissed?Color.FromArgb("#FFF1F2"):null);card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new MedicationEditorPage(Api,id))));data.Add(card);
            }
            if(medications.Count==0)data.Add(CareTheme.Card(CareTheme.Text("No medications are scheduled in the selected range.",13,false,CareTheme.Muted)));

            if(!missedAlertShown&&j.TryGetProperty("missed",out var missed)){missedAlertShown=true;var taskCount=missed.GetProperty("tasks").GetInt32();var appointmentCount=missed.GetProperty("appointments").GetInt32();var medicationCount=missed.GetProperty("medications").GetInt32();var handoffs=await Api.GetAsync("api/handoffs");var actionableHandoffs=handoffs.EnumerateArray().Where(x=>x.TryGetProperty("canRespond",out var canRespond)&&canRespond.GetBoolean()).ToList();var handoffCount=actionableHandoffs.Count;Func<Task>? reviewHandoff=handoffCount==0?null:()=>Navigation.PushAsync(new HandoffDetailPage(Api,actionableHandoffs[0].GetProperty("id").GetGuid()));if(taskCount+appointmentCount+medicationCount+handoffCount>0)await Navigation.PushModalAsync(new MissedCareAlertPage(taskCount,appointmentCount,medicationCount,handoffCount,reviewHandoff));}
        }
        catch(Exception ex){data.Clear();data.Add(CareTheme.Card(CareTheme.Text(ex.Message,12,false,CareTheme.Red)));}
    }
    static ImageButton TrashButton(bool enabled,string type)
    {
        var text=enabled?$"Delete {type}":$"Cannot delete {type} in its current status";var button=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,Padding=10,HeightRequest=40,WidthRequest=40,Aspect=Aspect.AspectFit,IsEnabled=enabled,Opacity=enabled?1:.28,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton MessageButton(bool hasMessage,string description)
    {
        var text=hasMessage?$"View {description}":$"No {description}";var button=new ImageButton{Source=hasMessage?"message_blue.svg":"message_gray.svg",BackgroundColor=Colors.Transparent,Padding=9,HeightRequest=40,WidthRequest=40,Aspect=Aspect.AspectFit,IsEnabled=hasMessage,Opacity=hasMessage?1:.5,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton RecipientInfoButton(string name)
    {
        var text=$"View {name} information";var button=new ImageButton{Source=new FontImageSource{Glyph="i",Color=CareTheme.Blue,Size=19},BackgroundColor=Color.FromArgb("#E8F1FF"),CornerRadius=18,Padding=8,HeightRequest=36,WidthRequest=36,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton RecipientAiButton(string name)
    {
        var text=$"Generate AI care summary for {name}";var button=new ImageButton{Source="ai_summary.png",BackgroundColor=Color.FromArgb("#E8F1FF"),CornerRadius=18,Padding=7,HeightRequest=36,WidthRequest=36,Aspect=Aspect.AspectFit,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton RecipientShortcutButton(string glyph,Color color,string text)
    {
        var button=new ImageButton{Source=new FontImageSource{Glyph=glyph,Color=color,Size=19},BackgroundColor=Color.FromArgb("#F2F6FC"),CornerRadius=18,Padding=8,HeightRequest=36,WidthRequest=36,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton MedicationCardsButton()
    {
        const string text="Display medication cards";var button=new ImageButton{Source=new FontImageSource{Glyph="▤",Color=CareTheme.Blue,Size=21},BackgroundColor=Colors.Transparent,Padding=9,HeightRequest=40,WidthRequest=40,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton DocumentButton(bool hasDocuments,string type)
    {
        var text=hasDocuments?$"View linked {type} documents":$"No linked documents; add a {type} document";var button=new ImageButton{Source=hasDocuments?"attachment_purple.svg":"attachment_gray.svg",BackgroundColor=Colors.Transparent,Padding=9,HeightRequest=40,WidthRequest=40,Aspect=Aspect.AspectFit,Opacity=hasDocuments?1:.5,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    static ImageButton ToggleButton(bool done,bool enabled,string type)
    {
        var text=!enabled?$"Cannot update {type} in its current status":done?$"Restore {type} to its previous status":$"Mark {type} done";var color=!enabled?Color.FromArgb("#AAB2BF"):done?Color.FromArgb("#F59E0B"):Color.FromArgb("#24A148");var button=new ImageButton{Source=new FontImageSource{Glyph=done?"↶":"✓",Color=color,Size=21},BackgroundColor=Colors.Transparent,Padding=9,HeightRequest=40,WidthRequest=40,IsEnabled=enabled,Opacity=enabled?1:.35,AutomationId=text};ToolTipProperties.SetText(button,text);SemanticProperties.SetDescription(button,text);return button;
    }
    async Task Delete(string type,Guid id,string endpoint)
    {
        if(!await DisplayAlertAsync($"Delete {type}?",$"Are you sure you want to permanently delete this {type}?","Delete","Cancel"))return;
        try{await Api.DeleteAsync(endpoint);await Load();}catch(Exception ex){await DisplayAlertAsync($"Could not delete {type}",ex.Message,"OK");}
    }
    async Task Toggle(string endpoint)
    {
        try{await Api.PutAsync(endpoint,new{});await Load();}catch(Exception ex){await DisplayAlertAsync("Could not update status",ex.Message,"OK");}
    }
}

public sealed class RecipientAiSummaryPage : CarePage
{
    readonly Guid circleId;readonly string recipientName;readonly VerticalStackLayout content=new(){Spacing=10};bool loading;
    public RecipientAiSummaryPage(ApiClient api,Guid circleId,string recipientName):base(api,$"{recipientName} Health & Wellness Summary"){this.circleId=circleId;this.recipientName=recipientName;Body.Add(CareTheme.Card(CareTheme.Text("AI-generated summaries use authorized CareTogether records, require human review, and are not medical advice.",12,false,CareTheme.Muted),Color.FromArgb("#E8F1FF")));Body.Add(content);var refresh=CareTheme.Primary("Regenerate Summary");refresh.Clicked+=async(_,_)=>await Load();Body.Add(refresh);Appearing+=async(_,_)=>{if(!loading&&content.Count==0)await Load();};}
    async Task Load()
    {
        if(loading)return;loading=true;content.Clear();content.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try
        {
            var start=Uri.EscapeDataString(new DateTimeOffset(DateTime.Today).ToString("O"));var result=await Api.GetAsync($"api/care-circles/{circleId}/ai-summary?startDate={start}");content.Clear();content.Add(CareTheme.Text($"{recipientName} • Generated {result.GetProperty("generatedAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}",12,false,CareTheme.Muted));var summary=result.GetProperty("summary").GetString()??"No summary was generated.";content.Add(CareTheme.Card(CareTheme.Text(summary,14)));
            content.Add(CareTheme.Text("Suggested follow-up",16,true));foreach(var action in result.GetProperty("suggestedActions").EnumerateArray())content.Add(CareTheme.Card(CareTheme.Row("✓",action.GetString()??"Review with the Care Circle","Suggested action",Color.FromArgb("#DDF5E8"),false)));
            content.Add(CareTheme.Text(result.GetProperty("disclaimer").GetString()??"AI output requires human review.",11,false,CareTheme.Muted));
        }
        catch(Exception ex){content.Clear();content.Add(CareTheme.Card(CareTheme.Text($"Could not generate the care summary.\n{ex.Message}",12,false,CareTheme.Red)));}
        finally{loading=false;}
    }
}

public sealed class MissedCareAlertPage : ContentPage
{
    public MissedCareAlertPage(int tasks,int appointments,int medications,int handoffs=0,Func<Task>? afterClose=null)
    {
        BackgroundColor=Color.FromRgba(16,24,40,.42);var close=new Button{Text=handoffs>0?"Review Handoff":"Review",BackgroundColor=CareTheme.Red,TextColor=Colors.White,CornerRadius=10,HeightRequest=44,FontAttributes=FontAttributes.Bold};close.Clicked+=async(_,_)=>{await Navigation.PopModalAsync();if(afterClose is not null)await afterClose();};var message=$"There are {tasks} missed task(s), {appointments} missed appointment(s), and {medications} medication(s) with missed doses."+(handoffs>0?$"\n\n{handoffs} pending handoff(s) require your response.":"");var card=CareTheme.Card(new VerticalStackLayout{Spacing=12,Children={CareTheme.Text("⚠  Care items need attention",19,true,CareTheme.Red),CareTheme.Text(message,14,false,CareTheme.Ink),close}},Color.FromArgb("#FFF1F2"));card.WidthRequest=330;card.Padding=20;card.Shadow=new Shadow{Brush=Colors.Black,Opacity=.22f,Radius=18,Offset=new Point(0,7)};Content=new Grid{Padding=24,Children={card}};card.HorizontalOptions=LayoutOptions.Center;card.VerticalOptions=LayoutOptions.Center;SemanticProperties.SetDescription(card,"Care items requiring attention");
    }
}

public sealed class CareChatPage : CarePage
{
    readonly CirclePickerView recipients;readonly VerticalStackLayout transcript=new(){Spacing=8};readonly ChatPromptEditor question=new(){Placeholder="Ask a question or create a care item…",MinimumHeightRequest=48,MaximumHeightRequest=120,AutoSize=EditorAutoSizeOption.TextChanges,BackgroundColor=Colors.White};readonly Button send=CareTheme.Small("Send");readonly Button dismissKeyboard=new(){Text="⌄",FontSize=24,BackgroundColor=Color.FromArgb("#EEF2F6"),TextColor=CareTheme.Muted,CornerRadius=12,WidthRequest=42,HeightRequest=44,Padding=0};readonly Button microphone=new(){Text="🎤",FontSize=20,BackgroundColor=Color.FromArgb("#E8F1FF"),TextColor=CareTheme.Blue,CornerRadius=12,WidthRequest=48,HeightRequest=44,Padding=0};readonly ScrollView conversationScroll;readonly IVoicePromptService voice=new VoicePromptService();readonly List<VisualElement> responseBubbles=[];
    readonly string pageContext;GuidedDraft? guidedDraft;
    public CareChatPage(ApiClient api,string pageContext="general"):base(api,"Ask CareTogether")
    {
        this.pageContext=NormalizeContext(pageContext);question.Placeholder=PromptPlaceholder(this.pageContext);
        PageLayout.Remove(Header);BodyScroll.Content=null;Header.Margin=new Thickness(0,2,0,8);Body.Insert(0,Header);
        BackButton.IsEnabled=false;BackButton.Opacity=.28;recipients=new(api);Body.Add(CareTheme.Card(CareTheme.Text($"{ContextHeading(this.pageContext)} Answers and actions use authorized CareTogether records. Review every proposed action before saving; this assistant does not provide medical advice.",12,false,CareTheme.Muted),Color.FromArgb("#E8F1FF")));Body.Add(recipients);Body.Add(CareTheme.Text("Try asking",14,true));var prompts=ContextPrompts(this.pageContext);var promptRow=new FlexLayout{Direction=Microsoft.Maui.Layouts.FlexDirection.Row,Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap,JustifyContent=Microsoft.Maui.Layouts.FlexJustify.Start};foreach(var prompt in prompts){var button=new Button{Text=prompt,FontSize=11,TextColor=CareTheme.Blue,BackgroundColor=Color.FromArgb("#E8F1FF"),CornerRadius=14,Padding=new Thickness(10,5),Margin=new Thickness(0,0,5,5)};button.Clicked+=async(_,_)=>{question.Unfocus();question.Text=prompt;await Ask();};promptRow.Children.Add(button);}Body.Add(promptRow);Body.Add(CareTheme.Text("Conversation",14,true));Body.Padding=new Thickness(16,10,16,4);var composer=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=6,Padding=new Thickness(16,8,16,10),BackgroundColor=CareTheme.Surface};composer.Add(question,0,0);composer.Add(dismissKeyboard,1,0);composer.Add(microphone,2,0);composer.Add(send,3,0);conversationScroll=new ScrollView{Content=transcript,Padding=new Thickness(16,2,16,8)};conversationScroll.GestureRecognizers.Add(CareTheme.Tap(()=>{question.Unfocus();return Task.CompletedTask;}));var chatLayout=new Grid{RowDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],SafeAreaEdges=new SafeAreaEdges(SafeAreaRegions.SoftInput)};chatLayout.Add(Body,0,0);chatLayout.Add(conversationScroll,0,1);chatLayout.Add(composer,0,2);Content=chatLayout;ToolTipProperties.SetText(dismissKeyboard,"Hide keyboard");SemanticProperties.SetDescription(dismissKeyboard,"Hide keyboard");ToolTipProperties.SetText(microphone,"Enter a prompt by voice");SemanticProperties.SetDescription(microphone,"Enter a prompt by voice");conversationScroll.SizeChanged+=(_,_)=>UpdateResponseWidths();transcript.ChildAdded+=(_,_)=>ScrollToLatest();dismissKeyboard.Clicked+=(_,_)=>question.Unfocus();microphone.Clicked+=async(_,_)=>{question.Unfocus();await Dictate();};question.SendRequested+=async(_,_)=>await Ask();send.Clicked+=async(_,_)=>await Ask();Appearing+=async(_,_)=>{await recipients.LoadAsync();if(transcript.Count==0)AddBubble("assistant",ContextWelcome(this.pageContext));};
    }
    async Task Dictate()
    {
        try{microphone.IsEnabled=false;send.IsEnabled=false;microphone.Text="●";microphone.TextColor=CareTheme.Red;ToolTipProperties.SetText(microphone,"Listening");var text=await voice.ListenAsync();if(!string.IsNullOrWhiteSpace(text))question.Text=text;}
        catch(Exception ex){await DisplayAlertAsync("Voice prompt",ex.Message,"OK");}
        finally{microphone.Text="🎤";microphone.TextColor=CareTheme.Blue;microphone.IsEnabled=true;send.IsEnabled=true;ToolTipProperties.SetText(microphone,"Enter a prompt by voice");}
    }
    async Task Ask()
    {
        if(!send.IsEnabled)return;var text=(question.Text??"").Trim();if(string.IsNullOrWhiteSpace(text))return;var lower=text.ToLowerInvariant();if(lower is "help" or "?"){AddBubble("you",text);ClearPrompt();AddBubble("assistant",HelpText);return;}if(lower=="cancel"){AddBubble("you",text);ClearPrompt();if(guidedDraft is not null){guidedDraft=null;AddBubble("assistant","The request was cancelled. Nothing was created.");}else AddBubble("assistant","There is no active request to cancel.");return;}var asksForMyTasks=lower.Contains("my task")||lower.Contains("tasks assigned to me")||lower.Contains("assigned tasks");if(recipients.SelectedId is null&&!asksForMyTasks){await DisplayAlertAsync("Recipient required","Select a care recipient before asking this question.","OK");return;}AddBubble("you",text);ClearPrompt();send.IsEnabled=false;send.Text="Thinking…";
        try
        {
            if(guidedDraft is not null){await ContinueGuidedDraft(text);return;}
            if(IsCreateIntent(lower,"task")||IsContextCreateIntent(lower,"tasks")){StartGuidedDraft("task");return;}
            if(IsCreateIntent(lower,"medication")||IsCreateIntent(lower,"medicine")||IsContextCreateIntent(lower,"medications")){StartGuidedDraft("medication");return;}
            await SubmitQuestion(text);
        }
        catch(Exception ex){AddBubble("assistant",$"I couldn't answer that question. {ex.Message}");}finally{send.IsEnabled=true;send.Text="Send";}
    }
    async Task SubmitQuestion(string text)
    {
        var result=await Api.PostAsync("api/chat/ask",new{careCircleId=recipients.SelectedId,question=text,clientNow=DateTimeOffset.Now});AddBubble("assistant",result.GetProperty("answer").GetString()??"No answer was returned.");if(result.TryGetProperty("sources",out var sources)&&sources.GetArrayLength()>0)transcript.Add(CareTheme.Text($"Sources: {string.Join(", ",sources.EnumerateArray().Select(x=>x.GetString()))}",10,false,CareTheme.Muted));if(result.TryGetProperty("pendingAction",out var action)&&action.ValueKind==JsonValueKind.Object)await ConfirmAction(action);
    }
    static bool IsCreateIntent(string text,string item)=>(text.Contains("add")||text.Contains("create")||text.Contains("new "))&&text.Contains(item);
    bool IsContextCreateIntent(string text,string context)=>pageContext==context&&(text is "add" or "create" or "new"||text.Contains("add one")||text.Contains("create one")||text.Contains("new one"));
    void StartGuidedDraft(string type)
    {
        guidedDraft=new GuidedDraft(type);if(type=="task"){guidedDraft.Fields.AddRange([new("title","What should the task be called?"),new("due","When is it due? Include a date and time, such as “tomorrow at 9 AM.”"),new("details","Enter instructions, or type “skip” if there are none."),new("assign","Should I assign this task to you? Answer yes or no.")]);AddBubble("assistant","Let's create a task. I’ll collect these fields: recipient, task title, due date and time, instructions, and assignee.");}else{guidedDraft.Fields.AddRange([new("name","What is the medication name?"),new("dosage","What is the dosage, such as “25 mg” or “1 tablet”?"),new("scheduleType","Is this medication taken once, daily, weekly, or monthly?")]);AddBubble("assistant","Let's add a medication. I’ll collect its recipient, name, dosage, schedule, dates, times, and instructions.");}AddBubble("assistant",guidedDraft.Fields[0].Prompt);
    }
    async Task ContinueGuidedDraft(string answer)
    {
        var draft=guidedDraft!;if(answer.Equals("cancel",StringComparison.OrdinalIgnoreCase)){guidedDraft=null;AddBubble("assistant","The draft was cancelled. Nothing was created.");return;}var field=draft.Fields[draft.Index];var value=answer.Trim();if(field.Key=="scheduleType")
        {
            value=value.ToLowerInvariant();if(value is not ("once" or "daily" or "weekly" or "monthly")){AddBubble("assistant","Please answer once, daily, weekly, or monthly.");return;}draft.Answers[field.Key]=value;if(value=="once")draft.Fields.AddRange([new("when","What date and time should it be taken? For example, “tomorrow at 8 AM.”"),new("details","Enter medication instructions, or type “skip”.")]);else{if(value=="weekly")draft.Fields.Add(new("weeklyDay","Which day of the week?"));if(value=="monthly")draft.Fields.Add(new("monthlyDay","Which day of the month, from 1 to 31?"));draft.Fields.AddRange([new("times","What time or times should it be taken? For example, “8 AM and 8 PM.”"),new("start","What is the schedule start date?"),new("end","What is the schedule end date?"),new("details","Enter medication instructions, or type “skip”.")]);}draft.Index++;AddBubble("assistant",draft.Fields[draft.Index].Prompt);return;
        }
        if(field.Key=="assign"&&value.ToLowerInvariant() is not ("yes" or "y" or "no" or "n")){AddBubble("assistant","Please answer yes or no.");return;}
        if(field.Key=="monthlyDay"&&(!int.TryParse(value,out var day)||day is <1 or >31)){AddBubble("assistant","Enter a day of the month from 1 through 31.");return;}
        if(field.Key is "due" or "when" or "start" or "end")value=ResolveRelativeDate(value);
        draft.Answers[field.Key]=value.Equals("skip",StringComparison.OrdinalIgnoreCase)?"":value;draft.Index++;if(draft.Index<draft.Fields.Count){AddBubble("assistant",draft.Fields[draft.Index].Prompt);return;}guidedDraft=null;await SubmitQuestion(BuildGuidedRequest(draft));
    }
    static string ResolveRelativeDate(string input)
    {
        var today=DateTime.Today;var value=input;value=Regex.Replace(value,@"\bnext\s+month\b",today.AddMonths(1).ToString("MMM d, yyyy",CultureInfo.InvariantCulture),RegexOptions.IgnoreCase);
        foreach(var day in Enum.GetValues<DayOfWeek>())
        {
            var delta=((int)day-(int)today.DayOfWeek+7)%7;if(delta==0)delta=7;value=Regex.Replace(value,$@"\bnext\s+{day}\b",today.AddDays(delta).ToString("MMM d, yyyy",CultureInfo.InvariantCulture),RegexOptions.IgnoreCase);
        }
        value=Regex.Replace(value,@"\btomorrow\b",today.AddDays(1).ToString("MMM d, yyyy",CultureInfo.InvariantCulture),RegexOptions.IgnoreCase);value=Regex.Replace(value,@"\btoday\b",today.ToString("MMM d, yyyy",CultureInfo.InvariantCulture),RegexOptions.IgnoreCase);return value;
    }
    static string BuildGuidedRequest(GuidedDraft draft)
    {
        string A(string key)=>draft.Answers.TryGetValue(key,out var value)?value:"";var details=string.IsNullOrWhiteSpace(A("details"))?"":$" instructions: {A("details")}";if(draft.Type=="task")return $"Add task {A("title")} {A("due")}{(A("assign").StartsWith("y",StringComparison.OrdinalIgnoreCase)?" assign to me":"")}{details}";var type=A("scheduleType");if(type=="once")return $"Add medication {A("name")} dosage: {A("dosage")} once {A("when")}{details}";var day=type=="weekly"?$" {A("weeklyDay")}":type=="monthly"?$" day {A("monthlyDay")}":"";return $"Add medication {A("name")} dosage: {A("dosage")} {type}{day} at {A("times")} start: {A("start")} end: {A("end")}{details}";
    }
    static string NormalizeContext(string context)=>context.Trim().ToLowerInvariant() switch{"task"=>"tasks","medication"=>"medications","appointment"=>"appointments","care journal"=>"journal","health tracking"=>"health","care handoff"=>"handoffs","groceries"=>"shopping",var value=>value};
    static string PromptPlaceholder(string context)=>context switch{"tasks"=>"Ask about tasks or create one…","medications"=>"Ask about medications or add one…","appointments"=>"Ask about appointments or schedule one…","journal"=>"Ask about recent care notes…","health"=>"Ask about vital readings…","handoffs"=>"Ask about care coverage and handoffs…","shopping"=>"Ask about shopping and needed items…","messages"=>"Ask about Care Circle communication…","documents"=>"Ask about health records and documents…",_=>"Ask a question or create a care item…"};
    static string ContextHeading(string context)=>context switch{"tasks"=>"Task assistant.","medications"=>"Medication assistant.","appointments"=>"Appointment assistant.","journal"=>"Care-journal assistant.","health"=>"Health-tracking assistant.","handoffs"=>"Handoff assistant.","shopping"=>"Shopping assistant.","messages"=>"Communication assistant.","documents"=>"Document assistant.","today"=>"Daily-care assistant.",_=>""};
    static string ContextWelcome(string context)=>context switch{"tasks"=>"I can show your assigned tasks, find overdue work, or guide you through creating a task one field at a time.","medications"=>"I can review today's medication cards or guide you through adding a medication and schedule.","appointments"=>"I can find the next appointment, summarize today's schedule, or help prepare a new appointment.","journal"=>"I can summarize recent care-journal updates or help record a new observation.","health"=>"I can find the latest vital reading or help record blood pressure, weight, glucose, or temperature.","handoffs"=>"I can help you review upcoming care, missed items, assignments, and information needed for a handoff.","shopping"=>"I can help review care needs and assignments related to the selected recipient.","messages"=>"I can help start a Care Circle conversation or summarize recipient-related care information.","documents"=>"I can help identify relevant care information and records for the selected recipient.","today"=>"Ask what's happening today, review missed care, or create a new care item.",_=>"Ask about your assignments across every care recipient, or choose a recipient for questions and new care items."};
    static string[] ContextPrompts(string context)=>context switch
    {
        "tasks"=>["My tasks today","My tasks for the next 7 days","Add a task","What care items are overdue?"],
        "medications"=>["What medications are due today?","Add a medication","What medication was missed?","What's happening today?"],
        "appointments"=>["When is the next appointment?","What's happening today?","Add appointment","What care items are overdue?"],
        "journal"=>["Show recent journal updates","Add journal entry","What's happening today?","What care items are overdue?"],
        "health"=>["What was the last BP reading?","Record blood pressure","Show recent journal updates","What's happening today?"],
        "handoffs"=>["What's happening today?","What care items are overdue?","My tasks today","When is the next appointment?"],
        "shopping"=>["My tasks today","What care items are overdue?","What's happening today?","Start a care conversation"],
        "messages"=>["Start a care conversation","Show recent journal updates","What's happening today?","What care items are overdue?"],
        "documents"=>["Show recent journal updates","What was the last BP reading?","When is the next appointment?","What's happening today?"],
        "emergency"=>["What's happening today?","What care items are overdue?","When is the next appointment?","Start a care conversation"],
        _=>["My tasks today","My tasks for the next 7 days","What's happening today?","Add a medication","Add appointment"]
    };
    async Task ConfirmAction(JsonElement action)
    {
        var type=action.GetProperty("type").GetString()??"item";var confirmation=action.GetProperty("confirmation").GetString()??"Review the details before continuing.";var labels=type switch{"task"=>("Create this task?","Create Task"),"appointment"=>("Schedule this appointment?","Schedule"),"medication"=>("Add this medication?","Add Medication"),"journal"=>("Add this journal entry?","Add Entry"),"health"=>("Record this health reading?","Record Reading"),"conversation"=>("Start this conversation?","Start Conversation"),_=>("Create this item?","Create")};if(!await DisplayAlertAsync(labels.Item1,confirmation,labels.Item2,"Cancel")){AddBubble("assistant","Nothing was created.");return;}
        try
        {
            var circleId=action.GetProperty("careCircleId").GetGuid();var title=action.GetProperty("title").GetString()??"Care item";var scheduledAt=action.GetProperty("scheduledAt").GetDateTimeOffset();var details=NullableText(action,"details");
            if(type=="task"){var assignToMe=action.GetProperty("assignToMe").GetBoolean();await Api.PostAsync("api/tasks",new{careCircleId=circleId,title,details,dueAt=scheduledAt,assignedToUserId=(Guid?)null,status=assignToMe?1:0,assignToMe});AddBubble("assistant",$"Created “{title}” for {scheduledAt.ToLocalTime():MMM d 'at' h:mm tt}.");}
            else if(type=="appointment"){var provider=NullableText(action,"provider")??"Provider";var location=NullableText(action,"location");await Api.PostAsync("api/appointments",new{careCircleId=circleId,title,provider,startsAt=scheduledAt,location,notes=details});AddBubble("assistant",$"Scheduled “{title}” for {scheduledAt.ToLocalTime():MMM d 'at' h:mm tt}.");}
            else if(type=="medication")
            {
                var dosage=NullableText(action,"dosage")??"";var scheduleType=NullableInt(action,"scheduleType")??1;var occurrences=NullableInt(action,"occurrences");var timesOfDay=NullableText(action,"timesOfDay");var startDate=NullableDate(action,"startDate");var endDate=NullableDate(action,"endDate");var weeklyDay=NullableInt(action,"weeklyDay");var monthlyDay=NullableInt(action,"monthlyDay");var schedule=scheduleType==0?$"Once • {scheduledAt.ToLocalTime():g}":scheduleType switch{1=>$"Daily • {occurrences}x",2=>$"Weekly • {occurrences}x",_=>$"Monthly • {occurrences}x"};await Api.PostAsync("api/medications",new{careCircleId=circleId,name=title,dosage,schedule,instructions=details,status=0,scheduleType,onceAt=scheduleType==0?scheduledAt:(DateTimeOffset?)null,occurrences,timesOfDay,startDate,endDate,weeklyDay,monthlyDay,scheduleUtcOffsetMinutes=(int)DateTimeOffset.Now.Offset.TotalMinutes});AddBubble("assistant",$"Added {title} {dosage} and created its medication cards.");
            }
            else if(type=="journal"){await Api.PostAsync("api/journal",new{careCircleId=circleId,body=details,category=NullableText(action,"category")});AddBubble("assistant","Added the care-journal entry.");}
            else if(type=="health"){await Api.PostAsync("api/health-readings",new{careCircleId=circleId,type=NullableText(action,"readingType")??title,value=NullableDecimal(action,"value"),secondaryValue=NullableDecimal(action,"secondaryValue"),pulse=NullableInt(action,"pulse"),recordedAt=scheduledAt});AddBubble("assistant",$"Recorded the {title.ToLowerInvariant()} reading.");}
            else if(type=="conversation"){var created=await Api.PostAsync("api/conversations",new{careCircleId=circleId,title});var id=created.GetProperty("id").GetGuid();AddBubble("assistant",$"Started the conversation “{title}”.");await Navigation.PushAsync(new LiveChatPage(Api,id,title));}
        }
        catch(Exception ex){AddBubble("assistant",$"I couldn't create that item. {ex.Message}");}
    }
    static string? NullableText(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString():null;
    static int? NullableInt(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetInt32():null;
    static decimal? NullableDecimal(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetDecimal():null;
    static DateOnly? NullableDate(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null&&DateOnly.TryParse(value.GetString(),out var date)?date:null;
    void ClearPrompt(){question.Text="";question.Unfocus();}
    protected override void OnDisappearing(){question.Unfocus();base.OnDisappearing();}
    void ScrollToLatest()=>Dispatcher.Dispatch(async()=>{await Task.Yield();await Task.Delay(60);await conversationScroll.ScrollToAsync(transcript,ScrollToPosition.End,true);});
    void UpdateResponseWidths(){var width=conversationScroll.Width;if(width<=0)return;foreach(var bubble in responseBubbles)bubble.WidthRequest=Math.Floor(width*.9);}
    void AddBubble(string role,string text){var mine=role=="you";var label=CareTheme.Text(text,13,false,mine?Colors.White:CareTheme.Ink);var bubble=CareTheme.Card(label,mine?CareTheme.Blue:Colors.White);bubble.HorizontalOptions=mine?LayoutOptions.End:LayoutOptions.Start;if(mine)bubble.MaximumWidthRequest=Math.Max(330,conversationScroll.Width*.9);else{responseBubbles.Add(bubble);if(conversationScroll.Width>0)bubble.WidthRequest=Math.Floor(conversationScroll.Width*.9);}SemanticProperties.SetDescription(bubble,mine?"Your question":"CareTogether answer");transcript.Add(bubble);}
    const string HelpText="""
Here are the available requests and examples.

ASK ABOUT YOUR WORK
• My assigned tasks today
  Example: “What are my tasks today?”
• My assigned tasks for a future period
  Example: “What are my tasks for the next 15 days?”
  You can use days, weeks, or months.

ASK ABOUT THE SELECTED RECIPIENT
• Today’s tasks and appointments
  Example: “What’s happening today?”
• Today’s medication cards
  Example: “What medications are due today?”
• Latest blood-pressure reading
  Example: “What was the last BP reading?”
• Missed or overdue care
  Example: “What care items were missed?”
• Next appointment
  Example: “When is the next appointment?”
• Recent care-journal entries
  Example: “Show recent journal updates.”

CREATE CARE ITEMS
• Task — the chatbot prompts for missing details
  Example: “Add a task.”
• Appointment
  Example: “Schedule an appointment with Dr. Reyes tomorrow at 2 PM location: Main Clinic notes: Bring medication list.”
• Medication — the chatbot prompts for its schedule
  Example: “Add a medication.”
• Care-journal entry
  Example: “Add journal entry: Ate well and walked for 20 minutes. category: General.”
• Health reading: blood pressure, weight, glucose, or temperature
  Example: “Record blood pressure 128/76 pulse: 72.”
• Care-circle conversation
  Example: “Start a conversation about medication changes.”

DATES AND COMMANDS
• Dates can include today, tomorrow, next Friday, a calendar date, or next month when prompted.
• Type “cancel” to discard an active guided request.
• Type “help” or “?” to show this guide again.

Select a care recipient before asking recipient-specific questions or creating an item. Every proposed action is shown for confirmation before it is saved.
""";
    sealed class GuidedDraft(string type)
    {
        public string Type { get; }=type;public int Index { get; set; }public List<GuidedField> Fields { get; }=[];public Dictionary<string,string> Answers { get; }=new(StringComparer.OrdinalIgnoreCase);
    }
    sealed record GuidedField(string Key,string Prompt);
}

public sealed class RecipientInfoPage : CarePage
{
    readonly Guid circleId;readonly string recipientName;readonly VerticalStackLayout content=new(){Spacing=8};
    public RecipientInfoPage(ApiClient api,Guid circleId,string recipientName):base(api,"Recipient Information"){this.circleId=circleId;this.recipientName=recipientName;Body.Add(content);Appearing+=async(_,_)=>await Load();}
    async Task Load()
    {
        content.Clear();content.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try
        {
            var circleTask=Api.GetAsync($"api/care-circles/{circleId}");var contactsTask=Api.GetAsync($"api/emergency-contacts?careCircleId={circleId}");await Task.WhenAll(circleTask,contactsTask);var circle=await circleTask;var contacts=await contactsTask;content.Clear();
            content.Add(CareTheme.Card(CareTheme.Row("👤",circle.GetProperty("recipientName").GetString()??recipientName,"Care recipient",Color.FromArgb("#F3E8D9"),false)));
            content.Add(CareTheme.Text("Recipient information",15,true));var notes=Value(circle,"notes");if(string.IsNullOrWhiteSpace(notes))content.Add(CareTheme.Card(CareTheme.Text("No additional recipient information has been recorded.",13,false,CareTheme.Muted)));else foreach(var detail in notes.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))content.Add(CareTheme.Card(CareTheme.Text(detail,13)));
            content.Add(CareTheme.Text("Care circle",15,true));var memberCount=0;foreach(var member in circle.GetProperty("members").EnumerateArray()){memberCount++;var active=member.GetProperty("isActive").GetBoolean();var role=Value(member,"role");var email=Value(member,"email");var detail=$"{role}{(string.IsNullOrWhiteSpace(email)?"":$" • {email}")}{(active?"":" • Deactivated")}";content.Add(CareTheme.Card(CareTheme.Row(active?"👤":"○",Value(member,"displayName"),detail,active?Color.FromArgb("#E8F1FF"):Color.FromArgb("#EEF0F3"),false)));}foreach(var invitation in circle.GetProperty("invitations").EnumerateArray()){memberCount++;content.Add(CareTheme.Card(CareTheme.Row("✉",Value(invitation,"email"),$"{Value(invitation,"role")} • Invitation pending",Color.FromArgb("#FFF4D9"),false)));}if(memberCount==0)content.Add(CareTheme.Card(CareTheme.Text("No care-circle members have been added.",13,false,CareTheme.Muted)));
            content.Add(CareTheme.Text("Emergency contacts",15,true));var contactCount=0;foreach(var contact in contacts.EnumerateArray()){contactCount++;var primary=contact.GetProperty("isPrimary").GetBoolean();content.Add(CareTheme.Card(CareTheme.Row(primary?"!":"☎",Value(contact,"name"),$"{Value(contact,"relationship")} • {Value(contact,"phone")}{(primary?" • Primary":"")}",primary?Color.FromArgb("#FFE0E1"):Color.FromArgb("#E8F1FF"),false)));}if(contactCount==0)content.Add(CareTheme.Card(CareTheme.Text("No emergency contacts have been recorded.",13,false,CareTheme.Muted)));
        }
        catch(Exception ex){content.Clear();content.Add(CareTheme.Card(CareTheme.Text($"Could not load recipient information.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
    static string Value(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??"":"";
}

public sealed class LiveTasksPage : CarePage
{
    readonly Picker recipientFilter=new(){Title="Recipient",BackgroundColor=Colors.White}; readonly Picker ownershipFilter=new(){ItemsSource=new[]{"All tasks","My tasks"},SelectedIndex=0,BackgroundColor=Colors.White};
    readonly CareDateRangeBar dateRange=new();readonly List<Guid?> recipientIds=[]; readonly VerticalStackLayout items=new(){Spacing=7}; bool loadingRecipients;
    public LiveTasksPage(ApiClient api):base(api,"Tasks",CareTheme.AddAction())
    {
        var filters=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=8};filters.Add(recipientFilter,0,0);filters.Add(ownershipFilter,1,0);Body.Add(filters);Body.Add(dateRange);Body.Add(items);
        var header=Header;if(header.Children.LastOrDefault() is Button add)add.Clicked+=async(_,_)=>await Navigation.PushAsync(new TaskEditorPage(api));
        recipientFilter.SelectedIndexChanged+=async(_,_)=>{if(!loadingRecipients)await LoadTasks();};ownershipFilter.SelectedIndexChanged+=async(_,_)=>await LoadTasks();dateRange.Changed+=LoadTasks;Appearing+=async(_,_)=>{await LoadRecipients();await LoadTasks();};
    }
    async Task LoadRecipients()
    {
        if(recipientIds.Count>0)return;loadingRecipients=true;recipientFilter.Items.Add("All recipients");recipientIds.Add(null);
        try{var circles=await Api.GetAsync("api/care-circles");foreach(var c in circles.EnumerateArray()){recipientFilter.Items.Add(c.GetProperty("recipientName").GetString()??"Care recipient");recipientIds.Add(c.GetProperty("id").GetGuid());}recipientFilter.SelectedIndex=recipientIds.Count==2?1:0;}finally{loadingRecipients=false;}
    }
    async Task LoadTasks()
    {
        items.Clear();items.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try{var query=new List<string>();if(recipientFilter.SelectedIndex>=0&&recipientFilter.SelectedIndex<recipientIds.Count&&recipientIds[recipientFilter.SelectedIndex] is Guid id)query.Add($"careCircleId={id}");if(ownershipFilter.SelectedIndex==1)query.Add("mine=true");var path="api/tasks"+(query.Count>0?"?"+string.Join("&",query):"");var data=await Api.GetAsync(path);items.Clear();foreach(var x in data.EnumerateArray().Where(x=>dateRange.Includes(x.GetProperty("dueAt").GetDateTimeOffset()))){var status=x.GetProperty("status").GetString()??"Open";var assignee=x.TryGetProperty("assigneeName",out var a)&&a.ValueKind!=JsonValueKind.Null?a.GetString():"Unassigned";var detail=$"{x.GetProperty("recipientName").GetString()} • {status.Replace("InProgress","In Progress")} • {assignee}\nDue {x.GetProperty("dueAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}";var icon=status switch{"Done"=>"✓","Cancelled"=>"×","InProgress"=>"▶","Assigned"=>"●",_=>"○"};var card=CareTheme.Card(CareTheme.Row(icon,x.GetProperty("title").GetString()??"Task",detail,null,false));var taskId=x.GetProperty("id").GetGuid();var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await Navigation.PushAsync(new TaskEditorPage(Api,taskId));card.GestureRecognizers.Add(tap);items.Add(card);}if(items.Count==0)items.Add(CareTheme.Card(CareTheme.Text("No tasks match these filters and date range.",13,false,CareTheme.Muted)));}catch(Exception ex){items.Clear();items.Add(CareTheme.Card(CareTheme.Text($"Could not load tasks.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
}
public class TaskEditorPage : CarePage
{
    readonly Guid? taskId; readonly CirclePickerView circles; readonly CareMemberPickerView assignees; readonly Entry title; readonly Editor details; readonly ImageButton detailsToggle; readonly DatePicker due; readonly Picker status; readonly CheckBox assignToMe; readonly Button save; string? previousDetails; bool loaded;
    static readonly string[] StatusLabels=["Open","Assigned","In Progress","Done","Cancelled"];
    public TaskEditorPage(ApiClient api,Guid? taskId=null):base(api,taskId is null?"Add Task":"Edit Task")
    {
        this.taskId=taskId;circles=new(api);assignees=new(api);title=Field("Task title");details=new Editor{Placeholder="Instructions (optional)",HeightRequest=90,BackgroundColor=Colors.White};detailsToggle=TextToggleButton("instructions");due=new DatePicker{MinimumDate=DateTime.Today.AddYears(-5),Date=DateTime.Today,BackgroundColor=Colors.White};status=new Picker{Title="Status",ItemsSource=StatusLabels,SelectedIndex=0,BackgroundColor=Colors.White};assignToMe=new CheckBox();
        var detailsHeader=new Grid{ColumnDefinitions=[new(GridLength.Star),new(40)]};detailsHeader.Add(CareTheme.Text("Instructions",12,false,CareTheme.Muted),0,0);detailsHeader.Add(detailsToggle,1,0);Body.Add(circles);Body.Add(title);Body.Add(detailsHeader);Body.Add(details);Body.Add(CareTheme.Text("Due date",12,false,CareTheme.Muted));Body.Add(due);Body.Add(assignees);Body.Add(new HorizontalStackLayout{Spacing=8,Children={assignToMe,CareTheme.Text("Assign to me")}});Body.Add(CareTheme.Text("Status",12,false,CareTheme.Muted));Body.Add(status);save=CareTheme.Primary(taskId is null?"Add Task":"Save Changes");save.Clicked+=Save;Body.Add(save);
        assignToMe.CheckedChanged+=(_,e)=>assignees.SelectionEnabled=!e.Value;
        circles.SelectedIdChanged+=async id=>await assignees.LoadAsync(id);details.TextChanged+=(_,_)=>UpdateTextToggle(detailsToggle,details.Text,previousDetails,"instructions");detailsToggle.Clicked+=(_,_)=>{if(!string.IsNullOrWhiteSpace(details.Text)){previousDetails=details.Text;details.Text=null;}else if(!string.IsNullOrWhiteSpace(previousDetails)){details.Text=previousDetails;previousDetails=null;}UpdateTextToggle(detailsToggle,details.Text,previousDetails,"instructions");};UpdateTextToggle(detailsToggle,null,null,"instructions");Appearing+=Load;
    }
    async void Load(object? sender,EventArgs e)
    {
        if(loaded)return;loaded=true;
        try
        {
            if(taskId is null){await circles.LoadAsync();return;}
            var item=await Api.GetAsync($"api/tasks/{taskId}");title.Text=item.GetProperty("title").GetString();previousDetails=item.TryGetProperty("previousDetails",out var pd)&&pd.ValueKind!=JsonValueKind.Null?pd.GetString():null;details.Text=item.TryGetProperty("details",out var d)&&d.ValueKind!=JsonValueKind.Null?d.GetString():null;UpdateTextToggle(detailsToggle,details.Text,previousDetails,"instructions");due.Date=item.GetProperty("dueAt").GetDateTimeOffset().ToLocalTime().Date;var circleId=item.GetProperty("careCircleId").GetGuid();Guid? assigned=item.TryGetProperty("assignedToUserId",out var a)&&a.ValueKind!=JsonValueKind.Null?a.GetGuid():null;await circles.LoadAsync(circleId);await assignees.LoadAsync(circleId,assigned);assignToMe.IsChecked=assigned is not null&&assigned==Api.UserId;var value=item.GetProperty("status").GetString()?.Replace("InProgress","In Progress");status.SelectedIndex=Math.Max(0,Array.IndexOf(StatusLabels,value));var linkedAppointmentCompleted=item.TryGetProperty("linkedAppointmentStatus",out var linkedStatus)&&linkedStatus.ValueKind!=JsonValueKind.Null&&linkedStatus.GetString()=="Completed";circles.SelectionEnabled=value!="Done"&&!linkedAppointmentCompleted;
        }
        catch(Exception ex){await DisplayAlertAsync("Could not load task",ex.Message,"OK");}
    }
    async void Save(object? sender,EventArgs e)
    {
        if(circles.SelectedId is null||string.IsNullOrWhiteSpace(title.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a title.","OK");return;}
        try{save.IsEnabled=false;var value=new{careCircleId=circles.SelectedId,title=title.Text.Trim(),details=details.Text,previousDetails,dueAt=new DateTimeOffset((due.Date??DateTime.Today).AddHours(9)),assignedToUserId=assignToMe.IsChecked?null:assignees.SelectedUserId,status=status.SelectedIndex,assignToMe=assignToMe.IsChecked};if(taskId is null)await Api.PostAsync("api/tasks",value);else await Api.PutAsync($"api/tasks/{taskId}",value);await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}
    }
    static ImageButton TextToggleButton(string type)=>new(){BackgroundColor=Colors.Transparent,Padding=8,HeightRequest=38,WidthRequest=38,AutomationId="TaskInstructionsClearRestore"};
    static void UpdateTextToggle(ImageButton button,string? text,string? previous,string type){var hasText=!string.IsNullOrWhiteSpace(text);var canRestore=!string.IsNullOrWhiteSpace(previous);var enabled=hasText||canRestore;var description=hasText?$"Clear {type}":canRestore?$"Restore {type}":$"No {type} to clear or restore";button.Source=new FontImageSource{Glyph=hasText?"×":"↶",Color=!enabled?Color.FromArgb("#AAB2BF"):hasText?CareTheme.Red:Color.FromArgb("#F59E0B"),Size=22};button.IsEnabled=enabled;button.Opacity=enabled?1:.35;ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);}
}

public sealed class AddTaskPage(ApiClient api) : TaskEditorPage(api);

public sealed class LiveMedicationsPage : LiveListPage
{
    public LiveMedicationsPage(ApiClient api):base(api,"Medications","api/medications","No medications have been added.",CareTheme.AddAction(),true){WireAdd(new AddMedicationPage(api));}
    protected override void Render(JsonElement x)
    {
        var id=x.GetProperty("id").GetGuid();var name=S(x,"name");var completed=x.GetProperty("status").GetInt32()==2;var hasLogs=x.TryGetProperty("hasLogs",out var logs)&&logs.GetBoolean();var hasDocuments=x.TryGetProperty("hasDocuments",out var documents)&&documents.GetBoolean();var canDelete=!hasLogs&&!hasDocuments;var recipient=S(x,"recipientName");var showRecipient=!string.IsNullOrWhiteSpace(recipient)&&!recipient.Equals("All recipient",StringComparison.OrdinalIgnoreCase)&&!recipient.Equals("All recipients",StringComparison.OrdinalIgnoreCase);var medicationDetail=$"{(showRecipient?$"{recipient} • ":"")}{S(x,"dosage")} • {S(x,"schedule")}";
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(44),new(44),new(44)],ColumnSpacing=2};var details=CareTheme.Row("💊",name,medicationDetail,Color.FromArgb("#FFE5E6"),false);details.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new MedicationEditorPage(Api,id))));grid.Add(details,0,0);
        var cards=ActionButton("▤",CareTheme.Blue,"Display medication cards");cards.Clicked+=async(_,_)=>await Navigation.PushAsync(new MedicationOccurrencesPage(Api,id,name,showRecipient?recipient:null));grid.Add(cards,1,0);
        var trash=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,Padding=10,WidthRequest=40,HeightRequest=40,Aspect=Aspect.AspectFit,IsEnabled=canDelete,Opacity=canDelete?1:.28,AutomationId=canDelete?"Delete medication":"Cannot delete medication with administration history or documents"};var trashDescription=canDelete?"Delete medication":"Cannot delete medication with administration history or attached documents";ToolTipProperties.SetText(trash,trashDescription);SemanticProperties.SetDescription(trash,trashDescription);if(canDelete)trash.Clicked+=async(_,_)=>await Delete(id,name);grid.Add(trash,2,0);
        var toggle=ActionButton(completed?"↶":"✓",completed?Color.FromArgb("#F59E0B"):Color.FromArgb("#24A148"),completed?"Restore medication to its previous status":"Mark medication completed");toggle.Clicked+=async(_,_)=>await Toggle(id);grid.Add(toggle,3,0);Items.Add(CareTheme.Card(grid));
    }
    static ImageButton ActionButton(string glyph,Color color,string description){var button=new ImageButton{Source=new FontImageSource{Glyph=glyph,Color=color,Size=21},BackgroundColor=Colors.Transparent,Padding=9,WidthRequest=40,HeightRequest=40,AutomationId=description};ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);return button;}
    async Task Delete(Guid id,string name){if(!await DisplayAlertAsync("Delete medication?",$"Delete {name} and all of its unlogged medication cards?","Delete","Cancel"))return;try{await Api.DeleteAsync($"api/medications/{id}");await Refresh();}catch(Exception ex){await DisplayAlertAsync("Could not delete medication",ex.Message,"OK");}}
    async Task Toggle(Guid id){try{await Api.PutAsync($"api/medications/{id}/toggle-completed",new{});await Refresh();}catch(Exception ex){await DisplayAlertAsync("Could not update medication",ex.Message,"OK");}}
}
public class MedicationEditorPage : CarePage
{
    readonly Guid? medicationId;readonly CirclePickerView circle;readonly Entry name;readonly Entry dose;readonly Picker scheduleType;readonly DatePicker onceDate;readonly TimePicker onceTime;readonly VerticalStackLayout onceOptions;readonly Picker occurrences;readonly VerticalStackLayout times;readonly List<TimePicker> timePickers=[];readonly DatePicker startDate;readonly DatePicker endDate;readonly Picker weeklyDay;readonly Picker monthlyDay;readonly VerticalStackLayout recurringOptions;readonly VerticalStackLayout weeklyOptions;readonly VerticalStackLayout monthlyOptions;readonly Editor instructions;readonly Picker status;readonly Button save;readonly ImageButton cardsButton;readonly ImageButton deleteButton;bool loaded;
    public MedicationEditorPage(ApiClient api,Guid? medicationId=null):base(api,medicationId is null?"Add Medication":"Edit Medication")
    {
        PageLayout.Remove(Header);Body.Add(Header);
        this.medicationId=medicationId;cardsButton=new ImageButton{Source=new FontImageSource{Glyph="▤",Color=CareTheme.Blue,Size=21},BackgroundColor=Colors.Transparent,WidthRequest=38,HeightRequest=38,Padding=7,IsVisible=medicationId is not null,AutomationId="ViewMedicationCards"};deleteButton=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,WidthRequest=38,HeightRequest=38,Padding=8,IsVisible=medicationId is not null,AutomationId="DeleteMedication"};ToolTipProperties.SetText(cardsButton,"View all medication cards");SemanticProperties.SetDescription(cardsButton,"View all medication cards");ToolTipProperties.SetText(deleteButton,"Delete medication");SemanticProperties.SetDescription(deleteButton,"Delete medication");var actions=new HorizontalStackLayout{Spacing=2,Children={cardsButton,deleteButton}};var pageHeader=(Grid)Body.Children[0];pageHeader.Add(actions,2,0);circle=new(api);name=Field("Medication name");dose=Field("Dosage");scheduleType=new Picker{Title="Schedule",ItemsSource=new[]{"Once","Daily","Weekly","Monthly"},SelectedIndex=1,BackgroundColor=Colors.White};onceDate=new DatePicker{MinimumDate=DateTime.Today.AddYears(-1),Date=DateTime.Today,BackgroundColor=Colors.White};onceTime=new TimePicker{Time=new TimeSpan(9,0,0),Format="h:mm tt",BackgroundColor=Colors.White};onceOptions=new VerticalStackLayout{Spacing=6,Children={CareTheme.Text("Date",12,false,CareTheme.Muted),onceDate,CareTheme.Text("Time",12,false,CareTheme.Muted),onceTime}};occurrences=new Picker{Title="Occurrences per day",ItemsSource=new[]{"1","2","3","4","5","6"},SelectedIndex=0,BackgroundColor=Colors.White};times=new VerticalStackLayout{Spacing=6};startDate=new DatePicker{Date=DateTime.Today,BackgroundColor=Colors.White};endDate=new DatePicker{Date=DateTime.Today.AddYears(1),BackgroundColor=Colors.White};weeklyDay=new Picker{Title="Day of week",ItemsSource=Enum.GetNames<DayOfWeek>(),SelectedIndex=(int)DateTime.Today.DayOfWeek,BackgroundColor=Colors.White};monthlyDay=new Picker{Title="Day of month",ItemsSource=Enumerable.Range(1,31).Select(x=>x.ToString()).ToArray(),SelectedIndex=Math.Min(DateTime.Today.Day,31)-1,BackgroundColor=Colors.White};weeklyOptions=new VerticalStackLayout{Spacing=6,Children={CareTheme.Text("Day of week",12,false,CareTheme.Muted),weeklyDay}};monthlyOptions=new VerticalStackLayout{Spacing=6,Children={CareTheme.Text("Day of month",12,false,CareTheme.Muted),monthlyDay}};recurringOptions=new VerticalStackLayout{Spacing=6,Children={CareTheme.Text("Number of times",12,false,CareTheme.Muted),occurrences,times,weeklyOptions,monthlyOptions,CareTheme.Text("Start date",12,false,CareTheme.Muted),startDate,CareTheme.Text("End date",12,false,CareTheme.Muted),endDate}};instructions=new Editor{Placeholder="Instructions (optional)",HeightRequest=90,BackgroundColor=Colors.White};status=new Picker{Title="Status",ItemsSource=new[]{"Active","Paused","Completed"},SelectedIndex=0,BackgroundColor=Colors.White};Body.Add(circle);Body.Add(name);Body.Add(dose);Body.Add(CareTheme.Text("Schedule",12,false,CareTheme.Muted));Body.Add(scheduleType);Body.Add(onceOptions);Body.Add(recurringOptions);Body.Add(CareTheme.Text("Instructions",12,false,CareTheme.Muted));Body.Add(instructions);Body.Add(CareTheme.Text("Status",12,false,CareTheme.Muted));Body.Add(status);save=CareTheme.Primary(medicationId is null?"Add Medication":"Save Changes");save.Clicked+=Save;Body.Add(save);cardsButton.Clicked+=async(_,_)=>await Navigation.PushAsync(new MedicationOccurrencesPage(Api,medicationId!.Value,name.Text??"Medication",circle.SelectedName));deleteButton.Clicked+=DeleteMedication;scheduleType.SelectedIndexChanged+=(_,_)=>UpdateScheduleVisibility();occurrences.SelectedIndexChanged+=(_,_)=>BuildTimePickers();BuildTimePickers();UpdateScheduleVisibility();Appearing+=Load;
        Body.Remove(Header);PageLayout.Add(Header,0,0);
    }
    void UpdateScheduleVisibility(){var once=scheduleType.SelectedIndex==0;onceOptions.IsVisible=once;recurringOptions.IsVisible=!once;weeklyOptions.IsVisible=scheduleType.SelectedIndex==2;monthlyOptions.IsVisible=scheduleType.SelectedIndex==3;}
    void BuildTimePickers(){var count=Math.Max(1,occurrences.SelectedIndex+1);var previous=timePickers.Select(x=>x.Time??new TimeSpan(9,0,0)).ToList();timePickers.Clear();times.Clear();times.Add(CareTheme.Text("Times of day",12,false,CareTheme.Muted));for(var i=0;i<count;i++){var picker=new TimePicker{Time=i<previous.Count?previous[i]:new TimeSpan(Math.Min(8+i*4,23),0,0),Format="h:mm tt",BackgroundColor=Colors.White};timePickers.Add(picker);times.Add(picker);}}
    async void Load(object? sender,EventArgs e)
    {
        if(loaded)return;loaded=true;
        try
        {
            if(medicationId is null){await circle.LoadAsync();return;}var item=await Api.GetAsync($"api/medications/{medicationId}");name.Text=item.GetProperty("name").GetString();dose.Text=item.GetProperty("dosage").GetString();instructions.Text=item.TryGetProperty("instructions",out var i)&&i.ValueKind!=JsonValueKind.Null?i.GetString():null;status.SelectedIndex=item.GetProperty("status").GetInt32();scheduleType.SelectedIndex=item.TryGetProperty("scheduleType",out var st)?st.GetInt32():1;
            if(item.TryGetProperty("onceAt",out var once)&&once.ValueKind!=JsonValueKind.Null){var value=once.GetDateTimeOffset().ToLocalTime();onceDate.Date=value.Date;onceTime.Time=value.TimeOfDay;}
            var occurrenceCount=item.TryGetProperty("occurrences",out var oc)&&oc.ValueKind!=JsonValueKind.Null?Math.Clamp(oc.GetInt32(),1,6):1;occurrences.SelectedIndex=occurrenceCount-1;BuildTimePickers();if(item.TryGetProperty("timesOfDay",out var tod)&&tod.ValueKind!=JsonValueKind.Null){var values=(tod.GetString()??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);for(var index=0;index<Math.Min(values.Length,timePickers.Count);index++)if(TimeSpan.TryParse(values[index],out var parsed))timePickers[index].Time=parsed;}
            if(item.TryGetProperty("startDate",out var sd)&&sd.ValueKind!=JsonValueKind.Null&&DateTime.TryParse(sd.GetString(),out var start))startDate.Date=start;if(item.TryGetProperty("endDate",out var ed)&&ed.ValueKind!=JsonValueKind.Null&&DateTime.TryParse(ed.GetString(),out var end))endDate.Date=end;if(item.TryGetProperty("weeklyDay",out var wd)&&wd.ValueKind!=JsonValueKind.Null)weeklyDay.SelectedIndex=wd.GetInt32();if(item.TryGetProperty("monthlyDay",out var md)&&md.ValueKind!=JsonValueKind.Null)monthlyDay.SelectedIndex=Math.Clamp(md.GetInt32(),1,31)-1;UpdateScheduleVisibility();await circle.LoadAsync(item.GetProperty("careCircleId").GetGuid());
        }
        catch(Exception ex){await DisplayAlertAsync("Could not load medication",ex.Message,"OK");}
    }
    async void Save(object? sender,EventArgs e)
    {
        if(circle.SelectedId is null||string.IsNullOrWhiteSpace(name.Text)||string.IsNullOrWhiteSpace(dose.Text)){await DisplayAlertAsync("Required","Choose a recipient and complete the medication name and dosage.","OK");return;}var isOnce=scheduleType.SelectedIndex==0;var start=startDate.Date??DateTime.Today;var end=endDate.Date??DateTime.Today;if(!isOnce&&end<start){await DisplayAlertAsync("Invalid dates","The end date cannot be before the start date.","OK");return;}
        try
        {
            save.IsEnabled=false;var timesValue=string.Join(",",timePickers.Select(x=>(x.Time??TimeSpan.Zero).ToString(@"hh\:mm")));var onceLocal=(onceDate.Date??DateTime.Today).Add(onceTime.Time??TimeSpan.Zero);var summary=isOnce?$"Once • {onceLocal:g}":scheduleType.SelectedIndex switch{1=>$"Daily • {timePickers.Count}x at {string.Join(", ",timePickers.Select(x=>DateTime.Today.Add(x.Time??TimeSpan.Zero).ToString("h:mm tt")))}",2=>$"Weekly on {weeklyDay.SelectedItem} • {timePickers.Count}x",_=>$"Monthly on day {monthlyDay.SelectedIndex+1} • {timePickers.Count}x"};var value=new{careCircleId=circle.SelectedId,name=name.Text.Trim(),dosage=dose.Text.Trim(),schedule=summary,instructions=instructions.Text,status=status.SelectedIndex,scheduleType=scheduleType.SelectedIndex,onceAt=isOnce?new DateTimeOffset(onceLocal):(DateTimeOffset?)null,occurrences=isOnce?(int?)null:timePickers.Count,timesOfDay=isOnce?null:timesValue,startDate=isOnce?(DateOnly?)null:DateOnly.FromDateTime(start),endDate=isOnce?(DateOnly?)null:DateOnly.FromDateTime(end),weeklyDay=scheduleType.SelectedIndex==2?(int?)weeklyDay.SelectedIndex:null,monthlyDay=scheduleType.SelectedIndex==3?(int?)(monthlyDay.SelectedIndex+1):null,scheduleUtcOffsetMinutes=(int)DateTimeOffset.Now.Offset.TotalMinutes};if(medicationId is null)await Api.PostAsync("api/medications",value);else await Api.PutAsync($"api/medications/{medicationId}",value);await Navigation.PopAsync();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}
    }
    async void DeleteMedication(object? sender,EventArgs e){if(medicationId is null||!await DisplayAlertAsync("Delete medication?","Delete this medication and all of its unlogged occurrence cards?","Delete","Cancel"))return;try{deleteButton.IsEnabled=false;await Api.DeleteAsync($"api/medications/{medicationId}");await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not delete medication",ex.Message,"OK");}finally{deleteButton.IsEnabled=true;}}
}
public sealed class AddMedicationPage(ApiClient api):MedicationEditorPage(api);

public sealed class MedicationOccurrencesPage : CarePage
{
    readonly Guid medicationId;readonly string medicationName;readonly VerticalStackLayout items=new(){Spacing=7};
    public MedicationOccurrencesPage(ApiClient api,Guid medicationId,string medicationName,string? recipientName=null):base(api,$"{medicationName} Cards{(string.IsNullOrWhiteSpace(recipientName)?"":$" • {recipientName}")}",CareTheme.PlusAction("Add medication card")){this.medicationId=medicationId;this.medicationName=medicationName;Body.Add(items);var header=Header;if(header.Children.LastOrDefault() is ImageButton add)add.Clicked+=async(_,_)=>await Navigation.PushAsync(new MedicationOccurrenceEditorPage(Api,medicationId,medicationName));Appearing+=async(_,_)=>await Load();}
    async Task Load(){items.Clear();items.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});try{var rows=await Api.GetAsync($"api/medications/{medicationId}/occurrences");items.Clear();foreach(var x in rows.EnumerateArray()){var id=x.GetProperty("id").GetGuid();var scheduled=x.GetProperty("scheduledAt").GetDateTimeOffset().ToLocalTime();var hasLogs=x.GetProperty("hasLogs").GetBoolean();var taken=x.GetProperty("isTaken").GetBoolean();var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(44),new(44)]};var card=CareTheme.Card(CareTheme.Row(taken?"✓":"●",scheduled.ToString("ddd, MMM d"),$"{scheduled:h:mm tt} • {(taken?"Administered":"Scheduled")}",null,false));if(!hasLogs)card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new MedicationOccurrenceEditorPage(Api,medicationId,medicationName,id,scheduled))));grid.Add(card,0,0);var complete=CompletionButton(taken);complete.Clicked+=async(_,_)=>await ToggleTaken(id);grid.Add(complete,1,0);var trash=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,Padding=10,WidthRequest=40,HeightRequest=40,IsEnabled=!hasLogs,Opacity=hasLogs?.35:1,AutomationId=$"Delete medication card {scheduled:g}"};ToolTipProperties.SetText(trash,hasLogs?"Cannot delete a card with administration history":"Delete medication card");SemanticProperties.SetDescription(trash,hasLogs?"Cannot delete a card with administration history":"Delete medication card");if(!hasLogs)trash.Clicked+=async(_,_)=>await Delete(id,scheduled);grid.Add(trash,2,0);items.Add(grid);}if(items.Count==0)items.Add(CareTheme.Card(CareTheme.Text("No medication cards have been generated.",13,false,CareTheme.Muted)));}catch(Exception ex){items.Clear();items.Add(CareTheme.Card(CareTheme.Text($"Could not load medication cards.\n{ex.Message}",12,false,CareTheme.Red)));}}
    static ImageButton CompletionButton(bool taken){var description=taken?"Restore medication card to scheduled":"Mark medication card administered";var button=new ImageButton{Source=new FontImageSource{Glyph=taken?"↶":"✓",Color=taken?Color.FromArgb("#F59E0B"):Color.FromArgb("#24A148"),Size=21},BackgroundColor=Colors.Transparent,Padding=9,WidthRequest=40,HeightRequest=40,AutomationId=description};ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);return button;}
    async Task ToggleTaken(Guid id){try{await Api.PutAsync($"api/medication-occurrences/{id}/toggle-taken",new{});await Load();}catch(Exception ex){await DisplayAlertAsync("Could not update card",ex.Message,"OK");}}
    async Task Delete(Guid id,DateTimeOffset scheduled){if(!await DisplayAlertAsync("Delete medication card?",$"Delete the occurrence scheduled for {scheduled:g}?","Delete","Cancel"))return;try{await Api.DeleteAsync($"api/medication-occurrences/{id}");await Load();}catch(Exception ex){await DisplayAlertAsync("Could not delete card",ex.Message,"OK");}}
}

public sealed class MedicationOccurrenceEditorPage : CarePage
{
    readonly Guid medicationId;readonly Guid? occurrenceId;readonly DatePicker date;readonly TimePicker time;readonly Button save;
    public MedicationOccurrenceEditorPage(ApiClient api,Guid medicationId,string medicationName,Guid? occurrenceId=null,DateTimeOffset? scheduled=null):base(api,occurrenceId is null?"Add Medication Card":"Edit Medication Card"){this.medicationId=medicationId;this.occurrenceId=occurrenceId;var local=scheduled?.ToLocalTime()??DateTimeOffset.Now;date=new DatePicker{Date=local.Date,BackgroundColor=Colors.White};time=new TimePicker{Time=local.TimeOfDay,Format="h:mm tt",BackgroundColor=Colors.White};Body.Add(CareTheme.Text(medicationName,14,true));Body.Add(CareTheme.Text("Date",12,false,CareTheme.Muted));Body.Add(date);Body.Add(CareTheme.Text("Time",12,false,CareTheme.Muted));Body.Add(time);save=CareTheme.Primary(occurrenceId is null?"Add Card":"Save Changes");save.Clicked+=Save;Body.Add(save);}
    async void Save(object? sender,EventArgs e){try{save.IsEnabled=false;var local=(date.Date??DateTime.Today).Add(time.Time??TimeSpan.Zero);var value=new{scheduledAt=new DateTimeOffset(local)};if(occurrenceId is null)await Api.PostAsync($"api/medications/{medicationId}/occurrences",value);else await Api.PutAsync($"api/medication-occurrences/{occurrenceId}",value);await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save card",ex.Message,"OK");}finally{save.IsEnabled=true;}}
}

public sealed class LiveAppointmentsPage : LiveListPage
{
    readonly CareDateRangeBar dateRange=new();
    public LiveAppointmentsPage(ApiClient api):base(api,"Appointments","api/appointments","No appointments match the selected filters and date range.",CareTheme.AddAction(),true){Body.Insert(1,dateRange);dateRange.Changed+=Refresh;WireAdd(new AppointmentEditorPage(api));}
    protected override bool Include(JsonElement item)=>dateRange.Includes(item.GetProperty("startsAt").GetDateTimeOffset());
    protected override void Render(JsonElement x){var id=x.GetProperty("id").GetGuid();var card=CareTheme.Card(CareTheme.Row("📅",S(x,"title"),$"{S(x,"provider")} • {x.GetProperty("startsAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}",Color.FromArgb("#FFF1F2")));card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new AppointmentEditorPage(Api,id))));Items.Add(card);}
}
public class AppointmentEditorPage : CarePage
{
    readonly Guid? appointmentId;readonly CirclePickerView circle;readonly Entry title;readonly Entry provider;readonly Entry location;readonly DatePicker date;readonly TimePicker time;readonly Editor notes;readonly ImageButton notesToggle;readonly CheckBox createTask;readonly CheckBox assignTaskToMe;readonly CareMemberPickerView taskAssignee;readonly Entry taskTitle;readonly Editor taskDetails;readonly HorizontalStackLayout taskToggleRow;readonly VerticalStackLayout taskOptions;readonly Label linkedTaskNotice;readonly ImageButton viewLinkedTasks;readonly ImageButton addLinkedTask;readonly HorizontalStackLayout linkedTaskRow;readonly Button save;string? previousNotes;string linkedTaskSummary="";bool loaded;
    public AppointmentEditorPage(ApiClient api,Guid? appointmentId=null):base(api,appointmentId is null?"Add Appointment":"Edit Appointment")
    {
        this.appointmentId=appointmentId;circle=new(api);title=Field("Appointment title");provider=Field("Doctor or provider");location=Field("Location (optional)");date=new DatePicker{MinimumDate=DateTime.Today.AddYears(-5),Date=DateTime.Today,BackgroundColor=Colors.White};time=new TimePicker{Time=new TimeSpan(9,0,0),Format="h:mm tt",BackgroundColor=Colors.White};notes=new Editor{Placeholder="Comments (optional)",HeightRequest=80,BackgroundColor=Colors.White};notesToggle=TextToggleButton();createTask=new CheckBox();assignTaskToMe=new CheckBox();taskAssignee=new(api);taskTitle=Field("Linked task title");taskDetails=new Editor{Placeholder="Linked task details",HeightRequest=90,BackgroundColor=Colors.White};linkedTaskNotice=CareTheme.Text("This appointment already has a linked task.",12,true,CareTheme.Blue);viewLinkedTasks=LinkedTaskButton("👁",CareTheme.Blue,"View linked task details");addLinkedTask=LinkedTaskButton("＋",CareTheme.Blue,"Add another linked task");linkedTaskRow=new HorizontalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center,IsVisible=false,Children={linkedTaskNotice,viewLinkedTasks,addLinkedTask}};
        taskToggleRow=new HorizontalStackLayout{Spacing=8,Children={createTask,CareTheme.Text("Create a linked task")}};taskOptions=new VerticalStackLayout{Spacing=8,IsVisible=false,Children={taskTitle,taskDetails,taskAssignee,new HorizontalStackLayout{Spacing=8,Children={assignTaskToMe,CareTheme.Text("Assign linked task to me")}},CareTheme.Text("The linked task uses the appointment date and time as its due time.",11,false,CareTheme.Muted)}};
        var notesHeader=new Grid{ColumnDefinitions=[new(GridLength.Star),new(40)]};notesHeader.Add(CareTheme.Text("Comments",12,false,CareTheme.Muted),0,0);notesHeader.Add(notesToggle,1,0);Body.Add(circle);Body.Add(title);Body.Add(provider);Body.Add(location);Body.Add(CareTheme.Text("Date",12,false,CareTheme.Muted));Body.Add(date);Body.Add(CareTheme.Text("Time",12,false,CareTheme.Muted));Body.Add(time);Body.Add(notesHeader);Body.Add(notes);Body.Add(taskToggleRow);Body.Add(taskOptions);Body.Add(linkedTaskRow);save=CareTheme.Primary(appointmentId is null?"Add Appointment":"Save Changes");save.Clicked+=Save;Body.Add(save);
        createTask.CheckedChanged+=(_,e)=>taskOptions.IsVisible=e.Value;assignTaskToMe.CheckedChanged+=(_,e)=>taskAssignee.SelectionEnabled=!e.Value;circle.SelectedIdChanged+=async id=>await taskAssignee.LoadAsync(id);title.TextChanged+=(_,_)=>PopulateTask();provider.TextChanged+=(_,_)=>PopulateTask();location.TextChanged+=(_,_)=>PopulateTask();notes.TextChanged+=(_,_)=>{UpdateTextToggle();PopulateTask();};notesToggle.Clicked+=(_,_)=>{if(!string.IsNullOrWhiteSpace(notes.Text)){previousNotes=notes.Text;notes.Text=null;}else if(!string.IsNullOrWhiteSpace(previousNotes)){notes.Text=previousNotes;previousNotes=null;}UpdateTextToggle();};viewLinkedTasks.Clicked+=async(_,_)=>await Navigation.PushModalAsync(new LinkedTasksBubblePage(linkedTaskSummary));addLinkedTask.Clicked+=(_,_)=>{createTask.IsChecked=true;taskOptions.IsVisible=true;PopulateTask();};UpdateTextToggle();Appearing+=Load;
    }
    async void Load(object? sender,EventArgs e)
    {
        if(loaded)return;loaded=true;
        try{if(appointmentId is null){await circle.LoadAsync();PopulateTask();return;}var item=await Api.GetAsync($"api/appointments/{appointmentId}");title.Text=item.GetProperty("title").GetString();provider.Text=item.GetProperty("provider").GetString();location.Text=item.TryGetProperty("location",out var l)&&l.ValueKind!=JsonValueKind.Null?l.GetString():null;previousNotes=item.TryGetProperty("previousNotes",out var pn)&&pn.ValueKind!=JsonValueKind.Null?pn.GetString():null;notes.Text=item.TryGetProperty("notes",out var n)&&n.ValueKind!=JsonValueKind.Null?n.GetString():null;UpdateTextToggle();var startsAt=item.GetProperty("startsAt").GetDateTimeOffset().ToLocalTime();date.Date=startsAt.Date;time.Time=startsAt.TimeOfDay;var circleId=item.GetProperty("careCircleId").GetGuid();await circle.LoadAsync(circleId);await taskAssignee.LoadAsync(circleId);var linkedTasks=item.TryGetProperty("linkedTasks",out var linked)?linked.EnumerateArray().ToList():[];if(linkedTasks.Count>0){taskToggleRow.IsVisible=false;taskOptions.IsVisible=false;linkedTaskRow.IsVisible=true;linkedTaskNotice.Text=linkedTasks.Count==1?"This appointment already has a linked task.":$"This appointment has {linkedTasks.Count} linked tasks.";linkedTaskSummary=string.Join("\n\n",linkedTasks.Select((x,index)=>$"{index+1}. {Text(x,"title")}\nDue: {x.GetProperty("dueAt").GetDateTimeOffset().ToLocalTime():MMM d, yyyy h:mm tt}\nAssigned to: {(string.IsNullOrWhiteSpace(Text(x,"assigneeName"))?"Unassigned":Text(x,"assigneeName"))}\nStatus: {Text(x,"status")}{(string.IsNullOrWhiteSpace(Text(x,"details"))?"":$"\nDetails: {Text(x,"details")}")}"));}var appointmentCompleted=item.GetProperty("status").GetInt32()==1;var linkedTaskLocked=linkedTasks.Any(x=>Text(x,"status") is "Done" or "Cancelled");circle.SelectionEnabled=!appointmentCompleted&&!linkedTaskLocked;addLinkedTask.IsEnabled=!appointmentCompleted;addLinkedTask.Opacity=appointmentCompleted?.35:1;PopulateTask();}catch(Exception ex){await DisplayAlertAsync("Could not load appointment",ex.Message,"OK");}
    }
    async void Save(object? sender,EventArgs e)
    {
        if(circle.SelectedId is null||string.IsNullOrWhiteSpace(title.Text)||string.IsNullOrWhiteSpace(provider.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a title and provider.","OK");return;}
        try
        {
            save.IsEnabled=false;var startsAt=(date.Date??DateTime.Today).Add(time.Time??new TimeSpan(9,0,0));var value=new{careCircleId=circle.SelectedId,title=title.Text.Trim(),provider=provider.Text.Trim(),startsAt=new DateTimeOffset(startsAt),location=location.Text,notes=notes.Text,previousNotes};var appointment=appointmentId is null?await Api.PostAsync("api/appointments",value):await Api.PutAsync($"api/appointments/{appointmentId}",value);var savedAppointmentId=appointment.GetProperty("id").GetGuid();
            if(createTask.IsVisible&&createTask.IsChecked){if(string.IsNullOrWhiteSpace(taskTitle.Text)){await DisplayAlertAsync("Task title required","Enter a title for the linked task.","OK");return;}var assignedTo=assignTaskToMe.IsChecked?null:taskAssignee.SelectedUserId;var taskStatus=assignTaskToMe.IsChecked||assignedTo is not null?1:0;await Api.PostAsync("api/tasks",new{careCircleId=circle.SelectedId,appointmentId=savedAppointmentId,title=taskTitle.Text.Trim(),details=taskDetails.Text,dueAt=new DateTimeOffset(startsAt),assignedToUserId=assignedTo,status=taskStatus,assignToMe=assignTaskToMe.IsChecked});}
            await Navigation.PopAsync();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}
    }
    void PopulateTask()
    {
        taskTitle.Text=string.IsNullOrWhiteSpace(title.Text)?"Appointment preparation":$"Prepare for {title.Text.Trim()}";
        var parts=new[]{string.IsNullOrWhiteSpace(provider.Text)?null:$"Provider: {provider.Text.Trim()}",string.IsNullOrWhiteSpace(location.Text)?null:$"Location: {location.Text.Trim()}",string.IsNullOrWhiteSpace(notes.Text)?null:$"Notes: {notes.Text.Trim()}"};taskDetails.Text=string.Join("\n",parts.Where(x=>x is not null));
    }
    static ImageButton TextToggleButton()=>new(){BackgroundColor=Colors.Transparent,Padding=8,HeightRequest=38,WidthRequest=38,AutomationId="AppointmentCommentsClearRestore"};
    static ImageButton LinkedTaskButton(string glyph,Color color,string description){var button=new ImageButton{Source=new FontImageSource{Glyph=glyph,Color=color,Size=20},BackgroundColor=Colors.Transparent,Padding=8,HeightRequest=38,WidthRequest=38,AutomationId=description};ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);return button;}
    static string Text(JsonElement x,string name)=>x.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??"":"";
    void UpdateTextToggle(){var hasText=!string.IsNullOrWhiteSpace(notes.Text);var canRestore=!string.IsNullOrWhiteSpace(previousNotes);var enabled=hasText||canRestore;var description=hasText?"Clear comments":canRestore?"Restore comments":"No comments to clear or restore";notesToggle.Source=new FontImageSource{Glyph=hasText?"×":"↶",Color=!enabled?Color.FromArgb("#AAB2BF"):hasText?CareTheme.Red:Color.FromArgb("#F59E0B"),Size=22};notesToggle.IsEnabled=enabled;notesToggle.Opacity=enabled?1:.35;ToolTipProperties.SetText(notesToggle,description);SemanticProperties.SetDescription(notesToggle,description);}
}
public sealed class LinkedTasksBubblePage : ContentPage
{
    public LinkedTasksBubblePage(string details)
    {
        BackgroundColor=Color.FromRgba(16,24,40,.42);Shell.SetNavBarIsVisible(this,false);var close=CareTheme.Primary("Close");close.Clicked+=async(_,_)=>await Navigation.PopModalAsync();var card=CareTheme.Card(new VerticalStackLayout{Spacing=12,Children={CareTheme.Text("Linked task details",18,true,CareTheme.Blue),new ScrollView{MaximumHeightRequest=430,Content=CareTheme.Text(string.IsNullOrWhiteSpace(details)?"No linked task details are available.":details,13)},close}},Colors.White);card.WidthRequest=350;card.Padding=20;card.Shadow=new Shadow{Brush=Colors.Black,Opacity=.22f,Radius=18,Offset=new Point(0,7)};Content=new Grid{Padding=22,Children={card}};card.HorizontalOptions=LayoutOptions.Center;card.VerticalOptions=LayoutOptions.Center;SemanticProperties.SetDescription(card,"Linked appointment task details");
    }
}
public sealed class AddAppointmentPage(ApiClient api):AppointmentEditorPage(api);

public sealed class LiveJournalPage : LiveListPage
{
    public LiveJournalPage(ApiClient api,Guid? circleId=null,string? recipientName=null):base(api,string.IsNullOrWhiteSpace(recipientName)?"Care Journal":$"{recipientName} Journal",circleId is null?"api/journal":$"api/journal?careCircleId={circleId}","No journal entries have been added.",CareTheme.AddAction(),circleId is null,circleId is null?"My entries":null){WireAdd(new AddJournalPage(api,circleId));}
    protected override void Render(JsonElement x)=>Items.Add(CareTheme.Card(new VerticalStackLayout{Spacing=5,Children={CareTheme.Text(S(x,"author"),13,true),CareTheme.Text(S(x,"body"),13),CareTheme.Text($"{S(x,"category")} • {x.GetProperty("createdAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}",11,false,CareTheme.Muted)}}));
}
public sealed class AddJournalPage : CarePage
{
    public AddJournalPage(ApiClient api,Guid? circleId=null):base(api,"Add Journal Entry"){var c=new CirclePickerView(api);var category=Field("Category (optional)");var body=new Editor{Placeholder="What happened?",HeightRequest=150,BackgroundColor=Colors.White};Body.Add(c);Body.Add(category);Body.Add(body);var save=CareTheme.Primary("Add Entry");save.Clicked+=async(_,_)=>{if(c.SelectedId is null||string.IsNullOrWhiteSpace(body.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a note.","OK");return;}try{save.IsEnabled=false;await Api.PostAsync("api/journal",new{careCircleId=c.SelectedId,body=body.Text.Trim(),category=category.Text});await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}};Body.Add(save);Appearing+=async(_,_)=>await c.LoadAsync(circleId);}
}

public sealed class LiveHealthPage : LiveListPage
{
    public LiveHealthPage(ApiClient api,Guid? circleId=null,string? recipientName=null):base(api,string.IsNullOrWhiteSpace(recipientName)?"Health Tracking":$"{recipientName} Health",circleId is null?"api/health-readings":$"api/health-readings?careCircleId={circleId}","No health readings have been recorded.",CareTheme.AddAction(),circleId is null)
    {
        var add=Header.Children.OfType<Button>().LastOrDefault();
        if(add is not null)
        {
            Header.Remove(add);
            var statistics=new ImageButton{Source=new FontImageSource{Glyph="▥",Color=CareTheme.Blue,Size=25},BackgroundColor=Colors.Transparent,WidthRequest=42,HeightRequest=48,Padding=7,AutomationId="ViewHealthStatistics"};
            ToolTipProperties.SetText(statistics,"View vital statistics and graphs");SemanticProperties.SetDescription(statistics,"View vital statistics and graphs");
            add.Clicked+=async(_,_)=>await Navigation.PushAsync(new AddHealthPage(api,circleId));
            statistics.Clicked+=async(_,_)=>await Navigation.PushAsync(new HealthStatisticsPage(api,circleId,recipientName));
            Header.Add(new HorizontalStackLayout{Spacing=2,Children={add,statistics}},2,0);
        }
    }
    protected override void Render(JsonElement x){var second=x.TryGetProperty("secondaryValue",out var s)&&s.ValueKind!=JsonValueKind.Null?$" / {s.GetDecimal()}":"";var pulse=x.TryGetProperty("pulse",out var p)&&p.ValueKind!=JsonValueKind.Null?$" • Pulse {p.GetInt32()}":"";Items.Add(CareTheme.Card(CareTheme.Row("♡",S(x,"type"),$"{x.GetProperty("value").GetDecimal()}{second}{pulse} • {x.GetProperty("recordedAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}",null,false)));}
}

public sealed class HealthStatisticsPage : CarePage
{
    readonly Guid? fixedCircleId;readonly CirclePickerView recipients;readonly Picker type=new(){Title="Vital type",ItemsSource=new[]{"Blood Pressure","Weight","Glucose","Temperature"},SelectedIndex=0,BackgroundColor=CareTheme.CardSurface};
    readonly VerticalStackLayout summary=new(){Spacing=8};readonly GraphicsView chart=new(){HeightRequest=285};readonly Label empty=CareTheme.Text("",13,false,CareTheme.Muted);bool loading;
    public HealthStatisticsPage(ApiClient api,Guid? circleId=null,string? recipientName=null):base(api,string.IsNullOrWhiteSpace(recipientName)?"Vital Statistics":$"{recipientName} Vital Statistics")
    {
        fixedCircleId=circleId;recipients=new CirclePickerView(api){IsVisible=circleId is null};recipients.SelectedIdChanged+=async _=>await Load();type.SelectedIndexChanged+=async(_,_)=>await Load();
        Body.Add(recipients);Body.Add(CareTheme.Text("Vital reading",12,false,CareTheme.Muted));Body.Add(type);Body.Add(summary);
        var chartCard=CareTheme.Card(new VerticalStackLayout{Spacing=8,Children={CareTheme.Text("Recorded trend",15,true),chart,empty}});chartCard.Padding=new Thickness(14);Body.Add(chartCard);
        Body.Add(CareTheme.Text("The graph summarizes recorded readings and is not a medical diagnosis. Contact a healthcare professional about concerning results.",11,false,CareTheme.Muted));
        Appearing+=async(_,_)=>{await recipients.LoadAsync(circleId);await Load();};
    }
    async Task Load()
    {
        if(loading)return;var circleId=fixedCircleId??recipients.SelectedId;if(circleId is null)return;loading=true;summary.Clear();empty.Text="Loading readings…";chart.Drawable=null;chart.Invalidate();
        try
        {
            var data=await Api.GetAsync($"api/health-readings?careCircleId={circleId}");var selected=type.SelectedItem?.ToString()??"Blood Pressure";
            var readings=data.EnumerateArray().Where(x=>string.Equals(x.GetProperty("type").GetString(),selected,StringComparison.OrdinalIgnoreCase)).Select(x=>new VitalPoint(x.GetProperty("recordedAt").GetDateTimeOffset().ToLocalTime(),(double)x.GetProperty("value").GetDecimal(),x.TryGetProperty("secondaryValue",out var second)&&second.ValueKind!=JsonValueKind.Null?(double?)second.GetDecimal():null)).OrderBy(x=>x.RecordedAt).ToList();
            if(readings.Count==0){empty.Text=$"No {selected.ToLowerInvariant()} readings have been recorded for this recipient.";return;}
            var latest=readings[^1];var unit=selected switch{"Weight"=>" lb","Glucose"=>" mg/dL","Temperature"=>" °F",_=>""};var primary=readings.Select(x=>x.Value).ToList();var secondary=readings.Where(x=>x.SecondaryValue is not null).Select(x=>x.SecondaryValue!.Value).ToList();
            summary.Add(StatisticsCard("Latest",Format(latest.Value,latest.SecondaryValue,unit),latest.RecordedAt.ToString("MMM d, yyyy • h:mm tt")));
            var metrics=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=7};metrics.Add(Metric("Average",Format(primary.Average(),secondary.Count==0?null:secondary.Average(),unit)),0,0);metrics.Add(Metric(selected=="Blood Pressure"?"Low systolic":"Minimum",$"{primary.Min():0.#}{unit}"),1,0);metrics.Add(Metric(selected=="Blood Pressure"?"High systolic":"Maximum",$"{primary.Max():0.#}{unit}"),2,0);summary.Add(metrics);
            chart.Drawable=new VitalChartDrawable(readings,selected);empty.Text=readings.Count==1?"Add more readings to see a trend over time.":$"{readings.Count} readings • {readings[0].RecordedAt:MMM d} – {readings[^1].RecordedAt:MMM d, yyyy}";chart.Invalidate();
        }
        catch(Exception ex){empty.Text=$"Could not load vital statistics.\n{ex.Message}";}
        finally{loading=false;}
    }
    static string Format(double value,double? secondary,string unit)=>secondary is null?$"{value:0.#}{unit}":$"{value:0.#} / {secondary:0.#}";
    static Border StatisticsCard(string title,string value,string detail)=>CareTheme.Card(new VerticalStackLayout{Spacing=3,Children={CareTheme.Text(title,12,false,CareTheme.Muted),CareTheme.Text(value,24,true,CareTheme.Blue),CareTheme.Text(detail,11,false,CareTheme.Muted)}});
    static Border Metric(string title,string value){var titleLabel=CareTheme.Text(title,10,false,CareTheme.Muted);titleLabel.HorizontalTextAlignment=TextAlignment.Center;var valueLabel=CareTheme.Text(value,15,true);valueLabel.HorizontalTextAlignment=TextAlignment.Center;return CareTheme.Card(new VerticalStackLayout{Spacing=3,Children={titleLabel,valueLabel}});}
}

public sealed record VitalPoint(DateTimeOffset RecordedAt,double Value,double? SecondaryValue);

public sealed class VitalChartDrawable(IReadOnlyList<VitalPoint> readings,string type) : IDrawable
{
    public void Draw(ICanvas canvas,RectF dirtyRect)
    {
        if(readings.Count==0)return;var left=44f;var right=14f;var top=18f;var bottom=38f;var width=Math.Max(1,dirtyRect.Width-left-right);var height=Math.Max(1,dirtyRect.Height-top-bottom);
        var values=readings.Select(x=>x.Value).Concat(readings.Where(x=>x.SecondaryValue is not null).Select(x=>x.SecondaryValue!.Value)).ToList();var min=values.Min();var max=values.Max();var padding=Math.Max((max-min)*.15,1);min-=padding;max+=padding;
        canvas.StrokeColor=CareTheme.Line;canvas.StrokeSize=1;canvas.FontColor=CareTheme.Muted;canvas.FontSize=10;
        for(var i=0;i<5;i++){var y=top+height*i/4;canvas.DrawLine(left,y,left+width,y);var label=max-(max-min)*i/4;canvas.DrawString($"{label:0.#}",0,y-7,left-6,16,HorizontalAlignment.Right,VerticalAlignment.Center);}
        DrawSeries(canvas,readings.Select(x=>x.Value).ToList(),CareTheme.Blue,left,top,width,height,min,max);
        if(readings.Any(x=>x.SecondaryValue is not null))DrawSeries(canvas,readings.Select(x=>x.SecondaryValue??double.NaN).ToList(),Color.FromArgb("#24A148"),left,top,width,height,min,max);
        canvas.FontColor=CareTheme.Muted;canvas.DrawString(readings[0].RecordedAt.ToString("MMM d"),left,top+height+8,70,18,HorizontalAlignment.Left,VerticalAlignment.Top);canvas.DrawString(readings[^1].RecordedAt.ToString("MMM d"),left+width-70,top+height+8,70,18,HorizontalAlignment.Right,VerticalAlignment.Top);
        if(type.Equals("Blood Pressure",StringComparison.OrdinalIgnoreCase)){canvas.FillColor=CareTheme.Blue;canvas.FillCircle(left+4,8,4);canvas.DrawString("Systolic",left+12,0,60,16,HorizontalAlignment.Left,VerticalAlignment.Center);canvas.FillColor=Color.FromArgb("#24A148");canvas.FillCircle(left+82,8,4);canvas.DrawString("Diastolic",left+90,0,65,16,HorizontalAlignment.Left,VerticalAlignment.Center);}
    }
    static void DrawSeries(ICanvas canvas,IReadOnlyList<double> values,Color color,float left,float top,float width,float height,double min,double max)
    {
        var valid=values.Select((value,index)=>(value,index)).Where(x=>!double.IsNaN(x.value)).ToList();if(valid.Count==0)return;canvas.StrokeColor=color;canvas.FillColor=color;canvas.StrokeSize=3;canvas.StrokeLineCap=LineCap.Round;
        PointF Point((double value,int index) item){var x=values.Count==1?left+width/2:left+(float)item.index/(values.Count-1)*width;var y=top+(float)((max-item.value)/(max-min))*height;return new PointF(x,y);}
        var previous=Point(valid[0]);canvas.FillCircle(previous.X,previous.Y,4);foreach(var item in valid.Skip(1)){var current=Point(item);canvas.DrawLine(previous.X,previous.Y,current.X,current.Y);canvas.FillCircle(current.X,current.Y,4);previous=current;}
    }
}
public sealed class AddHealthPage : CarePage
{
    public AddHealthPage(ApiClient api,Guid? circleId=null):base(api,"Add Health Reading"){var c=new CirclePickerView(api);var type=new Picker{ItemsSource=new[]{"Blood Pressure","Weight","Glucose","Temperature"},SelectedIndex=0,BackgroundColor=Colors.White};var value=Field("Primary value");value.Keyboard=Keyboard.Numeric;var second=Field("Secondary value (optional)");second.Keyboard=Keyboard.Numeric;var pulse=Field("Pulse (optional)");pulse.Keyboard=Keyboard.Numeric;Body.Add(c);Body.Add(type);Body.Add(value);Body.Add(second);Body.Add(pulse);var save=CareTheme.Primary("Add Reading");save.Clicked+=async(_,_)=>{if(c.SelectedId is null||!decimal.TryParse(value.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var v)){await DisplayAlertAsync("Required","Choose a recipient and enter a numeric value.","OK");return;}decimal? v2=decimal.TryParse(second.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var sv)?sv:null;int? pv=int.TryParse(pulse.Text,out var pi)?pi:null;try{save.IsEnabled=false;await Api.PostAsync("api/health-readings",new{careCircleId=c.SelectedId,type=type.SelectedItem?.ToString(),value=v,secondaryValue=v2,pulse=pv,recordedAt=DateTimeOffset.Now});await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}};Body.Add(save);Appearing+=async(_,_)=>await c.LoadAsync(circleId);}
}

public sealed class LiveDocumentsPage : LiveListPage
{
    public LiveDocumentsPage(ApiClient api):base(api,"Documents","api/documents","No documents have been added.",CareTheme.AddAction(),true){WireAdd(new AddDocumentPage(api));}
    protected override void Render(JsonElement x)=>Items.Add(CareTheme.Card(CareTheme.Row("▣",S(x,"name"),$"{S(x,"category")} • Added {x.GetProperty("createdAt").GetDateTimeOffset().ToLocalTime():MMM d}",null,false)));
}
public sealed class AddDocumentPage : CarePage
{
    public AddDocumentPage(ApiClient api):base(api,"Add Document"){var c=new CirclePickerView(api);var name=Field("Document name");var category=new Picker{ItemsSource=new[]{"Medical","Insurance","Legal","Other"},SelectedIndex=0,BackgroundColor=Colors.White};var notes=new Editor{Placeholder="Notes (optional)",HeightRequest=100,BackgroundColor=Colors.White};Body.Add(c);Body.Add(name);Body.Add(category);Body.Add(notes);Body.Add(CareTheme.Text("This release saves document metadata. File upload storage will be connected separately.",11,false,CareTheme.Muted));var save=CareTheme.Primary("Add Document");save.Clicked+=async(_,_)=>{if(c.SelectedId is null||string.IsNullOrWhiteSpace(name.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a document name.","OK");return;}try{save.IsEnabled=false;await Api.PostAsync("api/documents",new{careCircleId=c.SelectedId,name=name.Text.Trim(),category=category.SelectedItem?.ToString(),notes=notes.Text});await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}};Body.Add(save);Appearing+=async(_,_)=>await c.LoadAsync();}
}

public sealed class LinkedDocumentsPage : CarePage
{
    readonly Guid careCircleId;readonly Guid? taskId;readonly Guid? appointmentId;readonly Guid? medicationId;readonly VerticalStackLayout documents=new(){Spacing=7};bool autoUploadPending;
    public LinkedDocumentsPage(ApiClient api,Guid careCircleId,Guid? taskId,Guid? appointmentId,string itemTitle,bool startUpload=false,Guid? medicationId=null):base(api,$"{itemTitle} Documents",CareTheme.PlusAction("Upload a new document"))
    {
        this.careCircleId=careCircleId;this.taskId=taskId;this.appointmentId=appointmentId;this.medicationId=medicationId;autoUploadPending=startUpload;Body.Add(documents);var header=Header;if(header.Children.LastOrDefault() is ImageButton add)add.Clicked+=async(_,_)=>await Upload();Appearing+=async(_,_)=>{await Load();if(autoUploadPending){autoUploadPending=false;await Upload();}};
    }
    async Task Load()
    {
        documents.Clear();documents.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});
        try{var path=taskId is not null?$"api/documents?taskId={taskId}":appointmentId is not null?$"api/documents?appointmentId={appointmentId}":$"api/documents?medicationId={medicationId}";var rows=await Api.GetAsync(path);documents.Clear();foreach(var x in rows.EnumerateArray()){var id=x.GetProperty("id").GetGuid();var name=x.GetProperty("name").GetString()??"Document";var type=x.TryGetProperty("contentType",out var contentType)&&contentType.ValueKind!=JsonValueKind.Null?contentType.GetString():"Document";var row=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(44)},ColumnSpacing=8};var details=CareTheme.Card(CareTheme.Row("▣",name,$"{type} • {x.GetProperty("createdAt").GetDateTimeOffset().ToLocalTime():MMM d, yyyy}",null,false));details.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new DocumentViewerPage(Api,id,name))));var trash=new ImageButton{Source="trash_red.png",BackgroundColor=Colors.Transparent,WidthRequest=40,HeightRequest=40,Padding=9,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center};ToolTipProperties.SetText(trash,$"Delete {name}");SemanticProperties.SetDescription(trash,$"Delete {name}");trash.Clicked+=async(_,_)=>await Delete(id,name);row.Add(details,0,0);row.Add(trash,1,0);documents.Add(row);}if(documents.Count==0)documents.Add(CareTheme.Card(CareTheme.Text("No documents are linked yet. Use the plus icon to upload one.",13,false,CareTheme.Muted)));}catch(Exception ex){documents.Clear();documents.Add(CareTheme.Card(CareTheme.Text($"Could not load documents.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
    async Task Delete(Guid id,string name)
    {
        if(!await DisplayAlertAsync("Delete document?",$"Are you sure you want to permanently delete {name}?","Delete","Cancel"))return;
        try{await Api.DeleteAsync($"api/documents/{id}");await Load();}catch(Exception ex){await DisplayAlertAsync("Could not delete document",ex.Message,"OK");}
    }
    async Task Upload()
    {
        try
        {
            var file=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="Choose a document"});if(file is null)return;await using var input=await file.OpenReadAsync();using var memory=new MemoryStream();await input.CopyToAsync(memory);if(memory.Length>10*1024*1024){await DisplayAlertAsync("File too large","Documents are limited to 10 MB.","OK");return;}var type=string.IsNullOrWhiteSpace(file.ContentType)?MimeType(file.FileName):file.ContentType;var category=taskId is not null?"Task attachment":appointmentId is not null?"Appointment attachment":"Medication attachment";await Api.PostAsync("api/documents",new{careCircleId,taskId,appointmentId,medicationId,name=file.FileName,category,notes=(string?)null,contentType=type,contentBase64=Convert.ToBase64String(memory.ToArray())});await Load();
        }
        catch(Exception ex){await DisplayAlertAsync("Could not upload document",ex.Message,"OK");}
    }
    static string MimeType(string name)=>Path.GetExtension(name).ToLowerInvariant() switch{".pdf"=>"application/pdf",".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".gif"=>"image/gif",".txt"=>"text/plain",".json"=>"application/json",".csv"=>"text/csv",_=>"application/octet-stream"};
}

public sealed class DocumentViewerPage : CarePage
{
    readonly Guid documentId;readonly string documentName;bool loaded;
    public DocumentViewerPage(ApiClient api,Guid documentId,string documentName):base(api,documentName){this.documentId=documentId;this.documentName=documentName;Appearing+=Load;}
    async void Load(object? sender,EventArgs e)
    {
        if(loaded)return;loaded=true;
        try
        {
            var item=await Api.GetAsync($"api/documents/{documentId}");var base64=item.TryGetProperty("contentBase64",out var content)&&content.ValueKind!=JsonValueKind.Null?content.GetString():null;var type=item.TryGetProperty("contentType",out var contentType)&&contentType.ValueKind!=JsonValueKind.Null?contentType.GetString()??"application/octet-stream":"application/octet-stream";if(string.IsNullOrWhiteSpace(base64)){Body.Add(CareTheme.Card(CareTheme.Text("This document has metadata only and no uploaded file content.",13,false,CareTheme.Muted)));return;}var bytes=Convert.FromBase64String(base64);
            if(type.StartsWith("image/",StringComparison.OrdinalIgnoreCase)){Body.Add(new Image{Source=ImageSource.FromStream(()=>new MemoryStream(bytes)),Aspect=Aspect.AspectFit,HeightRequest=600});return;}
            if(type.StartsWith("text/",StringComparison.OrdinalIgnoreCase)||type is "application/json"){Body.Add(CareTheme.Card(CareTheme.Text(System.Text.Encoding.UTF8.GetString(bytes),13)));return;}
            if(type=="application/pdf"){var html=$"<html><body style='margin:0'><embed width='100%' height='100%' type='application/pdf' src='data:application/pdf;base64,{base64}'/></body></html>";Body.Add(new WebView{Source=new HtmlWebViewSource{Html=html},HeightRequest=650});return;}
            var open=CareTheme.Primary("Open Document");open.Clicked+=async(_,_)=>{var safeName=Path.GetFileName(documentName);var path=Path.Combine(FileSystem.CacheDirectory,safeName);await File.WriteAllBytesAsync(path,bytes);await Launcher.Default.OpenAsync(new OpenFileRequest(documentName,new ReadOnlyFile(path)));};Body.Add(CareTheme.Card(CareTheme.Text($"Preview is not available for {type}.",13,false,CareTheme.Muted)));Body.Add(open);
        }
        catch(Exception ex){Body.Add(CareTheme.Card(CareTheme.Text($"Could not display document.\n{ex.Message}",12,false,CareTheme.Red)));}
    }
}

public sealed class LiveConversationsPage : LiveListPage
{
    public LiveConversationsPage(ApiClient api):base(api,"Messages","api/conversations","No conversations have been started.",CareTheme.AddAction("New"),true){WireAdd(new AddConversationPage(api));}
    protected override void Render(JsonElement x){var id=x.GetProperty("id").GetGuid();var title=S(x,"title");var c=CareTheme.Card(CareTheme.Row("💬",title,"Open conversation"));c.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new LiveChatPage(Api,id,title))));Items.Add(c);}
}
public sealed class AddConversationPage : CarePage
{
    public AddConversationPage(ApiClient api):base(api,"New Conversation"){var c=new CirclePickerView(api);var title=Field("Conversation title");Body.Add(c);Body.Add(title);var save=CareTheme.Primary("Create Conversation");save.Clicked+=async(_,_)=>{if(c.SelectedId is null||string.IsNullOrWhiteSpace(title.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a title.","OK");return;}try{save.IsEnabled=false;await Api.PostAsync("api/conversations",new{careCircleId=c.SelectedId,title=title.Text.Trim()});await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}};Body.Add(save);Appearing+=async(_,_)=>await c.LoadAsync();}
}
public sealed class LiveChatPage : CarePage
{
    readonly Guid id;readonly VerticalStackLayout messages=new(){Spacing=7};
    public LiveChatPage(ApiClient api,Guid id,string title):base(api,title){this.id=id;Body.Add(messages);var text=new Editor{Placeholder="Write a message…",HeightRequest=80,BackgroundColor=Colors.White};Body.Add(text);var send=CareTheme.Primary("Send");send.Clicked+=async(_,_)=>{if(string.IsNullOrWhiteSpace(text.Text))return;try{await Api.PostAsync("api/messages",new{conversationId=id,body=text.Text.Trim()});text.Text="";await Load();}catch(Exception ex){await DisplayAlertAsync("Could not send",ex.Message,"OK");}};Body.Add(send);Appearing+=async(_,_)=>await Load();}
    async Task Load(){messages.Clear();try{var j=await Api.GetAsync($"api/conversations/{id}/messages");foreach(var x in j.EnumerateArray())messages.Add(CareTheme.Card(CareTheme.Text(x.GetProperty("body").GetString()??"",13)));if(messages.Count==0)messages.Add(CareTheme.Text("No messages yet.",13,false,CareTheme.Muted));}catch(Exception ex){messages.Add(CareTheme.Text(ex.Message,12,false,CareTheme.Red));}}
}

public sealed class LiveHandoffsPage : LiveListPage
{
    public LiveHandoffsPage(ApiClient api,Guid? circleId=null,string? recipientName=null):base(api,circleId is null?"Care Handoffs":$"{recipientName} Handoffs",circleId is null?"api/handoffs":$"api/handoffs?careCircleId={circleId}","No care handoffs have been created.",CareTheme.AddAction("New"),circleId is null,circleId is null?"Sent by me":null){WireAdd(new AddHandoffPage(api,null,circleId));}
    protected override void Render(JsonElement x){var id=x.GetProperty("id").GetGuid();var status=S(x,"statusName");var coverage=Range(x);var detail=$"{S(x,"recipientName")} • {S(x,"fromName")} → {S(x,"toName")}\n{status}{(string.IsNullOrWhiteSpace(coverage)?"":$" • {coverage}")}";var color=status switch{"Accepted" or "Active" or "Completed"=>Color.FromArgb("#DDF5E8"),"Declined" or "Cancelled"=>Color.FromArgb("#FFE8E9"),"Draft"=>Color.FromArgb("#EEF0F3"),_=>Color.FromArgb("#FFF4D9")};var icon=status switch{"Completed"=>"✓","Active"=>"●","Sent"=>"◷",_=>"🔄"};var card=CareTheme.Card(CareTheme.Row(icon,S(x,"summary"),detail,color,false));card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new HandoffDetailPage(Api,id))));var canCancel=status=="Sent"&&x.TryGetProperty("canCancel",out var allowed)&&allowed.GetBoolean();var description=canCancel?"Cancel pending handoff":"Only a pending handoff sent by you can be cancelled";var cancel=new ImageButton{Source=new FontImageSource{Glyph="⊘",Color=canCancel?CareTheme.Red:Color.FromArgb("#AAB2BF"),Size=22},BackgroundColor=Colors.Transparent,Padding=8,WidthRequest=42,HeightRequest=42,IsEnabled=canCancel,Opacity=canCancel?1:.35,AutomationId=description};ToolTipProperties.SetText(cancel,description);SemanticProperties.SetDescription(cancel,description);if(canCancel)cancel.Clicked+=async(_,_)=>await Cancel(id,S(x,"summary"));var row=new Grid{ColumnDefinitions=[new(GridLength.Star),new(46)],ColumnSpacing=4};row.Add(card,0,0);row.Add(cancel,1,0);Items.Add(row);}
    async Task Cancel(Guid id,string summary){if(!await DisplayAlertAsync("Cancel handoff?",$"Cancel the pending handoff “{summary}”?","Cancel Handoff","Keep"))return;try{await Api.PutAsync($"api/handoffs/{id}/cancel",new{});await Refresh();}catch(Exception ex){await DisplayAlertAsync("Could not cancel handoff",ex.Message,"OK");}}
    static string Range(JsonElement x){if(!x.TryGetProperty("coverageStartsAt",out var start)||start.ValueKind==JsonValueKind.Null)return "";var from=start.GetDateTimeOffset().ToLocalTime();if(!x.TryGetProperty("coverageEndsAt",out var end)||end.ValueKind==JsonValueKind.Null)return $"Starts {from:MMM d, h:mm tt}";var to=end.GetDateTimeOffset().ToLocalTime();return from.Date==to.Date?$"{from:MMM d, h:mm tt}–{to:h:mm tt}":$"{from:MMM d, h:mm tt}–{to:MMM d, h:mm tt}";}
}
public sealed class AddHandoffPage : CarePage
{
    readonly Guid? handoffId;readonly Guid? initialCircleId;readonly CirclePickerView circle;readonly Picker recipient=new(){Title="Caregiver receiving handoff",BackgroundColor=CareTheme.CardSurface};readonly List<Guid> userIds=[];readonly Editor summary=new(){Placeholder="What does the next caregiver need to know?",HeightRequest=100,BackgroundColor=CareTheme.CardSurface};readonly Editor risks=new(){Placeholder="Safety concerns, symptoms or risks (optional)",HeightRequest=80,BackgroundColor=CareTheme.CardSurface};readonly Editor next=new(){Placeholder="Tasks and follow-up steps (optional)",HeightRequest=80,BackgroundColor=CareTheme.CardSurface};readonly DatePicker startDate=new(){Date=DateTime.Today,BackgroundColor=CareTheme.CardSurface};readonly TimePicker startTime=new(){Time=DateTime.Now.TimeOfDay,Format="h:mm tt",BackgroundColor=CareTheme.CardSurface};readonly DatePicker endDate=new(){Date=DateTime.Today,BackgroundColor=CareTheme.CardSurface};readonly TimePicker endTime=new(){Time=DateTime.Now.AddHours(8).TimeOfDay,Format="h:mm tt",BackgroundColor=CareTheme.CardSurface};bool loaded;
    public AddHandoffPage(ApiClient api,Guid? handoffId=null,Guid? initialCircleId=null):base(api,handoffId is null?"New Handoff":"Edit Handoff")
    {
        this.handoffId=handoffId;this.initialCircleId=initialCircleId;PageLayout.SafeAreaEdges=new SafeAreaEdges(SafeAreaRegions.SoftInput);BodyScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Always;circle=new(api);Body.Add(circle);Body.Add(CareTheme.Text("Receiving caregiver",12,false,CareTheme.Muted));Body.Add(recipient);Body.Add(CareTheme.Text("Coverage starts",12,false,CareTheme.Muted));Body.Add(DateTimeRow(startDate,startTime));Body.Add(CareTheme.Text("Coverage ends",12,false,CareTheme.Muted));Body.Add(DateTimeRow(endDate,endTime));Body.Add(CareTheme.Text("Handoff brief",14,true));Body.Add(summary);Body.Add(risks);Body.Add(next);Body.Add(CareTheme.Text("CareTogether will include medications, open tasks and appointments scheduled during this coverage window in the review brief.",11,false,CareTheme.Muted));var reload=CareTheme.Small("Load Caregivers");reload.Clicked+=async(_,_)=>await LoadCaregivers(circle.SelectedId);Body.Add(reload);var buttons=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=8};var draft=CareTheme.Primary("Save Draft");draft.BackgroundColor=CareTheme.Muted;var send=CareTheme.Primary("Review & Send");draft.Clicked+=async(_,_)=>await Save(false,draft,send);send.Clicked+=async(_,_)=>await Save(true,draft,send);buttons.Add(draft,0,0);buttons.Add(send,1,0);Body.Add(buttons);KeepVisible(summary);KeepVisible(risks);KeepVisible(next);circle.SelectedIdChanged+=async id=>await LoadCaregivers(id);Appearing+=Load;
    }
    async void Load(object? sender,EventArgs e){if(loaded)return;loaded=true;try{if(handoffId is null){await circle.LoadAsync(initialCircleId);return;}var item=await Api.GetAsync($"api/handoffs/{handoffId}");var circleId=item.GetProperty("careCircleId").GetGuid();var to=item.GetProperty("toUserId").GetGuid();await circle.LoadAsync(circleId);await LoadCaregivers(circleId,to);summary.Text=S(item,"summary");risks.Text=S(item,"risks");next.Text=S(item,"nextSteps");SetDateTime(item,"coverageStartsAt",startDate,startTime);SetDateTime(item,"coverageEndsAt",endDate,endTime);circle.SelectionEnabled=false;}catch(Exception ex){await DisplayAlertAsync("Could not load handoff",ex.Message,"OK");}}
    async Task LoadCaregivers(Guid? circleId,Guid? selected=null){if(circleId is null)return;recipient.Items.Clear();userIds.Clear();var data=await Api.GetAsync($"api/care-circles/{circleId}");foreach(var m in data.GetProperty("members").EnumerateArray()){if(m.GetProperty("isActive").GetBoolean()&&!m.GetProperty("isCurrentUser").GetBoolean()){var id=m.GetProperty("userId").GetGuid();userIds.Add(id);recipient.Items.Add(m.GetProperty("displayName").GetString()??"Caregiver");}}recipient.SelectedIndex=selected is null?(userIds.Count>0?0:-1):userIds.IndexOf(selected.Value);}
    async Task Save(bool send,Button draft,Button sendButton){if(circle.SelectedId is null||recipient.SelectedIndex<0||string.IsNullOrWhiteSpace(summary.Text)){await DisplayAlertAsync("Required","Choose a care recipient and receiving caregiver, and enter a summary.","OK");return;}var starts=(startDate.Date??DateTime.Today).Add(startTime.Time??TimeSpan.Zero);var ends=(endDate.Date??DateTime.Today).Add(endTime.Time??TimeSpan.Zero);if(ends<=starts){await DisplayAlertAsync("Coverage window","Coverage must end after it starts.","OK");return;}try{draft.IsEnabled=sendButton.IsEnabled=false;var value=new{careCircleId=circle.SelectedId,toUserId=userIds[recipient.SelectedIndex],summary=summary.Text.Trim(),risks=risks.Text,nextSteps=next.Text,coverageStartsAt=new DateTimeOffset(starts),coverageEndsAt=new DateTimeOffset(ends),send};var result=handoffId is null?await Api.PostAsync("api/handoffs",value):await Api.PutAsync($"api/handoffs/{handoffId}",value);if(send&&result.TryGetProperty("notificationDelivery",out var delivery)&&delivery.TryGetProperty("message",out var message)&&!delivery.GetProperty("sent").GetBoolean())await DisplayAlertAsync("Handoff sent",$"The in-app notification was created. {message.GetString()}","OK");await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save handoff",ex.Message,"OK");}finally{draft.IsEnabled=sendButton.IsEnabled=true;}}
    static Grid DateTimeRow(DatePicker date,TimePicker time){var row=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=8};row.Add(date,0,0);row.Add(time,1,0);return row;}
    void KeepVisible(VisualElement field)=>field.Focused+=async(_,_)=>{await Task.Delay(300);await BodyScroll.ScrollToAsync(field,ScrollToPosition.MakeVisible,true);};
    static void SetDateTime(JsonElement item,string property,DatePicker date,TimePicker time){if(item.TryGetProperty(property,out var value)&&value.ValueKind!=JsonValueKind.Null){var local=value.GetDateTimeOffset().ToLocalTime();date.Date=local.Date;time.Time=local.TimeOfDay;}}
    static string S(JsonElement x,string name)=>x.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??"":"";
}

public sealed class HandoffDetailPage : CarePage
{
    readonly Guid id;readonly VerticalStackLayout content=new(){Spacing=10};bool busy;
    public HandoffDetailPage(ApiClient api,Guid id):base(api,"Handoff Details"){this.id=id;PageLayout.SafeAreaEdges=new SafeAreaEdges(SafeAreaRegions.SoftInput);BodyScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Always;Body.Add(content);Appearing+=async(_,_)=>await Load();}
    async Task Load(){if(busy)return;busy=true;content.Clear();content.Add(new ActivityIndicator{IsRunning=true,Color=CareTheme.Blue});try{var x=await Api.GetAsync($"api/handoffs/{id}");content.Clear();var status=S(x,"statusName");content.Add(Lifecycle(status));var coverage=Coverage(x);content.Add(CareTheme.Card(CareTheme.Row(status=="Completed"?"✓":status=="Active"?"●":"🔄",S(x,"recipientName"),$"{S(x,"fromName")} → {S(x,"toName")}\n{status}{(string.IsNullOrWhiteSpace(coverage)?"":$" • {coverage}")}",StatusColor(status),false)));AddSection("Handoff summary",S(x,"summary"));AddSection("Risks and safety concerns",S(x,"risks"));AddSection("Next steps",S(x,"nextSteps"));if(x.TryGetProperty("careBrief",out var brief))AddCareBrief(brief);if(B(x,"canRespond"))AddAcceptanceChecklist();if(B(x,"canActivate")){var activate=CareTheme.Primary("Start Care Coverage");activate.Clicked+=async(_,_)=>await Act("activate","Start this care coverage now?",new{});content.Add(activate);}if(B(x,"canComplete"))AddCompletion();if(status=="Completed"){AddSection("Completion report",S(x,"completionNotes"));AddSection("Unresolved items",S(x,"unresolvedItems"));}if(B(x,"canCarryForward")){var carry=CareTheme.Primary("Carry Unresolved Items Forward");carry.Clicked+=async(_,_)=>await CarryForward();content.Add(carry);}if(B(x,"canEdit")){var edit=CareTheme.Primary("Edit Handoff");edit.Clicked+=async(_,_)=>await Navigation.PushAsync(new AddHandoffPage(Api,id));content.Add(edit);}if(B(x,"canCancel")){var cancel=CareTheme.Primary("Cancel Handoff");cancel.BackgroundColor=CareTheme.Red;cancel.Clicked+=async(_,_)=>await Act("cancel","Cancel this handoff?",new{});content.Add(cancel);}}catch(Exception ex){content.Clear();content.Add(CareTheme.Text($"Could not load handoff.\n{ex.Message}",12,false,CareTheme.Red));}finally{busy=false;}}
    void AddAcceptanceChecklist()
    {
        content.Add(CareTheme.Text("Review and confirm",15,true));content.Add(CareTheme.Text("All required items must be reviewed before responsibility can be accepted.",12,false,CareTheme.Muted));
        var medication=Confirm("Medication schedule reviewed");var safety=Confirm("Risks and safety concerns reviewed");var schedule=Confirm("Tasks and appointments reviewed");var emergency=Confirm("Emergency plan and contacts reviewed");var instructions=Confirm("Instructions and next steps reviewed");var accept=CareTheme.Primary("Accept Responsibility");accept.IsEnabled=false;void Update()=>accept.IsEnabled=new[]{medication,safety,schedule,emergency,instructions}.All(x=>x.IsChecked);foreach(var check in new[]{medication,safety,schedule,emergency,instructions})check.CheckedChanged+=(_,_)=>Update();accept.Clicked+=async(_,_)=>await Act("accept","Accept this handoff and responsibility for the coverage period?",new{medicationReviewed=medication.IsChecked,safetyReviewed=safety.IsChecked,scheduleReviewed=schedule.IsChecked,emergencyPlanReviewed=emergency.IsChecked,instructionsReviewed=instructions.IsChecked});content.Add(CareTheme.Card(new VerticalStackLayout{Spacing=5,Children={CheckRow(medication,"Medication schedule reviewed"),CheckRow(safety,"Risks and safety concerns reviewed"),CheckRow(schedule,"Tasks and appointments reviewed"),CheckRow(emergency,"Emergency plan and contacts reviewed"),CheckRow(instructions,"Instructions and next steps reviewed")}}));var buttons=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=8};var decline=CareTheme.Primary("Decline");decline.BackgroundColor=CareTheme.Red;decline.Clicked+=async(_,_)=>await Act("decline","Decline this handoff?",new{});buttons.Add(decline,0,0);buttons.Add(accept,1,0);content.Add(buttons);
    }
    void AddCompletion()
    {
        content.Add(CareTheme.Text("Complete coverage",15,true));var notes=new Editor{Placeholder="What happened during this coverage period?",HeightRequest=100,BackgroundColor=CareTheme.CardSurface};var unresolved=new Editor{Placeholder="Unresolved items to carry forward (optional)",HeightRequest=90,BackgroundColor=CareTheme.CardSurface};KeepVisible(notes);KeepVisible(unresolved);content.Add(notes);content.Add(unresolved);var complete=CareTheme.Primary("Complete & Report");complete.Clicked+=async(_,_)=>await Act("complete","Complete this care coverage?",new{completionNotes=notes.Text,unresolvedItems=unresolved.Text});content.Add(complete);
    }
    void AddCareBrief(JsonElement brief)
    {
        content.Add(CareTheme.Text("Coverage brief",15,true));var stack=new VerticalStackLayout{Spacing=8};AddBriefGroup(stack,"Medications",brief,"medications",x=>$"{S(x,"name")} • {S(x,"dosage")} • {S(x,"schedule")}");AddBriefGroup(stack,"Tasks due",brief,"tasks",x=>$"{S(x,"title")} • {x.GetProperty("dueAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}");AddBriefGroup(stack,"Appointments",brief,"appointments",x=>$"{S(x,"title")} with {S(x,"provider")} • {x.GetProperty("startsAt").GetDateTimeOffset().ToLocalTime():MMM d, h:mm tt}");content.Add(CareTheme.Card(stack));
    }
    static void AddBriefGroup(VerticalStackLayout stack,string title,JsonElement brief,string property,Func<JsonElement,string> text){stack.Add(CareTheme.Text(title,13,true));var items=brief.GetProperty(property).EnumerateArray().ToList();if(items.Count==0)stack.Add(CareTheme.Text("None during this coverage window",12,false,CareTheme.Muted));else foreach(var item in items)stack.Add(CareTheme.Text($"• {text(item)}",12));}
    static Border Lifecycle(string current)
    {
        var stages=new[]{"Create","Review","Accept","Active","Complete"};var active=current switch{"Draft"=>0,"Sent"=>1,"Accepted"=>2,"Active"=>3,"Completed"=>4,_=>-1};var grid=new Grid{ColumnSpacing=3};foreach(var _ in stages)grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));for(var i=0;i<stages.Length;i++){var label=CareTheme.Text($"{(i<=active?"●":"○")}\n{stages[i]}",10,i==active,i<=active?CareTheme.Blue:CareTheme.Muted);label.HorizontalTextAlignment=TextAlignment.Center;grid.Add(label,i,0);}return CareTheme.Card(grid,Color.FromArgb("#F4F8FF"));
    }
    async Task CarryForward(){if(!await DisplayAlertAsync("Carry forward?","Create a new draft containing the unresolved items from this completed handoff?","Create Draft","Cancel"))return;try{var result=await Api.PostAsync($"api/handoffs/{id}/carry-forward",new{});var newId=result.GetProperty("id").GetGuid();await Navigation.PushAsync(new AddHandoffPage(Api,newId));}catch(Exception ex){await DisplayAlertAsync("Could not carry forward",ex.Message,"OK");}}
    void AddSection(string title,string value){content.Add(CareTheme.Text(title,13,true));content.Add(CareTheme.Card(CareTheme.Text(string.IsNullOrWhiteSpace(value)?"None specified":value,13,false,string.IsNullOrWhiteSpace(value)?CareTheme.Muted:CareTheme.Ink)));}
    async Task Act(string action,string confirmation,object value){var confirm=action switch{"accept"=>"Accept","activate"=>"Start","complete"=>"Complete","decline"=>"Decline",_=>"Continue"};if(!await DisplayAlertAsync("Confirm",confirmation,confirm,"Keep"))return;try{await Api.PutAsync($"api/handoffs/{id}/{action}",value);await Load();}catch(Exception ex){await DisplayAlertAsync("Could not update handoff",ex.Message,"OK");}}
    static CheckBox Confirm(string name)=>new(){AutomationId=name};static HorizontalStackLayout CheckRow(CheckBox check,string label)=>new(){Spacing=6,Children={check,CareTheme.Text(label,12)}};
    void KeepVisible(VisualElement field)=>field.Focused+=async(_,_)=>{await Task.Delay(300);await BodyScroll.ScrollToAsync(field,ScrollToPosition.MakeVisible,true);};
    static bool B(JsonElement x,string name)=>x.TryGetProperty(name,out var value)&&value.GetBoolean();
    static string Coverage(JsonElement x){if(!x.TryGetProperty("coverageStartsAt",out var start)||start.ValueKind==JsonValueKind.Null)return "Coverage time not set";var from=start.GetDateTimeOffset().ToLocalTime();if(!x.TryGetProperty("coverageEndsAt",out var end)||end.ValueKind==JsonValueKind.Null)return $"Starts {from:MMM d, h:mm tt}";var to=end.GetDateTimeOffset().ToLocalTime();return from.Date==to.Date?$"{from:MMM d, h:mm tt}–{to:h:mm tt}":$"{from:MMM d, h:mm tt}–{to:MMM d, h:mm tt}";}
    static Color StatusColor(string status)=>status switch{"Accepted" or "Active" or "Completed"=>Color.FromArgb("#DDF5E8"),"Declined" or "Cancelled"=>Color.FromArgb("#FFE8E9"),"Draft"=>Color.FromArgb("#EEF0F3"),_=>Color.FromArgb("#FFF4D9")};
    static string S(JsonElement x,string name)=>x.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??"":"";
}

public sealed class LiveGroceryPage : LiveListPage
{
    public LiveGroceryPage(ApiClient api):base(api,"Shopping & Groceries","api/groceries","No grocery items have been added.",CareTheme.AddAction(),true)
    {
        WireAdd(new GroceryEditorPage(api));
    }

    protected override void Render(JsonElement x)
    {
        var id=x.GetProperty("id").GetGuid();var purchased=x.GetProperty("isPurchased").GetBoolean();var status=S(x,"assignmentStatusName");var assignee=string.IsNullOrWhiteSpace(S(x,"assigneeName"))?"Unassigned":S(x,"assigneeName");var quantity=string.IsNullOrWhiteSpace(S(x,"quantity"))?"":$" • {S(x,"quantity")}";var notes=S(x,"notes");var neededBy=DateOnly.TryParse(S(x,"neededBy"),out var parsedDate)?parsedDate:DateOnly.FromDateTime(DateTime.Today);var overdue=!purchased&&neededBy<DateOnly.FromDateTime(DateTime.Today);
        var detail=$"{S(x,"recipientName")}{quantity}\n{(overdue?"OVERDUE • ":"Needed by ")}{neededBy:MMM d, yyyy} • {assignee} • {(purchased?"Purchased":status)}";var cardContent=CareTheme.Row(overdue?"!":purchased?"✓":"🛒",S(x,"name"),detail,overdue?Color.FromArgb("#FECACA"):purchased?Color.FromArgb("#DDF5E8"):status=="Declined"?Color.FromArgb("#FFE8E9"):Color.FromArgb("#E8F1FF"),false);
        var actions=new HorizontalStackLayout{Spacing=2,HorizontalOptions=LayoutOptions.End};
        if(x.TryGetProperty("canRespond",out var canRespond)&&canRespond.GetBoolean())
        {
            var decline=Icon("✕",CareTheme.Red,"Decline grocery assignment");decline.Clicked+=async(_,_)=>await Act(id,"decline","Decline this grocery assignment?");actions.Add(decline);
            var accept=Icon("✓",Color.FromArgb("#24A148"),"Accept grocery assignment");accept.Clicked+=async(_,_)=>await Act(id,"accept","Accept this grocery assignment?");actions.Add(accept);
        }
        var edit=Icon("✎",CareTheme.Blue,status=="Declined"?"Reassign grocery item":"Edit or reassign grocery item");edit.Clicked+=async(_,_)=>await Navigation.PushAsync(new GroceryEditorPage(Api,id));actions.Add(edit);
        var done=Icon(purchased?"↶":"✓",purchased?CareTheme.Blue:Color.FromArgb("#24A148"),purchased?"Restore grocery item":"Mark grocery item purchased");done.Clicked+=async(_,_)=>await Act(id,"toggle-purchased",purchased?"Restore this item to the shopping list?":"Mark this item as purchased?");actions.Add(done);
        var trash=Icon("⌫",CareTheme.Red,"Remove grocery item");trash.Clicked+=async(_,_)=>{if(await DisplayAlertAsync("Remove item?",$"Remove “{S(x,"name")}” from the grocery list?","Remove","Keep")){try{await Api.DeleteAsync($"api/groceries/{id}");await Refresh();}catch(Exception ex){await DisplayAlertAsync("Could not remove item",ex.Message,"OK");}}};actions.Add(trash);
        var wrapper=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=2};wrapper.Add(cardContent,0,0);wrapper.Add(actions,1,0);var card=CareTheme.Card(wrapper,overdue?Color.FromArgb("#FFF1F2"):null);card.GestureRecognizers.Add(CareTheme.Tap(()=>Navigation.PushAsync(new GroceryEditorPage(Api,id))));Items.Add(card);
        if(!string.IsNullOrWhiteSpace(notes))Items.Add(CareTheme.Text($"Note: {notes}",12,false,CareTheme.Muted));
    }

    async Task Act(Guid id,string action,string confirmation){if(!await DisplayAlertAsync("Confirm",confirmation,"Continue","Cancel"))return;try{await Api.PutAsync($"api/groceries/{id}/{action}",new{});await Refresh();}catch(Exception ex){await DisplayAlertAsync("Could not update item",ex.Message,"OK");}}
    static ImageButton Icon(string glyph,Color color,string description){var button=new ImageButton{Source=new FontImageSource{Glyph=glyph,Color=color,Size=21},BackgroundColor=Colors.Transparent,Padding=8,HeightRequest=40,WidthRequest=40,AutomationId=description};ToolTipProperties.SetText(button,description);SemanticProperties.SetDescription(button,description);return button;}
}

public sealed class GroceryEditorPage : CarePage
{
    readonly Guid? itemId;readonly CirclePickerView circle;readonly CareMemberPickerView assignee;readonly Entry name=Field("Item name");readonly Entry quantity=Field("Quantity (for example, 2 boxes)");readonly DatePicker neededBy=new(){Date=DateTime.Today.AddDays(1),MinimumDate=DateTime.Today.AddYears(-1),BackgroundColor=Colors.White};readonly Editor notes=new(){Placeholder="Notes or preferred brand (optional)",HeightRequest=90,BackgroundColor=Colors.White};bool loaded;
    public GroceryEditorPage(ApiClient api,Guid? itemId=null):base(api,itemId is null?"Add Grocery Item":"Edit Grocery Item")
    {
        this.itemId=itemId;circle=new(api);assignee=new(api);Body.Add(circle);Body.Add(name);Body.Add(quantity);Body.Add(CareTheme.Text("Needed By",12,false,CareTheme.Muted));Body.Add(neededBy);Body.Add(notes);Body.Add(assignee);var save=CareTheme.Primary(itemId is null?"Add Item":"Save Changes");save.Clicked+=async(_,_)=>await Save(save);Body.Add(save);circle.SelectedIdChanged+=async id=>await assignee.LoadAsync(id);Appearing+=Load;
    }
    async void Load(object? sender,EventArgs args)
    {
        if(loaded)return;loaded=true;try{if(itemId is null){await circle.LoadAsync();await assignee.LoadAsync(circle.SelectedId);return;}var x=await Api.GetAsync($"api/groceries/{itemId}");var circleId=x.GetProperty("careCircleId").GetGuid();await circle.LoadAsync(circleId);Guid? assigneeId=x.TryGetProperty("assignedToUserId",out var assigned)&&assigned.ValueKind!=JsonValueKind.Null?assigned.GetGuid():null;await assignee.LoadAsync(circleId,assigneeId);name.Text=S(x,"name");quantity.Text=S(x,"quantity");notes.Text=S(x,"notes");if(DateOnly.TryParse(S(x,"neededBy"),out var date))neededBy.Date=date.ToDateTime(TimeOnly.MinValue);}catch(Exception ex){await DisplayAlertAsync("Could not load item",ex.Message,"OK");}
    }
    async Task Save(Button button)
    {
        if(circle.SelectedId is null||string.IsNullOrWhiteSpace(name.Text)){await DisplayAlertAsync("Required","Choose a care recipient and enter an item name.","OK");return;}try{button.IsEnabled=false;var value=new{careCircleId=circle.SelectedId,name=name.Text.Trim(),quantity=quantity.Text,notes=notes.Text,neededBy=DateOnly.FromDateTime(neededBy.Date??DateTime.Today),assignedToUserId=assignee.SelectedUserId};if(itemId is null)await Api.PostAsync("api/groceries",value);else await Api.PutAsync($"api/groceries/{itemId}",value);await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save item",ex.Message,"OK");}finally{button.IsEnabled=true;}
    }
    static string S(JsonElement x,string key)=>x.TryGetProperty(key,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??"":"";
}

public sealed class LiveEmergencyPage : LiveListPage
{
    public LiveEmergencyPage(ApiClient api):base(api,"Emergency Contacts","api/emergency-contacts","No emergency contacts have been added.",CareTheme.AddAction(),true){WireAdd(new AddEmergencyContactPage(api));}
    protected override void Render(JsonElement x)=>Items.Add(CareTheme.Card(CareTheme.Row(x.GetProperty("isPrimary").GetBoolean()?"!":"☎",S(x,"name"),$"{S(x,"relationship")} • {S(x,"phone")}",x.GetProperty("isPrimary").GetBoolean()?Color.FromArgb("#FFE0E1"):null,false)));
}
public sealed class AddEmergencyContactPage : CarePage
{
    public AddEmergencyContactPage(ApiClient api):base(api,"Add Emergency Contact"){var c=new CirclePickerView(api);var name=Field("Name");var relationship=Field("Relationship");var phone=Field("Phone number");phone.Keyboard=Keyboard.Telephone;var primary=new CheckBox();Body.Add(c);Body.Add(name);Body.Add(relationship);Body.Add(phone);Body.Add(new HorizontalStackLayout{Children={primary,CareTheme.Text("Primary emergency contact")}});var save=CareTheme.Primary("Add Contact");save.Clicked+=async(_,_)=>{if(c.SelectedId is null||string.IsNullOrWhiteSpace(name.Text)||string.IsNullOrWhiteSpace(phone.Text)){await DisplayAlertAsync("Required","Choose a recipient and enter a name and phone number.","OK");return;}try{save.IsEnabled=false;await Api.PostAsync("api/emergency-contacts",new{careCircleId=c.SelectedId,name=name.Text.Trim(),relationship=relationship.Text??"",phone=phone.Text.Trim(),isPrimary=primary.IsChecked});await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Could not save",ex.Message,"OK");}finally{save.IsEnabled=true;}};Body.Add(save);Appearing+=async(_,_)=>await c.LoadAsync();}
}
