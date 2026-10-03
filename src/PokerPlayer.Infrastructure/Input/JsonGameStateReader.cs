using System.Text.Json;
using PokerPlayer.Application.Abstractions;
using PokerPlayer.Domain;
using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Infrastructure.Input;

/// <summary>Reads a game state from a .json or .txt file; both contain the same JSON structure.</summary>
public sealed class JsonGameStateReader : IGameStateReader
{
    private static readonly string[] SupportedExtensions = [".json", ".txt"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public GameState Read(string path)
    {
        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidInputException(ErrorCodes.InvalidFile,
                $"Invalid file. '{extension}' is not a supported file type, use {string.Join(" or ", SupportedExtensions)}.");
        if (!File.Exists(path))
            throw new InvalidInputException(ErrorCodes.InvalidFile, $"Invalid file. '{path}' does not exist.");

        return Parse(File.ReadAllText(path));
    }

    public static GameState Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidInputException(ErrorCodes.MalformedInput, "Malformed input. The input is empty.");

        GameStateDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GameStateDto>(json, Options);
        }
        catch (JsonException exception)
        {
            var location = exception.Path is { Length: > 0 } path ? $" at '{path}'" : "";
            throw new InvalidInputException(ErrorCodes.MalformedInput,
                $"Malformed input. The input is not valid JSON or a field has the wrong type{location}.");
        }

        return GameStateMapper.ToDomain(dto ?? throw new InvalidInputException(ErrorCodes.MalformedInput, "Malformed input. The input is empty."));
    }
}
