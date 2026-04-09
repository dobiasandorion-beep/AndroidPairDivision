using System.Collections.ObjectModel;
using System.Globalization;
using MyAndroidApp.Models;
using MyAndroidApp.Services;

namespace MyAndroidApp;

// 参加状態を文字（参加 / 不参加）に変換するクラス
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "参加" : "不参加";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

// 参加状態を色（緑 / グレー）に変換するクラス
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E"); // 参加=緑、不参加=グレー
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

// メンバー種別がビジターの時だけ表示するためのコンバーター
public class MemberTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (MemberType)value == MemberType.ビジター;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public partial class MainPage : ContentPage
{
    public ObservableCollection<Member> Members { get; set; } = new ObservableCollection<Member>();
    private readonly MemberDatabase _database;
    private string _editingMemberId = null;
    private MemberType _currentInputType = MemberType.メンバー;

    public MainPage(MemberDatabase database)
    {
        InitializeComponent();
        _database = database;
        MembersCollectionView.ItemsSource = Members;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _database.GetPreloadTask();
        await LoadMembersAsync();
    }

    private async Task LoadMembersAsync()
    {
        var membersFromDb = await _database.GetMembersAsync();
        
        // ソート順: ビジターが先頭 → 参加(true)が先 → 参加率(高→中→低) → 登録日時(古い順)
        var sortedMembers = membersFromDb
            .OrderBy(m => m.Type == MemberType.ビジター ? 0 : 1)
            .ThenByDescending(m => m.IsParticipating)
            .ThenBy(m => (int)m.ParticipationRate)
            .ThenBy(m => m.CreatedDate)
            .ToList();

        Members.Clear();
        foreach (var member in sortedMembers)
        {
            Members.Add(member);
        }
    }

    // メンバーをソート順に従って正しい位置に挿入（または移動）する
    private void SortAndInsertMember(Member member)
    {
        if (Members.Contains(member))
        {
            Members.Remove(member);
        }

        int targetIndex = 0;
        foreach (var existingMember in Members)
        {
            // 1. ビジターは最上位
            if (member.Type == MemberType.ビジター && existingMember.Type != MemberType.ビジター)
                break;
            if (member.Type != MemberType.ビジター && existingMember.Type == MemberType.ビジター)
            {
                targetIndex++;
                continue;
            }

            // 2. 参加状態の比較
            if (member.IsParticipating && !existingMember.IsParticipating) break;
            if (!member.IsParticipating && existingMember.IsParticipating)
            {
                targetIndex++;
                continue;
            }

            // 3. 参加率の比較
            if ((int)member.ParticipationRate < (int)existingMember.ParticipationRate) break;
            if ((int)member.ParticipationRate > (int)existingMember.ParticipationRate)
            {
                targetIndex++;
                continue;
            }

            // 4. 登録日時の比較
            if (member.CreatedDate < existingMember.CreatedDate) break;
            targetIndex++;
        }

        Members.Insert(targetIndex, member);
    }

    // メンバー新規登録ボタンが押された時
    private void OnNewMemberClicked(object sender, EventArgs e)
    {
        ClearForm();
        _editingMemberId = null;
        _currentInputType = MemberType.メンバー;
        DialogTitle.Text = "メンバー新規登録";
        DialogDeleteBtn.IsVisible = false;
        UpdateRateSelectionVisibility();
        DialogOverlay.IsVisible = true;
    }

    // ビジター新規登録ボタンが押された時
    private void OnNewVisitorClicked(object sender, EventArgs e)
    {
        ClearForm();
        _editingMemberId = null;
        _currentInputType = MemberType.ビジター;
        DialogTitle.Text = "ビジター新規登録";
        DialogDeleteBtn.IsVisible = false;
        UpdateRateSelectionVisibility();
        DialogOverlay.IsVisible = true;
    }

