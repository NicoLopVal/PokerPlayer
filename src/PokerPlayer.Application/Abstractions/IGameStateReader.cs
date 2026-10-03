using PokerPlayer.Domain;

namespace PokerPlayer.Application.Abstractions;

/// <summary>Loads a game state from an input source (e.g. a .json or .txt file).</summary>
public interface IGameStateReader
{
    GameState Read(string path);
}
