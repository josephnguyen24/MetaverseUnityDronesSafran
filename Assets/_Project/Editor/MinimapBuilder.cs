using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Étape 3 : minimap en relief sur la table (disque de 1 m, 150 m autour du drone, tourne avec son cap).
    /// Remplace la carte provisoire de l'étape 1. La main droite y envoie le drone (seule carte qui donne des ordres).
    /// </summary>
    public static class MinimapBuilder
    {
        static readonly Vector3 TableTop = new Vector3(0f, 0.9f, 1f);

        public static DroneMinimap Build(Transform parent, DroneMover drone, Transform truck, Transform foyer,
            MapFrame frame, Texture2D satellite)
        {
            // Socle sombre sous le disque.
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Socle minimap", parent, TableTop + new Vector3(0f, 0.01f, 0f),
                new Vector3(1.08f, 0.01f, 1.08f), SceneBuilder.Mat("Socle_Minimap", new Color(0.05f, 0.06f, 0.07f)), keepCollider: false);

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Minimap_Relief.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, "Assets/_Project/Materials/Minimap_Relief.mat");
            }
            mat.SetTexture("_BaseMap", satellite);
            mat.SetFloat("_Smoothness", 0f);
            // Légèrement auto-éclairée : lisible dans la tente sombre, tout en gardant l'ombrage du relief.
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", satellite);
            mat.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Minimap (centrée sur le drone)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = TableTop + new Vector3(0f, 0.022f, 0f);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>();
            var minimap = go.AddComponent<DroneMinimap>();
            minimap.frame = frame;
            minimap.drone = drone.transform;
            minimap.allowDroneOrders = true;
            minimap.allowMarkers = true;

            // Lettre N sur le bord, qui tourne autour du disque pour indiquer le nord.
            var n = new GameObject("Nord (minimap)");
            n.transform.SetParent(go.transform, false);
            n.transform.localPosition = new Vector3(0f, 0.01f, 0.545f);
            n.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = n.AddComponent<TextMeshPro>();
            tmp.text = "N";
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 50f;
            tmp.rectTransform.sizeDelta = new Vector2(0.07f, 0.07f);
            minimap.northMarker = n.transform;

            // Icônes : drone (flèche fixe au centre, pointe vers le fond de la table), camion, foyer, destination.
            GameObject arrow = new GameObject("Icône drone (minimap)");
            arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
            arrow.AddComponent<MeshRenderer>().sharedMaterial =
                SceneBuilder.Mat("Icone_Drone", new Color(0.1f, 0.6f, 1f), new Color(0.2f, 1.2f, 3f));
            arrow.transform.rotation = go.transform.rotation;
            Follow(arrow, drone.transform, minimap);

            AddIcon("Icône camion (minimap)", PrimitiveType.Cube, new Vector3(0.016f, 0.016f, 0.03f), truck, minimap,
                SceneBuilder.Mat("Icone_Camion", new Color(1f, 0.1f, 0.1f), new Color(2f, 0.1f, 0.1f)));

            var dest = new GameObject("Destination du drone").transform;
            dest.position = new Vector3(100000f, 0f, 100000f); // hors carte : icône cachée jusqu'au premier ordre
            AddIcon("Icône destination (minimap)", PrimitiveType.Cylinder, new Vector3(0.025f, 0.002f, 0.025f), dest, minimap,
                SceneBuilder.Mat("Marqueur_Waypoint", new Color(0f, 0.8f, 1f), new Color(0f, 1.5f, 2.5f)));

            var order = go.AddComponent<DroneOrderOnMap>();
            order.surface = minimap;
            order.drone = drone;
            order.markerPrefab = null;   // la minimap défile : on suit la destination sur le terrain à la place
            order.destination = dest;
            return minimap;
        }

        static void AddIcon(string name, PrimitiveType shape, Vector3 scale, Transform target, MapSurface surface, Material mat)
        {
            GameObject icon = GameObject.CreatePrimitive(shape);
            icon.name = name;
            icon.transform.localScale = scale;
            icon.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(icon.GetComponent<Collider>());
            Follow(icon, target, surface);
        }

        static void Follow(GameObject icon, Transform target, MapSurface surface)
        {
            var mi = icon.AddComponent<MapIcon>();
            mi.target = target;
            mi.surface = surface;
        }

        static Mesh ArrowMesh()
        {
            const string path = "Assets/_Project/Materials/Icone_Fleche.asset";
            AssetDatabase.DeleteAsset(path);
            var v = new List<Vector3>
            {
                new Vector3(0f, 0f, 0.035f), new Vector3(0.022f, 0f, -0.022f), new Vector3(0f, 0f, -0.01f), new Vector3(-0.022f, 0f, -0.022f)
            };
            var mesh = new Mesh { name = "Icone_Fleche" };
            mesh.SetVertices(v);
            // face du dessus uniquement (sens horaire vu d'en haut), normales vers le haut
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
