// IA des bots : elle ne voit que ce qu'un joueur humain verrait (GameView).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menteur.Core
{
    public abstract class BotAction { }
    public sealed class BotPlay : BotAction { public List<int> CardIds; }
    public sealed class BotCall : BotAction { }
    public sealed class BotShoot : BotAction { }

    public sealed class BotBrain
    {
        readonly Random rng;

        /// <summary>0 = prudent, 1 = téméraire.</summary>
        public readonly double Boldness;

        public BotBrain(Random random = null, double? boldness = null)
        {
            rng = random ?? new Random();
            Boldness = boldness ?? rng.NextDouble();
        }

        public BotAction Decide(GameView v)
        {
            if (v.IMustShoot) return new BotShoot();
            if (!v.IsMyTurn) return null;
            if (v.MustCall) return new BotCall();
            var table = v.TableRank.Value;
            var matching = v.Hand.Where(c => c.Matches(table)).ToList();
            var others = v.Hand.Where(c => !c.Matches(table)).ToList();
            if (v.CanCall && ShouldCall(v, table, matching.Count)) return new BotCall();
            if (v.Hand.Count == 0) return new BotCall();
            return new BotPlay { CardIds = ChoosePlay(matching, others) };
        }

        bool ShouldCall(GameView v, Rank table, int myMatching)
        {
            var n = v.LastPlayCount;
            v.Deck.TryGetValue(table, out var t);
            v.Deck.TryGetValue(Rank.Joker, out var j);
            var possibleElsewhere = t + j - myMatching;
            if (n > possibleElsewhere) return true; // mensonge certain
            var p = 0.12 + 0.12 * (n - 1);
            p += 0.35 * myMatching / Math.Max(1.0, t + j);
            p += 0.03 * Math.Max(0, v.Pile - n);
            var accused = v.Player(v.LastPlayBy);
            if (accused != null && accused.CardCount == 0) p += 0.12;
            if (myMatching == 0) p += 0.15;
            p += (Boldness - 0.5) * 0.2;
            p = Math.Max(0.05, Math.Min(0.9, p));
            return rng.NextDouble() < p;
        }

        List<int> ChoosePlay(List<Card> matching, List<Card> others)
        {
            if (matching.Count > 0)
            {
                // Les jokers en dernier : on pose d'abord les vraies cartes.
                matching = matching.OrderBy(c => c.Rank == Rank.Joker ? 1 : 0).ToList();
                var k = 1 + rng.Next(Math.Min(Rules.MaxCardsPerPlay, matching.Count));
                var chosen = matching.Take(k).Select(c => c.Id).ToList();
                if (k < Rules.MaxCardsPerPlay && others.Count > 0 && rng.NextDouble() < 0.15 + 0.25 * Boldness)
                    chosen.Add(others[rng.Next(others.Count)].Id);
                return chosen;
            }
            var count = Math.Min(others.Count, rng.NextDouble() < 0.7 - 0.3 * Boldness ? 1 : 2);
            return others.OrderBy(_ => rng.Next()).Take(count).Select(c => c.Id).ToList();
        }
    }
}
