using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Étape 2 : carte satellite fixe accrochée au mur gauche de la tente, comme une carte routière dépliée.
    /// Cadre, titre, flèche du nord, échelle 100 m. La main gauche pourra y poser des marqueurs (étape 4) ;
    /// la main droite ne peut pas envoyer le drone depuis cette carte.
    /// </summary>
    public static class WallMapBuilder
    {
        public const float Size = 2.2f;                     // côté de la carte, en mètres
        static readonly Vector3 Center = new Vector3(-2.93f, 1.5f, 0f); // devant le mur gauche (x = -3)
        // Face visible tournée vers l'intérieur de la tente (+X) ; l'est de la carte à droite, le nord en haut.
        static readonly Quaternion Facing = Quaternion.Euler(0f, -90f, 0f);

        public static FlatMapSurface Build(Transform parent, Texture2D satellite, MapFrame frame)
        {
            var root = new GameObject("Carte murale").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(Center, Facing);

            // Carte (même matériau satellite que la table, éclairage indépendant de la tente : lisible).
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Carte_Satellite.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, "Assets/_Project/Materials/Carte_Satellite.mat");
            }
            mat.SetTexture("_BaseMap", satellite);
            EditorUtility.SetDirty(mat);

            GameObject map = GameObject.CreatePrimitive(PrimitiveType.Quad);
            map.name = "Carte satellite (murale)";
            map.transform.SetParent(root, false);
            map.transform.localScale = new Vector3(Size, Size, 1f);
            map.GetComponent<Renderer>().sharedMaterial = mat;
            var surface = map.AddComponent<FlatMapSurface>();
            surface.frame = frame;
            surface.allowDroneOrders = false;   // choix de conception : ordres drone sur la minimap uniquement
            surface.allowMarkers = true;        // marqueurs rouges (étape 4)

            // Panneau de fond et cadre en bois.
            Material board = SceneBuilder.Mat("Panneau_Carte", new Color(0.85f, 0.82f, 0.74f));
            Material wood = SceneBuilder.Mat("Cadre_Bois", new Color(0.35f, 0.22f, 0.12f));
            float half = Size * 0.5f, b = 0.06f, depth = 0.04f;
            Box(root, "Panneau", new Vector3(0f, 0.1f, 0.02f), new Vector3(Size + 0.3f, Size + 0.5f, 0.02f), board);
            Box(root, "Cadre haut", new Vector3(0f, half + b * 0.5f, -0.005f), new Vector3(Size + 2 * b, b, depth), wood);
            Box(root, "Cadre bas", new Vector3(0f, -half - b * 0.5f, -0.005f), new Vector3(Size + 2 * b, b, depth), wood);
            Box(root, "Cadre gauche", new Vector3(-half - b * 0.5f, 0f, -0.005f), new Vector3(b, Size, depth), wood);
            Box(root, "Cadre droit", new Vector3(half + b * 0.5f, 0f, -0.005f), new Vector3(b, Size, depth), wood);

            // Titre au-dessus de la carte.
            Text(root, "Titre", "SECTEUR MASSIF SUD  -  1 km x 1 km", new Vector3(0f, half + 0.2f, -0.01f),
                new Vector2(1.9f, 0.14f), new Color(0.15f, 0.1f, 0.05f), FontStyles.Bold);

            // Flèche du nord (coin haut droit).
            Material ink = SceneBuilder.Mat("Encre_Carte", new Color(0.08f, 0.08f, 0.08f));
            Material white = SceneBuilder.Mat("Blanc_Carte", new Color(0.95f, 0.95f, 0.92f));
            var north = new GameObject("Nord").transform;
            north.SetParent(root, false);
            north.localPosition = new Vector3(half - 0.16f, half - 0.2f, -0.006f);
            Box(north, "Fond", new Vector3(0f, 0f, 0.002f), new Vector3(0.2f, 0.3f, 0.002f), white);
            Box(north, "Pointe", new Vector3(0f, 0.04f, 0f), new Vector3(0.07f, 0.07f, 0.004f), ink, new Vector3(0f, 0f, 45f));
            Box(north, "Tige", new Vector3(0f, -0.02f, 0f), new Vector3(0.015f, 0.12f, 0.004f), ink);
            Text(north, "Lettre N", "N", new Vector3(0f, 0.11f, -0.002f), new Vector2(0.12f, 0.08f), Color.black, FontStyles.Bold);

            // Échelle 100 m (coin bas gauche) : 100 m = Size / 10 sur une carte de 1 km.
            float bar = Size * 100f / TerrainGenerator.SizeXZ;
            var scale = new GameObject("Échelle 100 m").transform;
            scale.SetParent(root, false);
            scale.localPosition = new Vector3(-half + 0.08f + bar * 0.5f, -half + 0.1f, -0.006f);
            Box(scale, "Fond", new Vector3(0f, 0.025f, 0.002f), new Vector3(bar + 0.08f, 0.13f, 0.002f), white);
            Box(scale, "Barre noire", new Vector3(-bar * 0.25f, 0f, 0f), new Vector3(bar * 0.5f, 0.02f, 0.004f), ink);
            Box(scale, "Barre blanche", new Vector3(bar * 0.25f, 0f, 0f), new Vector3(bar * 0.5f, 0.02f, 0.004f), white);
            Text(scale, "Libellé", "100 m", new Vector3(0f, 0.05f, -0.002f), new Vector2(bar, 0.06f), Color.black, FontStyles.Normal);

            return surface;
        }

        static void Box(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Vector3? euler = null)
        {
            SceneBuilder.Prim(PrimitiveType.Cube, name, parent, localPos, localScale, mat, keepCollider: false, euler: euler);
        }

        static void Text(Transform parent, string name, string text, Vector3 localPos, Vector2 size, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 50f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
        }
    }
}
