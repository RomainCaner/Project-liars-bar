// Aides IMGUI : coordonnées « virtuelles » (largeur 400) converties en pixels,
// pour un rendu net et identique sur tous les écrans.
using System.Collections.Generic;
using UnityEngine;

namespace Menteur.Game
{
    public static class Ui
    {
        /// <summary>Largeur virtuelle courante (400 en colonne, plus en plein écran paysage).</summary>
        public static float W { get; private set; } = 400;
        public static float H { get; private set; }
        public static float Scale { get; private set; }
        public static float SafeTop { get; private set; }
        public static float SafeBottom { get; private set; }
        public static bool Landscape { get; private set; }
        static float offsetX;
        static Font titleFont;
        static bool fontLoaded;

        public static readonly Color Gold = new Color(0.85f, 0.68f, 0.32f);
        public static readonly Color Cream = new Color(0.96f, 0.92f, 0.83f);
        public static readonly Color Blood = new Color(0.75f, 0.22f, 0.17f);
        public static readonly Color Truth = new Color(0.30f, 0.69f, 0.48f);
        public static readonly Color Ink = new Color(0.14f, 0.10f, 0.08f);
        public static readonly Color Muted = new Color(0.66f, 0.60f, 0.53f);
        public static readonly Color Wood = new Color(0.10f, 0.07f, 0.04f);
        public static readonly Color PanelColor = new Color(0.17f, 0.11f, 0.07f);

        static readonly Dictionary<Color, Texture2D> textures = new Dictionary<Color, Texture2D>();
        static readonly Dictionary<long, GUIStyle> labels = new Dictionary<long, GUIStyle>();
        static GUIStyle field;
        static int fieldPx;

        /// <summary>
        /// À appeler au début de chaque OnGUI. column = true : colonne centrale de 400 de large
        /// (menus) ; false : toute la largeur de l'écran (jeu).
        /// </summary>
        public static void Begin(bool column = true)
        {
            clipping = false;
            Landscape = Screen.width > Screen.height * 0.8f;
            Scale = Landscape ? Screen.height / 640f : Mathf.Min(Screen.width / 400f, Screen.height / 640f);
            H = Screen.height / Scale;
            var full = Screen.width / Scale;
            W = column ? 400 : full;
            offsetX = column ? (Screen.width - 400 * Scale) / 2f : 0;
            var safe = Screen.safeArea;
            SafeTop = (Screen.height - safe.yMax) / Scale;
            SafeBottom = safe.yMin / Scale;
            if (!fontLoaded)
            {
                fontLoaded = true;
                titleFont = Resources.Load<Font>("Fonts/Rye");
            }
        }

        // Zone défilante en cours (les coordonnées IMGUI deviennent relatives au groupe).
        static bool clipping;
        static Vector2 clipOrigin;

        public static Rect Px(Rect r) => clipping
            ? new Rect((r.x - clipOrigin.x) * Scale, (r.y - clipOrigin.y) * Scale, r.width * Scale, r.height * Scale)
            : new Rect(offsetX + r.x * Scale, r.y * Scale, r.width * Scale, r.height * Scale);

