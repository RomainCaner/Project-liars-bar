// Personnage assis à la table : modèle KayKit (CC0) animé, ou silhouette de secours
// si les modèles ne sont pas installés (menu Menteur > Télécharger les personnages).
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Menteur.Game
{
    public sealed class CharacterRig : MonoBehaviour
    {
        public static bool ModelsInstalled => Resources.Load<GameObject>("Characters/Knight") != null;

        public const float Height = 1.49f;
        const float NeckHeight = 0.77f; // hauteur de l'os « head » debout, à l'échelle voulue
        const float HeadUp = 0.148f * Height;
        const float HeadSide = 0.228f * Height;

        static readonly string[] Models =
        {
            "Knight", "Barbarian", "Mage", "Rogue", "RogueHooded",
            "Skeleton_Minion", "Skeleton_Warrior", "Skeleton_Rogue", "Skeleton_Mage",
        };
        static readonly string[] Textures =
        {
            "knight_texture", "barbarian_texture", "mage_texture", "rogue_texture", "rogue_texture",
            "skeleton_texture", "skeleton_texture", "skeleton_texture", "skeleton_texture",
        };
        static readonly Regex Props = new Regex("Sword|Shield|Axe|Spellbook|Wand|Staff|Knife|Crossbow|Throwable|Mug");

        public bool Animated { get; private set; }
        public bool Dead { get; private set; }

        /// <summary>0..1 : amène la main droite (et le revolver) à la tempe.</summary>
        public float GunPose;

        /// <summary>Rotation de tête souhaitée, en degrés.</summary>
        public float LookYaw;

        Transform model, head, spine, chest, upperR, lowerR, handR, upperL;
        Animation anim;
        readonly Dictionary<string, AnimationState> states = new Dictionary<string, AnimationState>();
        readonly HashSet<string> mixed = new HashSet<string>();
        readonly List<string> gestures = new List<string>();
        float yaw, slump, slumpSide;

        public static CharacterRig Create(int skin, Transform parent, Material fallbackMaterial)
        {
            var go = new GameObject("Character");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CharacterRig>();
            rig.slumpSide = Random.value < 0.5f ? -1 : 1;
            var i = Mathf.Abs(skin) % Models.Length;
            var prefab = Resources.Load<GameObject>("Characters/" + Models[i]);
            // Squelettes pas encore téléchargés : on prend un aventurier à la place.
            if (prefab == null && i >= 5) prefab = Resources.Load<GameObject>("Characters/" + Models[i - 5]);
            if (prefab != null) rig.BuildModel(prefab, Resources.Load<Texture2D>("Characters/" + Textures[prefab.name.StartsWith("Skeleton") ? i : i % 5]));
            else rig.BuildFallback(fallbackMaterial, i % 5);
            return rig;
        }

        // --------------------------------------------------------- construction

        void BuildModel(GameObject prefab, Texture2D texture)
        {
            model = Instantiate(prefab, transform).transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
                if (Props.IsMatch(t.name) && t.GetComponent<Renderer>() != null) t.gameObject.SetActive(false);
            }
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
            head = B("head");
            spine = B("spine");
            chest = B("chest");
            upperR = B("upperarm.r");
            lowerR = B("lowerarm.r");
            handR = B("handslot.r") ?? B("hand.r");
            upperL = B("upperarm.l");

            if (texture != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.materials)
                    if (m.mainTexture == null) m.mainTexture = texture;
            }

            // Taille normalisée d'après le squelette (identique pour tous les personnages) :
            // les unités du FBX varient selon les réglages d'import, et les chapeaux fausseraient la mesure.
            var neck = head != null ? head.position.y - model.position.y : 0f;
            if (neck > 0.0001f)
            {
                model.localScale *= NeckHeight / neck;
            }
            else
            {
                var bounds = new Bounds();
                var has = false;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (!has) bounds = r.bounds;
                    else bounds.Encapsulate(r.bounds);
                    has = true;
                }
                if (has && bounds.size.y > 0.0001f) model.localScale *= Height / bounds.size.y;
            }

            // Le modèle doit regarder vers l'avant du siège, quel que soit l'axe d'export.
            var foot = B("foot.l");
            var toes = B("toes.l");
            if (foot != null && toes != null)
            {
                var fwd = toes.position - foot.position;
                fwd.y = 0;
                if (fwd.sqrMagnitude > 1e-8f)
                {
                    var want = transform.forward;
                    want.y = 0;
                    model.rotation = Quaternion.FromToRotation(fwd.normalized, want.normalized) * model.rotation;
                }
            }

            anim = model.GetComponentInChildren<Animation>();
            if (anim == null) return;
            foreach (AnimationState st in anim) states[st.name] = st;
            anim.playAutomatically = false;
            anim.Stop();
            var sit = State("Sit_Chair_Idle");
            if (sit == null) return;
            sit.wrapMode = WrapMode.Loop;
            sit.layer = 0;
            sit.time = Random.value * sit.length;
            anim.Play(sit.name);
            Animated = true;
        }

        void BuildFallback(Material baseMaterial, int skin)
        {
            Color[] coats =
            {
                new Color(0.55f, 0.57f, 0.60f), new Color(0.45f, 0.30f, 0.20f), new Color(0.38f, 0.28f, 0.55f),
                new Color(0.20f, 0.50f, 0.30f), new Color(0.12f, 0.40f, 0.22f),
            };
            Material Mat(Color c) => new Material(baseMaterial) { color = c };
            GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c)
            {
                var g = GameObject.CreatePrimitive(type);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                g.transform.localScale = scale;
                g.GetComponent<Renderer>().sharedMaterial = Mat(c);
                return g;
            }
            var pivot = new GameObject("Torso").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0, 0.5f, 0);
            spine = pivot;
            Part(PrimitiveType.Capsule, pivot, new Vector3(0, 0.3f, 0), new Vector3(0.42f, 0.34f, 0.34f), coats[skin]);
            var headPivot = new GameObject("Head").transform;
            headPivot.SetParent(pivot, false);
            headPivot.localPosition = new Vector3(0, 0.62f, 0);
            head = headPivot;
            Part(PrimitiveType.Sphere, headPivot, new Vector3(0, HeadUp, 0), Vector3.one * 0.42f, new Color(0.88f, 0.70f, 0.55f));
            Part(PrimitiveType.Sphere, headPivot, new Vector3(-0.08f, HeadUp + 0.03f, 0.19f), Vector3.one * 0.05f, Color.black);
            Part(PrimitiveType.Sphere, headPivot, new Vector3(0.08f, HeadUp + 0.03f, 0.19f), Vector3.one * 0.05f, Color.black);
        }

        AnimationState State(string name)
        {
            if (states.TryGetValue(name, out var st)) return st;
            foreach (var e in states)
            {
                if (e.Key.EndsWith("|" + name) || e.Key.EndsWith("_" + name) || e.Key.EndsWith(name))
                {
                    states[name] = e.Value;
                    return e.Value;
                }
            }
            return null;
        }

        // ------------------------------------------------------------- gestes

        /// <summary>Geste du haut du corps, par-dessus la pose assise.</summary>
        public void Gesture(string name, bool hold = false, float speed = 1f, bool loop = false)
        {
            if (!Animated || Dead) return;
            var st = State(name);
            if (st == null) return;
            if (mixed.Add(st.name) && spine != null) st.AddMixingTransform(spine, true);
            st.layer = 1;
            st.wrapMode = loop ? WrapMode.Loop : hold ? WrapMode.ClampForever : WrapMode.Once;
            st.speed = speed;
            st.time = 0;
            if (!gestures.Contains(st.name)) gestures.Add(st.name);
            anim.CrossFade(st.name, 0.2f, PlayMode.StopSameLayer);
        }

        public void ToIdle(float fade = 0.35f)
        {
            if (!Animated) return;
            foreach (var g in gestures) anim.Blend(g, 0f, fade);
        }

        public bool Busy
        {
            get
            {
                if (!Animated) return false;
                foreach (var g in gestures)
                    if (anim.IsPlaying(g) && states[g].weight > 0.05f) return true;
                return false;
            }
        }

        public void Die(bool instant = false)
        {
            if (Dead) return;
            Dead = true;
            if (instant) slump = 1;
            ToIdle(0.15f);
        }

        // -------------------------------------------------------------- points

        public Vector3 HeadCenter => head != null ? head.position + Vector3.up * HeadUp : transform.position + Vector3.up * 1.2f;

        /// <summary>Position de la main qui tient le revolver.</summary>
        public Vector3 GunHand
        {
            get
            {
                if (handR != null) return handR.position;
                return HeadCenter + Side * HeadSide;
            }
        }

        Vector3 Side
        {
            get
            {
                var s = transform.right;
                if (upperR != null && upperL != null)
                {
                    s = upperR.position - upperL.position;
                    s.y = 0;
                    if (s.sqrMagnitude < 1e-8f) s = transform.right;
                }
                return s.normalized;
            }
        }

        // --------------------------------------------------- après l'animation

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            yaw = Mathf.Lerp(yaw, Dead ? 0 : LookYaw, Mathf.Min(1, dt * 4));
            if (!Dead && Mathf.Abs(yaw) > 0.1f)
            {
                if (chest != null) chest.rotation = Quaternion.AngleAxis(yaw * 0.25f, Vector3.up) * chest.rotation;
                if (head != null) head.rotation = Quaternion.AngleAxis(yaw * 0.7f, Vector3.up) * head.rotation;
            }

            if (GunPose > 0.001f && upperR != null && lowerR != null && handR != null && head != null)
            {
                var target = head.position + Vector3.up * HeadUp * 0.8f + Side * HeadSide;
                SolveArm(upperR, lowerR, handR, target, Mathf.Clamp01(GunPose));
            }

            if (Dead)
            {
                slump = Mathf.Min(1, slump + dt * 2.5f);
                var s = 1 - Mathf.Pow(1 - slump, 3);
                var right = transform.right;
                var fwd = transform.forward;
                if (Animated)
                {
                    if (spine != null) spine.rotation = Quaternion.AngleAxis(26 * s, right) * spine.rotation;
                    if (chest != null) chest.rotation = Quaternion.AngleAxis(14 * s, right) * chest.rotation;
                    if (head != null)
                        head.rotation = Quaternion.AngleAxis(28 * s * slumpSide, fwd) * Quaternion.AngleAxis(17 * s, right) * head.rotation;
                }
                else if (spine != null)
                {
                    spine.localRotation = Quaternion.Euler(62 * s, 0, 14 * s * slumpSide);
                }
            }
        }

        /// <summary>Cinématique inverse (CCD) à deux articulations.</summary>
        static void SolveArm(Transform upper, Transform lower, Transform effector, Vector3 target, float weight)
        {
            var startU = upper.rotation;
            var startL = lower.localRotation;
            for (var iter = 0; iter < 6; iter++)
            {
                Aim(lower, effector, target);
                Aim(upper, effector, target);
            }
            if (weight >= 0.999f) return;
            var endL = lower.localRotation;
            upper.rotation = Quaternion.Slerp(startU, upper.rotation, weight);
            lower.localRotation = Quaternion.Slerp(startL, endL, weight);
        }

        static void Aim(Transform bone, Transform effector, Vector3 target)
        {
            var toEff = effector.position - bone.position;
            var toTarget = target - bone.position;
            if (toEff.sqrMagnitude < 1e-10f || toTarget.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(toEff, toTarget) * bone.rotation;
        }
    }
}
