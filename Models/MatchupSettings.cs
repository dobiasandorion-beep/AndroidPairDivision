namespace MyAndroidApp.Models;

public class MatchupSettings
{
    public bool BalanceParticipationRate { get; set; } = true;
    public bool PrioritizeGenderDoubles { get; set; } = false;
    public bool PrioritizeMixedDoubles { get; set; } = false;
    public bool AvoidSingleFemale { get; set; } = false;
    public bool AvoidSingleMale { get; set; } = false;
}
