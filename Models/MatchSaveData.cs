using System.Collections.Generic;

namespace MyAndroidApp.Models;

public class MatchSaveData
{
    public int CurrentRoundCount { get; set; }
    public List<RoundSaveData> Rounds { get; set; } = new List<RoundSaveData>();
}

public class RoundSaveData
{
    public int RoundNumber { get; set; }
    public List<string> BreakMemberIds { get; set; } = new List<string>();
    public List<CourtSaveData> Courts { get; set; } = new List<CourtSaveData>();
}

public class CourtSaveData
{
    public string MatchName { get; set; }
    public string P1Id { get; set; }
    public string P2Id { get; set; }
    public string P3Id { get; set; }
    public string P4Id { get; set; }
}
