namespace DeathRoll.Data;

public static class Cards
{
    public const string BaseCard = @"
┌─────────┐
│{0}{1}       │
│         │
│         │
│         │
│         │
│         │
│       {1}{0}│
└─────────┘";
    public const string SuitCard = @"{0}";

    public const string BlankCard = @"
┌─────────┐
│         │
│         │
│         │
│         │
│         │
│         │
│         │
└─────────┘";

    public static string[] ShowCard(Card card)
    {
        var space = card.Rank != 10 ? " " : "";
        var (rank, suit) = ParseRankAndSuit(card);

        return new[]
        {
            !card.IsHidden ? string.Format(BaseCard, rank, space) : BlankCard,
            !card.IsHidden ? string.Format(SuitCard, suit) : "",
        };
    }

    public static string ShowCardSimple(Card card)
    {
        var (rank, suit) = ParseRankAndSuit(card);
        return !card.IsHidden ? $"{rank}{suit}" : "";
    }

    private static (string, string) ParseRankAndSuit(Card card)
    {
        return (
            card.Rank switch
            {
                1 => "",
                11 => "",
                12 => "",
                13 => "",
                2  => "",
                3  => "",
                4  => "",
                5  => "",
                6  => "",
                7  => "",
                8  => "",
                9  => "",
                10 => "",
                _ => card.Rank.ToString()
            },
            card.Suit switch
            {
                0 => "♠",
                1 => "♥",
                2 => "♦",
                3 => "♣",
                _ => " ",
            }
        );
    }

    public class Card
    {
        public readonly int Rank;
        public readonly int Value;
        public readonly int Suit;
        public readonly bool IsAce;

        public bool IsHidden;

        public Card(int value)
        {

        }

        public Card (int rank, int suit, bool isHidden = false)
        {
            Rank = rank;
            Suit = suit;
            IsHidden = isHidden;

            Value = rank switch
            {
                > 10 => 10,
                _ => rank
            };
            IsAce = rank == 1;
        }
    }
}
