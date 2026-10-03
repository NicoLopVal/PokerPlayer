namespace PokerPlayer.Domain.Kpis;

/// <param name="EffectiveStack">min(Hero stack, largest villain stack): the most that can be won or lost.</param>
/// <param name="StackInBigBlinds">Effective stack / big blind.</param>
/// <param name="MRatio">Effective stack / (small blind + big blind).</param>
/// <param name="StackToPotRatio">Effective stack / pot (SPR). Null when the pot is empty.</param>
/// <param name="PotCommitment">Invested / (invested + stack). Null until Invested is provided.</param>
public sealed record StackKpis(
    int EffectiveStack,
    double StackInBigBlinds,
    double MRatio,
    double? StackToPotRatio,
    double? PotCommitment);

/// <param name="RequiredEquity">C / (P + C): equity needed for a call to break even.</param>
/// <param name="PotOddsRatio">P / C, read as "P : 1".</param>
/// <param name="MinimumDefenseFrequency">Share of hands Hero must continue with so a villain bluff does not auto-profit.</param>
public sealed record FacingBetKpis(
    int BetToCall,
    double RequiredEquity,
    double PotOddsRatio,
    double MinimumDefenseFrequency);

/// <param name="Label">Size as a share of the pot, e.g. "2/3 pot".</param>
/// <param name="Amount">Chips added on top of any call.</param>
/// <param name="FoldEquityNeeded">Risk / (pot + risk): how often villain must fold for a pure bluff to break even.</param>
/// <param name="OptimalBluffShare">B / (P + 2B): share of bets at this size that should be bluffs in a balanced strategy.</param>
/// <param name="EvWhenCalled">EV in chips if villain always calls, using Hero's equity against the whole range.</param>
/// <param name="BreakEvenFoldFrequency">Fold frequency that makes the bet worth at least folding (0 when it already profits when called).</param>
public sealed record ReferenceBetKpis(
    string Label,
    int Amount,
    double FoldEquityNeeded,
    double OptimalBluffShare,
    double EvWhenCalled,
    double BreakEvenFoldFrequency);

/// <param name="HandClass">E.g. "AKs", "T9o", "77".</param>
/// <param name="ChenScore">-1 (worst) to 20 (AA).</param>
/// <param name="TopPercent">Share of starting hands ranked at or above this one (0.01 = top 1%).</param>
public sealed record PreflopHandStrengthKpis(
    string HandClass,
    int ChenScore,
    double TopPercent);

/// <param name="VillainRangeWidth">Share of starting hands assumed in the villain range.</param>
/// <param name="ShareOfRangeBeaten">Share of the villain range Hero's starting hand ranks above.</param>
/// <param name="EstimatedEquity">Heuristic equity from hand strength vs range (no simulation).</param>
/// <param name="CallExpectedValue">eq·(P + C) - C in chips. Null when Hero can check.</param>
public sealed record PreflopEquityKpis(
    double VillainRangeWidth,
    double ShareOfRangeBeaten,
    double EstimatedEquity,
    double? CallExpectedValue);

/// <param name="Outs">Unseen cards that improve Hero to a clearly better hand.</param>
/// <param name="HitNextCard">o / N.</param>
/// <param name="HitByRiver">Chance to hit on turn or river. Flop only.</param>
/// <param name="RuleOfTwoAndFour">Quick estimate: 4·o% on the flop, 2·o% on the turn (as a fraction).</param>
/// <param name="OddsAgainst">(N - o) / o, read as "x : 1" against hitting. Null with no outs.</param>
public sealed record DrawKpis(
    int Outs,
    double HitNextCard,
    double? HitByRiver,
    double RuleOfTwoAndFour,
    double? OddsAgainst);

/// <param name="CurrentHand">Category of Hero's best hand right now.</param>
/// <param name="Equity">Estimated share of the pot Hero wins at showdown vs the villain range.</param>
/// <param name="HandStrength">Share of villain combos Hero beats right now (ties count half).</param>
/// <param name="PositivePotential">P(behind now, ahead at showdown). Null on the river.</param>
/// <param name="NegativePotential">P(ahead now, behind at showdown). Null on the river.</param>
/// <param name="EffectiveHandStrength">HS·(1 - NPot) + (1 - HS)·PPot.</param>
/// <param name="VillainRangeWidth">Share of starting hands assumed in the villain range.</param>
/// <param name="Samples">Simulated runouts (or exact combos on the river).</param>
/// <param name="CallExpectedValue">eq·(P + C) - C in chips. Null when Hero can check.</param>
public sealed record EquityKpis(
    string CurrentHand,
    double Equity,
    double HandStrength,
    double? PositivePotential,
    double? NegativePotential,
    double EffectiveHandStrength,
    double VillainRangeWidth,
    int Samples,
    double? CallExpectedValue);
