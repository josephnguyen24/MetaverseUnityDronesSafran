using System.IO;
using UnityEditor;
using UnityEngine;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Menu PyroScan > Outils > Capturer la vue du joueur : enregistre 4 images (nord, ouest, est, table)
    /// de ce que voit le joueur debout dans la tente, dans le dossier Captures/ du projet.
    /// Pratique pour vérifier la scène et pour illustrer le rapport.
    /// </summary>
    public static class ViewCapture
    {
        [MenuItem("PyroScan/Outils/Capturer la vue du joueur", priority = 50)]
        public static void Capture()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures");
            Directory.CreateDirectory(dir);

            var go = new GameObject("Camera capture (temporaire)");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SceneBuilder.FogColor;
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;

            (string name, Vector3 euler)[] views =
            {
                ("1_nord_vallee", new Vector3(5f, 0f, 0f)),
                ("2_ouest_carte_murale", new Vector3(5f, -90f, 0f)),
                ("3_est_pupitre", new Vector3(10f, 90f, 0f)),
                ("4_table", new Vector3(50f, 0f, 0f)),
            };
            foreach (var v in views)
            {
                go.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(v.euler));
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, v.name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            Debug.Log("[PyroScan] Captures enregistrées dans " + dir);
        }
    }
}
