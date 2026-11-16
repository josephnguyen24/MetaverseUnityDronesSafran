using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Réglages d'affichage propres au casque (Android / Meta Quest), appliqués au chargement de chaque scène.
    /// Le brouillard linéaire est coupé sur le casque : sur Quest il recouvrait toute la scène éclairée en gris.
    /// Sans effet dans l'éditeur et sur PC.
    /// </summary>
    public static class QuestRenderTweaks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Apply();
            Apply();
        }

        static void Apply()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            RenderSettings.fog = false;
            Debug.Log("[PyroScan] Réglages Quest appliqués (brouillard coupé).");
        }
    }
}
