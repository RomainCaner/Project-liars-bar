// Tests de la logique (règles, bots, réseau) sans Unity.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Menteur.Core;
using Menteur.Session;

static class Program
{
    static int failed, passed;

    static void Check(bool cond, string what)
    {
        if (!cond) throw new Exception("échec : " + what);
    }

    static void Throws(Action a, string what)
    {
        try { a(); }
        catch (GameError) { return; }
        throw new Exception("erreur attendue : " + what);
    }

    static void Test(string name, Action body)
    {
        try
        {
            body();
            passed++;
            Console.WriteLine("  ok   " + name);
        }
        catch (Exception e)
        {
            failed++;
            Console.WriteLine("  FAIL " + name + " — " + e.Message);
        }
    }

    static GameEngine Two(int seed = 1)
    {
        var e = new GameEngine(new Random(seed));
        e.AddPlayer("a", "Alice");
        e.AddPlayer("b", "Bob");
        return e;
    }

    static void Rig(GameEngine e, Rank table, params (string id, Rank[] hand)[] hands)
    {
        e.TableRank = table;
        var id = 1000;
        foreach (var (pid, hand) in hands) e.SeatOf(pid).Hand = hand.Select(r => new Card(id++, r)).ToList();
    }

    static void WaitFor(Func<bool> cond, Action tick, double seconds, string what)
    {
        var sw = Stopwatch.StartNew();
        while (!cond())
        {
            if (sw.Elapsed.TotalSeconds > seconds) throw new Exception("délai dépassé : " + what);
            tick();
            Thread.Sleep(1);
        }
    }

    /// <summary>Fait jouer une session automatiquement avec l'IA.</summary>
    static Action Autoplay(GameSession s, int seed)
    {
        var brain = new BotBrain(new Random(seed));
        var last = -1;
        return () =>
        {
            s.Tick();
            var v = s.View;
            if (v == null || v.Version == last) return;
            last = v.Version;
            switch (brain.Decide(v))
            {
                case BotPlay p: s.Play(p.CardIds); break;
                case BotCall _: s.CallLiar(); break;
                case BotShoot _: s.Shoot(); break;
            }
        };
    }

