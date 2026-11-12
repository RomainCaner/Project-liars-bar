// Sessions hôte / invité consommées par l'interface. C# pur (pas de dépendance Unity) :
// tout avance par Tick(), appelé à chaque image.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Menteur.Core;
using Menteur.Net;

namespace Menteur.Session
{
    public abstract class GameSession
    {
        public GameView View { get; protected set; }
        public abstract bool IsHost { get; }
        public bool Closed { get; protected set; }
        public bool Reconnecting { get; protected set; }
        public string ClosedReason { get; protected set; }
        public readonly Queue<string> Toasts = new Queue<string>();

        static readonly Stopwatch clock = Stopwatch.StartNew();
        protected static double Now => clock.Elapsed.TotalSeconds;

        protected void Toast(string msg) => Toasts.Enqueue(msg);

        public abstract void Play(List<int> cardIds);
        public abstract void CallLiar();
        public abstract void Shoot();
        public abstract void ChooseSkin(int skin);

        /// <summary>À appeler à chaque image.</summary>
        public abstract void Tick();
        public abstract void Close();

        /// <summary>Identifiant stable pendant la vie de l'app (reconnexion).</summary>
        public static readonly string ClientId = Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    /// <summary>Délais des animations côté hôte, en secondes.</summary>
    public sealed class HostTimings
    {
        public double Reveal = 3.4, AfterShot = 4.0, BotMin = 1.2, BotMax = 2.8, BotShoot = 2.4, TurnUnit = 1.0;

        public static HostTimings Instant => new HostTimings
        {
            Reveal = 0, AfterShot = 0, BotMin = 0, BotMax = 0, BotShoot = 0, TurnUnit = 0.001,
        };
    }

    public sealed class HostSession : GameSession
    {
        public const string HostId = "host";
        static readonly string[] BotNames = { "Gaston", "Lulu", "Bébert", "Mimi", "Le Borgne", "Rosalie", "Dédé", "Josette" };

        public readonly GameEngine Engine;
        public readonly bool Online;
        readonly HostTimings timings;
        readonly Random rng;
        readonly Dictionary<string, BotBrain> brains = new Dictionary<string, BotBrain>();
        readonly Dictionary<LineConnection, string> clients = new Dictionary<LineConnection, string>();
        LanServer server;
        DiscoveryBroadcaster broadcaster;
        int botCounter;

        // Prochaine action automatique (bot, transition, minuteur).
        int scheduledFor = -1;
        double fireAt;
        Action pending;
        double? deadline;

        public int? Port => server?.Port;
        public override bool IsHost => true;

        public HostSession(string hostName, bool online = true, HostTimings timings = null, Random random = null)
        {
            Online = online;
            this.timings = timings ?? new HostTimings();
            rng = random ?? new Random();
            Engine = new GameEngine(random);
            Engine.AddPlayer(HostId, hostName);
            Refresh();
        }

        public void StartNetwork()
        {
            if (!Online) return;
            server = new LanServer();
            server.Start();
            broadcaster = new DiscoveryBroadcaster();
            UpdateAnnouncement();
            broadcaster.Start();
        }

        // -------------------------------------------------------------- réseau

        void Send(LineConnection c, Dictionary<string, object> m) => c.Send(Protocol.Encode(m));

        void OnMessage(LineConnection c, Dictionary<string, object> m)
        {
            clients.TryGetValue(c, out var pid);
            try
            {
                switch (m.Str("t"))
                {
                    case "hello":
                        OnHello(c, m);
                        break;
                    case "ping":
                        Send(c, new Dictionary<string, object> { { "t", "pong" } });
                        break;
                    case "play":
                        if (pid == null) return;
                        Engine.PlayCards(pid, m.Get("cards").Arr().Select(x => (int)(long)x).ToList());
                        Changed();
                        break;
                    case "call":
                        if (pid == null) return;
                        Engine.CallLiar(pid);
                        Changed();
                        break;
                    case "shoot":
                        if (pid == null) return;
                        Engine.PullTrigger(pid);
                        Changed();
                        break;
                    case "skin":
                        if (pid == null) return;
                        Engine.SetSkin(pid, m.Int("skin"));
                        Changed();
                        break;
                }
            }
            catch (GameError e)
            {
                Send(c, new Dictionary<string, object> { { "t", "error" }, { "msg", e.Message } });
            }
            catch (Exception)
            {
                Send(c, new Dictionary<string, object> { { "t", "error" }, { "msg", "Action invalide" } });
            }
        }

