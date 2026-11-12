// Aides IMGUI : coordonnées « virtuelles » (largeur 400) converties en pixels,
// pour un rendu net et identique sur tous les écrans.
using System.Collections.Generic;
using UnityEngine;

namespace Menteur.Game
{
    public static class Ui
    {
        public const float W = 400;
        public static float H { get; private set; }
        public static float Scale { get; private set; }
        public static float SafeTop { get; private set; }
        public static float SafeBottom { get; private set; }
        static float offsetX;

        public static readonly Color Gold = new Color(0.85f, 0.68f, 0.32f);
        public static readonly Color Cream = new Color(0.96f, 0.92f, 0.83f);
        public static readonly Color Blood = new Color(0.75f, 0.22f, 0.17f);
        public static readonly Color Truth = new Color(0.30f, 0.69f, 0.48f);
        public static readonly Color Ink = new Color(0.14f, 0.10f, 0.08f);
        public static readonly Color Muted = new Color(0.66f, 0.60f, 0.53f);
        public static readonly Color Wood = new Color(0.10f, 0.07f, 0.04f);
        public static readonly Color Panel = new Color(0.17f, 0.11f, 0.07f);

        static readonly Dictionary<Color, Texture2D> textures = new Dictionary<Color, Texture2D>();
        static readonly Dictionary<long, GUIStyle> labels = new Dictionary<long, GUIStyle>();
        static GUIStyle field;
        static int fieldPx;

        /// <summary>À appeler au début de chaque OnGUI.</summary>
        public static void Begin()
        {
            clipping = false;
            Scale = Mathf.Min(Screen.width / W, Screen.height / 640f);
            H = Screen.height / Scale;
            offsetX = (Screen.width - W * Scale) / 2f;
            var safe = Screen.safeArea;
            SafeTop = (Screen.height - safe.yMax) / Scale;
            SafeBottom = safe.yMin / Scale;
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

        static GUIStyle LabelStyle(float size, bool bold, TextAnchor align, bool wrap)
        {
            var px = Mathf.Max(1, Mathf.RoundToInt(size * Scale));
            var key = px * 1000L + (int)align * 4 + (bold ? 2 : 0) + (wrap ? 1 : 0);
            if (labels.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
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
            bool bold = false, bool wrap = false)
        {
            var old = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(Px(r), text, LabelStyle(size, bold, align, wrap));
            GUI.contentColor = old;
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
            if (!enabled)
            {
                Fill(r, new Color(bg.r * 0.35f + 0.08f, bg.g * 0.35f + 0.07f, bg.b * 0.35f + 0.06f, 0.9f));
                Label(r, text, size, new Color(fg.r, fg.g, fg.b, 0.45f), TextAnchor.MiddleCenter, true);
                return false;
            }
            var hover = px.Contains(Event.current.mousePosition) && GUIUtility.hotControl != 0;
            Fill(r, hover ? Color.Lerp(bg, Color.white, 0.2f) : bg);
            Label(r, text, size, fg, TextAnchor.MiddleCenter, true);
            return GUI.Button(px, GUIContent.none, GUIStyle.none);
        }

        public static bool Primary(Rect r, string text, bool enabled = true) => Button(r, text, Gold, Ink, 15, enabled);

        public static bool Outline(Rect r, string text, bool enabled = true)
        {
            Frame(r, enabled ? Gold : Muted);
            return Button(R(r.x + 1.5f, r.y + 1.5f, r.width - 3, r.height - 3), text, new Color(0, 0, 0, 0.35f), Cream, 14, enabled);
        }

        public static string TextField(Rect r, string value, int maxLength, float size = 17)
        {
            var px = Mathf.RoundToInt(size * Scale);
            if (field == null || fieldPx != px)
            {
                fieldPx = px;
                field = new GUIStyle(GUI.skin.textField) { fontSize = px, alignment = TextAnchor.MiddleLeft };
                field.padding = new RectOffset(px / 2, px / 2, 0, 0);
                field.normal.background = field.focused.background = field.hover.background = Tex(Panel);
                field.normal.textColor = field.focused.textColor = field.hover.textColor = Cream;
            }
            Frame(R(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(1, 1, 1, 0.15f), 1);
            return GUI.TextField(Px(r), value ?? "", maxLength, field);
        }

        /// <summary>Carte à jouer dessinée en 2D. rank null = dos.</summary>
        public static void Card(Rect r, Menteur.Core.Rank? rank, bool selected = false, bool highlight = false)
        {
            if (selected) Fill(R(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Gold);
            else if (highlight) Fill(R(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Truth);
            else Fill(R(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0, 0, 0, 0.5f));
            if (rank == null)
            {
                Fill(r, new Color(0.40f, 0.10f, 0.08f));
                Frame(R(r.x + r.width * 0.1f, r.y + r.width * 0.1f, r.width * 0.8f, r.height - r.width * 0.2f),
                    new Color(Gold.r, Gold.g, Gold.b, 0.7f), 1.2f);
                return;
            }
            var k = rank.Value;
            var c = RankColor(k);
            Fill(r, Cream);
            var s = r.width;
            Label(R(r.x + s * 0.08f, r.y + s * 0.04f, s * 0.4f, s * 0.3f), Menteur.Core.RankInfo.Short(k), s * 0.26f, c,
                TextAnchor.UpperLeft, true);
            Label(R(r.x, r.y + r.height * 0.22f, s, r.height * 0.42f), Menteur.Core.RankInfo.Short(k), s * 0.62f, c,
                TextAnchor.MiddleCenter, true);
            Label(R(r.x, r.y + r.height * 0.68f, s, r.height * 0.2f), Menteur.Core.RankInfo.Label(k).ToUpperInvariant(),
                s * 0.17f, c, TextAnchor.MiddleCenter, true);
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
                Fill(R(x + i * (size + 3), y, size, size), i < shots ? Blood : new Color(1, 1, 1, 0.28f));
        }
    }
}
