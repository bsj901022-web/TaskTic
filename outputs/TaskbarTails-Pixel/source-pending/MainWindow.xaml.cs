using System;
using System.ComponentModel;
using System.Windows;

namespace TaskbarTails;

public partial class MainWindow : Window
{
    readonly App app;
    bool ready;
    public MainWindow(App owner)
    {
        app = owner; InitializeComponent();
        NameBox.Text = app.State.Name; SpeciesBox.Items.Clear(); foreach(var k in PetCatalog.All) SpeciesBox.Items.Add(k.Label); SpeciesBox.SelectedIndex = Array.FindIndex(PetCatalog.All,k=>k.Id==app.State.Species);
        FriendsCheck.IsChecked = app.State.DemoFriends; ScaleBox.SelectedIndex = app.State.Scale == 150 ? 1 : app.State.Scale == 200 ? 2 : 0; ready = true;
        Refresh();
        Closing += OnClosing;
    }
    void OnClosing(object? sender, CancelEventArgs e) { if (!app.Exiting) { e.Cancel = true; Hide(); } }
    public void Refresh()
    {
        var s = app.State;
        PetTitle.Text = s.Name; LevelLabel.Text = "Lv. " + s.Level;
        FoodLabel.Text = $"{s.Fullness:0} / 100"; FoodBar.Value = s.Fullness;
        HappyLabel.Text = $"{s.Happiness:0} / 100"; HappyBar.Value = s.Happiness;
        XpLabel.Text = $"{s.Experience % 100} / 100"; XpBar.Value = s.Experience % 100;
        MoodLabel.Text = s.Sleeping ? "쉿, 기분 좋은 꿈을 꾸고 있어요." : s.Fullness < 25 ? "배가 고파요. 간식 시간을 기다려요!" : "오늘도 함께 놀 준비 완료!";
        SleepButton.Content = s.Sleeping ? "깨우기" : "재우기";
        var kind=PetCatalog.Get(s.Species); ActionOne.Content=kind.FirstLabel; ActionTwo.Content=kind.SecondLabel;
        Preview.Species = s.Species; Preview.Sleeping = s.Sleeping; Preview.ShowName = false; Preview.FrontView = true;
        Preview.InvalidateVisual();
        VisibilityButton.Content = app.PetsVisible ? "캐릭터 숨기기" : "캐릭터 보이기";
        RoomInfo.Text = app.RoomSummary; RoomInfo.Visibility = RoomInfo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (StateStore.LastError != null) Notice.Text = StateStore.LastError;
    }
    public void Animate(double time) { if (!IsVisible) return; Preview.Phase = time; Preview.InvalidateVisual(); }
    void Feed_Click(object sender, RoutedEventArgs e) => app.Feed();
    void Play_Click(object sender, RoutedEventArgs e) => app.Play();
    void Sleep_Click(object sender, RoutedEventArgs e) => app.ToggleSleep();
    void Apply_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { Notice.Text = "친구의 이름을 입력해 주세요."; return; }
        app.State.Name = name; app.State.Species = PetCatalog.All[Math.Max(0, SpeciesBox.SelectedIndex)].Id;
        app.Changed("새 모습이 마음에 들어!"); Notice.Text = "친구의 이름과 모습을 저장했어요.";
    }
    void Scale_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!ready) return;
        app.State.Scale = ScaleBox.SelectedIndex switch { 1 => 150, 2 => 200, _ => 100 };
        StateStore.Save(app.State); Notice.Text = "작업표시줄 캐릭터 크기를 " + app.State.Scale + "%로 바꿨어요.";
    }
    void Friends_Changed(object sender, RoutedEventArgs e) { if (ready) app.SetFriends(FriendsCheck.IsChecked == true); }
    void Visibility_Click(object sender, RoutedEventArgs e) => app.ToggleVisible();
    void ActionOne_Click(object sender, RoutedEventArgs e)=>app.Specialty(0);
    void ActionTwo_Click(object sender, RoutedEventArgs e)=>app.Specialty(1);
    void Room_Click(object sender, RoutedEventArgs e)=>app.ShowRoom();
    void Bubble_Click(object sender, RoutedEventArgs e) => SendBubble();
    void Bubble_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { SendBubble(); e.Handled = true; } }
    void SendBubble()
    {
        var text = BubbleBox.Text.Trim(); if (text.Length == 0) { Notice.Text = "말풍선에 넣을 내용을 입력해 주세요."; return; }
        app.SendBubble(text); BubbleBox.Clear();
        Notice.Text = app.Room?.Connected == true ? "말풍선을 방 친구들에게 보냈어요." : "말풍선을 표시했어요. 방에 연결하면 친구에게도 보여요.";
    }
    void Quit_Click(object sender, RoutedEventArgs e) => app.Quit();
}


