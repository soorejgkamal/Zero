namespace Zero.Models;

public class PlayerDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int CardCount { get; set; }
    public int Score { get; set; }
    public bool IsEliminated { get; set; }
    public bool IsConnected { get; set; }
    public List<Card> Hand { get; set; } = new();
}

public class GameStateDto
{
    public string RoomId { get; set; } = "";
    public List<PlayerDto> Players { get; set; } = new();
    public int DeckCount { get; set; }
    public List<Sequence> TableSets { get; set; } = new();
    public string CurrentPlayerId { get; set; } = "";
    public GamePhase Phase { get; set; }
    public string? WinnerId { get; set; }
    public bool HasDiscardedThisTurn { get; set; }
    public string? NewlyDrawnCardId { get; set; }
}
