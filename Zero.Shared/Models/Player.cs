namespace Zero.Shared.Models;

public class Player
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<Card> Hand { get; set; } = new();
    public int Score { get; set; } = 0;
    public bool IsEliminated { get; set; } = false;
    public bool IsConnected { get; set; } = true;
    public string ConnectionId { get; set; } = "";
}
