namespace MyAndroidApp.Models;

public class DragInfo
{
    public Member Member { get; set; }
    
    // 元の場所がコートの場合
    public Matchup SourceMatchup { get; set; }
    public int PlayerIndex { get; set; } // 1〜4

    // 元の場所が休憩エリアの場合
    public MatchupRound SourceRound { get; set; }
    public bool IsFromBreakArea { get; set; }
}
