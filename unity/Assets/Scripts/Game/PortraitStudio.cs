// Génère, au lancement, le portrait de chaque personnage (rendu hors champ dans une texture).
// Si les modèles ne sont pas installés, on garde les images fournies (Resources/Portraits).
using System.Collections;
using UnityEngine;

namespace Menteur.Game
{
    public static class PortraitStudio
    {
        const int Layer = 31;

        public static IEnumerator Build(Texture[] portraits, Material fallbackMaterial)
        {
            if (!CharacterRig.ModelsInstalled) yield break;
            var root = new GameObject("PortraitStudio").transform;
            root.position = new Vector3(0, -100, 0);
            var rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.23f, 0.15f, 0.10f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 26;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 10;
            cam.targetTexture = rt;
            var lightGo = new GameObject("PortraitLight");
            lightGo.transform.SetParent(root, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.9f, 0.78f);
            light.cullingMask = 1 << Layer;
            lightGo.transform.rotation = Quaternion.Euler(25, 160, 0);

            for (var i = 0; i < portraits.Length; i++)
            {
                var seat = new GameObject("Seat").transform;
                seat.SetParent(root, false);
                var rig = CharacterRig.Create(i, seat, fallbackMaterial);
                foreach (var t in seat.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                // Une image pour que l'animation assise et les réglages s'appliquent.
                yield return null;
                var head = rig.HeadCenter;
                cam.transform.position = head + seat.forward * 2.0f + Vector3.up * 0.12f + seat.right * 0.25f;
                cam.transform.LookAt(head - Vector3.up * 0.06f);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                portraits[i] = tex;
                Object.Destroy(seat.gameObject);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(root.gameObject);
        }
    }
}
