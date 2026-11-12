// Configuration automatique du projet à l'ouverture dans Unity :
// scène principale, réglages (portrait, Android), téléchargement des personnages,
// et menu « Menteur » pour compiler l'APK en un clic.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Menteur.EditorTools
{
    [InitializeOnLoad]
    public static class MenteurSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string CharactersDir = "Assets/Resources/Characters";

        // Pack KayKit Adventurers (Kay Lousberg, licence CC0), version figée.
        const string BaseUrl =
            "https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/" +
            "672074b73ba276876a19e8816ecdc5241817ab47/addons/kaykit_character_pack_adventures/Characters/fbx/";

        static readonly string[] Files =
        {
            "knight_texture.png", "barbarian_texture.png", "mage_texture.png", "rogue_texture.png",
            "Knight.fbx", "Barbarian.fbx", "Mage.fbx", "Rogue.fbx", "RogueHooded.fbx",
        };

        static MenteurSetup()
        {
            EditorApplication.delayCall += FirstRun;
        }

        static void FirstRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                ApplySettings();
                EnsureScene();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Menteur : configuration automatique incomplète (" + e.Message + ")");
            }
            if (Application.isBatchMode || Missing().Count == 0 || SessionState.GetBool("menteur.asked", false)) return;
            SessionState.SetBool("menteur.asked", true);
            if (EditorUtility.DisplayDialog("Menteur !",
                    "Télécharger les personnages 3D animés (pack KayKit Adventurers, licence CC0, environ 100 Mo) ?\n\n" +
                    "Sans eux, le jeu fonctionne avec des silhouettes simplifiées. " +
                    "Tu pourras le faire plus tard via le menu Menteur.",
                    "Télécharger", "Plus tard"))
                DownloadCharacters();
        }

        static void ApplySettings()
        {
            if (PlayerSettings.productName == "Menteur") return;
            PlayerSettings.productName = "Menteur";
            PlayerSettings.companyName = "Menteur";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var active = EditorSceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(active.path) || active.isDirty) return; // ne pas écraser le travail en cours
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && File.Exists(s.path)))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static List<string> Missing() => Files.Where(f => !File.Exists(Path.Combine(CharactersDir, f))).ToList();

        [MenuItem("Menteur/Télécharger les personnages 3D")]
        public static void DownloadCharacters()
        {
            var missing = Missing();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Menteur !", "Les personnages sont déjà installés.", "OK");
                return;
            }
            Directory.CreateDirectory(CharactersDir);
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var web = new WebClient())
                {
                    for (var i = 0; i < missing.Count; i++)
                    {
                        EditorUtility.DisplayProgressBar("Menteur — téléchargement des personnages",
                            missing[i] + "  (" + (i + 1) + "/" + missing.Count + ")", (float)i / missing.Count);
                        var target = Path.Combine(CharactersDir, missing[i]);
                        var temp = target + ".part";
                        web.DownloadFile(BaseUrl + missing[i], temp);
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(temp, target);
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Menteur !",
                    "Téléchargement impossible : " + e.Message + "\n\nTu peux copier à la main les fichiers .fbx et .png du dossier " +
                    "Characters/fbx du pack « KayKit Adventurers » dans " + CharactersDir + ".", "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            Debug.Log("Menteur : personnages installés dans " + CharactersDir);
        }

        [MenuItem("Menteur/Réimporter les personnages")]
        public static void Reimport()
        {
            if (Directory.Exists(CharactersDir))
                AssetDatabase.ImportAsset(CharactersDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Menteur/Compiler l'APK Android")]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/menteur.apk");

        [MenuItem("Menteur/Compiler pour Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Menteur.exe");

        static void Build(BuildTarget target, string path)
        {
            EnsureScene();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None,
            });
            var ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            var full = Path.GetFullPath(path);
            if (ok) EditorUtility.RevealInFinder(full);
            EditorUtility.DisplayDialog("Menteur !",
                ok ? "Compilation réussie :\n" + full
                    : "La compilation a échoué. Regarde la Console pour le détail.\n(Pour Android, le module « Android Build Support » doit être installé depuis Unity Hub.)",
                "OK");
        }
    }

    /// <summary>Import des personnages : animations « Legacy » (pilotées en code) et seulement les clips utiles.</summary>
    public sealed class MenteurModelImport : AssetPostprocessor
    {
        static readonly string[] Clips =
        {
            "Sit_Chair_Idle", "Throw", "Interact", "Use_Item", "Unarmed_Melee_Attack_Punch_A",
            "1H_Ranged_Aiming", "Hit_A", "Hit_B", "Cheer", "Block",
        };

        bool Ours => assetPath.Replace('\\', '/').Contains("/Resources/Characters/");

        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            var all = importer.defaultClipAnimations;
            var kept = all.Where(c => Clips.Any(n => c.name == n || c.name.EndsWith("|" + n))).ToArray();
            // Si les noms ne correspondent pas (autre version du pack), on garde tout.
            if (kept.Length == 0) return;
            foreach (var c in kept)
            {
                c.loopTime = c.name.EndsWith("Sit_Chair_Idle");
                if (c.loopTime) c.wrapMode = WrapMode.Loop;
            }
            importer.clipAnimations = kept;
        }
    }
}
