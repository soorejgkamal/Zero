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
        
        // Try Ace as high (rank 14) – standard interpretation
        var sorted = SortSequenceCards(cards, aceIsLow: false);
        if (AreConsecutiveWithJokers(sorted, aceIsLow: false)) return true;
        
        // Try Ace as low (rank 1) when the sequence contains an Ace
        if (nonJokers.Any(c => c.Rank == Rank.Ace))
        {
            var sortedLow = SortSequenceCards(cards, aceIsLow: true);
            if (AreConsecutiveWithJokers(sortedLow, aceIsLow: true)) return true;
        }
        
        return false;
    }
    
    public static List<Card> SortSequenceCards(List<Card> cards)
    {
        // Auto-detect whether Ace should be treated as low
        var nonJokersSortedHigh = cards.Where(c => !c.IsJoker).OrderBy(c => (int)c.Rank!).ToList();
        bool aceIsLow = ShouldUseAceLow(nonJokersSortedHigh);
        return SortSequenceCards(cards, aceIsLow);
    }
    
    private static List<Card> SortSequenceCards(List<Card> cards, bool aceIsLow)
    {
        var jokers = cards.Where(c => c.IsJoker).ToList();
        Func<Card, int> getRank = aceIsLow
            ? (c => c.Rank == Rank.Ace ? 1 : (int)c.Rank!)
            : (c => (int)c.Rank!);
        
        var nonJokers = cards.Where(c => !c.IsJoker).OrderBy(getRank).ToList();
        
        if (nonJokers.Count == 0) return cards;
        
        var result = new List<Card>();
        int jokerIdx = 0;
        
        var ranks = nonJokers.Select(getRank).ToList();
        int minRank = ranks.First();
        int maxRank = ranks.Last();
        int rangeNeeded = maxRank - minRank + 1;
        int gaps = rangeNeeded - nonJokers.Count;
        
        if (gaps <= jokers.Count)
        {
            int nonJokerPos = 0;
            for (int r = minRank; r <= maxRank; r++)
            {
                if (nonJokerPos < nonJokers.Count && ranks[nonJokerPos] == r)
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
    
    /// <summary>
    /// Minimum rank of other (non-Ace) cards in order for Ace to be treated as rank 1 (low).
    /// This covers sequences such as A-2-3, A-2-3-4, A-joker-3, etc.
    /// A King alongside an Ace always signals high-Ace (Q-K-A), so King overrides this threshold.
    /// </summary>
    private const int AceLowOtherRankThreshold = 5;

    /// <summary>
    /// Returns true when Ace should be treated as rank 1 (low) rather than rank 14 (high).
    /// This applies when the sequence contains an Ace alongside low-rank cards (≤ AceLowOtherRankThreshold)
    /// but no King (which would indicate a high-Ace Q-K-A sequence).
    /// </summary>
    private static bool ShouldUseAceLow(List<Card> nonJokersSortedHigh)
    {
        if (!nonJokersSortedHigh.Any(c => c.Rank == Rank.Ace)) return false;
        var otherRanks = nonJokersSortedHigh.Where(c => c.Rank != Rank.Ace)
                                             .Select(c => (int)c.Rank!).ToList();
        if (!otherRanks.Any()) return false;
        return otherRanks.Min() <= AceLowOtherRankThreshold
            && !nonJokersSortedHigh.Any(c => c.Rank == Rank.King);
    }

    /// <summary>
    /// Returns true when an Ace card is in the low (rank-1) position for an existing sequence.
    /// Used by CanAddToLeft / CanAddMultipleToLeft to determine effective left rank.
    /// </summary>
    private static bool IsSequenceAceLow(Sequence seq)
    {
        var firstNonJoker = seq.Cards.FirstOrDefault(c => !c.IsJoker);
        if (firstNonJoker?.Rank != Rank.Ace) return false;
        var secondNonJoker = seq.Cards.Where(c => !c.IsJoker).Skip(1).FirstOrDefault();
        return secondNonJoker != null && (int)secondNonJoker.Rank! <= AceLowOtherRankThreshold;
    }
    
    private static bool AreConsecutiveWithJokers(List<Card> sortedCards, bool aceIsLow = false)
    {
        Func<Card, int> getRank = aceIsLow
            ? (c => c.Rank == Rank.Ace ? 1 : (int)c.Rank!)
            : (c => (int)c.Rank!);
        
        var nonJokers = sortedCards.Where(c => !c.IsJoker).OrderBy(getRank).ToList();
        if (nonJokers.Count == 0) return false;
        
        int jokerCount = sortedCards.Count(c => c.IsJoker);
        var ranks = nonJokers.Select(getRank).ToList();
        int minRank = ranks.First();
        int maxRank = ranks.Last();
        
        for (int i = 0; i < ranks.Count - 1; i++)
        {
            if (ranks[i] == ranks[i + 1]) return false;
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

        // Detect Ace-low sequence (Ace is the leftmost non-joker, followed by low cards)
        bool seqIsAceLow = IsSequenceAceLow(seq);

        int effectiveLeftRank = seqIsAceLow
            ? 1 - jokersAtStartOfSeq
            : (int)firstNonJokerOfSeq.Rank! - jokersAtStartOfSeq;

        // Non-Ace new cards must be strictly below effectiveLeftRank.
        // Ace is excluded here because it may act as rank 1 (handled by IsValidSequence).
        if (nonJokersNew.Where(c => c.Rank != Rank.Ace).Any(c => (int)c.Rank! >= effectiveLeftRank)) return false;

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

        // Detect Ace-low sequence: Ace is leftmost non-joker, followed by a low card
        bool seqIsAceLow = IsSequenceAceLow(seq);

        int effectiveLeftRank = seqIsAceLow
            ? 1 - jokersAtStart   // Ace acts as rank 1
            : (int)leftMostNonJoker.Rank! - jokersAtStart;

        // Special case: Ace can be added to the left when sequence starts at rank 2 (Ace acts as rank 1)
        if (!seqIsAceLow && card.Rank == Rank.Ace && effectiveLeftRank == 2)
            return true;
        
        int cardRankValue = (int)card.Rank!;
        return cardRankValue == effectiveLeftRank - 1;
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
