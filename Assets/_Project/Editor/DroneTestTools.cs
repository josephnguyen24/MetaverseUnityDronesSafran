using UnityEditor;
using UnityEngine;

namespace PyroScan.EditorTools
{
    /// <summary>Raccourcis de test en Play : envoyer le drone au-dessus de la vallée sans passer par la carte.</summary>
    public static class DroneTestTools
    {
        [MenuItem("PyroScan/Outils/Test : envoyer le drone au-dessus du feu (Play)", priority = 60)]
        static void SendDroneToFire()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[PyroScan] À utiliser en mode Play."); return; }
            var drone = Object.FindFirstObjectByType<DroneMover>();
            if (drone == null) return;
            var start = GameObject.Find("Point de départ du feu");
            Vector3 p = start != null ? start.transform.position : new Vector3(-150f, 0f, 400f);
            drone.GoTo(p + new Vector3(60f, 0f, -80f)); // un peu en retrait du foyer, caméra à 45° vers l'avant
            Debug.Log($"[PyroScan] Test : drone envoyé vers {p}.");
        }

        [MenuItem("PyroScan/Outils/Test : téléporter le drone au-dessus du feu (Play)", priority = 61)]
        static void TeleportDroneToFire()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[PyroScan] À utiliser en mode Play."); return; }
            var drone = Object.FindFirstObjectByType<DroneMover>();
            if (drone == null) return;
            var start = GameObject.Find("Point de départ du feu");
            Vector3 p = (start != null ? start.transform.position : new Vector3(-150f, 0f, 400f)) + new Vector3(0f, 0f, -90f);
            float y = MapFrame.Main != null ? MapFrame.Main.SampleHeight(p) : p.y;
            drone.transform.position = new Vector3(p.x, y + drone.altitude, p.z);
            drone.transform.rotation = Quaternion.identity;
            Debug.Log($"[PyroScan] Test : drone téléporté en {drone.transform.position}.");
        }
    }
}
