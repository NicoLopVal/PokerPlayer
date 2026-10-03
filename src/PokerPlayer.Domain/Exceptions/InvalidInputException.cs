namespace PokerPlayer.Domain.Exceptions;

/// <summary>
/// The input cannot be played. The message is the bot's reply: "{code}: {reason}",
/// e.g. "inv05: Invalid pot value. -5 is not a valid pot value".
/// </summary>
public sealed class InvalidInputException(string code, string reason) : Exception($"{code}: {reason}")
{
    public string Code { get; } = code;

    public string Reason { get; } = reason;
}
