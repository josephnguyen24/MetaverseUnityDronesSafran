using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Menu PyroScan > Construire la scène Rendu 1.
    /// Crée la scène en formes simples (greybox) : poste de commandement, table tactique,
    /// écran du drone, vallée, objets de l'histoire, lumière et ambiance, rig XR + simulateur.
    /// Relancer le menu reconstruit la scène de zéro (les matériaux sont réutilisés).
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/_Project";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Rendu1_Poste.unity";
        const string RtPath = MatDir + "/DroneCam.renderTexture";
        const string MarkerPath = PrefabDir + "/WaypointMarker.prefab";

        // La vallée (200 m x 200 m) est 40 m plus bas que le poste, devant le joueur (+Z).
        static readonly Vector3 ValleyPosition = new Vector3(0f, -40f, 160f);
        // Échelle de la maquette : 200 m -> 1,2 m sur la table.
        const float MiniScale = 0.006f;
        static readonly Vector3 TableCenter = new Vector3(0f, 0.45f, 1.0f);
        const float TableHeight = 0.9f;

        internal static readonly Color FogColor = new Color(0.46f, 0.42f, 0.37f); // brume légère de fumée (moins orangée qu'avant)

        [MenuItem("PyroScan/Construire la scène Rendu 1", priority = 1)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupAmbiance();
            BuildCommandPost(out Transform screenAnchor);
            Transform valley = BuildValley();
            Transform miniValley = BuildMiniValley(valley);
            RenderTexture droneRt = GetOrCreateRenderTexture();
            DroneMover drone = BuildUnits(valley, miniValley, droneRt);
            BuildScreen(screenAnchor, droneRt);

            TacticalTable table = miniValley.gameObject.AddComponent<TacticalTable>();
            table.valley = valley;
            table.drone = drone;
            table.markerPrefab = GetOrCreateMarkerPrefab();

            bool rigFound = SetupXR();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();

            string msg = "Scène créée : " + ScenePath + "\n\n";
            msg += rigFound
                ? "Lance Play, puis vise la maquette avec la manette simulée et appuie sur le bouton de sélection (grip) : le drone part."
                : "Attention : le prefab 'XR Interaction Setup' ou 'XR Origin (XR Rig)' est introuvable.\n" +
                  "Importe le sample 'Starter Assets' de XR Interaction Toolkit (Package Manager > Samples), puis relance ce menu.";
            EditorUtility.DisplayDialog("PyroScan", msg, "OK");
        }

        // ---------------------------------------------------------------- Ambiance
        internal static void SetupAmbiance()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.31f, 0.30f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 260f;

            // Soleil bas, orangé, filtré par la fumée, face au joueur (contre-jour sur la vallée).
            var sun = new GameObject("Soleil (filtré par la fumée)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.62f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(18f, 200f, 0f);
        }

        // ---------------------------------------------------------------- Poste de commandement
        static void BuildCommandPost(out Transform screenAnchor)
        {
            Material ground = Mat("Sol_Poste", new Color(0.18f, 0.17f, 0.16f));
            Material canvas = Mat("Toile_Tente", new Color(0.55f, 0.12f, 0.1f));
            Material dark = Mat("Mobilier_Sombre", new Color(0.12f, 0.12f, 0.13f));
            Material rock = Mat("Roche", new Color(0.4f, 0.36f, 0.32f));

            var post = new GameObject("Poste de commandement").transform;

            // Promontoire / falaise sous le poste, qui domine la vallée.
            Prim(PrimitiveType.Cube, "Falaise", post, new Vector3(0f, -20.5f, 15f), new Vector3(200f, 41f, 50f), rock);
            Prim(PrimitiveType.Plane, "Sol de la tente", post, new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 1f, 0.6f), ground);

            // Tente ouverte vers la vallée (+Z).
            var tent = new GameObject("Tente").transform;
            tent.SetParent(post, false);
            Prim(PrimitiveType.Cube, "Mur arrière", tent, new Vector3(0f, 1.5f, -3f), new Vector3(6f, 3f, 0.1f), canvas);
            Prim(PrimitiveType.Cube, "Mur gauche", tent, new Vector3(-3f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), canvas);
            Prim(PrimitiveType.Cube, "Mur droit", tent, new Vector3(3f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), canvas);
            Prim(PrimitiveType.Cube, "Toit", tent, new Vector3(0f, 3.05f, 0f), new Vector3(6.2f, 0.1f, 6.2f), canvas);

            // Table tactique (la maquette est posée dessus par BuildMiniValley).
            Prim(PrimitiveType.Cube, "Table tactique", post, TableCenter, new Vector3(1.4f, TableHeight, 1.4f), dark, keepCollider: false);

            // Pupitre latéral (batterie, vent : plus tard).
            Prim(PrimitiveType.Cube, "Pupitre", post, new Vector3(1.2f, 0.5f, 0.4f), new Vector3(0.5f, 1f, 0.5f), dark);

            // Support de l'écran du drone, derrière la table.
            Prim(PrimitiveType.Cube, "Cadre écran", post, new Vector3(0f, 1.85f, 2.13f), new Vector3(1.75f, 1.0f, 0.04f), dark, keepCollider: false);
            screenAnchor = new GameObject("Ancre écran").transform;
            screenAnchor.SetParent(post, false);
            screenAnchor.localPosition = new Vector3(0f, 1.85f, 2.1f);

            // Lumière d'appoint sous la tente.
            var lamp = new GameObject("Lampe de la tente").AddComponent<Light>();
            lamp.transform.SetParent(post, false);
            lamp.transform.localPosition = new Vector3(0f, 2.7f, 0.3f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.65f);
            lamp.range = 6f;
            lamp.intensity = 1.5f;

            // Gyrophare bleu du véhicule de commandement.
            var beacon = new GameObject("Gyrophare").AddComponent<Light>();
            beacon.transform.SetParent(post, false);
            beacon.transform.localPosition = new Vector3(2.5f, 2.7f, -2.4f);
            beacon.type = LightType.Point;
            beacon.color = new Color(0.2f, 0.4f, 1f);
            beacon.range = 8f;
            var blink = beacon.gameObject.AddComponent<LightFlicker>();
            blink.blink = true;
            blink.baseIntensity = 3f;
            blink.blinkPeriod = 0.8f;
        }

        static void BuildScreen(Transform anchor, RenderTexture rt)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Ecran_Drone.mat");
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, MatDir + "/Ecran_Drone.mat");
            }
            m.SetTexture("_BaseMap", rt);
            EditorUtility.SetDirty(m);

            Prim(PrimitiveType.Quad, "Écran du drone", anchor, Vector3.zero, new Vector3(1.6f, 0.9f, 1f), m, keepCollider: false);
        }

        // ---------------------------------------------------------------- Vallée
        static Transform BuildValley()
        {
            Material grass = Mat("Herbe_Seche", new Color(0.36f, 0.33f, 0.2f));
            Material rock = Mat("Roche", new Color(0.4f, 0.36f, 0.32f));
            Material road = Mat("Route", new Color(0.25f, 0.25f, 0.25f));
            Material tentMat = Mat("Tentes_Campement", new Color(0.9f, 0.5f, 0.1f));
            Material metal = Mat("Metal", new Color(0.55f, 0.55f, 0.58f));
            Material baseMat = Mat("Base_Drone", new Color(0.95f, 0.8f, 0.1f));
            Material trunk = Mat("Tronc", new Color(0.3f, 0.2f, 0.12f));
            Material leaves = Mat("Feuillage", new Color(0.16f, 0.3f, 0.12f));
            Material fire = Mat("Foyer", new Color(1f, 0.25f, 0.05f), new Color(4f, 0.9f, 0.1f));

            var valley = new GameObject("Valley").transform;
            valley.position = ValleyPosition;

            Prim(PrimitiveType.Plane, "Sol", valley, Vector3.zero, new Vector3(20f, 1f, 20f), grass);
            Prim(PrimitiveType.Cube, "Versant ouest", valley, new Vector3(-90f, 10f, 0f), new Vector3(40f, 40f, 200f), rock, euler: new Vector3(0f, 0f, -25f));
            Prim(PrimitiveType.Cube, "Versant est", valley, new Vector3(90f, 10f, 0f), new Vector3(40f, 40f, 200f), rock, euler: new Vector3(0f, 0f, 25f));

            Prim(PrimitiveType.Plane, "Route forestière", valley, new Vector3(30f, 0.05f, -20f), new Vector3(0.8f, 1f, 16f), road);

            var camp = new GameObject("Campement forestier").transform;
            camp.SetParent(valley, false);
            camp.localPosition = new Vector3(30f, 0f, 70f);
            Prim(PrimitiveType.Cube, "Tente 1", camp, new Vector3(-6f, 1.5f, 0f), new Vector3(4f, 3f, 4f), tentMat);
            Prim(PrimitiveType.Cube, "Tente 2", camp, new Vector3(0f, 1.5f, 5f), new Vector3(4f, 3f, 4f), tentMat);
            Prim(PrimitiveType.Cube, "Tente 3", camp, new Vector3(6f, 1.5f, 0f), new Vector3(4f, 3f, 4f), tentMat);
            Prim(PrimitiveType.Cylinder, "Citerne", camp, new Vector3(0f, 1.5f, -6f), new Vector3(2f, 1.5f, 2f), metal);

            Prim(PrimitiveType.Cylinder, "Base de ravitaillement", valley, new Vector3(-40f, 0.1f, -80f), new Vector3(8f, 0.1f, 8f), baseMat);

            Prim(PrimitiveType.Sphere, "Foyer d'incendie", valley, new Vector3(-20f, 1.5f, 40f), new Vector3(3f, 3f, 3f), fire);
            var glow = new GameObject("Lueur du foyer").AddComponent<Light>();
            glow.transform.SetParent(valley, false);
            glow.transform.localPosition = new Vector3(-20f, 4f, 40f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.4f, 0.1f);
            glow.range = 30f;
            var flicker = glow.gameObject.AddComponent<LightFlicker>();
            flicker.baseIntensity = 6f;
            flicker.amplitude = 3f;

            // Forêt : arbres en formes simples, sans collider (le rayon touche le sol).
            var forest = new GameObject("Forêt").transform;
            forest.SetParent(valley, false);
            Random.InitState(42);
            int placed = 0, tries = 0;
            while (placed < 70 && tries < 2000)
            {
                tries++;
                var p = new Vector3(Random.Range(-65f, 65f), 0f, Random.Range(-95f, 95f));
                if (Mathf.Abs(p.x - 30f) < 7f) continue;                              // route
                if (Vector3.Distance(p, new Vector3(30f, 0f, 70f)) < 16f) continue;    // campement
                if (Vector3.Distance(p, new Vector3(-40f, 0f, -80f)) < 10f) continue;  // base
                if (Vector3.Distance(p, new Vector3(-20f, 0f, 40f)) < 5f) continue;    // foyer

                var tree = new GameObject("Arbre " + (placed + 1)).transform;
                tree.SetParent(forest, false);
                tree.localPosition = p;
                float s = Random.Range(0.8f, 1.3f);
                tree.localScale = Vector3.one * s;
                Prim(PrimitiveType.Cylinder, "Tronc", tree, new Vector3(0f, 2f, 0f), new Vector3(0.6f, 2f, 0.6f), trunk, keepCollider: false);
                Prim(PrimitiveType.Sphere, "Feuillage", tree, new Vector3(0f, 5.5f, 0f), new Vector3(4f, 4f, 4f), leaves, keepCollider: false);
                placed++;
            }

            return valley;
        }

        // ---------------------------------------------------------------- Maquette
        static Transform BuildMiniValley(Transform valley)
        {
            GameObject mini = Object.Instantiate(valley.gameObject);
            mini.name = "MiniValley";
            mini.transform.position = new Vector3(TableCenter.x, TableHeight + 0.005f, TableCenter.z);
            mini.transform.rotation = valley.rotation;
            mini.transform.localScale = Vector3.one * MiniScale;

            // Pas de lumières sur la maquette.
            foreach (Light l in mini.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(l.gameObject);

            return mini.transform;
        }

        // ---------------------------------------------------------------- Unités (drone, camion)
        static DroneMover BuildUnits(Transform valley, Transform mini, RenderTexture rt)
        {
            Material droneMat = Mat("Drone", new Color(0.85f, 0.85f, 0.85f));
            Material dark = Mat("Mobilier_Sombre", new Color(0.12f, 0.12f, 0.13f));
            Material truckMat = Mat("Camion", new Color(0.8f, 0.05f, 0.05f));
            Material iconDrone = Mat("Icone_Drone", new Color(0.1f, 0.6f, 1f), new Color(0.2f, 1.2f, 3f));
            Material iconTruck = Mat("Icone_Camion", new Color(1f, 0.1f, 0.1f), new Color(2f, 0.1f, 0.1f));

            var units = new GameObject("Unités").transform;

            // Drone PyroScan-1, au-dessus de la base de ravitaillement.
            var drone = new GameObject("Drone PyroScan-1").transform;
            drone.SetParent(units, false);
            drone.position = valley.TransformPoint(new Vector3(-40f, 15f, -80f));
            Prim(PrimitiveType.Cube, "Corps", drone, Vector3.zero, new Vector3(1.2f, 0.2f, 1.2f), droneMat, keepCollider: false);
            Prim(PrimitiveType.Cylinder, "Hélice AV-G", drone, new Vector3(-0.6f, 0.12f, 0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            Prim(PrimitiveType.Cylinder, "Hélice AV-D", drone, new Vector3(0.6f, 0.12f, 0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            Prim(PrimitiveType.Cylinder, "Hélice AR-G", drone, new Vector3(-0.6f, 0.12f, -0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            Prim(PrimitiveType.Cylinder, "Hélice AR-D", drone, new Vector3(0.6f, 0.12f, -0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            DroneMover mover = drone.gameObject.AddComponent<DroneMover>();

            // Caméra embarquée : filme vers l'avant et le bas, rend dans l'écran du poste.
            var camGo = new GameObject("Caméra drone");
            camGo.transform.SetParent(drone, false);
            camGo.transform.localPosition = new Vector3(0f, -0.2f, 0.3f);
            camGo.transform.localEulerAngles = new Vector3(50f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.farClipPlane = 400f;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;

            // Camion de pompiers, en bas de la route.
            var truck = Prim(PrimitiveType.Cube, "Camion de pompiers", units, Vector3.zero, new Vector3(2.5f, 2.5f, 6f), truckMat, keepCollider: false);
            truck.transform.position = valley.TransformPoint(new Vector3(30f, 1.25f, -90f));

            // Icônes sur la maquette.
            var droneIcon = Prim(PrimitiveType.Sphere, "Icône drone (maquette)", units, Vector3.zero, Vector3.one * 0.04f, iconDrone, keepCollider: false);
            var di = droneIcon.AddComponent<MiniIcon>();
            di.target = drone; di.valley = valley; di.miniValley = mini;

            var truckIcon = Prim(PrimitiveType.Cube, "Icône camion (maquette)", units, Vector3.zero, new Vector3(0.02f, 0.02f, 0.04f), iconTruck, keepCollider: false);
            var ti = truckIcon.AddComponent<MiniIcon>();
            ti.target = truck.transform; ti.valley = valley; ti.miniValley = mini;

            return mover;
        }

        // ---------------------------------------------------------------- XR
        internal static bool SetupXR()
        {
            GameObject rigPrefab = FindPrefab("XR Interaction Setup") ?? FindPrefab("XR Origin (XR Rig)");
            if (rigPrefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (Camera c in rig.GetComponentsInChildren<Camera>(true))
                {
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = FogColor;
                    c.farClipPlane = 400f;
                }
            }
            else
            {
                // Secours sans rig XR : simple caméra à hauteur d'yeux.
                var cam = new GameObject("Main Camera (sans XR)").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0f, 1.6f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FogColor;
            }

            GameObject sim = FindPrefab("XR Interaction Simulator") ?? FindPrefab("XR Device Simulator");
            if (sim != null)
                PrefabUtility.InstantiatePrefab(sim);
            else
                Debug.LogWarning("[PyroScan] Simulateur introuvable : importe le sample 'XR Interaction Simulator' (ou 'XR Device Simulator').");

            return rigPrefab != null;
        }

        internal static GameObject FindPrefab(string exactName)
        {
            string path = AssetDatabase.FindAssets(exactName + " t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == exactName)
                .OrderBy(p => p.Contains("Starter Assets") ? 0 : 1)
                .FirstOrDefault();
            return path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }

        // ---------------------------------------------------------------- Outils
        internal static RenderTexture GetOrCreateRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt != null) return rt;
            rt = new RenderTexture(1024, 576, 24) { name = "DroneCam" };
            AssetDatabase.CreateAsset(rt, RtPath);
            return rt;
        }

        internal static GameObject GetOrCreateMarkerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath);
            if (existing != null) return existing;

            Material m = Mat("Marqueur_Waypoint", new Color(0f, 0.8f, 1f), new Color(0f, 1.5f, 2.5f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WaypointMarker";
            go.transform.localScale = Vector3.one * 0.03f;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, MarkerPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        internal static Material Mat(string name, Color color, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool keepCollider = true, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler ?? Vector3.zero;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
