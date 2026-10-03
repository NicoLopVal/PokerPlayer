namespace PokerPlayer.Domain.Kpis.Calculators;

/// <summary>EV formulas in chips, relative to folding (EV 0). f = villain fold probability, eq = Hero equity.</summary>
public static class ExpectedValue
{
    public const double Fold = 0;

    /// <summary>eq·(P + C) - C, where P includes the bet faced.</summary>
    public static double Call(double equity, int pot, int call) => equity * (pot + call) - call;

    /// <summary>
    /// EV when villain calls a bet/raise: Hero risks <paramref name="risk"/> (call + raise) and villain adds
    /// <paramref name="villainAdds"/>. With no bet faced (risk = villainAdds = B) this is eq·(P + 2B) - B.
    /// </summary>
    public static double WhenCalled(double equityWhenCalled, int pot, int risk, int villainAdds) =>
        equityWhenCalled * (pot + risk + villainAdds) - risk;

    /// <summary>f·P + (1 - f)·EV(when called).</summary>
    public static double BetOrRaise(double foldProbability, int pot, double whenCalled) =>
        foldProbability * pot + (1 - foldProbability) * whenCalled;

    /// <summary>f·P - (1 - f)·B: zero-equity bluff.</summary>
    public static double PureBluff(double foldProbability, int pot, int bet) =>
        foldProbability * pot - (1 - foldProbability) * bet;

    /// <summary>f·(dead money) + (1 - f)·[eq·(pot + S) - (1 - eq)·S].</summary>
    public static double Shove(double foldProbability, int deadMoney, double equity, int pot, int stack) =>
        foldProbability * deadMoney + (1 - foldProbability) * (equity * (pot + stack) - (1 - equity) * stack);

    /// <summary>
    /// Smallest fold probability for which a bet/raise beats EV 0, solving f·P + (1 - f)·X = 0.
    /// Returns 0 when the bet already profits when called.
    /// </summary>
    public static double BreakEvenFoldFrequency(int pot, double whenCalled) =>
        whenCalled >= 0 ? 0 : -whenCalled / (pot - whenCalled);
}
