using System;
using System.Collections.Generic;

namespace Menteur.Core
{
    public enum Rank { King = 0, Queen = 1, Ace = 2, Joker = 3 }

    public static class RankInfo
    {
        public static string Label(Rank r) => r switch
        {
            Rank.King => "Roi",
            Rank.Queen => "Dame",
            Rank.Ace => "As",
            _ => "Joker",
        };

        public static string Plural(Rank r) => r switch
        {
            Rank.King => "Rois",
            Rank.Queen => "Dames",
            Rank.Ace => "As",
            _ => "Jokers",
        };

        public static string Short(Rank r) => r switch
        {
            Rank.King => "R",
            Rank.Queen => "D",
            Rank.Ace => "A",
            _ => "J",
        };

        public static readonly Rank[] TableRanks = { Rank.King, Rank.Queen, Rank.Ace };
    }

    public readonly struct Card : IEquatable<Card>
    {
        public readonly int Id;
        public readonly Rank Rank;

        public Card(int id, Rank rank)
        {
            Id = id;
            Rank = rank;
        }

        /// <summary>Une carte « dit la vérité » si c'est la carte du tour ou un joker.</summary>
        public bool Matches(Rank table) => Rank == table || Rank == Rank.Joker;

        public Dictionary<string, object> ToJson() => new Dictionary<string, object> { { "id", Id }, { "r", (int)Rank } };

        public static Card FromJson(Dictionary<string, object> j) => new Card(j.Int("id"), (Rank)j.Int("r"));

        public bool Equals(Card o) => o.Id == Id && o.Rank == Rank;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => Id * 31 + (int)Rank;
        public override string ToString() => RankInfo.Short(Rank) + "#" + Id;
    }

    public static class Rules
    {
        public const int CardsPerHand = 5;
        public const int MaxCardsPerPlay = 3;
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;
        public const int Chambers = 6;
        public const int SkinCount = 9;
        public static readonly string[] SkinNames =
        {
            "Chevalier", "Barbare", "Mage", "Voleuse", "Encapuchonnée",
            "Squelette", "Guerrier mort", "Spectre", "Liche",
        };

        /// <summary>Jusqu'à 4 joueurs : 6 Rois, 6 Dames, 6 As (+ jokers) ; davantage au-delà.</summary>
        public static Dictionary<Rank, int> DeckComposition(int players, int? jokers = null)
        {
            var j = jokers ?? (players <= 4 ? 2 : 3);
            var needed = players * CardsPerHand - j;
            var perRank = (int)Math.Ceiling(needed / 3.0);
            if (perRank < 6) perRank = 6;
            return new Dictionary<Rank, int>
            {
                { Rank.King, perRank }, { Rank.Queen, perRank }, { Rank.Ace, perRank }, { Rank.Joker, j },
            };
        }

        public static List<Card> BuildDeck(Dictionary<Rank, int> composition)
        {
            var deck = new List<Card>();
            var id = 0;
            foreach (Rank r in Enum.GetValues(typeof(Rank)))
            {
                composition.TryGetValue(r, out var n);
                for (var i = 0; i < n; i++) deck.Add(new Card(id++, r));
            }
            return deck;
        }
    }
}
