using System.Collections.ObjectModel;
using System.Text.Json;
using MyAndroidApp.Models;
using MyAndroidApp.Services;

namespace MyAndroidApp;

public class MatchSettingItem
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public bool IsEnabled { get; set; }
}

public partial class SettingsPage : ContentPage
{
    private readonly MemberDatabase _database;
    public ObservableCollection<MatchSettingItem> MatchSettings { get; set; } = new ObservableCollection<MatchSettingItem>();
    private MatchSettingItem _selectedItem;
    private Frame _selectedFrame;

    public SettingsPage(MemberDatabase database)
    {
        InitializeComponent();
        _database = database;
        if (SettingsCollectionView != null)
            SettingsCollectionView.ItemsSource = MatchSettings;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // コート数の読み込み
        int savedCourtCount = Preferences.Default.Get("CourtCount", 2);
        CourtCountEntry.Text = savedCourtCount.ToString();

        // コール回数の読み込み
        int savedCallCount = Preferences.Default.Get("CallCount", 1);
        CallCountEntry.Text = savedCallCount.ToString();

        // 試合時間の読み込み
        string savedMatchTime = Preferences.Default.Get("MatchTime", "無制限");
        MatchTimePicker.SelectedItem = savedMatchTime;

        LoadMatchupSettings();
    }

    private void OnOpenMatchSettingsClicked(object sender, EventArgs e)
    {
        LoadMatchupSettings();
        MatchSettingsOverlay.IsVisible = true;
    }

    private void LoadMatchupSettings()
    {
        string json = Preferences.Default.Get("MatchupSettings", string.Empty);
        List<MatchSettingItem> settings;

        if (string.IsNullOrEmpty(json))
        {
            settings = new List<MatchSettingItem>
            {
                new MatchSettingItem { Key = "PrioritizeGender", DisplayName = "男子/女子ダブルスを優先する", IsEnabled = true },
                new MatchSettingItem { Key = "PrioritizeMixed", DisplayName = "ミックスを優先する", IsEnabled = false },
                new MatchSettingItem { Key = "AvoidSingleFemale", DisplayName = "女性が一人だけにならないようにする", IsEnabled = true },
                new MatchSettingItem { Key = "AvoidSingleMale", DisplayName = "男性が一人だけにならないようにする", IsEnabled = true }
            };
        }
        else
        {
            try { settings = JsonSerializer.Deserialize<List<MatchSettingItem>>(json); }
            catch { settings = new List<MatchSettingItem>(); }
        }

        MatchSettings.Clear();
        if (settings != null)
        {
            foreach (var s in settings) MatchSettings.Add(s);
        }
    }

    private void OnSaveMatchSettingsClicked(object sender, EventArgs e)
    {
        string json = JsonSerializer.Serialize(MatchSettings.ToList());
        Preferences.Default.Set("MatchupSettings", json);
        MatchSettingsOverlay.IsVisible = false;
    }

    private void OnCancelMatchSettingsClicked(object sender, EventArgs e)
    {
        MatchSettingsOverlay.IsVisible = false;
    }

    private async void OnSettingItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label tappedLabel && tappedLabel.BindingContext is MatchSettingItem tappedItem)
        {
            var tappedFrame = (tappedLabel.Parent as Grid)?.Parent as Frame;
            if (_selectedItem == null)
            {
                _selectedItem = tappedItem;
                _selectedFrame = tappedFrame;
                if (_selectedFrame != null)
                {
                    _selectedFrame.BackgroundColor = Color.FromArgb("#2196F3");
                    tappedLabel.TextColor = Colors.White;
                }
                return;
            }
            if (_selectedItem == tappedItem)
            {
                ResetItemLook(_selectedFrame);
                _selectedItem = null;
                _selectedFrame = null;
                return;
            }
            int oldIndex = MatchSettings.IndexOf(_selectedItem);
            int newIndex = MatchSettings.IndexOf(tappedItem);
            if (oldIndex >= 0 && newIndex >= 0)
            {
                MatchSettings.Move(oldIndex, newIndex);
                SettingsCollectionView.ScrollTo(newIndex, position: ScrollToPosition.MakeVisible, animate: true);
            }
            ResetItemLook(_selectedFrame);
            if (tappedFrame != null)
            {
                await tappedFrame.ScaleTo(1.05, 100, Easing.CubicOut);
                await tappedFrame.ScaleTo(1.0, 100, Easing.CubicIn);
            }
            _selectedItem = null;
            _selectedFrame = null;
        }
    }

    private void ResetItemLook(Frame frame)
    {
        if (frame == null) return;
        frame.BackgroundColor = Color.FromArgb("#F9F9F9");
        if (frame.Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Label label) label.TextColor = Color.FromArgb("#333333");
            }
        }
    }

    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        if (int.TryParse(CourtCountEntry.Text, out int courtCount) && 
            int.TryParse(CallCountEntry.Text, out int callCount))
        {
            Preferences.Default.Set("CourtCount", courtCount);
            Preferences.Default.Set("CallCount", callCount);
            Preferences.Default.Set("MatchTime", MatchTimePicker.SelectedItem?.ToString() ?? "無制限");
            await DisplayAlert("完了", "設定を保存しました。", "OK");
        }
        else
        {
            await DisplayAlert("エラー", "数字を正しく入力してください。", "OK");
        }
    }

    private async void OnResetEventClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("確認", "イベントデータをリセットしますか？\n※ビジターが削除され、全メンバーが「不参加」に戻ります", "はい", "いいえ");
        if (confirm)
        {
            await _database.ResetAllParticipationAsync();
            await DisplayAlert("完了", "イベントをリセットしました。\n全員が「不参加」になりました。", "OK");
        }
    }
}
