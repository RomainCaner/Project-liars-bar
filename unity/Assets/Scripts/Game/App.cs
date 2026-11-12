// Point d'entrée : écrans (accueil, rejoindre, salon, table) et interface de jeu.
// Tout est créé en code au lancement : aucune scène ni prefab à configurer.
using System.Collections.Generic;
using System.Linq;
using Menteur.Core;
using Menteur.Net;
using Menteur.Session;
using UnityEngine;

namespace Menteur.Game
{
    public sealed class App : MonoBehaviour
    {
        enum Screen_ { Home, Rules, Join, Table }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<App>() != null) return;
            var go = new GameObject("Menteur");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        TableScene table;
        Screen_ screen = Screen_.Home;
        GameSession session;
        GameView view, shownView;
        string pseudo = "", ip = "", toast, joinError;
        float toastUntil, scroll, revealAt, shotAt, flashUntil, ipsAt;
        bool confirmLeave, shootPressed;
        readonly HashSet<int> selected = new HashSet<int>();
        DiscoveryListener discovery;
        List<string> ips = new List<string>();
        readonly Texture2D[] portraits = new Texture2D[Rules.SkinCount];

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
            pseudo = PlayerPrefs.GetString("pseudo", "");
            Sfx.Init(gameObject);
            for (var i = 0; i < portraits.Length; i++) portraits[i] = Resources.Load<Texture2D>("Portraits/" + i);
            table = new GameObject("Table").AddComponent<TableScene>();
            table.transform.SetParent(transform, false);
        }

        void OnDestroy() => CloseSession();
        void OnApplicationQuit() => CloseSession();

        // -------------------------------------------------------------- sessions

        void Toast(string msg)
        {
            toast = msg;
            toastUntil = Time.time + 2.4f;
        }

        string Pseudo()
        {
            var n = (pseudo ?? "").Trim();
            if (n.Length == 0)
            {
                Toast("Choisis d'abord un pseudo");
                return null;
            }
            if (n.Length > 16) n = n.Substring(0, 16);
            PlayerPrefs.SetString("pseudo", n);
            return n;
        }

        void Host(bool online)
        {
            var n = Pseudo();
            if (n == null) return;
            var h = new HostSession(n, online);
            try
            {
                h.StartNetwork();
            }
            catch (System.Exception e)
            {
                Toast("Impossible d'ouvrir le serveur : " + e.Message);
            }
            if (!online)
            {
                h.AddBot();
                h.AddBot();
            }
            Open(h);
        }

        void Join(string host, int port)
        {
            var n = Pseudo();
            if (n == null) return;
            joinError = null;
            Open(new ClientSession(host, port, n));
        }

        void Open(GameSession s)
        {
            StopDiscovery();
            session = s;
            view = shownView = null;
            selected.Clear();
            confirmLeave = false;
            scroll = 0;
            screen = Screen_.Table;
        }

        void CloseSession()
        {
            session?.Close();
            session = null;
            view = shownView = null;
            table?.Apply(null);
            Sfx.StopAmbience();
            StopDiscovery();
        }

        void Leave()
        {
            CloseSession();
            screen = Screen_.Home;
            confirmLeave = false;
        }

        void StartDiscovery()
        {
            if (discovery != null) return;
            discovery = new DiscoveryListener();
            if (!discovery.Start()) discovery = null;
        }

        void StopDiscovery()
        {
            discovery?.Stop();
            discovery = null;
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            Sfx.Tick();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == Screen_.Table) confirmLeave = !confirmLeave;
                else if (screen != Screen_.Home)
                {
                    StopDiscovery();
                    screen = Screen_.Home;
                }
            }
            if (session == null) return;
            session.Tick();
            while (session.Toasts.Count > 0) Toast(session.Toasts.Dequeue());
            var v = session.View;
            if (v != null && !ReferenceEquals(v, shownView))
            {
                OnView(shownView, v);
                shownView = view = v;
            }
            if (session.Closed && !session.IsHost && view == null)
            {
                // Échec de la toute première connexion : retour à l'écran « Rejoindre ».
                joinError = session.ClosedReason;
                CloseSession();
                screen = Screen_.Join;
                StartDiscovery();
            }
        }

        void OnView(GameView p, GameView v)
        {
            table.Apply(v);
            var ids = new HashSet<int>(v.Hand.Select(c => c.Id));
            selected.RemoveWhere(id => !ids.Contains(id));
            if (!v.CanPlay) selected.Clear();
            if (v.ShotDied == null) shootPressed = false;
            if (p == null)
            {
                if (v.Phase != Phase.Lobby) Sfx.StartAmbience();
                return;
            }
            if (v.Phase == Phase.Playing && (p.Phase != Phase.Playing || v.Round != p.Round)) Sfx.Play("deal", 0.8f);
            if (v.Round == p.Round && v.Pile > p.Pile) Sfx.Play("play");
            if (!p.IsMyTurn && v.IsMyTurn) Sfx.Play("turn", 0.7f);
            if (p.Phase == Phase.Playing && v.Phase == Phase.Reveal)
            {
                revealAt = Time.time;
                Sfx.Play("slam");
                Sfx.Later(0.6f + 0.3f * v.Reveal.Cards.Count, v.Reveal.WasLie ? "lie" : "truth");
            }
            if (p.ShotDied == null && v.ShotDied != null)
            {
                shotAt = Time.time;
                Sfx.Play("spin");
                Sfx.Later(0.9f, v.ShotDied.Value ? "bang" : "click");
                if (v.ShotDied.Value)
                {
                    flashUntil = Time.time + 0.9f + 0.6f;
#if UNITY_ANDROID || UNITY_IOS
                    Invoke(nameof(Vibrate), 0.9f);
#endif
                }
            }
            if (p.Phase != Phase.GameOver && v.Phase == Phase.GameOver) Sfx.Later(0.6f, v.WinnerId == v.MyId ? "win" : "lose");
            if (p.Phase == Phase.Lobby && v.Phase != Phase.Lobby) Sfx.StartAmbience();
            if (v.Phase == Phase.Lobby && p.Phase != Phase.Lobby) Sfx.StopAmbience();
        }