        void Bye(LineConnection c, string msg)
        {
            Send(c, new Dictionary<string, object> { { "t", "bye" }, { "msg", msg } });
            clients.Remove(c);
            c.Close();
        }

        void OnHello(LineConnection c, Dictionary<string, object> m)
        {
            var cid = m.Str("cid") ?? "";
            var name = (m.Str("name") ?? "").Trim();
            if (name.Length == 0) name = "Joueur";
            if (name.Length > 16) name = name.Substring(0, 16);
            if (cid.Length == 0 || cid == HostId) { Bye(c, "Identifiant invalide"); return; }

            var existing = Engine.SeatOrNull(cid);
            if (existing != null && !existing.IsBot)
            {
                // Reconnexion : l'ancienne connexion éventuelle est fermée.
                foreach (var old in clients.Where(e => e.Value == cid && e.Key != c).Select(e => e.Key).ToList())
                {
                    clients.Remove(old);
                    old.Close();
                }
                clients[c] = cid;
                if (!existing.Connected) Engine.SetConnected(cid, true);
                Toast(existing.Name + " est de retour");
                Changed();
                return;
            }
            if (Engine.Phase != Phase.Lobby) { Bye(c, "La partie a déjà commencé"); return; }
            if (Engine.Seats.Count >= Rules.MaxPlayers) { Bye(c, "La table est pleine"); return; }
            Engine.AddPlayer(cid, UniqueName(name));
            clients[c] = cid;
            Toast(Engine.SeatOf(cid).Name + " a rejoint la table");
            Changed();
        }

        string UniqueName(string name)
        {
            var taken = new HashSet<string>(Engine.Seats.Select(s => s.Name.ToLowerInvariant()));
            if (!taken.Contains(name.ToLowerInvariant())) return name;
            for (var i = 2; ; i++)
            {
                var n = name + " " + i;
                if (!taken.Contains(n.ToLowerInvariant())) return n;
            }
        }

        void OnGone(LineConnection c)
        {
            if (!clients.TryGetValue(c, out var pid)) return;
            clients.Remove(c);
            if (pid == null || clients.ContainsValue(pid)) return;
            var s = Engine.SeatOrNull(pid);
            if (s == null) return;
            if (Engine.Phase == Phase.Lobby)
            {
                Engine.RemovePlayer(pid);
                Toast(s.Name + " a quitté la table");
            }
            else
            {
                Engine.SetConnected(pid, false);
                Toast(s.Name + " s'est déconnecté — un bot prend sa place");
            }
            Changed();
        }

        void UpdateAnnouncement()
        {
            if (broadcaster == null) return;
            broadcaster.Info = new Dictionary<string, object>
            {
                { "name", Engine.SeatOf(HostId).Name }, { "port", server?.Port ?? Protocol.GamePort },
                { "players", Engine.Seats.Count }, { "max", Rules.MaxPlayers },
                { "open", Engine.Phase == Phase.Lobby && Engine.Seats.Count < Rules.MaxPlayers },
            };
        }

        // --------------------------------------------------------------- salon

        void Act(Action f)
        {
            try
            {
                f();
                Changed();
            }
            catch (GameError e)
            {
                Toast(e.Message);
            }
        }

        public void AddBot()
        {
            var used = new HashSet<string>(Engine.Seats.Select(s => s.Name));
            var free = BotNames.Where(n => !used.Contains(n)).ToList();
            var name = free.Count == 0 ? "Bot " + (botCounter + 1) : free[rng.Next(free.Count)];
            var id = "bot-" + botCounter++;
            Act(() =>
            {
                Engine.AddPlayer(id, name, isBot: true);
                brains[id] = new BotBrain(rng);
            });
        }

        public void Kick(string playerId)
        {
            if (playerId == HostId) return;
            foreach (var c in clients.Where(e => e.Value == playerId).Select(e => e.Key).ToList())
                Bye(c, "Tu as été retiré de la table");
            Act(() =>
            {
                Engine.RemovePlayer(playerId);
                brains.Remove(playerId);
            });
        }

