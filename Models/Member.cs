using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SQLite;

namespace MyAndroidApp.Models;

public enum Gender
{
    男性,
    女性
}

public enum ParticipationRate
{
    高,
    中,
    低
}

public enum MemberType
{
    メンバー,
    ビジター
}

public class Member : INotifyPropertyChanged
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string Name { get; set; } = string.Empty;
    
    public string Reading { get; set; } = string.Empty;
    
    private Gender _gender;
    public Gender Gender 
    { 
        get => _gender; 
        set { _gender = value; OnPropertyChanged(); OnPropertyChanged(nameof(GenderBackgroundColor)); } 
    }

    // 性別に応じた背景色（対戦表用）
    public Color GenderBackgroundColor => Gender == Gender.男性 ? Color.FromArgb("#E3F2FD") : Color.FromArgb("#FCE4EC"); // 薄い青 : 薄いピンク

    // 登録日時（ソート用）
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    private ParticipationRate _participationRate = ParticipationRate.中; // デフォルトは「中」
    public ParticipationRate ParticipationRate
    {
        get => _participationRate;
        set { _participationRate = value; OnPropertyChanged(); }
    }

    private MemberType _type = MemberType.メンバー;
    public MemberType Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    private bool _isParticipating = false;
    public bool IsParticipating 
    { 
        get => _isParticipating; 
        set { _isParticipating = value; OnPropertyChanged(); } 
    }
    
    private int _matchCount = 0;
    public int MatchCount 
    { 
        get => _matchCount; 
        set { _matchCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchRatioText)); } 
    }

    private int _breakCount = 0;
    public int BreakCount
    {
        get => _breakCount;
        set { _breakCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchRatioText)); }
    }

    public double MatchRate => this.MatchCount + this.BreakCount == 0 ? 0 : ((double)this.MatchCount / (this.MatchCount + this.BreakCount));

    // 「試合数 / (試合数＋休憩数)」という表示用の文字を作る
    public string MatchRatioText => $"{MatchCount} / {MatchCount + BreakCount}";

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
