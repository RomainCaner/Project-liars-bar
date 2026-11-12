// Moteur de jeu autoritaire (tourne uniquement chez l'hôte). Aucune dépendance Unity :
// il est compilé et testé à part (voir unity/Tests).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menteur.Core
{
    public enum Phase { Lobby = 0, Playing = 1, Reveal = 2, Roulette = 3, GameOver = 4 }

    public sealed class GameError : Exception
    {
        public GameError(string message) : base(message) { }
    }

    /// <summary>Réglages choisis par l'hôte dans le salon.</summary>
    public sealed class GameSettings
    {
        public int Jokers = 2;        // 0, 2 ou 4
        public int Bullets = 1;       // 1 ou 2
        public bool Respin;           // barillet re-tourné avant chaque tir
        public int TurnSeconds;       // 0 = pas de minuteur

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public double Risk(int shots)
        {
            if (Respin) return (double)Bullets / Rules.Chambers;
            var left = Rules.Chambers - shots;
            return left <= 0 ? 1 : Math.Min(1, (double)Bullets / left);
        }

        public Dictionary<string, object> ToJson() => new Dictionary<string, object>
        {
            { "jokers", Jokers }, { "bullets", Bullets }, { "respin", Respin }, { "turn", TurnSeconds },
        };

        public static GameSettings FromJson(Dictionary<string, object> j)
        {
            if (j == null) return new GameSettings();
            return new GameSettings
            {
                Jokers = j.Int("jokers", 2), Bullets = j.Int("bullets", 1),
                Respin = j.Bool("respin"), TurnSeconds = j.Int("turn"),
            };
        }
    }

    public sealed class Seat
    {
        public readonly string Id;
        public string Name;
        public bool IsBot;
        public bool Connected = true;
        public bool Alive = true;
        public int Skin;
        public List<Card> Hand = new List<Card>();
        public HashSet<int> BulletChambers = new HashSet<int> { 0 };
        public int ShotsFired;

        public Seat(string id, string name, bool isBot, int skin)
        {
            Id = id;
            Name = name;
            IsBot = isBot;
            Skin = skin;
        }
    }

    public sealed class PlayRecord
    {
        public readonly string PlayerId;
        public readonly List<Card> Cards;
        public PlayRecord(string playerId, List<Card> cards) { PlayerId = playerId; Cards = cards; }
    }

    public sealed class RevealInfo
    {
        public string AccuserId, AccusedId;
        public List<Card> Cards;
        public bool WasLie;
        public string LoserId => WasLie ? AccusedId : AccuserId;
    }

    public sealed class GameEngine
    {
        readonly Random rng;
        public readonly List<Seat> Seats = new List<Seat>();

        public Phase Phase = Phase.Lobby;
        public Rank? TableRank;
        public int Round;
        public int CurrentIndex;
        public PlayRecord LastPlay;
        public int PileCount;
        public RevealInfo Reveal;
        public string ShooterId;
        public bool? LastShotDied;
        public string WinnerId;
        public Dictionary<Rank, int> Composition = Rules.DeckComposition(2);
        public GameSettings Settings = new GameSettings();
        public int Version;
        public readonly List<string> Log = new List<string>();

        public GameEngine(Random random = null) { rng = random ?? new Random(); }

        // ------------------------------------------------------------ helpers

        public Seat SeatOf(string id) => Seats.FirstOrDefault(s => s.Id == id) ?? throw new GameError("Joueur inconnu");
        public Seat SeatOrNull(string id) => Seats.FirstOrDefault(s => s.Id == id);
        public Seat CurrentSeat => Phase == Phase.Playing && Seats.Count > 0 ? Seats[CurrentIndex] : null;
        public List<Seat> AliveSeats => Seats.Where(s => s.Alive).ToList();

        /// <summary>Le joueur doit accuser : il est le dernier à avoir des cartes.</summary>
        public bool MustCall(Seat s)
        {
            if (LastPlay == null) return false;
            if (s.Hand.Count == 0) return true;
            return !Seats.Any(o => o != s && o.Alive && o.Hand.Count > 0);
        }

        void AddLog(string line)
        {
            Log.Add(line);
            if (Log.Count > 40) Log.RemoveAt(0);
        }

        void Touch() => Version++;

        // -------------------------------------------------------------- salon

        public void AddPlayer(string id, string name, bool isBot = false, int? skin = null)
        {
            if (Phase != Phase.Lobby) throw new GameError("La partie a déjà commencé");
            if (Seats.Count >= Rules.MaxPlayers) throw new GameError("La table est pleine");
            if (SeatOrNull(id) != null) throw new GameError("Joueur déjà présent");
            Seats.Add(new Seat(id, name, isBot, skin ?? FreeSkin(isBot)));
            AddLog(name + " rejoint la table");
            Touch();
        }

        int FreeSkin(bool random)
        {
            var used = new HashSet<int>(Seats.Select(s => s.Skin));
            var free = Enumerable.Range(0, Rules.SkinCount).Where(i => !used.Contains(i)).ToList();
            if (free.Count == 0) return Seats.Count % Rules.SkinCount;
            return random ? free[rng.Next(free.Count)] : free[0];
        }

        public void SetSkin(string id, int skin)
        {
            if (Phase != Phase.Lobby) throw new GameError("La partie a déjà commencé");
            if (skin < 0 || skin >= Rules.SkinCount) throw new GameError("Personnage inconnu");
            SeatOf(id).Skin = skin;
            Touch();
        }

        public void UpdateSettings(GameSettings s)
        {
            if (Phase != Phase.Lobby && Phase != Phase.GameOver) throw new GameError("Partie en cours");
            if ((s.Jokers != 0 && s.Jokers != 2 && s.Jokers != 4) || s.Bullets < 1 || s.Bullets > 2 || s.TurnSeconds < 0)
                throw new GameError("Réglage invalide");
            Settings = s.Clone();
            Touch();
        }

        public void RemovePlayer(string id)
        {
            if (Phase != Phase.Lobby) throw new GameError("La partie a déjà commencé");
            var s = SeatOrNull(id);
            if (s == null) return;
            Seats.Remove(s);
            AddLog(s.Name + " quitte la table");
            Touch();
        }

        public void SetConnected(string id, bool connected)
        {
            var s = SeatOrNull(id);
            if (s == null) return;
            s.Connected = connected;
            AddLog(connected ? s.Name + " est revenu" : s.Name + " s'est déconnecté");
            Touch();
        }

        // ------------------------------------------------------------- partie

        public void StartGame()
        {
            if (Phase != Phase.Lobby && Phase != Phase.GameOver) throw new GameError("Partie déjà en cours");
            if (Seats.Count < Rules.MinPlayers) throw new GameError("Il faut au moins " + Rules.MinPlayers + " joueurs");
            Composition = Rules.DeckComposition(Seats.Count, Settings.Jokers);
            foreach (var s in Seats)
            {
                s.Alive = true;
                s.Hand = new List<Card>();
                s.ShotsFired = 0;
                var chambers = Enumerable.Range(0, Rules.Chambers).OrderBy(_ => rng.Next()).ToList();
                s.BulletChambers = new HashSet<int>(chambers.Take(Settings.Bullets));
            }
            Round = 0;
            WinnerId = null;
            Log.Clear();
            AddLog("La partie commence !");
            StartRound(rng.Next(Seats.Count));
        }

        public void BackToLobby()
        {
            Phase = Phase.Lobby;
            TableRank = null;
            LastPlay = null;
            Reveal = null;
            ShooterId = null;
            LastShotDied = null;
            WinnerId = null;
            PileCount = 0;
            foreach (var s in Seats)
            {
                s.Alive = true;
                s.Hand = new List<Card>();
                s.ShotsFired = 0;
            }
            Seats.RemoveAll(s => !s.IsBot && !s.Connected);
            Touch();
        }

        int NextAliveIndex(int from, bool includeSelf = false)
        {
            var n = Seats.Count;
            for (var k = includeSelf ? 0 : 1; k <= n; k++)
            {
                var i = (from + k) % n;
                if (Seats[i].Alive) return i;
            }
            return from;
        }

        void StartRound(int startIndex)
        {
            Round++;
            TableRank = RankInfo.TableRanks[rng.Next(RankInfo.TableRanks.Length)];
            var deck = Rules.BuildDeck(Composition).OrderBy(_ => rng.Next()).ToList();
            foreach (var s in Seats)
            {
                s.Hand = new List<Card>();
                if (!s.Alive) continue;
                for (var i = 0; i < Rules.CardsPerHand && deck.Count > 0; i++)
                {
                    s.Hand.Add(deck[deck.Count - 1]);
                    deck.RemoveAt(deck.Count - 1);
                }
                s.Hand.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            }
            LastPlay = null;
            PileCount = 0;
            Reveal = null;
            ShooterId = null;
            LastShotDied = null;
            CurrentIndex = NextAliveIndex(startIndex, includeSelf: true);
            Phase = Phase.Playing;
            AddLog("Manche " + Round + " — carte du tour : " + RankInfo.Label(TableRank.Value));
            Touch();
        }

        Seat RequireCurrent(string playerId)
        {
            if (Phase != Phase.Playing) throw new GameError("Ce n'est pas le moment");
            var cur = Seats[CurrentIndex];
            if (cur.Id != playerId) throw new GameError("Ce n'est pas ton tour");
            return cur;
        }

        public void PlayCards(string playerId, IList<int> cardIds)
        {
            var s = RequireCurrent(playerId);
            if (MustCall(s)) throw new GameError("Tu es le dernier à avoir des cartes : tu dois accuser");
            var ids = new HashSet<int>(cardIds);
            if (ids.Count == 0 || ids.Count > Rules.MaxCardsPerPlay)
                throw new GameError("Pose entre 1 et " + Rules.MaxCardsPerPlay + " cartes");
            var played = s.Hand.Where(c => ids.Contains(c.Id)).ToList();
            if (played.Count != ids.Count) throw new GameError("Carte invalide");
            s.Hand.RemoveAll(c => ids.Contains(c.Id));
            LastPlay = new PlayRecord(s.Id, played);
            PileCount += played.Count;
            var word = played.Count > 1 ? RankInfo.Plural(TableRank.Value) : RankInfo.Label(TableRank.Value);
            AddLog(s.Name + " pose " + played.Count + " carte" + (played.Count > 1 ? "s" : "") +
                   " (« " + played.Count + " " + word + " »)");
            AdvanceTurn();
            Touch();
        }

        void AdvanceTurn()
        {
            var n = Seats.Count;
            for (var k = 1; k <= n; k++)
            {
                var i = (CurrentIndex + k) % n;
                var s = Seats[i];
                if (s.Alive && s.Hand.Count > 0 && s.Id != LastPlay?.PlayerId)
                {
                    CurrentIndex = i;
                    return;
                }
            }
            CurrentIndex = NextAliveIndex(CurrentIndex);
        }

        public void CallLiar(string playerId)
        {
            var s = RequireCurrent(playerId);
            var play = LastPlay ?? throw new GameError("Rien à contester");
            if (play.PlayerId == s.Id) throw new GameError("Tu ne peux pas t'accuser");
            var lie = play.Cards.Any(c => !c.Matches(TableRank.Value));
            Reveal = new RevealInfo { AccuserId = s.Id, AccusedId = play.PlayerId, Cards = play.Cards, WasLie = lie };
            Phase = Phase.Reveal;
            var accused = SeatOf(play.PlayerId);
            AddLog(s.Name + " crie MENTEUR sur " + accused.Name + " !");
            AddLog(lie
                ? accused.Name + " mentait (" + string.Join(", ", play.Cards.Select(c => RankInfo.Label(c.Rank))) + ")"
                : accused.Name + " disait la vérité !");
            Touch();
        }

        public void ProceedToRoulette()
        {
            if (Phase != Phase.Reveal) throw new GameError("Ce n'est pas le moment");
            ShooterId = Reveal.LoserId;
            LastShotDied = null;
            Phase = Phase.Roulette;
            AddLog(SeatOf(ShooterId).Name + " prend le revolver…");
            Touch();
        }

        public void PullTrigger(string playerId)
        {
            if (Phase != Phase.Roulette) throw new GameError("Ce n'est pas le moment");
            if (ShooterId != playerId) throw new GameError("Ce n'est pas à toi de tirer");
            if (LastShotDied != null) throw new GameError("Déjà tiré");
            var s = SeatOf(playerId);
            var died = Settings.Respin
                ? rng.Next(Rules.Chambers) < Settings.Bullets
                : s.BulletChambers.Contains(s.ShotsFired);
            s.ShotsFired++;
            LastShotDied = died;
            if (died)
            {
                s.Alive = false;
                AddLog("PAN ! " + s.Name + " est éliminé.");
            }
            else
            {
                AddLog(Settings.Respin ? "Clic… " + s.Name + " survit." :
                    "Clic… " + s.Name + " survit (" + s.ShotsFired + "/" + Rules.Chambers + ").");
            }
            Touch();
        }

        public void ProceedAfterShot()
        {
            if (Phase != Phase.Roulette || LastShotDied == null) throw new GameError("Ce n'est pas le moment");
            var alive = AliveSeats;
            if (alive.Count <= 1)
            {
                Phase = Phase.GameOver;
                WinnerId = alive.Count == 0 ? null : alive[0].Id;
                if (alive.Count > 0) AddLog(alive[0].Name + " remporte la partie !");
                Touch();
                return;
            }
            StartRound(Seats.FindIndex(s => s.Id == ShooterId));
        }

        // --------------------------------------------------------- projection

        /// <summary>État vu par un joueur : ni mains adverses, ni position des balles.</summary>
        public Dictionary<string, object> ViewFor(string playerId)
        {
            var me = SeatOrNull(playerId);
            var cur = CurrentSeat;
            var players = new List<object>();
            foreach (var s in Seats)
            {
                players.Add(new Dictionary<string, object>
                {
                    { "id", s.Id }, { "name", s.Name }, { "bot", s.IsBot }, { "conn", s.Connected },
                    { "alive", s.Alive }, { "cards", s.Hand.Count }, { "shots", s.ShotsFired }, { "skin", s.Skin },
                });
            }
            var deck = new Dictionary<string, object>();
            foreach (var e in Composition) deck[((int)e.Key).ToString()] = e.Value;
            return new Dictionary<string, object>
            {
                { "v", Version },
                { "phase", (int)Phase },
                { "round", Round },
                { "table", TableRank.HasValue ? (object)(int)TableRank.Value : null },
                { "current", cur?.Id },
                { "me", playerId },
                { "players", players },
                { "hand", me == null ? new List<object>() : me.Hand.Select(c => (object)c.ToJson()).ToList() },
                { "mustCall", me != null && cur == me && MustCall(me) },
                { "lastPlay", LastPlay == null ? null : new Dictionary<string, object>
                    { { "by", LastPlay.PlayerId }, { "count", LastPlay.Cards.Count } } },
                { "pile", PileCount },
                { "reveal", Reveal == null ? null : new Dictionary<string, object>
                    {
                        { "accuser", Reveal.AccuserId }, { "accused", Reveal.AccusedId },
                        { "cards", Reveal.Cards.Select(c => (object)c.ToJson()).ToList() }, { "lie", Reveal.WasLie },
                    } },
                { "shooter", ShooterId },
                { "shot", LastShotDied },
                { "winner", WinnerId },
                { "deck", deck },
                { "settings", Settings.ToJson() },
                { "log", Log.Skip(Math.Max(0, Log.Count - 8)).Cast<object>().ToList() },
            };
        }
    }
}
