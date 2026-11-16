using TMPro;
using UnityEditor;
using UnityEngine;

namespace PyroScan.EditorTools
{
    /// <summary>Étape 7 : détection de feu par « IA » sur la caméra du drone, affichée sur l'écran de la tente.</summary>
    public static class DetectionBuilder
    {
        public static FireDetector Build(Camera droneCamera, AircraftDispatcher dispatcher)
        {
            Transform post = GameObject.Find("Poste de commandement")?.transform;
            var root = new GameObject("IA détection feu (écran drone)").transform;
            root.SetParent(post, false);
            root.localPosition = new Vector3(0f, 1.85f, 2.093f); // juste devant l'image du drone (1,6 m x 0,9 m)

            Material orange = UnlitMat("IA_Cadre", new Color(1f, 0.55f, 0.1f));
            Material red = UnlitMat("IA_Alerte", new Color(1f, 0.12f, 0.08f));

            var detector = root.gameObject.AddComponent<FireDetector>();
            detector.droneCamera = droneCamera;
            detector.grid = Object.FindFirstObjectByType<FireGrid>();
            detector.radio = dispatcher;
            detector.overlayRoot = root;
            detector.boxMaterial = orange;

            // Cadre rouge autour de l'écran (clignote en cas d'alerte).
            const float w = 1.6f, h = 0.9f, t = 0.015f;
            detector.alertFrame = new Renderer[]
            {
                Bar(root, "Alerte haut", new Vector3(0f, h / 2f - t / 2f, 0f), new Vector3(w, t, 1f), red),
                Bar(root, "Alerte bas", new Vector3(0f, -h / 2f + t / 2f, 0f), new Vector3(w, t, 1f), red),
                Bar(root, "Alerte gauche", new Vector3(-w / 2f + t / 2f, 0f, 0f), new Vector3(t, h, 1f), red),
                Bar(root, "Alerte droite", new Vector3(w / 2f - t / 2f, 0f, 0f), new Vector3(t, h, 1f), red),
            };

            // Bandeau d'état en haut de l'écran.
            var go = new GameObject("Bandeau IA");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, -h / 2f + 0.045f, -0.001f); // en bas, pour ne pas gêner les cadres
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 0.45f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(1.5f, 0.06f);
            tmp.text = "IA DÉTECTION FEU : ACTIVE";
            detector.banner = tmp;
            return detector;
        }

        static Renderer Bar(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject q = SceneBuilder.Prim(PrimitiveType.Quad, name, parent, pos + new Vector3(0f, 0f, -0.0005f), scale, m, keepCollider: false);
            var r = q.GetComponent<Renderer>();
            r.enabled = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        static Material UnlitMat(string name, Color c)
        {
            string path = $"Assets/_Project/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
