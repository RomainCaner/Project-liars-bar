// Vue d'une partie telle que reçue par un joueur (hôte compris).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menteur.Core
{
    public sealed class PlayerView
    {
        public string Id, Name;
        public bool IsBot, Connected, Alive;
        public int CardCount, Shots, Skin;

        public static PlayerView FromJson(Dictionary<string, object> j) => new PlayerView
        {
            Id = j.Str("id"), Name = j.Str("name"), IsBot = j.Bool("bot"), Connected = j.Bool("conn", true),
            Alive = j.Bool("alive", true), CardCount = j.Int("cards"), Shots = j.Int("shots"), Skin = j.Int("skin"),
        };
    }

    public sealed class RevealView
    {
        public string AccuserId, AccusedId;
        public List<Card> Cards;
        public bool WasLie;
        public string LoserId => WasLie ? AccusedId : AccuserId;
    }

    public sealed class GameView
    {
        public Dictionary<string, object> Raw;
        public int Version, Round, Pile, LastPlayCount;
        public Phase Phase;
        public Rank? TableRank;
        public string CurrentId, MyId, LastPlayBy, ShooterId, WinnerId;
        public List<PlayerView> Players;
        public List<Card> Hand;
        public bool MustCall;
        public RevealView Reveal;
        public bool? ShotDied;
        public Dictionary<Rank, int> Deck;
        public List<string> Log;
        public GameSettings Settings;

        /// <summary>Millisecondes restantes au minuteur au moment de la réception.</summary>
        public int? TurnLeftMs;
        public DateTime ReceivedAt;

        public static GameView FromJson(Dictionary<string, object> j)
        {
            var v = new GameView
            {
                Raw = j,
                Version = j.Int("v"),
                Phase = (Phase)j.Int("phase"),
                Round = j.Int("round"),
                CurrentId = j.Str("current"),
                MyId = j.Str("me"),
                MustCall = j.Bool("mustCall"),
                Pile = j.Int("pile"),
                ShooterId = j.Str("shooter"),
                ShotDied = j.BoolOrNull("shot"),
                WinnerId = j.Str("winner"),
                Settings = GameSettings.FromJson(j.Get("settings").Obj()),
                TurnLeftMs = j.IntOrNull("turnLeft"),
                ReceivedAt = DateTime.UtcNow,
            };
            var t = j.IntOrNull("table");
            v.TableRank = t.HasValue ? (Rank)t.Value : (Rank?)null;
            v.Players = j.Get("players").Arr().Select(p => PlayerView.FromJson(p.Obj())).ToList();
            v.Hand = j.Get("hand").Arr().Select(c => Card.FromJson(c.Obj())).ToList();
            var lp = j.Get("lastPlay").Obj();
            v.LastPlayBy = lp?.Str("by");
            v.LastPlayCount = lp?.Int("count") ?? 0;
            var r = j.Get("reveal").Obj();
            if (r != null)
            {
                v.Reveal = new RevealView
                {
                    AccuserId = r.Str("accuser"), AccusedId = r.Str("accused"), WasLie = r.Bool("lie"),
                    Cards = r.Get("cards").Arr().Select(c => Card.FromJson(c.Obj())).ToList(),
                };
            }
            v.Deck = new Dictionary<Rank, int>();
            var d = j.Get("deck").Obj();
            if (d != null) foreach (var e in d) v.Deck[(Rank)int.Parse(e.Key)] = d.Int(e.Key);
            v.Log = j.Get("log").Arr().Select(x => x as string ?? "").ToList();
            return v;
        }

        public PlayerView Player(string id) => id == null ? null : Players.FirstOrDefault(p => p.Id == id);
        public PlayerView Me => Player(MyId);
        public string NameOf(string id) => Player(id)?.Name ?? "?";
        public bool IsMyTurn => Phase == Phase.Playing && CurrentId == MyId;
        public bool CanCall => IsMyTurn && LastPlayBy != null && LastPlayBy != MyId;
        public bool CanPlay => IsMyTurn && !MustCall && Hand.Count > 0;
        public bool IMustShoot => Phase == Phase.Roulette && ShooterId == MyId && ShotDied == null;
        public double RiskFor(PlayerView p) => Settings.Risk(p.Shots);

        public int? SecondsLeft
        {
            get
            {
                if (!TurnLeftMs.HasValue) return null;
                var left = TurnLeftMs.Value - (DateTime.UtcNow - ReceivedAt).TotalMilliseconds;
                return Math.Max(0, (int)Math.Ceiling(left / 1000.0));
            }
        }
    }
}
