using AndroidX.ConstraintLayout.Helper.Widget;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyAndroidApp.Models;

public class Matchup : INotifyPropertyChanged
{
    // 大枠の親への参照（ドラッグ＆ドロップ時に回戦内の休憩リストを探すため）
    public MatchupRound ParentRound { get; set; }

    public string MatchName { get; set; } = string.Empty;

    // プレイヤー1〜4のデータ
    private Member _player1;
    public Member Player1 { get => _player1; set { _player1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(P1Name)); } }

    private Member _player2;
    public Member Player2 { get => _player2; set { _player2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(P2Name)); } }

    private Member _player3;
    public Member Player3 { get => _player3; set { _player3 = value; OnPropertyChanged(); OnPropertyChanged(nameof(P3Name)); } }

    private Member _player4;
    public Member Player4 { get => _player4; set { _player4 = value; OnPropertyChanged(); OnPropertyChanged(nameof(P4Name)); } }

    // 画面に表示するためのプロパティ
    public string P1Name => Player1?.Name ?? "---";
    public string P2Name => Player2?.Name ?? "---";
    public string P3Name => Player3?.Name ?? "---";
    public string P4Name => Player4?.Name ?? "---";

    public string pair1 => $"{Player1?.Id}_{Player2?.Id}";
    public string pair1b => $"{Player2?.Id}_{Player1?.Id}";
    public string pair2 => $"{Player3?.Id}_{Player4?.Id}";
    public string pair2b => $"{Player4?.Id}_{Player3?.Id}";
    public List<string> pairs => new List<string>() { this.pair1, this.pair1b, this.pair2, this.pair2b };

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
