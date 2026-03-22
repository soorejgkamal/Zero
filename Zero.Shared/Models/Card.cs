namespace Zero.Shared.Models;

public enum Suit { Hearts, Diamonds, Clubs, Spades, Joker }
public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

public class Card
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Suit Suit { get; set; }
    public Rank? Rank { get; set; } // null for Joker
    public bool IsJoker => Suit == Suit.Joker;
    
    public int PointValue => IsJoker ? 10 : (Rank >= Models.Rank.Jack ? 10 : (int)Rank!);
    
    public string DisplayName => IsJoker ? "🃏" : $"{RankSymbol}{SuitSymbol}";
    
    public string RankSymbol => Rank switch {
        Models.Rank.Ace => "A",
        Models.Rank.Jack => "J",
        Models.Rank.Queen => "Q",
        Models.Rank.King => "K",
        _ => ((int)Rank!).ToString()
    };
    
    public string SuitSymbol => Suit switch {
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs => "♣",
        Suit.Spades => "♠",
        _ => "🃏"
    };
    
    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
}
