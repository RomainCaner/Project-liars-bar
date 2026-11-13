// Accessoires du pack KayKit Dungeon Remastered (CC0, Kay Lousberg), dans Resources/Props.
// Ils sont mis à l'échelle des personnages et orientés automatiquement, quels que soient
// les réglages d'import du FBX.
using System.Collections.Generic;
using UnityEngine;

namespace Menteur.Game
{
    public static class Props
    {
        /// <summary>Facteur entre les unités natives KayKit et le monde (même que pour les personnages).</summary>
        public const float F = 0.62f;

        // Hauteur native de chaque modèle (mesurée sur les fichiers du pack).
        static readonly Dictionary<string, float> NativeHeight = new Dictionary<string, float>
        {
            { "table_long", 1.0f }, { "table_medium", 1.0f }, { "chair", 1.23f }, { "stool", 0.5f },
            { "barrel_large", 2.0f }, { "barrel_small", 1.02f }, { "barrel_small_stack", 1.77f },
            { "keg", 2.05f }, { "keg_decorated", 2.05f }, { "crates_stacked", 2.14f },
            { "bottle_A_brown", 0.89f }, { "bottle_A_labeled_green", 0.89f }, { "bottle_B_green", 0.89f },
            { "bottle_C_brown", 0.91f }, { "candle_lit", 1.05f }, { "candle_triple", 0.87f }, { "candle_thin_lit", 1.05f },
            { "torch_mounted", 1.06f }, { "banner_patternA_red", 3.2f }, { "banner_red", 3.2f },
            { "shelf_large", 0.45f }, { "shelf_small_candles", 0.99f }, { "wall", 4f }, { "wall_shelves", 4f },
            { "wall_window_closed", 4f }, { "wall_pillar", 4f }, { "floor_wood_large", 0.15f },
            { "floor_wood_large_dark", 0.15f }, { "plate_food_A", 0.79f },
        };

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static Texture2D texture;
        static bool textureLoaded;

        public static bool Available => Load("table_long") != null;

        static GameObject Load(string name)
        {
            if (!cache.TryGetValue(name, out var go)) cache[name] = go = Resources.Load<GameObject>("Props/" + name);
            return go;
        }

        /// <summary>
        /// Instancie un accessoire posé au sol en <paramref name="pos"/>, tourné de <paramref name="yaw"/> degrés,
        /// à l'échelle <paramref name="size"/> (1 = taille d'origine du pack). Renvoie null si absent.
        /// </summary>
        public static Transform Spawn(string name, Transform parent, Vector3 pos, float yaw = 0, float size = 1f,
            bool shadows = true)
        {
            var prefab = Load(name);
            if (prefab == null) return null;
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var model = Object.Instantiate(prefab, holder).transform;
            model.localPosition = Vector3.zero;
            if (!textureLoaded)
            {
                textureLoaded = true;
                texture = Resources.Load<Texture2D>("Props/dungeon_texture");
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (texture == null) continue;
                foreach (var m in r.materials)
                    if (m.mainTexture == null) m.mainTexture = texture;
            }
            // Mise à l'échelle d'après la hauteur réelle importée.
            if (NativeHeight.TryGetValue(name, out var native))
            {
                var b = Bounds(model);
                if (b.size.y > 1e-5f) model.localScale *= F * size * native / b.size.y;
                b = Bounds(model);
                // Pose le bas du modèle à l'origine du support (sauf objets fixés au mur).
                if (!name.StartsWith("torch") && !name.StartsWith("banner") && !name.StartsWith("shelf"))
                    model.position += Vector3.up * (holder.position.y - b.min.y);
            }
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            return holder;
        }

        public static Bounds Bounds(Transform t)
        {
            var has = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) b = r.bounds;
                else b.Encapsulate(r.bounds);
                has = true;
            }
            return b;
        }

        /// <summary>
        /// Oriente le modèle pour que la partie « haute » (dossier de chaise, décor d'un mur…)
        /// se trouve du côté <paramref name="towards"/>. Nécessite des maillages lisibles (réglé à l'import).
        /// </summary>
        public static void FaceUpperPart(Transform holder, Vector3 towards, float minHeightRatio = 0.6f)
        {
            var model = holder.childCount > 0 ? holder.GetChild(0) : null;
            if (model == null) return;
            var b = Bounds(model);
            var cut = b.min.y + b.size.y * minHeightRatio;
            var sum = Vector3.zero;
            var n = 0;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var verts = mesh.vertices;
                for (var i = 0; i < verts.Length; i += 3)
                {
                    var w = mf.transform.TransformPoint(verts[i]);
                    if (w.y < cut) continue;
                    sum += w;
                    n++;
                }
            }
            if (n == 0) return;
            var dir = sum / n - b.center;
            dir.y = 0;
            towards.y = 0;
            if (dir.sqrMagnitude < 1e-6f || towards.sqrMagnitude < 1e-6f) return;
            var angle = Vector3.SignedAngle(dir, towards, Vector3.up);
            holder.rotation = Quaternion.AngleAxis(angle, Vector3.up) * holder.rotation;
        }

        /// <summary>Oriente un objet d'après la position moyenne de tous ses sommets (objets muraux).</summary>
        public static void FaceBulk(Transform holder, Vector3 towards) => FaceUpperPart(holder, towards, 0f);
    }
}
