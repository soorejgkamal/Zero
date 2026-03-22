using Zero.Shared.Models;

namespace Zero.Client.Helpers;

public static class CardSorter
{
    public static List<Card> SortHand(List<Card> hand)
    {
        return hand
            .OrderBy(c => c.IsJoker ? 5 : (int)c.Suit)
            .ThenBy(c => c.Rank ?? 0)
            .ToList();
    }

    public static Dictionary<Suit, List<Card>> GroupBySuit(List<Card> hand)
    {
        var groups = new Dictionary<Suit, List<Card>>();
        
        foreach (var card in hand)
        {
            if (!groups.ContainsKey(card.Suit))
                groups[card.Suit] = new List<Card>();
            groups[card.Suit].Add(card);
        }

        foreach (var suit in groups.Keys)
        {
            groups[suit] = groups[suit].OrderBy(c => c.Rank ?? 0).ToList();
        }

        return groups;
    }

    public static List<List<Card>> FindPotentialSequences(List<Card> hand, Suit suit)
    {
        var suitCards = hand.Where(c => c.Suit == suit || c.IsJoker).OrderBy(c => c.Rank ?? 0).ToList();
        var sequences = new List<List<Card>>();

        if (suitCards.Count < 3) return sequences;

        for (int i = 0; i < suitCards.Count - 2; i++)
        {
            var seq = new List<Card> { suitCards[i] };
            
            for (int j = i + 1; j < suitCards.Count; j++)
            {
                if (CanContinueSequence(seq, suitCards[j]))
                {
                    seq.Add(suitCards[j]);
                }
                else
                {
                    break;
                }
            }

            if (seq.Count >= 3)
            {
                sequences.Add(seq);
            }
        }

        return sequences;
    }

    private static bool CanContinueSequence(List<Card> seq, Card card)
    {
        if (card.IsJoker) return true;
        
        var lastNonJoker = seq.LastOrDefault(c => !c.IsJoker);
        if (lastNonJoker == null) return true;

        if (card.Suit != lastNonJoker.Suit) return false;

        int gap = (int)card.Rank! - (int)lastNonJoker.Rank!;
        int jokersAfterLast = seq.Skip(seq.IndexOf(lastNonJoker) + 1).Count(c => c.IsJoker);

        return gap == jokersAfterLast + 1;
    }
}