    static int Main()
    {
        Test("JSON aller-retour", () =>
        {
            var e = Two();
            e.StartGame();
            var json = MiniJson.Serialize(e.ViewFor("a"));
            var v = GameView.FromJson(MiniJson.Deserialize(json).Obj());
            Check(v.Players.Count == 2 && v.Hand.Count == 5 && v.MyId == "a", "vue relue");
            Check(MiniJson.Deserialize("{\"s\":\"é\\n\\\"x\\\"\",\"n\":-2.5,\"l\":[1,true,null]}").Obj().Str("s") == "é\n\"x\"", "échappements");
            Check(!json.ToLowerInvariant().Contains("chamber"), "position des balles secrète");
        });

        Test("paquet", () =>
        {
            for (var n = 2; n <= 4; n++) Check(Rules.DeckComposition(n).Values.Sum() == 20, "20 cartes");
            for (var n = 5; n <= 6; n++) Check(Rules.DeckComposition(n).Values.Sum() >= n * 5, "assez de cartes");
        });

        Test("distribution et coups invalides", () =>
        {
            var e = Two();
            e.StartGame();
            Check(e.Seats.All(s => s.Hand.Count == 5), "5 cartes");
            Check(e.Seats.SelectMany(s => s.Hand).Select(c => c.Id).Distinct().Count() == 10, "pas de doublon");
            var cur = e.CurrentSeat;
            var other = e.Seats.First(s => s != cur);
            Throws(() => e.PlayCards(other.Id, new[] { other.Hand[0].Id }), "hors tour");
            Throws(() => e.PlayCards(cur.Id, new int[0]), "zéro carte");
            Throws(() => e.PlayCards(cur.Id, cur.Hand.Take(4).Select(c => c.Id).ToList()), "4 cartes");
            Throws(() => e.PlayCards(cur.Id, new[] { other.Hand[0].Id }), "carte d'un autre");
            Throws(() => e.CallLiar(cur.Id), "rien à contester");
        });

        Test("accusation à tort / mensonge démasqué", () =>
        {
            var e = Two();
            e.StartGame();
            e.CurrentIndex = 0;
            Rig(e, Rank.Queen, ("a", new[] { Rank.Queen, Rank.Joker, Rank.King }), ("b", new[] { Rank.Ace }));
            e.PlayCards("a", e.SeatOf("a").Hand.Take(2).Select(c => c.Id).ToList());
            e.CallLiar("b");
            Check(!e.Reveal.WasLie && e.Reveal.LoserId == "b", "l'accusateur tire");
            e.ProceedToRoulette();
            Check(e.ShooterId == "b", "tireur");

            e = Two();
            e.StartGame();
            e.CurrentIndex = 0;
            Rig(e, Rank.King, ("a", new[] { Rank.King, Rank.Ace }), ("b", new[] { Rank.Queen }));
            e.PlayCards("a", e.SeatOf("a").Hand.Select(c => c.Id).ToList());
            e.CallLiar("b");
            Check(e.Reveal.WasLie && e.Reveal.LoserId == "a", "le menteur tire");
        });

        Test("dernier avec des cartes : accusation obligatoire", () =>
        {
            var e = Two();
            e.StartGame();
            e.CurrentIndex = 0;
            Rig(e, Rank.Ace, ("a", new[] { Rank.Ace }), ("b", new[] { Rank.King, Rank.Queen }));
            e.PlayCards("a", new[] { e.SeatOf("a").Hand[0].Id });
            var b = e.SeatOf("b");
            Check(e.CurrentSeat == b && e.MustCall(b), "doit accuser");
            Throws(() => e.PlayCards("b", new[] { b.Hand[0].Id }), "ne peut pas poser");
            e.CallLiar("b");
        });

        Test("barillet et variantes", () =>
        {
            var e = Two();
            e.StartGame();
            var b = e.SeatOf("b");
            b.BulletChambers = new HashSet<int> { 2 };
            for (var shot = 0; shot < 3; shot++)
            {
                e.Phase = Phase.Roulette;
                e.ShooterId = "b";
                e.LastShotDied = null;
                e.PullTrigger("b");
                Check(e.LastShotDied == (shot == 2), "tir " + shot);
            }
            e.ProceedAfterShot();
            Check(e.Phase == Phase.GameOver && e.WinnerId == "a", "gagnant");

            foreach (var j in new[] { 0, 2, 4 })
            {
                var g = Two();
                g.UpdateSettings(new GameSettings { Jokers = j });
                g.StartGame();
                Check(g.Composition[Rank.Joker] == j, "jokers " + j);
            }
            Throws(() => Two().UpdateSettings(new GameSettings { Jokers = 3 }), "jokers invalides");
            var h = Two();
            h.UpdateSettings(new GameSettings { Bullets = 2 });
            h.StartGame();
            Check(h.Seats.All(s => s.BulletChambers.Count == 2), "2 balles");
            Check(new GameSettings { Respin = true }.Risk(0) == new GameSettings { Respin = true }.Risk(5), "risque constant");
        });

        Test("2000 parties complètes entre bots", () =>
        {
            var pick = new Random(42);
            for (var g = 0; g < 2000; g++)
            {
                var n = 2 + pick.Next(5);
                var e = new GameEngine(new Random(g));
                var brains = new Dictionary<string, BotBrain>();
                for (var i = 0; i < n; i++)
                {
                    e.AddPlayer("p" + i, "P" + i, isBot: true);
                    brains["p" + i] = new BotBrain(new Random(g * 10 + i));
                }
                if (g % 3 == 1) e.UpdateSettings(new GameSettings { Jokers = 4, Bullets = 2 });
                if (g % 3 == 2) e.UpdateSettings(new GameSettings { Jokers = 0, Respin = true });
                e.StartGame();
                var steps = 0;
                while (e.Phase != Phase.GameOver)
                {
                    Check(++steps < 20000, "partie bloquée");
                    switch (e.Phase)
                    {
                        case Phase.Playing:
                            var cur = e.CurrentSeat;
                            switch (brains[cur.Id].Decide(GameView.FromJson(e.ViewFor(cur.Id))))
                            {
                                case BotPlay p: e.PlayCards(cur.Id, p.CardIds); break;
                                case BotCall _: e.CallLiar(cur.Id); break;
                                default: throw new Exception("aucune action");
                            }
                            break;
                        case Phase.Reveal: e.ProceedToRoulette(); break;
                        case Phase.Roulette:
                            if (e.LastShotDied == null) e.PullTrigger(e.ShooterId);
                            else e.ProceedAfterShot();
                            break;
                    }
                }
                Check(e.AliveSeats.Count == 1 && e.WinnerId == e.AliveSeats[0].Id, "un seul survivant");
            }
        });

        Test("partie en réseau : hôte + 2 invités + 1 bot", () =>
        {
            var host = new HostSession("Hôte", true, HostTimings.Instant, new Random(7));
            host.StartNetwork();
            var port = host.Port.Value;
            var c1 = new ClientSession("127.0.0.1", port, "Léa", "c1");
            var c2 = new ClientSession("127.0.0.1", port, "Léa", "c2");
            Action idle = () => { host.Tick(); c1.Tick(); c2.Tick(); };
            WaitFor(() => host.Engine.Seats.Count == 3, idle, 10, "invités connectés");
            Check(host.Engine.Seats.Any(s => s.Name == "Léa 2"), "pseudos dédoublonnés");
            c2.ChooseSkin(4);
            WaitFor(() => host.Engine.SeatOf("c2").Skin == 4, idle, 5, "choix du personnage");
            host.AddBot();
            WaitFor(() => c1.View != null && c1.View.Players.Count == 4, idle, 5, "état diffusé");
            var play = new[] { Autoplay(host, 1), Autoplay(c1, 2), Autoplay(c2, 3) };
            host.StartGame();
            WaitFor(() => host.Engine.Phase == Phase.GameOver, () => { foreach (var p in play) p(); }, 60, "fin de partie");
            WaitFor(() => c1.View.Phase == Phase.GameOver && c2.View.Phase == Phase.GameOver, idle, 5, "invités à jour");
            Check(c1.View.WinnerId == host.Engine.WinnerId && c2.View.WinnerId == host.Engine.WinnerId, "même gagnant");
            c1.Close();
            c2.Close();
            host.Close();
        });

        Test("déconnexion en partie : un bot prend la place", () =>
        {
            var host = new HostSession("Hôte", true, HostTimings.Instant, new Random(9));
            host.StartNetwork();
            var c = new ClientSession("127.0.0.1", host.Port.Value, "Tom", "t");
            Action idle = () => { host.Tick(); c.Tick(); };
            WaitFor(() => host.Engine.Seats.Count == 2, idle, 10, "connexion");
            var play = Autoplay(host, 4);
            host.StartGame();
            c.Close();
            WaitFor(() => host.Engine.Phase == Phase.GameOver, play, 30, "fin de partie");
            Check(!host.Engine.SeatOf("t").Connected, "marqué déconnecté");
            host.Close();
        });

        Test("partie lancée : les retardataires sont refusés", () =>
        {
            var host = new HostSession("Hôte", true, null, new Random(1));
            host.StartNetwork();
            host.AddBot();
            host.StartGame();
            var late = new ClientSession("127.0.0.1", host.Port.Value, "Retard", "r");
            WaitFor(() => late.Closed, () => { host.Tick(); late.Tick(); }, 10, "refus");
            Check(late.ClosedReason != null && late.ClosedReason.Contains("déjà commencé"), "raison : " + late.ClosedReason);
            host.Close();
        });

        Test("hôte injoignable", () =>
        {
            var c = new ClientSession("127.0.0.1", 1, "X", "x");
            WaitFor(() => c.Closed, c.Tick, 10, "échec de connexion");
            Check(c.ClosedReason.Contains("Impossible de joindre"), "message");
        });

        Test("minuteur : l'IA joue pour un joueur trop lent", () =>
        {
            var host = new HostSession("Hôte", false, HostTimings.Instant, new Random(3));
            host.AddBot();
            host.UpdateSettings(new GameSettings { TurnSeconds = 20 });
            host.StartGame();
            WaitFor(() => host.Engine.Phase == Phase.GameOver, host.Tick, 30, "fin sans intervention");
        });

        Console.WriteLine(failed == 0 ? $"\n{passed} tests réussis" : $"\n{failed} échec(s) sur {passed + failed}");
        return failed == 0 ? 0 : 1;
    }
}