    // 一覧のメンバーがタップされた時（編集ダイアログを開く）
    private void OnMemberTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Member selectedMember)
        {
            _editingMemberId = selectedMember.Id;
            _currentInputType = selectedMember.Type; // 現在のタイプを保持
            NameEntry.Text = selectedMember.Name;
            ReadingEntry.Text = selectedMember.Reading;
            
            if (selectedMember.Gender == Gender.男性) OnGenderMaleTapped(null, null);
            else OnGenderFemaleTapped(null, null);

            if (selectedMember.ParticipationRate == ParticipationRate.高) OnRateHighTapped(null, null);
            else if (selectedMember.ParticipationRate == ParticipationRate.中) OnRateMidTapped(null, null);
            else OnRateLowTapped(null, null);
            
            DialogTitle.Text = (_currentInputType == MemberType.ビジター) ? "ビジター編集" : "メンバー編集";
            DialogDeleteBtn.IsVisible = true;
            UpdateRateSelectionVisibility();
            DialogOverlay.IsVisible = true;
        }
    }

    // 一覧の「参加/不参加」エリアがタップされた時
    private async void OnToggleParticipationClicked(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Member member)
        {
            // 状態を反転させる
            member.IsParticipating = !member.IsParticipating;
            
            // データベースに即時保存
            await _database.SaveMemberAsync(member);
            
            // ここでは SortAndInsertMember(member) を呼び出さないことで、
            // リスト内の位置移動（ソート）を発生させず、その場で見た目だけを変えます。
        }
    }

    // 保存ボタンが押された時
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var inputName = NameEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(inputName))
        {
            await DisplayAlert("エラー", "名前を入力してください", "OK");
            return;
        }

        bool isDuplicate = Members.Any(m => m.Name == inputName && m.Id != _editingMemberId);
        if (isDuplicate)
        {
            await DisplayAlert("エラー", "その名前はすでに登録されています。", "OK");
            return;
        }

        Gender selectedGender = GenderFemaleFrame.BackgroundColor == Colors.White ? Gender.女性 : Gender.男性;
        ParticipationRate selectedRate = RateHighFrame.BackgroundColor == Colors.White ? ParticipationRate.高 : 
                                         RateLowFrame.BackgroundColor == Colors.White ? ParticipationRate.低 : ParticipationRate.中;

        if (_currentInputType == MemberType.ビジター)
        {
            selectedRate = ParticipationRate.中;
        }

        Member memberToSave;

        if (_editingMemberId == null)
        {
            memberToSave = new Member
            {
                Name = inputName,
                Reading = ReadingEntry.Text ?? string.Empty,
                Gender = selectedGender,
                ParticipationRate = selectedRate,
                Type = _currentInputType // 保存時のタイプを設定
            };
        }
        else
        {
            memberToSave = Members.FirstOrDefault(m => m.Id == _editingMemberId);
            if (memberToSave != null)
            {
                memberToSave.Name = inputName;
                memberToSave.Reading = ReadingEntry.Text ?? string.Empty;
                memberToSave.Gender = selectedGender;
                memberToSave.ParticipationRate = selectedRate;
                // Typeは編集ダイアログでは変更しない（現在のTypeを維持）
            }
        }

        if (memberToSave != null)
        {
            await _database.SaveMemberAsync(memberToSave);
            SortAndInsertMember(memberToSave);
        }

        NameEntry.IsEnabled = false;
        ReadingEntry.IsEnabled = false;
        DialogOverlay.IsVisible = false;
        await Task.Delay(100);
        NameEntry.IsEnabled = true;
        ReadingEntry.IsEnabled = true;
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_editingMemberId == null) return;

        bool confirm = await DisplayAlert("確認", "このメンバーを削除しますか？", "はい", "いいえ");
        if (confirm)
        {
            var memberToRemove = Members.FirstOrDefault(m => m.Id == _editingMemberId);
            if (memberToRemove != null)
            {
                await _database.DeleteMemberAsync(memberToRemove);
                Members.Remove(memberToRemove);
            }
            DialogOverlay.IsVisible = false;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        NameEntry.IsEnabled = false;
        ReadingEntry.IsEnabled = false;
        DialogOverlay.IsVisible = false;
        await Task.Delay(100);
        NameEntry.IsEnabled = true;
        ReadingEntry.IsEnabled = true;
    }

    private async void OnOverlayTapped(object sender, TappedEventArgs e)
    {
        NameEntry.IsEnabled = false;
        ReadingEntry.IsEnabled = false;
        DialogOverlay.IsVisible = false;
        await Task.Delay(100);
        NameEntry.IsEnabled = true;
        ReadingEntry.IsEnabled = true;
    }

    private void ClearForm()
    {
        NameEntry.Text = string.Empty;
        ReadingEntry.Text = string.Empty;
        OnGenderMaleTapped(null, null);
        OnRateMidTapped(null, null);
    }

    private void UnfocusEntries()
    {
        NameEntry.Unfocus();
        ReadingEntry.Unfocus();
    }

    private void OnGenderMaleTapped(object sender, EventArgs e)
    {
        UnfocusEntries();
        GenderMaleFrame.BackgroundColor = Colors.White;
        GenderMaleLabel.TextColor = Color.FromArgb("#333333");
        GenderFemaleFrame.BackgroundColor = Colors.Transparent;
        GenderFemaleLabel.TextColor = Color.FromArgb("#888888");
    }

    private void OnGenderFemaleTapped(object sender, EventArgs e)
    {
        UnfocusEntries();
        GenderFemaleFrame.BackgroundColor = Colors.White;
        GenderFemaleLabel.TextColor = Color.FromArgb("#333333");
        GenderMaleFrame.BackgroundColor = Colors.Transparent;
        GenderMaleLabel.TextColor = Color.FromArgb("#888888");
    }

    private void OnRateHighTapped(object sender, EventArgs e)
    {
        UnfocusEntries();
        RateHighFrame.BackgroundColor = Colors.White;
        RateHighLabel.TextColor = Color.FromArgb("#333333");
        RateMidFrame.BackgroundColor = Colors.Transparent;
        RateMidLabel.TextColor = Color.FromArgb("#888888");
        RateLowFrame.BackgroundColor = Colors.Transparent;
        RateLowLabel.TextColor = Color.FromArgb("#888888");
    }

    private void OnRateMidTapped(object sender, EventArgs e)
    {
        UnfocusEntries();
        RateMidFrame.BackgroundColor = Colors.White;
        RateMidLabel.TextColor = Color.FromArgb("#333333");
        RateHighFrame.BackgroundColor = Colors.Transparent;
        RateHighLabel.TextColor = Color.FromArgb("#888888");
        RateLowFrame.BackgroundColor = Colors.Transparent;
        RateLowLabel.TextColor = Color.FromArgb("#888888");
    }

    private void OnRateLowTapped(object sender, EventArgs e)
    {
        UnfocusEntries();
        RateLowFrame.BackgroundColor = Colors.White;
        RateLowLabel.TextColor = Color.FromArgb("#333333");
        RateHighFrame.BackgroundColor = Colors.Transparent;
        RateHighLabel.TextColor = Color.FromArgb("#888888");
        RateMidFrame.BackgroundColor = Colors.Transparent;
        RateMidLabel.TextColor = Color.FromArgb("#888888");
    }

    private void UpdateRateSelectionVisibility()
    {
        if (RateSelectionArea == null)
            return;

        bool visible = _currentInputType != MemberType.ビジター;
        RateSelectionArea.IsVisible = visible;
    }
}
