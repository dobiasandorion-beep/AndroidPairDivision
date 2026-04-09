using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace MyAndroidApp.Models;

// 第N回戦（大枠）の情報をまとめるクラス
public class MatchupRound : INotifyPropertyChanged
{
    public int RoundNumber { get; set; }

    public string RoundTitle => $"第 {RoundNumber} 回戦";

    public ObservableCollection<Matchup> Courts { get; set; } = new ObservableCollection<Matchup>();

    // この回戦で休憩になったメンバー
    public ObservableCollection<Member> BreakMembers { get; set; } = new ObservableCollection<Member>();

    public Color BackgroundColor => RoundNumber % 2 == 0 ? Color.FromArgb("#F0F8FF") : Color.FromArgb("#FFF5EE");

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