        public void UpdateSettings(GameSettings s) => Act(() => Engine.UpdateSettings(s));
        public void StartGame() => Act(Engine.StartGame);
        public void BackToLobby() => Act(Engine.BackToLobby);

        public override void Play(List<int> cardIds) => Act(() => Engine.PlayCards(HostId, cardIds));
        public override void CallLiar() => Act(() => Engine.CallLiar(HostId));
        public override void Shoot() => Act(() => Engine.PullTrigger(HostId));
        public override void ChooseSkin(int skin) => Act(() => Engine.SetSkin(HostId, skin));

        // ----------------------------------------------------------- diffusion

        Dictionary<string, object> ViewFor(string id)
        {
            var v = Engine.ViewFor(id);
            if (deadline.HasValue) v["turnLeft"] = (int)Math.Max(0, (deadline.Value - Now) * 1000);
            return v;
        }

        void Refresh() => View = GameView.FromJson(ViewFor(HostId));

        void Changed()
        {
            Schedule();
            Refresh();
            foreach (var e in clients.ToList())
            {
                if (e.Value == null) continue;
                Send(e.Key, new Dictionary<string, object> { { "t", "state" }, { "state", ViewFor(e.Value) } });
            }
            UpdateAnnouncement();
        }

        bool Automated(Seat s) => s.IsBot || !s.Connected;

        BotBrain BrainFor(string id)
        {
            if (!brains.TryGetValue(id, out var b)) brains[id] = b = new BotBrain(rng, 0.3);
            return b;
        }

        void Later(double delay, Action f)
        {
            fireAt = Now + delay;
            pending = f;
        }

        void Schedule()
        {
            if (scheduledFor == Engine.Version) return;
            scheduledFor = Engine.Version;
            pending = null;
            deadline = null;
            var secs = Engine.Settings.TurnSeconds;
            double? turnLimit = secs > 0 ? timings.TurnUnit * secs : (double?)null;
            switch (Engine.Phase)
            {
                case Phase.Reveal:
                    Later(timings.Reveal, Engine.ProceedToRoulette);
                    break;
                case Phase.Roulette:
                    if (Engine.LastShotDied != null)
                    {
                        Later(timings.AfterShot, Engine.ProceedAfterShot);
                    }
                    else
                    {
                        var shooter = Engine.SeatOf(Engine.ShooterId);
                        if (Automated(shooter)) Later(timings.BotShoot, () => Engine.PullTrigger(shooter.Id));
                        else if (turnLimit.HasValue)
                        {
                            deadline = Now + turnLimit.Value;
                            Later(turnLimit.Value, () => Engine.PullTrigger(shooter.Id));
                        }
                    }
                    break;
                case Phase.Playing:
                    var cur = Engine.CurrentSeat;
                    if (Automated(cur))
                    {
                        Later(timings.BotMin + rng.NextDouble() * (timings.BotMax - timings.BotMin), () => AutoAct(cur));
                    }
                    else if (turnLimit.HasValue)
                    {
                        // Minuteur : passé le délai, l'IA joue à la place du joueur.
                        deadline = Now + turnLimit.Value;
                        Later(turnLimit.Value, () =>
                        {
                            Toast("Temps écoulé pour " + cur.Name);
                            AutoAct(cur);
                        });
                    }
                    break;
            }
        }

        void AutoAct(Seat seat)
        {
            var action = BrainFor(seat.Id).Decide(GameView.FromJson(Engine.ViewFor(seat.Id)));
            switch (action)
            {
                case BotPlay p: Engine.PlayCards(seat.Id, p.CardIds); break;
                case BotCall _: Engine.CallLiar(seat.Id); break;
                case BotShoot _: Engine.PullTrigger(seat.Id); break;
            }
        }

        public override void Tick()
        {
            if (Closed) return;
            if (server != null)
            {
                while (server.Events.TryDequeue(out var ev))
                {
                    switch (ev.Type)
                    {
                        case NetEvent.Kind.Connected: clients[ev.Conn] = null; break;
                        case NetEvent.Kind.Message:
                            if (clients.ContainsKey(ev.Conn)) OnMessage(ev.Conn, ev.Msg);
                            break;
                        case NetEvent.Kind.Closed: OnGone(ev.Conn); break;
                    }
                }
                // Un invité muet depuis trop longtemps est considéré comme parti.
                var now = DateTime.UtcNow;
                foreach (var c in clients.Keys.Where(c => (now - c.LastSeen).TotalSeconds > 9).ToList())
                {
                    c.Close();
                    OnGone(c);
                }
            }
            if (pending != null && Now >= fireAt)
            {
                var f = pending;
                pending = null;
                try
                {
                    f();
                }
                catch (GameError)
                {
                    return;
                }
                Changed();
            }
        }

