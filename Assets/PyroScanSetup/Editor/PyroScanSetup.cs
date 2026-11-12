using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Sample = UnityEditor.PackageManager.UI.Sample;

namespace PyroScan.Setup
{
    /// <summary>
    /// Installation automatique, en 3 étapes enchaînées au chargement de l'éditeur :
    ///   1. installe le package XR Interaction Toolkit (version recommandée pour cet éditeur) ;
    ///   2. importe ses samples "Starter Assets" et "XR Interaction Simulator" (ou "XR Device Simulator") ;
    ///   3. lance une fois le menu PyroScan > Construire la scène Rendu 1.
    /// Ce script ne dépend pas de XR Interaction Toolkit, il compile donc dans un projet vierge.
    /// Relance manuelle : menu PyroScan > Installer XR Interaction Toolkit.
    /// </summary>
    [InitializeOnLoad]
    public static class PyroScanSetup
    {
        const string XriPackage = "com.unity.xr.interaction.toolkit";
        const string ScenePath = "Assets/_Project/Scenes/Rendu1_Poste.unity";
        const string BuildMenu = "PyroScan/Construire la scène Rendu 1";

        const string AddRequestedKey = "PyroScan.Setup.AddRequested";      // par session
        const string SamplesTriedKey = "PyroScan.Setup.SamplesTried";      // par session
        static string AutoBuildDoneKey => "PyroScan.Setup.AutoBuildDone." + Application.dataPath; // par projet

        static AddRequest addRequest;

        static PyroScanSetup()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("PyroScan/Installer XR Interaction Toolkit", priority = 100)]
        static void RunFromMenu()
        {
            SessionState.SetBool(AddRequestedKey, false);
            SessionState.SetBool(SamplesTriedKey + ".Starter Assets", false);
            SessionState.SetBool(SamplesTriedKey + ".XR Interaction Simulator", false);
            SessionState.SetBool(SamplesTriedKey + ".XR Device Simulator", false);
            EditorPrefs.DeleteKey(AutoBuildDoneKey);
            Run();
        }

        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            PackageInfo xri = PackageInfo.FindForAssetPath("Packages/" + XriPackage + "/package.json");

            // Étape 1 : package
            if (xri == null)
            {
                if (SessionState.GetBool(AddRequestedKey, false)) return;
                SessionState.SetBool(AddRequestedKey, true);
                Debug.Log("[PyroScan] Installation de XR Interaction Toolkit...");
                addRequest = Client.Add(XriPackage);
                EditorApplication.update += WatchAdd;
                return;
            }

            // Étape 2 : samples
            var samples = Sample.FindByPackage(XriPackage, xri.version).ToList();
            bool starterOk = samples.Any(s => s.displayName == "Starter Assets" && s.isImported);
            bool simOk = samples.Any(s => (s.displayName == "XR Interaction Simulator" || s.displayName == "XR Device Simulator") && s.isImported);

            if (!starterOk)
            {
                ImportOnce(samples, "Starter Assets");
                return; // l'import déclenche une recompilation, puis Run() est rappelé
            }
            if (!simOk)
            {
                if (!ImportOnce(samples, "XR Interaction Simulator"))
                    ImportOnce(samples, "XR Device Simulator");
                return;
            }

            // Étape 3 : construire la scène une seule fois
            if (EditorPrefs.GetBool(AutoBuildDoneKey, false) || File.Exists(ScenePath)) return;
            EditorPrefs.SetBool(AutoBuildDoneKey, true);
            if (!EditorApplication.ExecuteMenuItem(BuildMenu))
                Debug.LogWarning("[PyroScan] Menu '" + BuildMenu + "' introuvable : vérifie la Console (erreurs de compilation ?).");
        }

        // Un seul import par passage : deux imports enchaînés font échouer le second.
        static bool ImportOnce(System.Collections.Generic.List<Sample> samples, string name)
        {
            int i = samples.FindIndex(s => s.displayName == name);
            if (i < 0) return false;
            string key = SamplesTriedKey + "." + name;
            if (SessionState.GetBool(key, false)) return true;
            SessionState.SetBool(key, true);
            Debug.Log("[PyroScan] Import du sample '" + name + "'...");
            samples[i].Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
            return true;
        }

        static void WatchAdd()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;
            EditorApplication.update -= WatchAdd;

            if (addRequest.Status == StatusCode.Success)
                Debug.Log("[PyroScan] XR Interaction Toolkit " + addRequest.Result.version + " installé.");
            else
                Debug.LogError("[PyroScan] Échec de l'installation : " + addRequest.Error?.message +
                               "\nInstalle-le via Window > Package Manager > Unity Registry.");
            addRequest = null;
            EditorApplication.delayCall += Run;
        }
    }
}
