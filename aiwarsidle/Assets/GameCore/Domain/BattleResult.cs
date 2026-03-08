namespace AIWarsIdle.GameCore.Domain
{
    public sealed class BattleResult
    {
        public bool Win;
        public double AttackRoll;
        public double DefenseRoll;
        public double WinChance;
        public int LeaguePointsDelta;
        public double SoftReward;
    }
}
