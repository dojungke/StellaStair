namespace StellaStair.Battle
{
    public enum TacticalPartyLevel
    {
        Beginner,
        Intermediate,
        Advanced,
        Star
    }

    public enum TacticalMissionGrade
    {
        Beginner,
        Intermediate,
        Advanced,
        Star
    }

    public static class TacticalMissionGradeUtility
    {
        public static string GetDisplayName(TacticalPartyLevel level) => level switch
        {
            TacticalPartyLevel.Beginner => "\uCD08\uAE09",
            TacticalPartyLevel.Intermediate => "\uC911\uAE09",
            TacticalPartyLevel.Advanced => "\uC0C1\uAE09",
            TacticalPartyLevel.Star => "\uBCC4",
            _ => "\uCD08\uAE09"
        };

        public static string GetDisplayName(TacticalMissionGrade grade) => grade switch
        {
            TacticalMissionGrade.Beginner => "\uCD08\uAE09",
            TacticalMissionGrade.Intermediate => "\uC911\uAE09",
            TacticalMissionGrade.Advanced => "\uC0C1\uAE09",
            TacticalMissionGrade.Star => "\uBCC4",
            _ => "\uCD08\uAE09"
        };

        public static bool IsSameAsPartyLevel(TacticalMissionGrade grade, TacticalPartyLevel partyLevel) =>
            (int)grade == (int)partyLevel;

        public static TacticalPartyLevel GetNextPartyLevel(TacticalPartyLevel level) => level switch
        {
            TacticalPartyLevel.Beginner => TacticalPartyLevel.Intermediate,
            TacticalPartyLevel.Intermediate => TacticalPartyLevel.Advanced,
            TacticalPartyLevel.Advanced => TacticalPartyLevel.Star,
            _ => TacticalPartyLevel.Star
        };
    }
}