        /// <summary>Zone défilante au doigt (ou à la molette). Le contenu se dessine en coordonnées normales.</summary>
        public static void BeginScroll(Rect area, ref float scroll, float contentHeight)
        {
            var px = Px(area);
            var e = Event.current;
            if (px.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag) scroll -= e.delta.y / Scale;
                else if (e.type == EventType.ScrollWheel) scroll += e.delta.y * 12;
            }
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, contentHeight - area.height));
            GUI.BeginGroup(px);
            clipping = true;
            clipOrigin = new Vector2(area.x, area.y + scroll);
        }

        public static void EndScroll()
        {
            clipping = false;
            GUI.EndGroup();
        }

        public static float TextHeight(string text, float size, float width, bool bold = false) =>
            LabelStyle(size, bold, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(text), width * Scale) / Scale;

        public static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);

        /// <summary>Convertit un point écran (origine en bas à gauche) en coordonnées virtuelles.</summary>
        public static Vector2 FromScreen(Vector3 p) => new Vector2((p.x - offsetX) / Scale, (Screen.height - p.y) / Scale);

        public static Texture2D Tex(Color c)
        {
            if (textures.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            textures[c] = t;
            return t;
        }

        static GUIStyle LabelStyle(float size, bool bold, TextAnchor align, bool wrap, bool title = false)
        {
            var px = Mathf.Max(1, Mathf.RoundToInt(size * Scale));
            var key = px * 1000L + (int)align * 4 + (bold ? 2 : 0) + (wrap ? 1 : 0) + (title ? 1L << 40 : 0);
            if (labels.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                font = title && titleFont != null ? titleFont : null,
                fontSize = px, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = align, wordWrap = wrap, richText = true,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            s.normal.textColor = Color.white;
            labels[key] = s;
            return s;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(Px(r), Tex(c));
            GUI.color = old;
        }

        /// <summary>Remplit tout l'écran (y compris hors de la colonne centrale).</summary>
        public static void FillScreen(Color c) => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Tex(c));

        public static void Frame(Rect r, Color c, float t = 1.5f)
        {
            Fill(R(r.x, r.y, r.width, t), c);
            Fill(R(r.x, r.yMax - t, r.width, t), c);
            Fill(R(r.x, r.y, t, r.height), c);
            Fill(R(r.xMax - t, r.y, t, r.height), c);
        }

        public static void Label(Rect r, string text, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            bool bold = false, bool wrap = false, bool title = false)
        {
            var old = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(Px(r), text, LabelStyle(size, bold && !title, align, wrap, title));
            GUI.contentColor = old;
        }

        /// <summary>Titre en police western (Rye), avec ombre portée.</summary>
        public static void Title(Rect r, string text, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Label(R(r.x + 1.5f, r.y + 2, r.width, r.height), text, size, new Color(0, 0, 0, 0.8f), align, false, false, true);
            Label(r, text, size, color, align, false, false, true);
        }

        // ------------------------------------------------------------ arrondis

        static readonly Dictionary<int, GUIStyle> rounds = new Dictionary<int, GUIStyle>();

        /// <summary>Style 9-tranches blanc aux coins arrondis (teinté via GUI.color).</summary>
        static GUIStyle RoundStyle(float radius)
        {
            var px = Mathf.Clamp(Mathf.RoundToInt(radius * Scale), 1, 64);
            if (rounds.TryGetValue(px, out var st) && st.normal.background != null) return st;
            var size = px * 2 + 2;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + 0.5f, px, size - px);
                var cy = Mathf.Clamp(y + 0.5f, px, size - px);
                var d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                var a = Mathf.Clamp01(px - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(pixels);
            t.Apply();
            st = new GUIStyle { border = new RectOffset(px, px, px, px) };
            st.normal.background = t;
            rounds[px] = st;
            return st;
        }

        public static void Round(Rect r, Color c, float radius = 10)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            RoundStyle(radius).Draw(Px(r), false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Panneau arrondi avec ombre portée et liseré.</summary>
        public static void Panel(Rect r, Color c, float radius = 12, Color? border = null)
        {
            Round(R(r.x + 2, r.y + 4, r.width, r.height), new Color(0, 0, 0, 0.35f), radius);
            if (border.HasValue)
            {
                Round(r, border.Value, radius);
                Round(R(r.x + 1.5f, r.y + 1.5f, r.width - 3, r.height - 3), c, radius - 1.5f);
            }
            else Round(r, c, radius);
        }

        /// <summary>Texte avec une ombre, lisible par-dessus la scène 3D.</summary>
        public static void Shadowed(Rect r, string text, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            bool bold = true)
        {
            Label(R(r.x + 1, r.y + 1, r.width, r.height), text, size, new Color(0, 0, 0, 0.85f), align, bold);
            Label(r, text, size, color, align, bold);
        }

        public static bool Button(Rect r, string text, Color bg, Color fg, float size = 15, bool enabled = true)
        {
            var px = Px(r);
            var radius = Mathf.Min(12, r.height / 2);
            if (!enabled)
            {
                Round(r, new Color(bg.r * 0.3f + 0.07f, bg.g * 0.3f + 0.06f, bg.b * 0.3f + 0.05f, 0.85f), radius);
                Label(r, text, size, new Color(fg.r, fg.g, fg.b, 0.4f), TextAnchor.MiddleCenter, true);
                return false;
            }
            var pressed = GUIUtility.hotControl != 0 && px.Contains(Event.current.mousePosition);
            var dy = pressed ? 2 : 0;
            Round(R(r.x, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.45f), radius);
            Round(R(r.x, r.y + dy, r.width, r.height), Color.Lerp(bg, Color.black, 0.25f), radius);
            Round(R(r.x, r.y + dy, r.width, r.height - 3), pressed ? Color.Lerp(bg, Color.white, 0.12f) : bg, radius);
            Round(R(r.x + 3, r.y + dy + 2, r.width - 6, r.height * 0.42f), new Color(1, 1, 1, bg.a > 0.6f ? 0.13f : 0.05f), radius - 2);
            Label(R(r.x, r.y + dy - 1, r.width, r.height), text, size, fg, TextAnchor.MiddleCenter, true);
            return GUI.Button(px, GUIContent.none, GUIStyle.none);
        }

        public static bool Primary(Rect r, string text, bool enabled = true) => Button(r, text, Gold, Ink, 15, enabled);

        public static bool Outline(Rect r, string text, bool enabled = true)
        {
            var radius = Mathf.Min(12, r.height / 2);
            Round(r, enabled ? new Color(Gold.r, Gold.g, Gold.b, 0.85f) : Muted, radius);
            return Button(R(r.x + 1.5f, r.y + 1.5f, r.width - 3, r.height - 3), text, new Color(0.10f, 0.07f, 0.05f, 0.92f), Cream, 14, enabled);
        }

        public static string TextField(Rect r, string value, int maxLength, float size = 17)
        {
            var px = Mathf.RoundToInt(size * Scale);
            if (field == null || fieldPx != px)
            {
                fieldPx = px;
                field = new GUIStyle(GUI.skin.textField) { fontSize = px, alignment = TextAnchor.MiddleLeft };
                field.padding = new RectOffset(px / 2, px / 2, 0, 0);
                field.normal.background = field.focused.background = field.hover.background = Tex(PanelColor);
                field.normal.textColor = field.focused.textColor = field.hover.textColor = Cream;
            }
            Round(R(r.x - 1.5f, r.y - 1.5f, r.width + 3, r.height + 3), new Color(Gold.r, Gold.g, Gold.b, 0.5f), 10);
            Round(r, PanelColor, 9);
            field.normal.background = field.focused.background = field.hover.background = null;
            return GUI.TextField(Px(r), value ?? "", maxLength, field);
        }

        static readonly Texture2D[] cardTex = new Texture2D[5];
        static bool cardsLoaded;

        static Texture2D CardTexture(Menteur.Core.Rank? rank)
        {
            if (!cardsLoaded)
            {
                cardsLoaded = true;
                string[] names = { "king", "queen", "ace", "joker", "back" };
                for (var i = 0; i < 5; i++) cardTex[i] = Resources.Load<Texture2D>("Cards/" + names[i]);
            }
            return cardTex[rank.HasValue ? (int)rank.Value : 4];
        }

        /// <summary>Carte à jouer (images de Resources/Cards). rank null = dos. angle en degrés.</summary>
        public static void Card(Rect r, Menteur.Core.Rank? rank, bool selected = false, bool highlight = false, float angle = 0)
        {
            var px = Px(r);
            var pivot = new Vector2(px.center.x, px.yMax);
            var matrix = GUI.matrix;
            if (Mathf.Abs(angle) > 0.01f) GUIUtility.RotateAroundPivot(angle, pivot);
            var radius = r.width * 0.1f;
            if (selected) Round(R(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Gold, radius + 4);
            else if (highlight) Round(R(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Truth, radius + 4);
            else Round(R(r.x + 1, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.45f), radius);
            var tex = CardTexture(rank);
            if (tex != null)
            {
                GUI.DrawTexture(px, tex, ScaleMode.StretchToFill, true);
            }
            else if (rank == null)
            {
                Round(r, new Color(0.40f, 0.10f, 0.08f), radius);
            }
            else
            {
                var k = rank.Value;
                var c = RankColor(k);
                Round(r, Cream, radius);
                Label(R(r.x, r.y + r.height * 0.22f, r.width, r.height * 0.42f), Menteur.Core.RankInfo.Short(k), r.width * 0.62f, c,
                    TextAnchor.MiddleCenter, true);
                Label(R(r.x, r.y + r.height * 0.68f, r.width, r.height * 0.2f), Menteur.Core.RankInfo.Label(k).ToUpperInvariant(),
                    r.width * 0.17f, c, TextAnchor.MiddleCenter, true);
            }
            GUI.matrix = matrix;
        }

        public static Color RankColor(Menteur.Core.Rank r)
        {
            switch (r)
            {
                case Menteur.Core.Rank.King: return new Color(0.69f, 0.19f, 0.16f);
                case Menteur.Core.Rank.Queen: return new Color(0.42f, 0.24f, 0.60f);
                case Menteur.Core.Rank.Ace: return new Color(0.12f, 0.23f, 0.36f);
                default: return new Color(0.18f, 0.49f, 0.31f);
            }
        }

        /// <summary>Six points du barillet : rouges pour les coups déjà tirés.</summary>
        public static void ShotDots(float x, float y, int shots, float size = 7)
        {
            for (var i = 0; i < Menteur.Core.Rules.Chambers; i++)
                Round(R(x + i * (size + 3), y, size, size), i < shots ? Blood : new Color(1, 1, 1, 0.28f), size / 2);
        }
    }
}
