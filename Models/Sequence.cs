namespace Zero.Models;

public class Sequence
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public List<Card> Cards { get; set; } = new();
    public string PlacedByPlayerId { get; set; } = "";
}
