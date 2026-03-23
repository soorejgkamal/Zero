using Zero.Models;

namespace Zero.Services;

public static class GameEngine
{
    public static List<Card> CreateDeck()
    {
        var deck = new List<Card>();
        
        // Add 2 full standard decks
        for (int d = 0; d < 2; d++)
        {
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                if (suit == Suit.Joker) continue;
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    deck.Add(new Card { Suit = suit, Rank = rank });
                }
            }
        }
        
        // Add only 2 jokers total
        deck.Add(new Card { Suit = Suit.Joker, Rank = null });
        deck.Add(new Card { Suit = Suit.Joker, Rank = null });
        
        // Shuffle
        var rng = new Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        
        return deck;
    }
    
    public static void DealCards(GameRoom room, int cardsPerPlayer = 10)
    {
        foreach (var player in room.Players.Where(p => !p.IsEliminated))
        {
            player.Hand.Clear();
            for (int i = 0; i < cardsPerPlayer && room.Deck.Count > 0; i++)
            {
                player.Hand.Add(room.Deck[0]);
                room.Deck.RemoveAt(0);
            }
        }
    }
    
    public static bool IsValidSequence(List<Card> cards)
    {
        if (cards.Count < 3) return false;
        
        var nonJokers = cards.Where(c => !c.IsJoker).ToList();
        if (nonJokers.Count == 0) return false;
        
        var suit = nonJokers[0].Suit;
        if (nonJokers.Any(c => c.Suit != suit)) return false;
        
        var sorted = SortSequenceCards(cards);
        return AreConsecutiveWithJokers(sorted);
    }
    
    public static List<Card> SortSequenceCards(List<Card> cards)
    {
        var nonJokers = cards.Where(c => !c.IsJoker).OrderBy(c => (int)c.Rank!).ToList();
        var jokers = cards.Where(c => c.IsJoker).ToList();
        
        if (nonJokers.Count == 0) return cards;
        
        var result = new List<Card>();
        int jokerIdx = 0;
        
        int minRank = (int)nonJokers.First().Rank!;
        int maxRank = (int)nonJokers.Last().Rank!;
        int rangeNeeded = maxRank - minRank + 1;
        int gaps = rangeNeeded - nonJokers.Count;
        
        if (gaps <= jokers.Count)
        {
            int nonJokerPos = 0;
            for (int r = minRank; r <= maxRank; r++)
            {
                if (nonJokerPos < nonJokers.Count && (int)nonJokers[nonJokerPos].Rank! == r)
                {
                    result.Add(nonJokers[nonJokerPos++]);
                }
                else if (jokerIdx < jokers.Count)
                {
                    result.Add(jokers[jokerIdx++]);
                }
            }
            while (jokerIdx < jokers.Count)
                result.Add(jokers[jokerIdx++]);
        }
        else
        {
            result.AddRange(nonJokers);
            result.AddRange(jokers);
        }
        
        return result;
    }
    
    private static bool AreConsecutiveWithJokers(List<Card> sortedCards)
    {
        var nonJokers = sortedCards.Where(c => !c.IsJoker).OrderBy(c => (int)c.Rank!).ToList();
        if (nonJokers.Count == 0) return false;
        
        int jokerCount = sortedCards.Count(c => c.IsJoker);
        int minRank = (int)nonJokers.First().Rank!;
        int maxRank = (int)nonJokers.Last().Rank!;
        
        for (int i = 0; i < nonJokers.Count - 1; i++)
        {
            if (nonJokers[i].Rank == nonJokers[i + 1].Rank) return false;
        }
        
        int rangeCovered = maxRank - minRank + 1;
        int gaps = rangeCovered - nonJokers.Count;
        
        return gaps <= jokerCount && (nonJokers.Count + jokerCount) == sortedCards.Count;
    }
    
    public static bool CanAddMultipleToLeft(Sequence seq, List<Card> cards)
    {
        if (cards.Count == 0) return false;
        if (cards.Count == 1) return CanAddToLeft(seq, cards[0]);

        var nonJokersNew = cards.Where(c => !c.IsJoker).ToList();
        var seqSuit = seq.Cards.FirstOrDefault(c => !c.IsJoker)?.Suit;

        if (nonJokersNew.Count > 0)
        {
            if (seqSuit == null) return false;
            if (nonJokersNew.Any(c => c.Suit != seqSuit)) return false;
        }

        int jokersAtStartOfSeq = seq.Cards.TakeWhile(c => c.IsJoker).Count();
        var firstNonJokerOfSeq = seq.Cards.FirstOrDefault(c => !c.IsJoker);
        if (firstNonJokerOfSeq == null) return false;
        int effectiveLeftRank = (int)firstNonJokerOfSeq.Rank! - jokersAtStartOfSeq;

        if (nonJokersNew.Any(c => (int)c.Rank! >= effectiveLeftRank)) return false;

        var combined = cards.Concat(seq.Cards).ToList();
        return IsValidSequence(combined);
    }

    public static bool CanAddMultipleToRight(Sequence seq, List<Card> cards)
    {
        if (cards.Count == 0) return false;
        if (cards.Count == 1) return CanAddToRight(seq, cards[0]);

        var nonJokersNew = cards.Where(c => !c.IsJoker).ToList();
        var seqSuit = seq.Cards.FirstOrDefault(c => !c.IsJoker)?.Suit;

        if (nonJokersNew.Count > 0)
        {
            if (seqSuit == null) return false;
            if (nonJokersNew.Any(c => c.Suit != seqSuit)) return false;
        }

        int jokersAtEndOfSeq = seq.Cards.AsEnumerable().Reverse().TakeWhile(c => c.IsJoker).Count();
        var lastNonJokerOfSeq = seq.Cards.LastOrDefault(c => !c.IsJoker);
        if (lastNonJokerOfSeq == null) return false;
        int effectiveRightRank = (int)lastNonJokerOfSeq.Rank! + jokersAtEndOfSeq;

        if (nonJokersNew.Any(c => (int)c.Rank! <= effectiveRightRank)) return false;

        var combined = seq.Cards.Concat(cards).ToList();
        return IsValidSequence(combined);
    }

    public static bool CanAddToLeft(Sequence seq, Card card)
    {
        if (card.IsJoker) return true;
        
        var leftMostNonJoker = seq.Cards.FirstOrDefault(c => !c.IsJoker);
        if (leftMostNonJoker == null) return false;
        
        if (card.Suit != leftMostNonJoker.Suit) return false;
        
        int jokersAtStart = seq.Cards.TakeWhile(c => c.IsJoker).Count();
        int leftmostNonJokerRank = (int)leftMostNonJoker.Rank!;
        int effectiveLeftRank = leftmostNonJokerRank - jokersAtStart;
        
        return (int)card.Rank! == effectiveLeftRank - 1;
    }
    
    public static bool CanAddToRight(Sequence seq, Card card)
    {
        if (card.IsJoker) return true;
        
        var rightMostNonJoker = seq.Cards.LastOrDefault(c => !c.IsJoker);
        if (rightMostNonJoker == null) return false;
        
        if (card.Suit != rightMostNonJoker.Suit) return false;
        
        int jokersAtEnd = seq.Cards.AsEnumerable().Reverse().TakeWhile(c => c.IsJoker).Count();
        int rightmostNonJokerRank = (int)rightMostNonJoker.Rank!;
        int effectiveRightRank = rightmostNonJokerRank + jokersAtEnd;
        
        return (int)card.Rank! == effectiveRightRank + 1 && (int)card.Rank! <= (int)Rank.Ace;
    }
    
    public static int CalculateHandScore(List<Card> hand)
    {
        return hand.Sum(c => c.PointValue);
    }
}