        public override void Close()
        {
            if (Closed) return;
            Closed = true;
            pending = null;
            broadcaster?.Stop();
            foreach (var c in clients.Keys.ToList())
            {
                Send(c, new Dictionary<string, object> { { "t", "bye" }, { "msg", "L'hôte a fermé la partie" } });
                c.Close();
            }
            clients.Clear();
            server?.Stop();
        }
    }

    public sealed class ClientSession : GameSession
    {
        readonly string host, name, cid;
        readonly int port;
        readonly ConcurrentQueue<NetEvent> events = new ConcurrentQueue<NetEvent>();
        LineConnection conn;
        Task<LineConnection> connecting;
        bool byeReceived;
        int attempts;
        double nextAttempt, lastPing, lastHeard;

        public override bool IsHost => false;

        /// <summary>Vrai tant que la toute première connexion n'a pas abouti.</summary>
        public bool Connecting => conn == null && !Closed && View == null;

        public ClientSession(string host, int port, string name, string cid = null)
        {
            this.host = host;
            this.port = port;
            this.name = name;
            this.cid = cid ?? ClientId;
            Open();
        }

        void Open()
        {
            connecting = LanClient.ConnectAsync(host, port, events);
        }

        void Send(Dictionary<string, object> m) => conn?.Send(Protocol.Encode(m));

        void Lost()
        {
            conn = null;
            if (byeReceived || attempts >= 6 || View == null)
            {
                Closed = true;
                ClosedReason = ClosedReason ?? (View == null
                    ? "Impossible de joindre " + host + ". Vérifie que vous êtes sur le même réseau."
                    : "Connexion perdue avec l'hôte");
                return;
            }
            Reconnecting = true;
            attempts++;
            nextAttempt = Now + 2;
        }

        public override void Tick()
        {
            if (Closed) return;
            if (connecting != null && connecting.IsCompleted)
            {
                var t = connecting;
                connecting = null;
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    conn = t.Result;
                    conn.Start();
                    Reconnecting = false;
                    attempts = 0;
                    lastHeard = lastPing = Now;
                    Send(new Dictionary<string, object> { { "t", "hello" }, { "name", name }, { "cid", cid } });
                }
                else
                {
                    Lost();
                }
            }
            if (conn == null && connecting == null && Reconnecting && Now >= nextAttempt) Open();

            while (events.TryDequeue(out var ev))
            {
                if (ev.Conn != conn) continue;
                if (ev.Type == NetEvent.Kind.Closed)
                {
                    Lost();
                    continue;
                }
                lastHeard = Now;
                var m = ev.Msg;
                switch (m.Str("t"))
                {
                    case "state":
                        var v = GameView.FromJson(m.Get("state").Obj());
                        if (View == null || v.Version >= View.Version) View = v;
                        break;
                    case "error":
                        Toast(m.Str("msg"));
                        break;
                    case "bye":
                        byeReceived = true;
                        ClosedReason = m.Str("msg");
                        break;
                }
            }
            if (conn != null)
            {
                if (Now - lastPing > 2)
                {
                    lastPing = Now;
                    Send(new Dictionary<string, object> { { "t", "ping" } });
                }
                if (Now - lastHeard > 9)
                {
                    conn.Close();
                    Lost();
                }
            }
        }

        public override void Play(List<int> cardIds) =>
            Send(new Dictionary<string, object> { { "t", "play" }, { "cards", cardIds } });

        public override void CallLiar() => Send(new Dictionary<string, object> { { "t", "call" } });
        public override void Shoot() => Send(new Dictionary<string, object> { { "t", "shoot" } });

        public override void ChooseSkin(int skin) =>
            Send(new Dictionary<string, object> { { "t", "skin" }, { "skin", skin } });

        public override void Close()
        {
            if (Closed) return;
            Closed = true;
            conn?.Close();
        }
    }
}