#if UNITY_ANDROID || UNITY_IOS
        void Vibrate() => Handheld.Vibrate();
#endif

        // ------------------------------------------------------------------- IMGUI

        void OnGUI()
        {
            Ui.Begin();
            switch (screen)
            {
                case Screen_.Home: DrawHome(); break;
                case Screen_.Rules: DrawRules(); break;
                case Screen_.Join: DrawJoin(); break;
                case Screen_.Table: DrawTable(); break;
            }
            if (toast != null && Time.time < toastUntil)
            {
                var y = Ui.H - Ui.SafeBottom - 150;
                Ui.Fill(Ui.R(30, y, Ui.W - 60, 36), new Color(0.12f, 0.08f, 0.05f, 0.95f));
                Ui.Label(Ui.R(36, y, Ui.W - 72, 36), toast, 13, Ui.Cream, TextAnchor.MiddleCenter, false, true);
            }
        }

        static readonly Color Veil = new Color(0.03f, 0.08f, 0.06f, 0.88f);

        void Header(string title, System.Action back)
        {
            var y = Ui.SafeTop + 8;
            if (Ui.Button(Ui.R(8, y, 44, 40), "<", new Color(0, 0, 0, 0.3f), Ui.Cream, 22)) back();
            Ui.Label(Ui.R(60, y, Ui.W - 120, 40), title, 20, Ui.Cream, TextAnchor.MiddleCenter, true);
        }

        void DrawHome()
        {
            Ui.FillScreen(Veil);
            var y = Ui.SafeTop + Mathf.Max(20, (Ui.H - 640) * 0.35f + 24);
            Ui.Card(Ui.R(118, y + 8, 56, 81), Rank.King);
            Ui.Card(Ui.R(222, y + 8, 56, 81), Rank.Queen);
            Ui.Card(Ui.R(170, y, 56, 81), Rank.Joker);
            y += 100;
            Ui.Shadowed(Ui.R(0, y, Ui.W, 56), "MENTEUR !", 44, Ui.Gold);
            Ui.Label(Ui.R(0, y + 54, Ui.W, 22), "Bluff, cartes et barillet", 15, Ui.Muted);
            y += 100;
            Ui.Label(Ui.R(40, y, 320, 18), "Ton pseudo", 12, Ui.Muted, TextAnchor.MiddleLeft);
            pseudo = Ui.TextField(Ui.R(40, y + 20, 320, 44), pseudo, 16);
            y += 82;
            if (Ui.Primary(Ui.R(40, y, 320, 50), "CRÉER UNE PARTIE")) Host(true);
            y += 60;
            if (Ui.Primary(Ui.R(40, y, 320, 50), "REJOINDRE UNE PARTIE") && Pseudo() != null)
            {
                joinError = null;
                screen = Screen_.Join;
                StartDiscovery();
            }
            y += 60;
            if (Ui.Outline(Ui.R(40, y, 320, 48), "Entraînement contre des bots")) Host(false);
            y += 58;
            if (Ui.Outline(Ui.R(40, y, 155, 40), "Règles du jeu"))
            {
                scroll = 0;
                screen = Screen_.Rules;
            }
            if (Ui.Outline(Ui.R(205, y, 155, 40), Sfx.On ? "Son : activé" : "Son : coupé")) Sfx.On = !Sfx.On;
            y += 54;
            Ui.Label(Ui.R(30, y, Ui.W - 60, 40),
                "Tous les appareils doivent être sur le même Wi-Fi (ou sur le partage de connexion de l'hôte).",
                11.5f, new Color(1, 1, 1, 0.4f), TextAnchor.UpperCenter, false, true);
        }

        static readonly string[][] RulesText =
        {
            new[] { "Le but", "Être le dernier joueur en vie. Bluffe, démasque les menteurs et prie pour que le barillet soit clément." },
            new[] { "Le paquet", "6 Rois, 6 Dames, 6 As et 2 Jokers (un peu plus au-delà de 4 joueurs). Chaque manche, tout le monde reçoit 5 cartes et une « carte du tour » est tirée au hasard : Roi, Dame ou As." },
            new[] { "À ton tour", "Pose face cachée 1 à 3 cartes en annonçant qu'elles sont toutes la carte du tour. Tu peux mentir ! Les Jokers comptent comme n'importe quelle carte." },
            new[] { "MENTEUR !", "Au lieu de jouer, tu peux accuser le joueur précédent. Ses cartes sont révélées : s'il y a au moins une carte qui n'est ni la carte du tour ni un Joker, il a menti et c'est lui qui tire. Sinon, c'est toi." },
            new[] { "Le barillet", "Chaque joueur a son propre revolver : 6 chambres, 1 seule balle placée au hasard. À chaque tir raté, le risque augmente. Un « PAN » et tu es éliminé." },
            new[] { "Mains vides", "Un joueur sans carte est sauté. Si tu es le dernier à avoir des cartes, tu es obligé d'accuser." },
            new[] { "Variantes", "Dans le salon, l'hôte peut changer le nombre de Jokers, mettre 2 balles dans les barillets, re-tourner le barillet à chaque tir (risque constant) et activer un minuteur : passé le délai, l'IA joue à ta place." },
            new[] { "Jouer à plusieurs", "Un joueur crée la partie, les autres la rejoignent depuis le même Wi-Fi. Si le Wi-Fi bloque les connexions, l'hôte active son partage de connexion. Si la partie n'apparaît pas, tape l'adresse IP affichée chez l'hôte." },
            new[] { "Crédits", "Personnages 3D : KayKit Adventurers par Kay Lousberg (kaylousberg.com), licence CC0. Sons synthétisés pour le jeu." },
        };

        void DrawRules()
        {
            Ui.FillScreen(Veil);
            Header("Règles", () => screen = Screen_.Home);
            var top = Ui.SafeTop + 56;
            var area = Ui.R(0, top, Ui.W, Ui.H - top - Ui.SafeBottom);
            var total = 10f;
            foreach (var r in RulesText) total += 30 + Ui.TextHeight(r[1], 14, Ui.W - 48) + 16;
            Ui.BeginScroll(area, ref scroll, total);
            var y = top + 6;
            foreach (var r in RulesText)
            {
                Ui.Label(Ui.R(24, y, Ui.W - 48, 26), r[0], 17, Ui.Gold, TextAnchor.MiddleLeft, true);
                var h = Ui.TextHeight(r[1], 14, Ui.W - 48);
                Ui.Label(Ui.R(24, y + 30, Ui.W - 48, h), r[1], 14, Ui.Cream, TextAnchor.UpperLeft, false, true);
                y += 30 + h + 16;
            }
            Ui.EndScroll();
        }

        void DrawJoin()
        {
            Ui.FillScreen(Veil);
            Header("Rejoindre une partie", () =>
            {
                StopDiscovery();
                screen = Screen_.Home;
            });
            var y = Ui.SafeTop + 66;
            Ui.Label(Ui.R(24, y, Ui.W - 48, 24), "Parties à proximité", 16, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 32;
            var hosts = discovery != null ? discovery.Current() : new List<HostAnnouncement>();
            if (hosts.Count == 0)
            {
                Ui.Fill(Ui.R(24, y, Ui.W - 48, 64), new Color(0, 0, 0, 0.3f));
                Ui.Label(Ui.R(36, y, Ui.W - 72, 64),
                    discovery != null
                        ? "Recherche en cours… Demande à l'hôte de créer la partie sur le même Wi-Fi que toi."
                        : "Recherche automatique indisponible : utilise l'adresse IP.",
                    13, Ui.Muted, TextAnchor.MiddleLeft, false, true);
                y += 74;
            }
            foreach (var h in hosts.Take(5))
            {
                var label = "Table de " + h.Name + "  ·  " + h.Players + "/" + h.MaxPlayers + (h.Open ? "" : "  ·  en cours");
                if (Ui.Button(Ui.R(24, y, Ui.W - 48, 50), label, new Color(0, 0, 0, 0.4f), h.Open ? Ui.Cream : Ui.Muted, 14, h.Open))
                    Join(h.Address, h.Port);
                Ui.Label(Ui.R(24, y + 32, Ui.W - 60, 16), h.Address, 10.5f, Ui.Muted, TextAnchor.MiddleRight);
                y += 58;
            }
            y += 14;
            Ui.Label(Ui.R(24, y, Ui.W - 48, 24), "Ou par adresse IP", 16, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 32;
            ip = Ui.TextField(Ui.R(24, y, Ui.W - 48, 44), ip, 40);
            y += 54;
            if (Ui.Primary(Ui.R(24, y, Ui.W - 48, 50), "SE CONNECTER", ip.Trim().Length > 0))
            {
                var text = ip.Trim();
                var port = Protocol.GamePort;
                var colon = text.LastIndexOf(':');
                if (colon > 0)
                {
                    int.TryParse(text.Substring(colon + 1), out port);
                    if (port <= 0) port = Protocol.GamePort;
                    text = text.Substring(0, colon);
                }
                Join(text, port);
            }
            y += 62;
            if (joinError != null) Ui.Label(Ui.R(24, y, Ui.W - 48, 60), joinError, 13, Ui.Blood, TextAnchor.UpperCenter, false, true);
        }

        // ------------------------------------------------------------------- table

        void DrawTable()
        {
            if (session == null)
            {
                screen = Screen_.Home;
                return;
            }
            var client = session as ClientSession;
            if (view == null)
            {
                Ui.FillScreen(Veil);
                Ui.Label(Ui.R(0, Ui.H / 2 - 20, Ui.W, 40), "Connexion à la table…", 18, Ui.Cream);
                if (Ui.Outline(Ui.R(120, Ui.H / 2 + 40, 160, 44), "Annuler")) Leave();
                return;
            }
            if (session.Closed && client != null)
            {
                Ui.FillScreen(Veil);
                Ui.Label(Ui.R(30, Ui.H / 2 - 50, Ui.W - 60, 60), session.ClosedReason ?? "Connexion perdue", 17, Ui.Cream,
                    TextAnchor.MiddleCenter, false, true);
                if (Ui.Primary(Ui.R(120, Ui.H / 2 + 30, 160, 48), "RETOUR")) Leave();
                return;
            }

            if (view.Phase == Phase.Lobby) DrawLobby();
            else DrawGame();

            if (session != null && session.Reconnecting)
            {
                Ui.Fill(Ui.R(0, Ui.SafeTop, Ui.W, 24), Ui.Blood);
                Ui.Label(Ui.R(0, Ui.SafeTop, Ui.W, 24), "Connexion perdue — reconnexion…", 12, Color.white);
            }
            if (confirmLeave) DrawLeaveConfirm();
        }

        void DrawLeaveConfirm()
        {
            Ui.FillScreen(new Color(0, 0, 0, 0.7f));
            var y = Ui.H / 2 - 80;
            Ui.Fill(Ui.R(40, y, 320, 160), Ui.Panel);
            Ui.Frame(Ui.R(40, y, 320, 160), Ui.Gold);
            Ui.Label(Ui.R(50, y + 14, 300, 30), "Quitter la table ?", 18, Ui.Cream, TextAnchor.MiddleCenter, true);
            Ui.Label(Ui.R(56, y + 46, 288, 44),
                session.IsHost ? "La partie sera fermée pour tout le monde." : "Un bot jouera à ta place.", 13, Ui.Muted,
                TextAnchor.MiddleCenter, false, true);
            if (Ui.Outline(Ui.R(56, y + 100, 138, 44), "Rester")) confirmLeave = false;
            if (Ui.Button(Ui.R(206, y + 100, 138, 44), "Quitter", Ui.Blood, Color.white)) Leave();
        }

        static readonly int[] JokerOptions = { 0, 2, 4 };
        static readonly int[] BulletOptions = { 1, 2 };
        static readonly int[] TimerOptions = { 0, 20, 30, 45 };

        void DrawLobby()
        {
            Ui.FillScreen(Veil);
            var v = view;
            var host = session as HostSession;
            Header("Le salon", () => confirmLeave = true);
            var top = Ui.SafeTop + 54;
            var bottomH = host != null ? 118f : 50f;
            var area = Ui.R(0, top, Ui.W, Ui.H - top - Ui.SafeBottom - bottomH);
            var contentH = (host != null && host.Online ? 92 : 30) + 130 + 190 + 34 + v.Players.Count * 46 + 16;
            Ui.BeginScroll(area, ref scroll, contentH);
            var y = top + 4;

            if (host != null && host.Online)
            {
                if (Time.time > ipsAt)
                {
                    ipsAt = Time.time + 3;
                    ips = Protocol.LocalAddresses().Select(a => a.ToString()).ToList();
                }
                Ui.Fill(Ui.R(20, y, Ui.W - 40, 82), new Color(0, 0, 0, 0.35f));
                Ui.Frame(Ui.R(20, y, Ui.W - 40, 82), new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f), 1);
                Ui.Label(Ui.R(32, y + 6, Ui.W - 64, 34), "Tes amis te trouvent dans « Rejoindre ». Sinon, ils tapent cette adresse :", 12,
                    Ui.Muted, TextAnchor.UpperLeft, false, true);
                var suffix = host.Port.HasValue && host.Port.Value != Protocol.GamePort ? ":" + host.Port.Value : "";
                var shown = ips.Count == 0 ? "Aucun réseau détecté" : string.Join("   ", ips.Take(2).Select(a => a + suffix));
                Ui.Label(Ui.R(32, y + 42, Ui.W - 64, 34), shown, ips.Count > 1 ? 17 : 23, ips.Count == 0 ? Ui.Blood : Ui.Gold,
                    TextAnchor.MiddleLeft, true);
                y += 92;
            }
            else if (host != null)
            {
                Ui.Label(Ui.R(20, y, Ui.W - 40, 24), "Mode entraînement : ajoute ou retire des bots puis lance.", 12.5f, Ui.Muted,
                    TextAnchor.MiddleLeft);
                y += 30;
            }

            // Personnage
            Ui.Label(Ui.R(20, y, 200, 24), "Ton personnage", 15, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 28;
            var mySkin = v.Me != null ? v.Me.Skin : 0;
            for (var i = 0; i < Rules.SkinCount; i++)
            {
                var r = Ui.R(20 + i * 73, y, 66, 66);
                Ui.Fill(Ui.R(r.x - 2, r.y - 2, r.width + 4, r.height + 4), i == mySkin ? Ui.Gold : new Color(1, 1, 1, 0.15f));
                Ui.Fill(r, new Color(0.23f, 0.16f, 0.12f));
                if (portraits[i] != null) GUI.DrawTexture(Ui.Px(r), portraits[i], ScaleMode.ScaleAndCrop);
                Ui.Label(Ui.R(r.x - 4, y + 68, 74, 18), Rules.SkinNames[i], 10.5f, i == mySkin ? Ui.Gold : Ui.Muted,
                    TextAnchor.MiddleCenter, i == mySkin);
                if (GUI.Button(Ui.Px(r), GUIContent.none, GUIStyle.none)) session.ChooseSkin(i);
            }
            y += 102;

            // Règles de la partie
            Ui.Label(Ui.R(20, y, 300, 24), host != null ? "Règles de la partie" : "Règles (choisies par l'hôte)", 15, Ui.Gold,
                TextAnchor.MiddleLeft, true);
            y += 30;
            var s = v.Settings;
            var jokers = Chips(y, "Jokers", JokerOptions, new[] { "Aucun", "2", "4" }, s.Jokers, host != null);
            var bullets = Chips(y + 40, "Balles", BulletOptions, new[] { "1", "2" }, s.Bullets, host != null);
            var respin = Chips(y + 80, "Barillet", new[] { 0, 1 }, new[] { "Classique", "Re-tourné" }, s.Respin ? 1 : 0, host != null);
            var timer = Chips(y + 120, "Minuteur", TimerOptions, new[] { "Aucun", "20 s", "30 s", "45 s" }, s.TurnSeconds, host != null);
            if (host != null && (jokers != s.Jokers || bullets != s.Bullets || (respin == 1) != s.Respin || timer != s.TurnSeconds))
                host.UpdateSettings(new GameSettings { Jokers = jokers, Bullets = bullets, Respin = respin == 1, TurnSeconds = timer });
            y += 160;

            // Joueurs
            Ui.Label(Ui.R(20, y, 300, 24), "Joueurs (" + v.Players.Count + "/" + Rules.MaxPlayers + ")", 15, Ui.Gold,
                TextAnchor.MiddleLeft, true);
            y += 30;
            foreach (var p in v.Players.ToList())
            {
                Ui.Fill(Ui.R(20, y, Ui.W - 40, 40), new Color(0, 0, 0, 0.3f));
                var pr = Ui.R(24, y + 3, 34, 34);
                Ui.Fill(pr, new Color(0.23f, 0.16f, 0.12f));
                if (portraits[p.Skin % portraits.Length] != null)
                    GUI.DrawTexture(Ui.Px(pr), portraits[p.Skin % portraits.Length], ScaleMode.ScaleAndCrop);
                Ui.Label(Ui.R(66, y + 2, 220, 20), p.Id == v.MyId ? p.Name + " (toi)" : p.Name, 14, Ui.Cream, TextAnchor.MiddleLeft, true);
                Ui.Label(Ui.R(66, y + 21, 220, 16),
                    (p.Id == HostSession.HostId ? "Hôte" : p.IsBot ? "Bot" : "Joueur") + " · " + Rules.SkinNames[p.Skin % Rules.SkinCount],
                    11, Ui.Muted, TextAnchor.MiddleLeft);
                if (host != null && p.Id != HostSession.HostId &&
                    Ui.Button(Ui.R(Ui.W - 62, y + 4, 36, 32), "X", new Color(0, 0, 0, 0.3f), Ui.Muted, 14))
                    host.Kick(p.Id);
                y += 46;
            }
            Ui.EndScroll();

            var by = Ui.H - Ui.SafeBottom - bottomH + 6;
            if (host == null)
            {
                Ui.Label(Ui.R(0, by, Ui.W, 36), "En attente que l'hôte lance la partie…", 14, Ui.Cream);
                return;
            }
            if (Ui.Outline(Ui.R(20, by, Ui.W - 40, 46), "Ajouter un bot", v.Players.Count < Rules.MaxPlayers)) host.AddBot();
            var ready = v.Players.Count >= Rules.MinPlayers;
            if (Ui.Primary(Ui.R(20, by + 56, Ui.W - 40, 50), ready ? "LANCER LA PARTIE" : "IL FAUT AU MOINS 2 JOUEURS", ready))
                host.StartGame();
        }

        /// <summary>Ligne de réglage à choix ; renvoie la valeur choisie.</summary>
        int Chips(float y, string label, int[] values, string[] names, int current, bool editable)
        {
            Ui.Label(Ui.R(20, y, 80, 32), label, 13.5f, Ui.Cream, TextAnchor.MiddleLeft, true);
            var x = 100f;
            var w = Mathf.Min(86f, (Ui.W - 20 - x - (values.Length - 1) * 6) / values.Length);
            var result = current;
            for (var i = 0; i < values.Length; i++)
            {
                var on = values[i] == current;
                var r = Ui.R(x + i * (w + 6), y, w, 32);
                if (editable)
                {
                    if (Ui.Button(r, names[i], on ? Ui.Gold : new Color(0, 0, 0, 0.45f), on ? Ui.Ink : Ui.Cream, 12.5f)) result = values[i];
                }
                else
                {
                    Ui.Fill(r, on ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.85f) : new Color(0, 0, 0, 0.25f));
                    Ui.Label(r, names[i], 12.5f, on ? Ui.Ink : Ui.Muted, TextAnchor.MiddleCenter, true);
                }
            }
            return result;
        }

        // --------------------------------------------------------------------- jeu

        void DrawGame()
        {
            var v = view;
            var top = Ui.SafeTop;

            // Bandeau du haut.
            Ui.Fill(Ui.R(-400, 0, 1200, top + 52), new Color(0, 0, 0, 0.45f));
            if (Ui.Button(Ui.R(6, top + 6, 40, 40), "X", new Color(0, 0, 0, 0.3f), Ui.Cream, 16)) confirmLeave = true;
            Ui.Label(Ui.R(52, top + 6, 90, 40), "Manche " + v.Round, 14, Ui.Muted, TextAnchor.MiddleLeft, true);
            var secs = v.SecondsLeft;
            if (secs.HasValue)
            {
                Ui.Fill(Ui.R(132, top + 13, 50, 26), secs.Value <= 5 ? Ui.Blood : new Color(0, 0, 0, 0.5f));
                Ui.Label(Ui.R(132, top + 13, 50, 26), secs.Value + " s", 13, Color.white, TextAnchor.MiddleCenter, true);
            }
            if (Ui.Button(Ui.R(188, top + 9, 46, 34), Sfx.On ? "Son" : "Muet", new Color(0, 0, 0, 0.3f), Sfx.On ? Ui.Cream : Ui.Muted, 11))
                Sfx.On = !Sfx.On;
            if (v.TableRank.HasValue)
            {
                var k = v.TableRank.Value;
                Ui.Fill(Ui.R(240, top + 5, 154, 42), new Color(0, 0, 0, 0.55f));
                Ui.Frame(Ui.R(240, top + 5, 154, 42), Ui.Gold, 1.2f);
                Ui.Label(Ui.R(246, top + 7, 112, 14), "CARTE DU TOUR", 8.5f, Ui.Muted, TextAnchor.MiddleRight);
                Ui.Label(Ui.R(246, top + 20, 112, 24), RankInfo.Plural(k).ToUpperInvariant(), 17, Ui.Gold, TextAnchor.MiddleRight, true);
                Ui.Card(Ui.R(364, top + 9, 24, 34), k);
            }

            DrawPlayerLabels(v);

            // Message d'état.
            var status = Status(v, out var highlight);
            if (status.Length > 0)
            {
                var r = Ui.R(20, top + 60, Ui.W - 40, 34);
                Ui.Fill(r, highlight ? Ui.Gold : new Color(0, 0, 0, 0.6f));
                Ui.Label(Ui.R(28, r.y, r.width - 16, r.height), status, 13, highlight ? Ui.Ink : Ui.Cream, TextAnchor.MiddleCenter, highlight, true);
            }

            if (v.Reveal != null && v.Phase == Phase.Reveal) DrawReveal(v);
            if (v.Phase == Phase.Roulette) DrawRoulette(v);
            DrawBottom(v);

            // Flash rouge du coup de feu.
            if (Time.time < flashUntil && Time.time > flashUntil - 0.6f)
                Ui.FillScreen(new Color(Ui.Blood.r, Ui.Blood.g, Ui.Blood.b, (flashUntil - Time.time) / 0.6f * 0.55f));
            if (table.ScreenFlash > 0.01f) Ui.FillScreen(new Color(1, 1, 1, table.ScreenFlash));

            if (v.Phase == Phase.GameOver) DrawGameOver(v);
        }

        void DrawPlayerLabels(GameView v)
        {
            foreach (var p in v.Players)
            {
                if (p.Id == v.MyId || !table.LabelPoint(p.Id, out var sp)) continue;
                var c = Ui.FromScreen(sp);
                var current = v.Phase == Phase.Playing && v.CurrentId == p.Id;
                var r = Ui.R(Mathf.Clamp(c.x - 48, 2, Ui.W - 98), Mathf.Max(Ui.SafeTop + 98, c.y - 44), 96, 44);
                Ui.Fill(r, current ? new Color(0.24f, 0.16f, 0.04f, 0.9f) : new Color(0.04f, 0.03f, 0.02f, 0.72f));
                Ui.Frame(r, current ? Ui.Gold : new Color(1, 1, 1, 0.25f), current ? 2 : 1);
                Ui.Label(Ui.R(r.x + 3, r.y + 2, r.width - 6, 17), p.Name, 12.5f, !p.Alive ? Ui.Muted : current ? Ui.Gold : Ui.Cream,
                    TextAnchor.MiddleCenter, true);
                if (!p.Alive)
                {
                    Ui.Label(Ui.R(r.x, r.y + 20, r.width, 18), "Éliminé", 11.5f, Ui.Blood, TextAnchor.MiddleCenter, true);
                    continue;
                }
                var info = !p.Connected ? "déconnecté" : p.CardCount + (p.CardCount > 1 ? " cartes" : " carte");
                Ui.Label(Ui.R(r.x, r.y + 18, r.width, 14), info, 10, !p.Connected ? Ui.Blood : Ui.Muted);
                Ui.ShotDots(r.x + (r.width - 6 * 9 + 3) / 2, r.y + 34, p.Shots, 6);
            }
        }

        static string Status(GameView v, out bool highlight)
        {
            highlight = false;
            if (v.Phase == Phase.Reveal && v.Reveal != null)
                return v.NameOf(v.Reveal.AccuserId) + " accuse " + v.NameOf(v.Reveal.AccusedId) + " !";
            if (v.Phase != Phase.Playing || !v.TableRank.HasValue) return "";
            var word = v.LastPlayCount + " " + (v.LastPlayCount > 1 ? RankInfo.Plural(v.TableRank.Value) : RankInfo.Label(v.TableRank.Value));
            if (v.IsMyTurn)
            {
                highlight = true;
                if (v.MustCall) return "Tu es le dernier avec des cartes : accuse !";
                return v.LastPlayBy == null ? "À toi de commencer : choisis tes cartes" : v.NameOf(v.LastPlayBy) + " a posé " + word + "… à toi !";
            }
            if (v.LastPlayBy == null) return "Au tour de " + v.NameOf(v.CurrentId);
            return (v.LastPlayBy == v.MyId ? "Tu as posé " : v.NameOf(v.LastPlayBy) + " a posé ") + word + " · au tour de " + v.NameOf(v.CurrentId);
        }

        void DrawReveal(GameView v)
        {
            var r = v.Reveal;
            var elapsed = Time.time - revealAt;
            var n = r.Cards.Count;
            const float w = 62, h = 90, gap = 10;
            var x0 = (Ui.W - (n * w + (n - 1) * gap)) / 2;
            var y = Ui.H * 0.34f;
            for (var i = 0; i < n; i++)
            {
                var shown = elapsed > 0.35f + 0.3f * i;
                Ui.Card(Ui.R(x0 + i * (w + gap), y, w, h), shown ? r.Cards[i].Rank : (Rank?)null, false,
                    shown && v.TableRank.HasValue && r.Cards[i].Matches(v.TableRank.Value));
            }
            if (elapsed < 0.6f + 0.3f * n) return;
            var c = r.WasLie ? Ui.Blood : Ui.Truth;
            var box = Ui.R(80, y + h + 16, 240, 50);
            Ui.Fill(box, new Color(0, 0, 0, 0.7f));
            Ui.Frame(box, c, 3);
            Ui.Label(box, r.WasLie ? "MENSONGE !" : "VÉRITÉ !", 28, c, TextAnchor.MiddleCenter, true);
            Ui.Shadowed(Ui.R(0, box.yMax + 8, Ui.W, 22), v.NameOf(r.LoserId) + " va tirer…", 14, Ui.Cream, TextAnchor.MiddleCenter, false);
        }

        void DrawRoulette(GameView v)
        {
            var shooter = v.Player(v.ShooterId);
            var mine = v.ShooterId == v.MyId;
            var died = v.ShotDied;
            var resultShown = died.HasValue && Time.time - shotAt > 0.9f;
            var shots = (shooter != null ? shooter.Shots : 0) - (died.HasValue && !resultShown ? 1 : 0);
            if (shots < 0) shots = 0;
            string title, subtitle;
            var color = Ui.Cream;
            if (!resultShown)
            {
                title = mine ? "À toi de tirer…" : (shooter != null ? shooter.Name : "?") + " prend le revolver…";
                var risk = Mathf.RoundToInt((float)v.Settings.Risk(shots) * 100);
                subtitle = v.Settings.Respin
                    ? "Barillet re-tourné — " + risk + " % de risque"
                    : "Chambre " + (shots + 1) + " sur " + Rules.Chambers + " — " + risk + " % de risque";
            }
            else if (died.Value)
            {
                title = "PAN !";
                color = Ui.Blood;
                subtitle = mine ? "Tu es éliminé." : (shooter != null ? shooter.Name : "?") + " est éliminé.";
            }
            else
            {
                title = "clic…";
                color = Ui.Truth;
                subtitle = mine ? "Tu survis… pour l'instant." : (shooter != null ? shooter.Name : "?") + " survit.";
            }
            var y = Ui.SafeTop + 60;
            Ui.Fill(Ui.R(20, y, Ui.W - 40, 74), new Color(0, 0, 0, 0.62f));
            Ui.Label(Ui.R(24, y + 4, Ui.W - 48, 42), title, title == "PAN !" ? 38 : 21, color, TextAnchor.MiddleCenter, true);
            Ui.Label(Ui.R(24, y + 46, Ui.W - 48, 22), subtitle, 13, Ui.Cream);
            if (!v.IMustShoot) return;
            var by = Ui.H - Ui.SafeBottom - 196;
            if (Ui.Button(Ui.R(20, by, Ui.W - 40, 62), "APPUYER SUR LA DÉTENTE", Ui.Blood, Color.white, 18, !shootPressed))
            {
                shootPressed = true;
                session.Shoot();
            }
        }

        void DrawBottom(GameView v)
        {
            var me = v.Me;
            var alive = me != null && me.Alive;
            var bottom = Ui.H - Ui.SafeBottom;
            var n = v.Hand.Count;
            var cw = n > 0 ? Mathf.Min(64f, (Ui.W - 24 - 6 * (n - 1)) / n) : 64f;
            var ch = cw * 1.45f;
            var panelH = 24 + (alive ? ch + 26 + 60 : 40);
            Ui.Fill(Ui.R(-400, bottom - panelH, 1200, panelH + Ui.SafeBottom + 4), new Color(0, 0, 0, 0.55f));
            var y = bottom - panelH + 6;
            Ui.Label(Ui.R(12, y, 90, 16), me == null ? "Spectateur" : alive ? "Ton barillet" : "Éliminé", 11.5f, alive ? Ui.Muted : Ui.Blood,
                TextAnchor.MiddleLeft);
            if (me != null) Ui.ShotDots(92, y + 4, me.Shots, 8);
            if (alive)
                Ui.Label(Ui.R(Ui.W - 150, y, 138, 16), "Risque : " + Mathf.RoundToInt((float)v.RiskFor(me) * 100) + " %", 11.5f, Ui.Muted,
                    TextAnchor.MiddleRight);
            if (!alive)
            {
                Ui.Label(Ui.R(0, y + 18, Ui.W, 22), "Tu regardes les autres s'entretuer.", 12.5f, Ui.Muted);
                return;
            }

            // Ma main.
            var hy = y + 36;
            if (n == 0)
            {
                Ui.Label(Ui.R(0, hy, Ui.W, ch), "Plus de cartes : tu attends la fin de la manche.", 12.5f, Ui.Muted);
            }
            else
            {
                var x0 = (Ui.W - (n * cw + (n - 1) * 6)) / 2;
                for (var i = 0; i < n; i++)
                {
                    var card = v.Hand[i];
                    var sel = selected.Contains(card.Id);
                    var r = Ui.R(x0 + i * (cw + 6), hy - (sel ? 14 : 0), cw, ch);
                    Ui.Card(r, card.Rank, sel);
                    if (v.CanPlay && v.Phase == Phase.Playing && GUI.Button(Ui.Px(r), GUIContent.none, GUIStyle.none))
                    {
                        if (sel) selected.Remove(card.Id);
                        else if (selected.Count < Rules.MaxCardsPerPlay) selected.Add(card.Id);
                    }
                }
            }
            if (v.Phase != Phase.Playing) return;
            var by = bottom - 56;
            var word = v.TableRank.HasValue
                ? (selected.Count > 1 ? RankInfo.Plural(v.TableRank.Value) : RankInfo.Label(v.TableRank.Value)).ToUpperInvariant()
                : "";
            var label = selected.Count == 0 ? (v.CanPlay ? "TOUCHE TES CARTES" : "POSER") : "POSER " + selected.Count + " " + word;
            if (Ui.Primary(Ui.R(12, by, 184, 48), label, v.CanPlay && selected.Count > 0))
            {
                session.Play(selected.ToList());
                selected.Clear();
            }
            if (Ui.Button(Ui.R(204, by, 184, 48), "MENTEUR !", Ui.Blood, Color.white, 16, v.CanCall)) session.CallLiar();
        }

        void DrawGameOver(GameView v)
        {
            Ui.FillScreen(new Color(0, 0, 0, 0.72f));
            var won = v.WinnerId == v.MyId;
            var y = Ui.H / 2 - 130;
            Ui.Shadowed(Ui.R(0, y, Ui.W, 52), won ? "VICTOIRE !" : v.NameOf(v.WinnerId) + " gagne", won ? 40 : 30, Ui.Gold);
            Ui.Label(Ui.R(0, y + 56, Ui.W, 24), won ? "Dernier survivant de la table." : "Le barillet a parlé.", 15, Ui.Muted);
            var host = session as HostSession;
            if (host != null)
            {
                if (Ui.Primary(Ui.R(60, y + 110, 280, 52), "NOUVELLE PARTIE")) host.BackToLobby();
            }
            else
            {
                Ui.Label(Ui.R(20, y + 110, Ui.W - 40, 52), "En attente de l'hôte pour une nouvelle partie…", 13.5f, Ui.Muted,
                    TextAnchor.MiddleCenter, false, true);
            }
            if (Ui.Outline(Ui.R(60, y + 174, 280, 46), "Quitter la table")) Leave();
        }
    }
}
