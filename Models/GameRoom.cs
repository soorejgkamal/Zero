namespace Zero.Models;

public enum GamePhase { Lobby, Playing, RoundEnd, GameOver }

public class GameRoom
{
    public string RoomId { get; set; } = "";
    public List<Player> Players { get; set; } = new();
    public List<Card> Deck { get; set; } = new();
    public List<Card> DiscardPile { get; set; } = new();
    public List<Sequence> TableSets { get; set; } = new();
    public int CurrentPlayerIndex { get; set; } = 0;
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public string? LastDrawPlayerId { get; set; }
    public string? WinnerId { get; set; }
    public bool HasDrawnThisTurn { get; set; } = false;
}
