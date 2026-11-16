using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Menu PyroScan > Build And Run sur Meta Quest :
    /// règle les Player Settings Android pour le Quest, construit l'APK et l'installe sur le casque branché.
    /// </summary>
    public static class QuestBuild
    {
        const string ApkPath = "Builds/Quest/PyroScan.apk";

        [MenuItem("PyroScan/Build And Run sur Meta Quest", priority = 20)]
        public static void BuildAndRun()
        {
            ApplyQuestPlayerSettings();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("PyroScan", "Aucune scène dans les Build Settings.", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.AutoRunPlayer
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
                Debug.Log($"[PyroScan] Build Quest réussi ({sum.totalSize / (1024 * 1024)} Mo, {sum.totalTime:mm\\:ss}) : {ApkPath}. Lancement sur le casque.");
            else
                Debug.LogError($"[PyroScan] Build Quest : {sum.result} ({sum.totalErrors} erreur(s)). Voir la Console.");
        }

        [MenuItem("PyroScan/Appliquer les réglages Meta Quest", priority = 21)]
        public static void ApplyQuestPlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            if (PlayerSettings.GetApplicationIdentifier(android).Contains("template"))
                PlayerSettings.SetApplicationIdentifier(android, "com.ece.pyroscan");
            PlayerSettings.productName = "PyroScan";
            PlayerSettings.companyName = "ECE";
            AssetDatabase.SaveAssets();
            Debug.Log("[PyroScan] Réglages Android pour Meta Quest appliqués (IL2CPP, ARM64, API 32, com.ece.pyroscan).");
        }
    }

    /// <summary>
    /// Retire le simulateur XR des scènes au moment du build : il reste disponible dans l'éditeur
    /// pour tester sans casque, mais n'est jamais embarqué sur le Quest.
    /// </summary>
    public class StripXRSimulatorFromBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return; // Play mode dans l'éditeur : on garde le simulateur
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.Contains("XR Interaction Simulator") || root.name.Contains("XR Device Simulator"))
                {
                    Debug.Log("[PyroScan] Simulateur retiré du build : " + root.name);
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
