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
            if (FindFirstObjectByType<App>() != null) return;
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
        readonly Texture[] portraits = new Texture[Rules.SkinCount];

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
            pseudo = PlayerPrefs.GetString("pseudo", "");
            Sfx.Init(gameObject);
            for (var i = 0; i < portraits.Length; i++) portraits[i] = Resources.Load<Texture2D>("Portraits/" + (i % 5));
            table = new GameObject("Table").AddComponent<TableScene>();
            table.transform.SetParent(transform, false);
            // Matériau par défaut du projet (Standard ou Lit en URP), pour les silhouettes de secours.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var defaultMaterial = new Material(probe.GetComponent<Renderer>().sharedMaterial);
            Destroy(probe);
            StartCoroutine(PortraitStudio.Build(portraits, defaultMaterial));
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
            // Retour Android / Échap (via IMGUI : marche avec l'ancien et le nouveau système d'entrée).
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (screen == Screen_.Table) confirmLeave = !confirmLeave;
                else if (screen != Screen_.Home)
                {
                    StopDiscovery();
                    screen = Screen_.Home;
                }
                e.Use();
            }
            Ui.Begin(column: !(screen == Screen_.Table && view != null && view.Phase != Phase.Lobby));
            switch (screen)
            {
                case Screen_.Home: DrawHome(); break;
                case Screen_.Rules: DrawRules(); break;
                case Screen_.Join: DrawJoin(); break;
                case Screen_.Table: DrawTable(); break;
            }
            if (toast != null && Time.time < toastUntil)
            {
                var y = Ui.H - Ui.SafeBottom - 170;
                var tw = Mathf.Min(360, Ui.W - 40);
                Ui.Panel(Ui.R((Ui.W - tw) / 2, y, tw, 38), new Color(0.12f, 0.08f, 0.05f, 0.96f), 19, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.6f));
                Ui.Label(Ui.R((Ui.W - tw) / 2 + 10, y, tw - 20, 38), toast, 13, Ui.Cream, TextAnchor.MiddleCenter, false, true);
            }
        }

        static readonly Color Veil = new Color(0.03f, 0.08f, 0.06f, 0.88f);

        void Header(string title, System.Action back)
        {
            var y = Ui.SafeTop + 8;
            if (Ui.Button(Ui.R(8, y, 44, 40), "<", new Color(0, 0, 0, 0.35f), Ui.Cream, 22)) back();
            Ui.Title(Ui.R(60, y, Ui.W - 120, 40), title, 22, Ui.Gold);
        }

        void DrawHome()
        {
            Ui.FillScreen(Veil);
            var y = Ui.SafeTop + Mathf.Max(20, (Ui.H - 640) * 0.35f + 24);
            Ui.Card(Ui.R(118, y + 8, 56, 81), Rank.King);
            Ui.Card(Ui.R(222, y + 8, 56, 81), Rank.Queen);
            Ui.Card(Ui.R(170, y, 56, 81), Rank.Joker);
            y += 100;
            Ui.Title(Ui.R(0, y, Ui.W, 56), "MENTEUR !", 46, Ui.Gold);
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
            new[] { "Crédits", "Personnages et décor 3D : packs KayKit Adventurers, Skeletons et Dungeon Remastered par Kay Lousberg (kaylousberg.com), licence CC0. Police Rye par Sorkin Type (licence SIL OFL). Sons synthétisés pour le jeu." },
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
                Ui.Title(Ui.R(24, y, Ui.W - 48, 26), r[0], 17, Ui.Gold, TextAnchor.MiddleLeft);
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
                Ui.Round(Ui.R(24, y, Ui.W - 48, 64), new Color(0, 0, 0, 0.3f), 12);
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
            var x0 = (Ui.W - 320) / 2;
            Ui.Panel(Ui.R(x0, y, 320, 160), Ui.PanelColor, 16, Ui.Gold);
            Ui.Title(Ui.R(x0 + 10, y + 14, 300, 30), "Quitter la table ?", 19, Ui.Cream);
            Ui.Label(Ui.R(x0 + 16, y + 46, 288, 44),
                session.IsHost ? "La partie sera fermée pour tout le monde." : "Un bot jouera à ta place.", 13, Ui.Muted,
                TextAnchor.MiddleCenter, false, true);
            if (Ui.Outline(Ui.R(x0 + 16, y + 100, 138, 44), "Rester")) confirmLeave = false;
            if (Ui.Button(Ui.R(x0 + 166, y + 100, 138, 44), "Quitter", Ui.Blood, Color.white)) Leave();
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
            var contentH = (host != null && host.Online ? 92 : 30) + 218 + 190 + 34 + v.Players.Count * 46 + 16;
            Ui.BeginScroll(area, ref scroll, contentH);
            var y = top + 4;

            if (host != null && host.Online)
            {
                if (Time.time > ipsAt)
                {
                    ipsAt = Time.time + 3;
                    ips = Protocol.LocalAddresses().Select(a => a.ToString()).ToList();
                }
                Ui.Panel(Ui.R(20, y, Ui.W - 40, 82), new Color(0.06f, 0.04f, 0.03f, 0.9f), 12, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.55f));
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

            // Personnage (9 au choix, sur deux rangées)
            Ui.Title(Ui.R(20, y, 300, 24), "Ton personnage", 15, Ui.Gold, TextAnchor.MiddleLeft);
            y += 28;
            var mySkin = v.Me != null ? v.Me.Skin : 0;
            for (var i = 0; i < Rules.SkinCount; i++)
            {
                var row = i / 5;
                var col = i % 5;
                var rx = 20 + col * 73 + (row == 1 ? 36 : 0);
                var r = Ui.R(rx, y + row * 92, 62, 62);
                Ui.Round(Ui.R(r.x - 3, r.y - 3, r.width + 6, r.height + 6), i == mySkin ? Ui.Gold : new Color(1, 1, 1, 0.12f), 34);
                Ui.Round(r, new Color(0.23f, 0.16f, 0.12f), 31);
                if (portraits[i] != null) GUI.DrawTexture(Ui.Px(Ui.R(r.x + 3, r.y + 3, r.width - 6, r.height - 6)), portraits[i], ScaleMode.ScaleAndCrop);
                Ui.Label(Ui.R(r.x - 8, r.y + 64, 78, 18), Rules.SkinNames[i], 10, i == mySkin ? Ui.Gold : Ui.Muted,
                    TextAnchor.MiddleCenter, i == mySkin);
                if (GUI.Button(Ui.Px(r), GUIContent.none, GUIStyle.none)) session.ChooseSkin(i);
            }
            y += 190;

            // Règles de la partie
            Ui.Title(Ui.R(20, y, 340, 24), host != null ? "Règles de la partie" : "Règles (choisies par l'hôte)", 15, Ui.Gold,
                TextAnchor.MiddleLeft);
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
            Ui.Title(Ui.R(20, y, 300, 24), "Joueurs (" + v.Players.Count + "/" + Rules.MaxPlayers + ")", 15, Ui.Gold,
                TextAnchor.MiddleLeft);
            y += 30;
            foreach (var p in v.Players.ToList())
            {
                Ui.Round(Ui.R(20, y, Ui.W - 40, 40), new Color(0, 0, 0, 0.32f), 10);
                var pr = Ui.R(24, y + 3, 34, 34);
                Ui.Round(pr, new Color(0.23f, 0.16f, 0.12f), 17);
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
                    Ui.Round(r, on ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.85f) : new Color(0, 0, 0, 0.25f), 10);
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
            var W = Ui.W;

            // Bandeau du haut.
            Ui.Round(Ui.R(8, top + 6, 52, 40), new Color(0, 0, 0, 0.55f), 12);
            if (Ui.Button(Ui.R(8, top + 6, 52, 40), "X", new Color(0, 0, 0, 0.01f), Ui.Cream, 16)) confirmLeave = true;
            Ui.Round(Ui.R(66, top + 6, 108, 40), new Color(0, 0, 0, 0.55f), 12);
            Ui.Title(Ui.R(66, top + 6, 108, 40), "Manche " + v.Round, 15, Ui.Cream);
            if (Ui.Button(Ui.R(180, top + 6, 58, 40), Sfx.On ? "Son" : "Muet", new Color(0, 0, 0, 0.55f), Sfx.On ? Ui.Cream : Ui.Muted, 12))
                Sfx.On = !Sfx.On;
            var secs = v.SecondsLeft;
            if (secs.HasValue)
            {
                Ui.Round(Ui.R(244, top + 6, 56, 40), secs.Value <= 5 ? Ui.Blood : new Color(0, 0, 0, 0.55f), 12);
                Ui.Label(Ui.R(244, top + 6, 56, 40), secs.Value + " s", 15, Color.white, TextAnchor.MiddleCenter, true);
            }
            if (v.TableRank.HasValue)
            {
                var k = v.TableRank.Value;
                var bx = W - 186;
                Ui.Panel(Ui.R(bx, top + 4, 178, 50), new Color(0.06f, 0.04f, 0.03f, 0.88f), 14, Ui.Gold);
                Ui.Label(Ui.R(bx + 8, top + 8, 120, 14), "CARTE DU TOUR", 9, Ui.Muted, TextAnchor.MiddleRight);
                Ui.Title(Ui.R(bx + 8, top + 21, 120, 28), RankInfo.Plural(k).ToUpperInvariant(), 19, Ui.Gold, TextAnchor.MiddleRight);
                Ui.Card(Ui.R(bx + 138, top + 8, 29, 42), k);
            }

            DrawPlayerLabels(v);

            // Message d'état.
            var status = Status(v, out var highlight);
            if (status.Length > 0)
            {
                var sw = Mathf.Min(520, W - 40);
                var r = Ui.R((W - sw) / 2, top + 60, sw, 36);
                Ui.Panel(r, highlight ? Ui.Gold : new Color(0.06f, 0.04f, 0.03f, 0.82f), 18);
                Ui.Label(Ui.R(r.x + 12, r.y, r.width - 24, r.height), status, 14, highlight ? Ui.Ink : Ui.Cream, TextAnchor.MiddleCenter, highlight, true);
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
            // Positions à l'écran, puis on écarte les étiquettes qui se chevauchent.
            var boxes = new List<KeyValuePair<PlayerView, Rect>>();
            foreach (var p in v.Players)
            {
                if (p.Id == v.MyId || !table.LabelPoint(p.Id, out var sp)) continue;
                var c = Ui.FromScreen(sp);
                var r = Ui.R(Mathf.Clamp(c.x - 56, 4, Ui.W - 116), Mathf.Clamp(c.y - 50, Ui.SafeTop + 102, Ui.H - 260), 112, 50);
                boxes.Add(new KeyValuePair<PlayerView, Rect>(p, r));
            }
            for (var pass = 0; pass < 4; pass++)
            for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i].Value;
                var b = boxes[j].Value;
                if (!a.Overlaps(b)) continue;
                var push = (Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y)) / 2 + 2;
                if (a.y <= b.y) { a.y -= push; b.y += push; }
                else { a.y += push; b.y -= push; }
                boxes[i] = new KeyValuePair<PlayerView, Rect>(boxes[i].Key, a);
                boxes[j] = new KeyValuePair<PlayerView, Rect>(boxes[j].Key, b);
            }
            foreach (var e in boxes)
            {
                var p = e.Key;
                var r = e.Value;
                var current = v.Phase == Phase.Playing && v.CurrentId == p.Id;
                Ui.Panel(r, current ? new Color(0.26f, 0.17f, 0.05f, 0.93f) : new Color(0.05f, 0.035f, 0.025f, 0.82f), 12,
                    current ? Ui.Gold : new Color(1, 1, 1, 0.18f));
                var pr = Ui.R(r.x + 5, r.y + 5, 40, 40);
                Ui.Round(pr, new Color(0.23f, 0.16f, 0.12f), 20);
                var portrait = portraits[p.Skin % portraits.Length];
                if (portrait != null)
                {
                    var old = GUI.color;
                    if (!p.Alive) GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                    GUI.DrawTexture(Ui.Px(Ui.R(pr.x + 2, pr.y + 2, 36, 36)), portrait, ScaleMode.ScaleAndCrop);
                    GUI.color = old;
                }
                Ui.Label(Ui.R(r.x + 50, r.y + 4, r.width - 54, 18), p.Name, 13, !p.Alive ? Ui.Muted : current ? Ui.Gold : Ui.Cream,
                    TextAnchor.MiddleLeft, true);
                if (!p.Alive)
                {
                    Ui.Label(Ui.R(r.x + 50, r.y + 24, r.width - 54, 18), "Éliminé", 12, Ui.Blood, TextAnchor.MiddleLeft, true);
                    continue;
                }
                var info = !p.Connected ? "déconnecté" : p.CardCount + (p.CardCount > 1 ? " cartes" : " carte");
                Ui.Label(Ui.R(r.x + 50, r.y + 21, r.width - 54, 14), info, 10.5f, !p.Connected ? Ui.Blood : Ui.Muted, TextAnchor.MiddleLeft);
                Ui.ShotDots(r.x + 50, r.y + 37, p.Shots, 6);
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
            const float w = 70, h = 102, gap = 12;
            var x0 = (Ui.W - (n * w + (n - 1) * gap)) / 2;
            var y = Ui.H * 0.3f;
            for (var i = 0; i < n; i++)
            {
                var shown = elapsed > 0.35f + 0.3f * i;
                Ui.Card(Ui.R(x0 + i * (w + gap), y, w, h), shown ? r.Cards[i].Rank : (Rank?)null, false,
                    shown && v.TableRank.HasValue && r.Cards[i].Matches(v.TableRank.Value));
            }
            if (elapsed < 0.6f + 0.3f * n) return;
            var c = r.WasLie ? Ui.Blood : Ui.Truth;
            var box = Ui.R((Ui.W - 260) / 2, y + h + 18, 260, 56);
            Ui.Panel(box, new Color(0.05f, 0.03f, 0.02f, 0.9f), 14, c);
            Ui.Title(box, r.WasLie ? "MENSONGE !" : "VÉRITÉ !", 30, c);
            Ui.Shadowed(Ui.R(0, box.yMax + 10, Ui.W, 22), v.NameOf(r.LoserId) + " va tirer…", 15, Ui.Cream, TextAnchor.MiddleCenter, false);
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
            var name = shooter != null ? shooter.Name : "?";
            if (!resultShown)
            {
                title = mine ? "À toi de tirer…" : name + " prend le revolver…";
                var risk = Mathf.RoundToInt((float)v.Settings.Risk(shots) * 100);
                subtitle = v.Settings.Respin
                    ? "Barillet re-tourné — " + risk + " % de risque"
                    : "Chambre " + (shots + 1) + " sur " + Rules.Chambers + " — " + risk + " % de risque";
            }
            else if (died.Value)
            {
                title = "PAN !";
                color = Ui.Blood;
                subtitle = mine ? "Tu es éliminé." : name + " est éliminé.";
            }
            else
            {
                title = "clic…";
                color = Ui.Truth;
                subtitle = mine ? "Tu survis… pour l'instant." : name + " survit.";
            }
            var bw = Mathf.Min(460, Ui.W - 40);
            var box = Ui.R((Ui.W - bw) / 2, Ui.SafeTop + 60, bw, 80);
            Ui.Panel(box, new Color(0.05f, 0.03f, 0.02f, 0.86f), 16, new Color(color.r, color.g, color.b, 0.7f));
            Ui.Title(Ui.R(box.x + 8, box.y + 4, box.width - 16, 46), title, title == "PAN !" ? 40 : 22, color);
            Ui.Label(Ui.R(box.x + 8, box.y + 50, box.width - 16, 22), subtitle, 13.5f, Ui.Cream);
            if (!v.IMustShoot) return;
            var w = Mathf.Min(420, Ui.W - 40);
            if (Ui.Button(Ui.R((Ui.W - w) / 2, Ui.H - Ui.SafeBottom - 210, w, 64), "APPUYER SUR LA DÉTENTE", Ui.Blood, Color.white, 19, !shootPressed))
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
            var W = Ui.W;

            // Barillet et risque, en bas à gauche.
            Ui.Round(Ui.R(10, bottom - 112, 150, 52), new Color(0.05f, 0.035f, 0.025f, 0.8f), 12);
            Ui.Label(Ui.R(20, bottom - 108, 130, 18), me == null ? "Spectateur" : alive ? "Ton barillet" : "Éliminé", 11.5f,
                alive ? Ui.Muted : Ui.Blood, TextAnchor.MiddleLeft, true);
            if (me != null) Ui.ShotDots(20, bottom - 88, me.Shots, 9);
            if (alive)
                Ui.Label(Ui.R(20, bottom - 76, 130, 14), "Risque : " + Mathf.RoundToInt((float)v.RiskFor(me) * 100) + " %", 10.5f, Ui.Muted,
                    TextAnchor.MiddleLeft);
            if (!alive)
            {
                Ui.Shadowed(Ui.R(0, bottom - 50, W, 30), "Tu regardes les autres s'entretuer.", 14, Ui.Muted, TextAnchor.MiddleCenter, false);
                return;
            }

            // Ma main, en éventail.
            var n = v.Hand.Count;
            var cw = Ui.Landscape ? 78f : Mathf.Min(70f, (W - 40) / Mathf.Max(1, n));
            var ch = cw * 1.45f;
            var handBottom = bottom - (v.Phase == Phase.Playing ? 66 : 14);
            if (n == 0)
            {
                Ui.Shadowed(Ui.R(0, handBottom - 40, W, 30), "Plus de cartes : tu attends la fin de la manche.", 13, Ui.Muted, TextAnchor.MiddleCenter, false);
            }
            else
            {
                var spacing = cw * 0.78f;
                var x0 = W / 2 - (spacing * (n - 1) + cw) / 2;
                var mouse = Event.current.mousePosition;
                for (var i = 0; i < n; i++)
                {
                    var card = v.Hand[i];
                    var sel = selected.Contains(card.Id);
                    var t = n == 1 ? 0 : i / (float)(n - 1) - 0.5f;
                    var angle = t * 16f;
                    var lift = Mathf.Abs(t) * 10f;
                    var r = Ui.R(x0 + i * spacing, handBottom - ch + lift - (sel ? 22 : 0), cw, ch);
                    Ui.Card(r, card.Rank, sel, false, angle);
                    if (v.CanPlay && v.Phase == Phase.Playing && GUI.Button(Ui.Px(Ui.R(r.x, r.y, i == n - 1 ? cw : spacing, ch)), GUIContent.none, GUIStyle.none))
                    {
                        if (sel) selected.Remove(card.Id);
                        else if (selected.Count < Rules.MaxCardsPerPlay) selected.Add(card.Id);
                    }
                }
            }
            if (v.Phase != Phase.Playing) return;
            var bw = Mathf.Min(210f, (W - 36) / 2);
            var by = bottom - 58;
            var word = v.TableRank.HasValue
                ? (selected.Count > 1 ? RankInfo.Plural(v.TableRank.Value) : RankInfo.Label(v.TableRank.Value)).ToUpperInvariant()
                : "";
            var label = selected.Count == 0 ? (v.CanPlay ? "CHOISIS TES CARTES" : "POSER") : "POSER " + selected.Count + " " + word;
            if (Ui.Primary(Ui.R(W / 2 - bw - 6, by, bw, 50), label, v.CanPlay && selected.Count > 0))
            {
                session.Play(selected.ToList());
                selected.Clear();
            }
            if (Ui.Button(Ui.R(W / 2 + 6, by, bw, 50), "MENTEUR !", Ui.Blood, Color.white, 17, v.CanCall)) session.CallLiar();
        }

        void DrawGameOver(GameView v)
        {
            Ui.FillScreen(new Color(0, 0, 0, 0.7f));
            var won = v.WinnerId == v.MyId;
            var y = Ui.H / 2 - 140;
            var x0 = (Ui.W - 320) / 2;
            Ui.Panel(Ui.R(x0 - 20, y - 20, 360, 280), new Color(0.10f, 0.07f, 0.05f, 0.95f), 20, Ui.Gold);
            var winner = v.Player(v.WinnerId);
            if (winner != null && portraits[winner.Skin % portraits.Length] != null)
            {
                Ui.Round(Ui.R(Ui.W / 2 - 36, y - 6, 72, 72), Ui.Gold, 36);
                GUI.DrawTexture(Ui.Px(Ui.R(Ui.W / 2 - 33, y - 3, 66, 66)), portraits[winner.Skin % portraits.Length], ScaleMode.ScaleAndCrop);
            }
            Ui.Title(Ui.R(0, y + 70, Ui.W, 46), won ? "VICTOIRE !" : v.NameOf(v.WinnerId) + " gagne", won ? 36 : 28, Ui.Gold);
            Ui.Label(Ui.R(0, y + 114, Ui.W, 22), won ? "Dernier survivant de la table." : "Le barillet a parlé.", 14, Ui.Muted);
            var host = session as HostSession;
            if (host != null)
            {
                if (Ui.Primary(Ui.R(x0, y + 148, 320, 50), "NOUVELLE PARTIE")) host.BackToLobby();
            }
            else
            {
                Ui.Label(Ui.R(x0, y + 148, 320, 50), "En attente de l'hôte pour une nouvelle partie…", 13, Ui.Muted,
                    TextAnchor.MiddleCenter, false, true);
            }
            if (Ui.Outline(Ui.R(x0, y + 206, 320, 44), "Quitter la table")) Leave();
        }
    }
}
