// Scène 3D construite entièrement en code : salle de bar, table, personnages,
// cartes et revolver. Elle se synchronise sur l'état de la partie (Apply).
using System;
using System.Collections.Generic;
using System.Linq;
using Menteur.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Menteur.Game
{
    [DefaultExecutionOrder(100)] // après CharacterRig, pour que le revolver suive la main
    public sealed class TableScene : MonoBehaviour
    {
        const float TableY = 0.8f, TableL = 3.3f, TableW = 1.25f;

        public sealed class SeatView
        {
            public string Id;
            public CharacterRig Rig;
            public Transform Root;
            public Vector3 StackPos, Toward;
            public readonly List<Transform> Stack = new List<Transform>();
            public float NextFidget;
        }

        public Camera Cam { get; private set; }
        public readonly Dictionary<string, SeatView> Seats = new Dictionary<string, SeatView>();

        /// <summary>Opacité du flash blanc plein écran (quand c'est moi qui meurs).</summary>
        public float ScreenFlash { get; private set; }

        Shader shader;
        Material cardBack, wood, darkWood;
        Transform world, gun, drum, lamp;
        Light muzzle;
        readonly List<Transform> pile = new List<Transform>();
        readonly List<Tween> tweens = new List<Tween>();
        GameView prev;
        string seatKey = "", gunHolder;
        SeatView gunFollow;
        float shake, camRoll, camDrop;
        Vector3 camBase;

        static readonly Vector3 Pile = new Vector3(0, TableY + 0.035f, 0.15f);
        static readonly Vector3 GunRest = new Vector3(0.3f, TableY + 0.06f, -0.6f);
        static readonly Quaternion GunRestRot = Quaternion.Euler(0, 55, 90);
        static readonly Vector3 CamTarget = new Vector3(0, 0.55f, 0.55f);

        // Places autour de la table : position (x, z) et point regardé.
        static readonly Dictionary<string, Vector4> Slots = new Dictionary<string, Vector4>
        {
            { "leftNear", new Vector4(-0.86f, -0.2f, 0, -0.2f) }, { "leftFar", new Vector4(-0.86f, 0.95f, 0, 0.95f) },
            { "far", new Vector4(0, 1.98f, 0, 0) },
            { "rightFar", new Vector4(0.86f, 0.95f, 0, 0.95f) }, { "rightNear", new Vector4(0.86f, -0.2f, 0, -0.2f) },
        };

        static readonly string[][] SlotOrder =
        {
            new string[0], new[] { "far" }, new[] { "leftFar", "rightFar" }, new[] { "leftFar", "far", "rightFar" },
            new[] { "leftNear", "leftFar", "rightFar", "rightNear" },
            new[] { "leftNear", "leftFar", "far", "rightFar", "rightNear" },
        };

        // ---------------------------------------------------------------- tweens

        sealed class Tween
        {
            public float Start, Dur;
            public Action<float> Update;
            public Action Done;
            public bool EaseOut;
        }

        void Run(float dur, Action<float> update, float delay = 0, bool easeOut = false, Action done = null) =>
            tweens.Add(new Tween { Start = Time.time + delay, Dur = Mathf.Max(0.0001f, dur), Update = update, Done = done, EaseOut = easeOut });

        void After(float delay, Action f) => Run(0.0001f, _ => { }, delay, false, f);

        void RunTweens()
        {
            for (var i = tweens.Count - 1; i >= 0; i--)
            {
                var tw = tweens[i];
                if (Time.time < tw.Start) continue;
                var p = Mathf.Clamp01((Time.time - tw.Start) / tw.Dur);
                var e = tw.EaseOut ? 1 - Mathf.Pow(1 - p, 3) : (p < 0.5f ? 2 * p * p : 1 - Mathf.Pow(-2 * p + 2, 2) / 2);
                tw.Update(e);
                if (p < 1) continue;
                tweens.RemoveAt(i);
                tw.Done?.Invoke();
            }
        }

        void MoveTo(Transform t, Vector3 pos, Quaternion rot, float dur, float delay = 0, float arc = 0, Action done = null)
        {
            var p0 = Vector3.zero;
            var q0 = Quaternion.identity;
            var started = false;
            Run(dur, e =>
            {
                if (t == null) return;
                if (!started)
                {
                    started = true;
                    p0 = t.position;
                    q0 = t.rotation;
                }
                t.position = Vector3.Lerp(p0, pos, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * arc;
                t.rotation = Quaternion.Slerp(q0, rot, e);
            }, delay, true, done);
        }

        // ------------------------------------------------------------ construction

        Material Mat(Color c, float smooth = 0.15f)
        {
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g.transform;
        }

        void Awake()
        {
            // Le shader par défaut du projet (Standard, ou Lit en URP) : toujours inclus dans les builds.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shader = probe.GetComponent<Renderer>().sharedMaterial.shader;
            Destroy(probe);

            world = new GameObject("World").transform;
            world.SetParent(transform, false);
            wood = Mat(new Color(0.42f, 0.26f, 0.15f), 0.35f);
            darkWood = Mat(new Color(0.25f, 0.15f, 0.08f), 0.3f);
            cardBack = Mat(new Color(0.45f, 0.11f, 0.09f), 0.3f);

            BuildCamera();
            BuildRoom();
            BuildTable();
            BuildGun();
        }

        void BuildCamera()
        {
            foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsOfType<Light>()) l.gameObject.SetActive(false);
            var go = new GameObject("TableCamera");
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.05f, 0.03f, 0.02f);
            Cam.fieldOfView = 74;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 40;
            go.AddComponent<AudioListener>();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.33f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.fogColor = new Color(0.05f, 0.03f, 0.02f);
        }

        void BuildRoom()
        {
            var floor = Prim(PrimitiveType.Plane, world, Vector3.zero, new Vector3(1.4f, 1, 1.4f), Mat(new Color(0.33f, 0.21f, 0.12f), 0.2f), false);
            floor.name = "Floor";
            var brick = Mat(new Color(0.36f, 0.14f, 0.10f), 0.05f);
            const float h = 4.2f;
            Prim(PrimitiveType.Cube, world, new Vector3(0, h / 2, 5), new Vector3(10, h, 0.2f), brick, false);
            Prim(PrimitiveType.Cube, world, new Vector3(0, h / 2, -5), new Vector3(10, h, 0.2f), brick, false);
            Prim(PrimitiveType.Cube, world, new Vector3(-5, h / 2, 0), new Vector3(0.2f, h, 10), brick, false);
            Prim(PrimitiveType.Cube, world, new Vector3(5, h / 2, 0), new Vector3(0.2f, h, 10), brick, false);
            Prim(PrimitiveType.Cube, world, new Vector3(0, h, 0), new Vector3(10, 0.1f, 10), Mat(new Color(0.08f, 0.05f, 0.03f)), false);

            // Comptoir et bouteilles au fond.
            Prim(PrimitiveType.Cube, world, new Vector3(0, 0.55f, 4.4f), new Vector3(5, 1.1f, 0.7f), darkWood);
            Prim(PrimitiveType.Cube, world, new Vector3(0, 1.13f, 4.4f), new Vector3(5.2f, 0.08f, 0.85f), wood);
            Color[] glass =
            {
                new Color(0.18f, 0.42f, 0.23f), new Color(0.48f, 0.23f, 0.07f), new Color(0.61f, 0.48f, 0.16f),
                new Color(0.23f, 0.16f, 0.36f), new Color(0.42f, 0.12f, 0.12f), new Color(0.16f, 0.31f, 0.42f),
            };
            var glassMats = glass.Select(c => Mat(c, 0.8f)).ToArray();
            for (var shelf = 0; shelf < 2; shelf++)
            {
                Prim(PrimitiveType.Cube, world, new Vector3(0, 1.9f + shelf * 0.62f, 4.82f), new Vector3(4.4f, 0.05f, 0.3f), darkWood, false);
                for (var i = 0; i < 16; i++)
                {
                    var bh = 0.28f + ((i * 7 + shelf * 3) % 5) * 0.03f;
                    var m = glassMats[(i * 7 + shelf * 3) % glassMats.Length];
                    var x = -2.05f + i * 0.27f;
                    var y = 1.93f + shelf * 0.62f;
                    Prim(PrimitiveType.Cylinder, world, new Vector3(x, y + bh / 2, 4.82f), new Vector3(0.1f, bh / 2, 0.1f), m, false);
                    Prim(PrimitiveType.Cylinder, world, new Vector3(x, y + bh + 0.05f, 4.82f), new Vector3(0.035f, 0.05f, 0.035f), m, false);
                }
            }

            // Enseigne lumineuse.
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Roboto", "DejaVu Sans" }, 64);
            if (font != null)
            {
                var sign = new GameObject("Sign");
                sign.transform.SetParent(world, false);
                sign.transform.localPosition = new Vector3(0, 3.35f, 4.85f);
                sign.transform.localScale = Vector3.one * 0.05f;
                var tm = sign.AddComponent<TextMesh>();
                tm.font = font;
                tm.text = "MENTEUR";
                tm.fontSize = 64;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(1f, 0.55f, 0.45f);
                sign.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            AddLight(world, new Vector3(0, 3.2f, 4.3f), new Color(1f, 0.3f, 0.22f), 2.2f, 6, LightType.Point);
            AddLight(world, new Vector3(-4.6f, 2.6f, 0), new Color(1f, 0.66f, 0.38f), 1.4f, 7, LightType.Point);
            AddLight(world, new Vector3(4.6f, 2.6f, 0), new Color(1f, 0.66f, 0.38f), 1.4f, 7, LightType.Point);

            // Lampe suspendue : la lumière principale.
            lamp = new GameObject("Lamp").transform;
            lamp.SetParent(world, false);
            lamp.localPosition = new Vector3(0, h, 0);
            var black = Mat(new Color(0.03f, 0.03f, 0.03f));
            Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, -0.72f, 0), new Vector3(0.016f, 0.72f, 0.016f), black, false);
            Prim(PrimitiveType.Sphere, lamp, new Vector3(0, -1.52f, 0), new Vector3(0.7f, 0.26f, 0.7f), Mat(new Color(0.10f, 0.22f, 0.15f), 0.5f), false);
            var spot = AddLight(lamp, new Vector3(0, -1.7f, 0), new Color(1f, 0.85f, 0.63f), 3.2f, 9, LightType.Spot);
            spot.spotAngle = 118;
            spot.transform.localRotation = Quaternion.Euler(90, 0, 0);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.7f;
            // Lumière d'appoint depuis la caméra pour lire les visages.
            var fill = AddLight(world, Vector3.zero, new Color(1f, 0.82f, 0.66f), 0.55f, 0, LightType.Directional);
            fill.transform.rotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 1f));
        }

        Light AddLight(Transform parent, Vector3 pos, Color color, float intensity, float range, LightType type)
        {
            var go = new GameObject(type + "Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            if (range > 0) l.range = range;
            return l;
        }

        void BuildTable()
        {
            var felt = Mat(new Color(0.10f, 0.30f, 0.21f), 0.02f);
            // Plateau : un rectangle et deux demi-cercles aux extrémités.
            var mid = TableL - TableW;
            Prim(PrimitiveType.Cube, world, new Vector3(0, TableY - 0.04f, 0), new Vector3(TableW, 0.08f, mid), wood);
            Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableY - 0.04f, mid / 2), new Vector3(TableW, 0.04f, TableW), wood);
            Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableY - 0.04f, -mid / 2), new Vector3(TableW, 0.04f, TableW), wood);
            const float inset = 0.16f;
            Prim(PrimitiveType.Cube, world, new Vector3(0, TableY + 0.003f, 0), new Vector3(TableW - inset, 0.012f, mid), felt, false);
            Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableY + 0.003f, mid / 2), new Vector3(TableW - inset, 0.006f, TableW - inset), felt, false);
            Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableY + 0.003f, -mid / 2), new Vector3(TableW - inset, 0.006f, TableW - inset), felt, false);
            foreach (var x in new[] { -0.45f, 0.45f })
            foreach (var z in new[] { -1.15f, 1.15f })
                Prim(PrimitiveType.Cylinder, world, new Vector3(x, (TableY - 0.08f) / 2, z), new Vector3(0.09f, (TableY - 0.08f) / 2, 0.09f), darkWood);
        }

        void BuildGun()
        {
            gun = new GameObject("Revolver").transform;
            gun.SetParent(world, false);
            var metal = Mat(new Color(0.38f, 0.40f, 0.43f), 0.7f);
            var dark = Mat(new Color(0.14f, 0.15f, 0.17f), 0.6f);
            // Canon le long de +Z (pratique avec LookRotation).
            var barrel = Prim(PrimitiveType.Cylinder, gun, new Vector3(0, 0.02f, 0.12f), new Vector3(0.028f, 0.1f, 0.028f), metal);
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cube, gun, new Vector3(0, 0.01f, -0.01f), new Vector3(0.035f, 0.06f, 0.1f), dark);
            drum = Prim(PrimitiveType.Cylinder, gun, new Vector3(0, 0.008f, 0.01f), new Vector3(0.066f, 0.03f, 0.066f), metal);
            drum.localRotation = Quaternion.Euler(90, 0, 0);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI / 3;
                Prim(PrimitiveType.Cylinder, drum, new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f), new Vector3(0.24f, 1.03f, 0.24f), dark, false);
            }
            var grip = Prim(PrimitiveType.Cube, gun, new Vector3(0, -0.055f, -0.06f), new Vector3(0.03f, 0.1f, 0.045f), Mat(new Color(0.42f, 0.23f, 0.10f), 0.4f));
            grip.localRotation = Quaternion.Euler(-20, 0, 0);
            muzzle = AddLight(gun, new Vector3(0, 0.02f, 0.26f), new Color(1f, 0.7f, 0.38f), 0, 4, LightType.Point);
            gun.localScale = Vector3.one * 1.6f;
            gun.position = GunRest;
            gun.rotation = GunRestRot;
        }

        Transform NewCard(Vector3 pos, Quaternion rot, float scale = 1f)
        {
            var c = Prim(PrimitiveType.Cube, world, Vector3.zero, new Vector3(0.105f, 0.004f, 0.15f) * scale, cardBack);
            c.position = pos;
            c.rotation = rot;
            return c;
        }

        // ----------------------------------------------------------------- sièges

        static string KeyOf(GameView v) => string.Join("|", v.Players.Select(p => p.Id + ":" + p.Skin)) + "#" + v.MyId;

        void BuildSeats(GameView v)
        {
            foreach (var s in Seats.Values)
            {
                foreach (var c in s.Stack) if (c != null) Destroy(c.gameObject);
                Destroy(s.Root.gameObject);
            }
            Seats.Clear();
            gunFollow = null;
            var ids = v.Players.Select(p => p.Id).ToList();
            var me = Mathf.Max(0, ids.IndexOf(v.MyId));
            var others = ids.Skip(me + 1).Concat(ids.Take(me)).Where(id => id != v.MyId).ToList();
            var order = SlotOrder[Mathf.Min(5, others.Count)];
            for (var i = 0; i < others.Count && i < order.Length; i++)
            {
                var slot = Slots[order[i]];
                var player = v.Player(others[i]);
                var root = new GameObject("Seat-" + others[i]).transform;
                root.SetParent(world, false);
                root.position = new Vector3(slot.x, 0, slot.y);
                var toward = new Vector3(slot.z - slot.x, 0, slot.w - slot.y).normalized;
                root.rotation = Quaternion.LookRotation(toward);
                BuildChair(root);
                var rig = CharacterRig.Create(player.Skin, root, wood);
                Seats[others[i]] = new SeatView
                {
                    Id = others[i], Root = root, Rig = rig, Toward = toward,
                    StackPos = new Vector3(slot.x, TableY + 0.014f, slot.y) + toward * (slot.x == 0 ? 0.62f : 0.42f),
                    NextFidget = Time.time + 4 + UnityEngine.Random.value * 8,
                };
            }
            seatKey = KeyOf(v);
        }

        void BuildChair(Transform root)
        {
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0.47f, -0.05f), new Vector3(0.5f, 0.05f, 0.45f), darkWood);
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0.8f, -0.28f), new Vector3(0.5f, 0.6f, 0.05f), darkWood);
            foreach (var x in new[] { -0.21f, 0.21f })
            foreach (var z in new[] { -0.25f, 0.15f })
                Prim(PrimitiveType.Cube, root, new Vector3(x, 0.235f, z), new Vector3(0.04f, 0.47f, 0.04f), darkWood, false);
        }

        /// <summary>Cartes en main, posées face cachée devant le joueur.</summary>
        void SetStack(SeatView s, int n)
        {
            while (s.Stack.Count > n)
            {
                var last = s.Stack[s.Stack.Count - 1];
                s.Stack.RemoveAt(s.Stack.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }
            var right = Vector3.Cross(Vector3.up, s.Toward);
            var yaw = Quaternion.LookRotation(s.Toward);
            while (s.Stack.Count < n)
            {
                var i = s.Stack.Count;
                var pos = s.StackPos + right * ((i - 2) * 0.034f) + Vector3.up * (0.004f * i);
                s.Stack.Add(NewCard(pos, yaw * Quaternion.Euler(0, (i - 2) * 7f, 0), 0.8f));
            }
        }

        // -------------------------------------------------------------- tas central

        void PilePose(int i, out Vector3 pos, out Quaternion rot)
        {
            pos = Pile + new Vector3(Mathf.Cos(i * 2.4f) * 0.07f, 0.004f * i, Mathf.Sin(i * 2.4f) * 0.07f);
            rot = Quaternion.Euler(0, (i * 67) % 80 - 40, 0);
        }

        void ClearPile(bool animate)
        {
            foreach (var c in pile)
            {
                if (c == null) continue;
                var card = c;
                if (!animate)
                {
                    Destroy(card.gameObject);
                    continue;
                }
                var s0 = card.localScale;
                Run(0.35f, e =>
                {
                    if (card != null) card.localScale = s0 * (1 - e * 0.95f);
                }, 0, false, () =>
                {
                    if (card != null) Destroy(card.gameObject);
                });
            }
            pile.Clear();
        }

        void FlyToPile(Vector3 from, int count)
        {
            for (var k = 0; k < count; k++)
            {
                PilePose(pile.Count, out var pos, out var rot);
                var card = NewCard(from, rot);
                pile.Add(card);
                MoveTo(card, pos, rot, 0.5f, k * 0.12f, 0.25f);
            }
        }

        // ------------------------------------------------------------------ revolver

        void GunPoseFor(SeatView s, out Vector3 pos, out Quaternion rot)
        {
            pos = s.Rig.GunHand;
            var dir = s.Rig.HeadCenter - pos;
            rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : gun.rotation;
        }

        void RaiseGun(SeatView s)
        {
            s.Rig.Gesture("1H_Ranged_Aiming", hold: true);
            var p0 = gun.position;
            var q0 = gun.rotation;
            Run(0.9f, e =>
            {
                s.Rig.GunPose = e;
                GunPoseFor(s, out var pos, out var rot);
                gun.position = Vector3.Lerp(p0, pos, e);
                gun.rotation = Quaternion.Slerp(q0, rot, e);
            }, 0, false, () =>
            {
                if (gunHolder == s.Id) gunFollow = s;
            });
        }

        void RaiseGunToMe()
        {
            var pos = Cam.transform.TransformPoint(new Vector3(0.17f, -0.02f, 0.6f));
            var temple = Cam.transform.TransformPoint(new Vector3(-0.15f, 0.05f, -0.1f));
            MoveTo(gun, pos, Quaternion.LookRotation(temple - pos), 1.0f);
            ScaleGun(0.75f);
        }

        void ScaleGun(float to)
        {
            var s0 = gun.localScale.x;
            Run(0.8f, e => gun.localScale = Vector3.one * Mathf.Lerp(s0, to, e));
        }

        void LowerGun()
        {
            if (gunFollow != null && !gunFollow.Rig.Dead)
            {
                var s = gunFollow;
                Run(0.6f, e => s.Rig.GunPose = 1 - e);
                s.Rig.ToIdle();
            }
            gunFollow = null;
            gunHolder = null;
            ScaleGun(1.6f);
            MoveTo(gun, GunRest, GunRestRot, 0.8f);
        }

        void Fire(bool died, bool shooterIsMe, SeatView seat)
        {
            var spin = 0f;
            Run(0.9f, e =>
            {
                var a = e * 780f;
                drum.Rotate(0, a - spin, 0, Space.Self);
                spin = a;
            }, 0, true, () => AfterSpin(died, shooterIsMe, seat));
        }

        void AfterSpin(bool died, bool shooterIsMe, SeatView seat)
        {
            if (!died)
            {
                if (seat != null)
                {
                    After(0.5f, () =>
                    {
                        LowerGun();
                        seat.Rig.Gesture("Cheer", speed: 1.2f);
                    });
                }
                return;
            }
            Run(0.35f, e => muzzle.intensity = 6 * (1 - e));
            shake = shooterIsMe ? 0.07f : 0.03f;
            foreach (var other in Seats.Values)
                if (other != seat && !other.Rig.Dead) other.Rig.Gesture(UnityEngine.Random.value < 0.5f ? "Hit_A" : "Hit_B", speed: 1.3f);
            if (shooterIsMe)
            {
                Run(0.5f, e => ScreenFlash = 1 - e);
                Run(0.9f, e => { camRoll = 28 * e; camDrop = 0.5f * e; }, 0, true);
                Run(1.2f, e => { camRoll = 28 * (1 - e); camDrop = 0.5f * (1 - e); }, 2.4f);
            }
            else if (seat != null)
            {
                seat.Rig.Die();
                gunFollow = null;
                gunHolder = null;
                // Le revolver tombe sur la table.
                MoveTo(gun, GunRest, GunRestRot, 0.5f, 0.15f, 0.1f);
            }
        }

        // ---------------------------------------------------------------- synchro

        public void Apply(GameView v)
        {
            if (v == null || v.Phase == Phase.Lobby)
            {
                prev = null;
                return;
            }
            var rebuilt = KeyOf(v) != seatKey;
            if (rebuilt) BuildSeats(v);
            var newRound = prev == null || rebuilt || v.Round != prev.Round;
            if (newRound)
            {
                ClearPile(!rebuilt && prev != null);
                if (gunHolder != null) LowerGun();
            }

            foreach (var p in v.Players)
            {
                if (!Seats.TryGetValue(p.Id, out var seat)) continue;
                if (newRound && p.Alive && prev != null && !rebuilt)
                {
                    SetStack(seat, 0);
                    for (var k = 1; k <= p.CardCount; k++)
                    {
                        var n = k;
                        var round = v.Round;
                        After(0.25f + k * 0.11f, () =>
                        {
                            if (prev != null && prev.Round == round && seat.Stack.Count < n) SetStack(seat, n);
                        });
                    }
                }
                else
                {
                    SetStack(seat, p.Alive ? p.CardCount : 0);
                }
                var dyingNow = v.Phase == Phase.Roulette && v.ShooterId == p.Id && v.ShotDied == true && prev != null && prev.ShotDied == null;
                if (!p.Alive && !dyingNow) seat.Rig.Die(instant: true);
            }

            // Cartes jouées : elles volent vers le tas.
            if (!newRound && v.Pile > prev.Pile && v.LastPlayBy != null)
            {
                var n = v.Pile - prev.Pile;
                if (v.LastPlayBy == v.MyId) FlyToPile(Cam.transform.TransformPoint(new Vector3(0, -0.25f, 0.7f)), n);
                else if (Seats.TryGetValue(v.LastPlayBy, out var seat))
                {
                    seat.Rig.Gesture("Throw", speed: 1.4f);
                    var from = seat.StackPos + Vector3.up * 0.05f;
                    After(0.3f, () => FlyToPile(from, n));
                }
                else FlyToPile(Pile + Vector3.up, n);
            }
            if ((rebuilt || newRound) && v.Pile > 0)
            {
                for (var i = pile.Count; i < v.Pile; i++)
                {
                    PilePose(i, out var pos, out var rot);
                    pile.Add(NewCard(pos, rot));
                }
            }

            // Accusation : gestes de l'accusateur et de l'accusé.
            var revealNow = v.Reveal != null && (v.Phase == Phase.Reveal || v.Phase == Phase.Roulette);
            if (revealNow && !(prev != null && prev.Reveal != null && prev.Round == v.Round))
            {
                var r = v.Reveal;
                if (Seats.TryGetValue(r.AccuserId, out var accuser)) accuser.Rig.Gesture("Unarmed_Melee_Attack_Punch_A", speed: 1.1f);
                if (Seats.TryGetValue(r.AccusedId, out var accused))
                {
                    accused.Rig.Gesture("Block", speed: 1.2f);
                    After(1.5f, () => accused.Rig.Gesture(r.WasLie ? "Hit_B" : "Cheer"));
                }
                // Les cartes contestées se soulèvent du tas.
                var taken = pile.Skip(Mathf.Max(0, pile.Count - r.Cards.Count)).ToList();
                for (var i = 0; i < taken.Count; i++)
                {
                    var x = (i - (taken.Count - 1) / 2f) * 0.14f;
                    MoveTo(taken[i], Pile + new Vector3(x, 0.22f, -0.1f), Quaternion.Euler(-55, 0, 0), 0.6f, 0.15f + i * 0.25f);
                }
            }

            // Roulette.
            if (v.Phase == Phase.Roulette && v.ShooterId != null && v.ShooterId != gunHolder && v.ShotDied == null)
            {
                gunHolder = v.ShooterId;
                if (v.ShooterId == v.MyId) RaiseGunToMe();
                else if (Seats.TryGetValue(v.ShooterId, out var shooter)) RaiseGun(shooter);
            }
            if (v.Phase == Phase.Roulette && v.ShotDied != null && prev != null && prev.ShotDied == null)
            {
                Seats.TryGetValue(v.ShooterId ?? "", out var shooter);
                Fire(v.ShotDied.Value, v.ShooterId == v.MyId, shooter);
            }
            if (v.Phase == Phase.GameOver)
            {
                if (gunHolder != null) LowerGun();
                if ((prev == null || prev.Phase != Phase.GameOver) && v.WinnerId != null && Seats.TryGetValue(v.WinnerId, out var w))
                    w.Rig.Gesture("Cheer", loop: true);
            }
            prev = v;
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            RunTweens();
            var t = Time.time;

            // Cadrage : plus l'écran est étroit, plus la caméra recule.
            var halfH = Mathf.Atan(Mathf.Tan(Cam.fieldOfView * Mathf.Deg2Rad / 2) * Cam.aspect);
            var dist = Mathf.Max(2.2f, 1.15f / Mathf.Tan(halfH));
            camBase = new Vector3(0, 1.0f + dist * 0.55f, -(0.45f + dist * 0.85f));
            shake *= 0.9f;
            var jitter = new Vector3((UnityEngine.Random.value - 0.5f) * shake, (UnityEngine.Random.value - 0.5f) * shake, 0);
            Cam.transform.position = camBase + jitter + Vector3.up * (Mathf.Sin(t * 0.7f) * 0.01f - camDrop);
            Cam.transform.rotation = Quaternion.LookRotation(CamTarget - Cam.transform.position) * Quaternion.Euler(0, 0, camRoll);

            if (lamp != null) lamp.localRotation = Quaternion.Euler(Mathf.Cos(t * 0.45f) * 1.2f, 0, Mathf.Sin(t * 0.6f) * 1.5f);

            // Les joueurs regardent celui dont c'est le tour (ou le tireur).
            string focus = null;
            if (prev != null) focus = prev.Phase == Phase.Roulette ? prev.ShooterId : prev.CurrentId;
            var target = Pile;
            SeatView focusSeat = null;
            if (focus != null && Seats.TryGetValue(focus, out focusSeat)) target = focusSeat.Rig.HeadCenter;
            else if (focus != null && prev != null && focus == prev.MyId) target = Cam.transform.position;
            foreach (var s in Seats.Values)
            {
                if (s == focusSeat)
                {
                    s.Rig.LookYaw = 0;
                }
                else
                {
                    var dir = target - s.Root.position;
                    dir.y = 0;
                    s.Rig.LookYaw = Mathf.Clamp(Vector3.SignedAngle(s.Root.forward, dir, Vector3.up), -57, 57);
                }
                // Petits gestes d'attente.
                if (prev != null && prev.Phase == Phase.Playing && !s.Rig.Dead && t > s.NextFidget && !s.Rig.Busy)
                {
                    s.Rig.Gesture(UnityEngine.Random.value < 0.5f ? "Interact" : "Use_Item", speed: 0.8f);
                    s.NextFidget = t + 7 + UnityEngine.Random.value * 9;
                }
            }
        }

        void LateUpdate()
        {
            // Le revolver suit la main du tireur (après l'animation et l'IK du personnage).
            if (gunFollow == null) return;
            GunPoseFor(gunFollow, out var pos, out var rot);
            gun.position = pos;
            gun.rotation = rot;
        }

        /// <summary>Position, à l'écran, de l'étiquette d'un joueur.</summary>
        public bool LabelPoint(string id, out Vector3 screen)
        {
            screen = Vector3.zero;
            if (!Seats.TryGetValue(id, out var s)) return false;
            screen = Cam.WorldToScreenPoint(s.Rig.HeadCenter + Vector3.up * 0.42f);
            return screen.z > 0;
        }
    }
}
