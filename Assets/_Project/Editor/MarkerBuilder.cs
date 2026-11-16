using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Étapes 4 et 6a : balises rouges.
    /// La caméra du drone se pilote au joystick gauche (nacelle) ; la gâchette gauche pose une balise au point visé
    /// (viseur au centre de l'écran du drone). Gâchette gauche sur une carte : enlever la balise visée.
    /// Chaque balise apparaît en fumigène dans la vallée, en épingle sur la carte murale et en colonne sur la minimap.
    /// </summary>
    public static class MarkerBuilder
    {
        public static MarkerService Build(MapSurface wallMap, MapSurface minimap, Camera droneCamera)
        {
            var go = new GameObject("Balises rouges");
            var service = go.AddComponent<MarkerService>();
            service.maxMarkers = 10;

            var views = go.AddComponent<MarkerViews>();
            views.service = service;
            views.surfaces = new[] { wallMap, minimap };
            views.markerMaterial = SceneBuilder.Mat("Marqueur_Rouge", new Color(0.9f, 0.05f, 0.05f), new Color(2.5f, 0.1f, 0.1f));
            views.worldBeacons = true;
            views.smokeMaterial = FireBuilder.SmokeMaterial();

            var tool = go.AddComponent<LeftHandMarkerTool>();
            tool.service = service;
            tool.droneCamera = droneCamera;
            tool.rayOrigin = FindLeftAim();
            if (tool.rayOrigin == null)
                Debug.LogWarning("[PyroScan] Manette gauche introuvable dans le XR Origin : renseigne 'Ray Origin' sur LeftHandMarkerTool.");

            if (droneCamera != null)
            {
                var gimbal = droneCamera.GetComponent<DroneCameraGimbal>() ?? droneCamera.gameObject.AddComponent<DroneCameraGimbal>();
                gimbal.pitch = 45f;
                gimbal.yaw = 0f;
            }
            else Debug.LogWarning("[PyroScan] Caméra du drone introuvable : le balisage ne fonctionnera pas.");

            BuildCrosshair();
            DisableLeftLocomotion();
            return service;
        }

        /// <summary>Viseur rouge au centre de l'écran du drone (juste devant l'image).</summary>
        static void BuildCrosshair()
        {
            Transform post = GameObject.Find("Poste de commandement")?.transform;
            var root = new GameObject("Viseur caméra drone").transform;
            root.SetParent(post, false);
            root.localPosition = new Vector3(0f, 1.85f, 2.095f);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Viseur.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, "Assets/_Project/Materials/Viseur.mat");
            }
            mat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f));
            EditorUtility.SetDirty(mat);
            // Croix avec un trou au centre pour ne pas masquer le point visé.
            const float len = 0.05f, gap = 0.012f, thick = 0.004f;
            float off = gap + len * 0.5f;
            SceneBuilder.Prim(PrimitiveType.Quad, "Haut", root, new Vector3(0f, off, 0f), new Vector3(thick, len, 1f), mat, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Quad, "Bas", root, new Vector3(0f, -off, 0f), new Vector3(thick, len, 1f), mat, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Quad, "Gauche", root, new Vector3(-off, 0f, 0f), new Vector3(len, thick, 1f), mat, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Quad, "Droite", root, new Vector3(off, 0f, 0f), new Vector3(len, thick, 1f), mat, keepCollider: false);
        }

        /// <summary>Le joystick gauche sert désormais à la caméra du drone : on coupe le déplacement et la téléportation à gauche.</summary>
        static void DisableLeftLocomotion()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) return;
            int count = 0;
            foreach (MonoBehaviour mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string type = mb.GetType().Name;
                if (type.Contains("ContinuousMoveProvider"))
                {
                    mb.enabled = false;
                    count++;
                }
            }
            Transform left = Find(origin.transform, "Left Controller");
            if (left != null)
                foreach (Transform t in left.GetComponentsInChildren<Transform>(true))
                    if (t != left && t.name.Contains("Teleport") && t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        count++;
                    }
            Debug.Log($"[PyroScan] Joystick gauche libéré pour la caméra du drone ({count} élément(s) de déplacement désactivé(s)).");
        }

        static Transform FindLeftAim()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) return null;
            Transform controller = Find(origin.transform, "Left Controller");
            if (controller == null) return null;
            return Find(controller, "Near-Far Interactor") ?? controller;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
