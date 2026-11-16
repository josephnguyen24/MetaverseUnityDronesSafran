using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Menu PyroScan > Étape 1 - Construire la mission (terrain 1 km).
    /// Crée la scène Mission_MassifSud : terrain généré, lac, hameau, campement, base drone,
    /// poste de commandement avec une carte satellite provisoire sur la table (envoi du drone à la main droite).
    /// La scène Rendu1_Poste n'est pas modifiée.
    /// </summary>
    public static class MissionBuilder
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/Mission_MassifSud.unity";
        const string MapDir = "Assets/_Project/Maps";
        public const string SatellitePath = MapDir + "/Satellite_MassifSud.png";

        [MenuItem("PyroScan/Construire la mission (étapes 1-7)", priority = 2)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                SceneBuilder.EnsureFolder(SceneDir);
                SceneBuilder.EnsureFolder(MapDir);
                SceneBuilder.EnsureFolder("Assets/_Project/Materials");
                SceneBuilder.EnsureFolder("Assets/_Project/Prefabs");

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SceneBuilder.SetupAmbiance();
                RenderSettings.fogStartDistance = 150f;
                RenderSettings.fogEndDistance = 900f;

                TerrainGenerator.Layout layout = TerrainGenerator.CreateLayout();
                Terrain terrain = TerrainGenerator.Generate(layout);
                MapFrame frame = terrain.GetComponent<MapFrame>();

                EditorUtility.DisplayProgressBar("PyroScan", "Hameau, campement, lac...", 0.75f);
                var places = new GameObject("Lieux de la mission").transform;
                BuildLake(places, layout);
                BuildHamlet(places, layout, terrain);
                BuildCamp(places, layout, terrain);
                BuildDronePad(places, layout, terrain);
                Transform foyer = BuildFoyer(places, layout, terrain);

                EditorUtility.DisplayProgressBar("PyroScan", "Vue satellite...", 0.85f);
                Texture2D satellite = TerrainGenerator.CaptureSatellite(terrain, SatellitePath);

                EditorUtility.DisplayProgressBar("PyroScan", "Poste de commandement...", 0.9f);
                FlatMapSurface tableMap = BuildCommandPost(satellite, frame);
                FlatMapSurface wallMap = WallMapBuilder.Build(GameObject.Find("Poste de commandement").transform, satellite, frame); // étape 2

                RenderTexture rt = SceneBuilder.GetOrCreateRenderTexture();
                DroneMover drone = BuildUnits(layout, terrain, frame, rt, tableMap, out Transform truck);
                DroneMinimap minimap = MinimapBuilder.Build(GameObject.Find("Poste de commandement").transform, drone, truck, foyer, frame, satellite); // étape 3

                bool rig = SceneBuilder.SetupXR();
                foreach (var o in Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None))
                    if (o.GetComponent<EditorRigFix>() == null) o.gameObject.AddComponent<EditorRigFix>();
                MarkerService beacons = MarkerBuilder.Build(wallMap, minimap, drone.GetComponentInChildren<Camera>()); // étapes 4 + 6a (après le rig XR : besoin de la manette gauche)
                FireBuilder.Build(layout, terrain, frame, foyer, wallMap, minimap); // étape 5
                AircraftDispatcher air = AircraftBuilder.Build(layout, beacons); // étape 6b
                DetectionBuilder.Build(drone.GetComponentInChildren<Camera>(), air); // étape 7
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (c.targetTexture == null) c.farClipPlane = 1500f;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                SceneBuilder.AddSceneToBuild(ScenePath);
                AssetDatabase.SaveAssets();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("PyroScan",
                    "Mission créée : " + ScenePath + "\n\n" +
                    (rig ? "Lance Play :\n- le feu démarre au sud-ouest et avance avec le vent\n- main droite + grip sur la minimap : envoyer le drone\n- joystick gauche (PC : PgPréc/PgSuiv/Début/Fin) : caméra du drone\n- gâchette gauche (ou B) : poser une balise\n- gâchette gauche sur une carte : enlever une balise\n- boutons du pupitre (grip, ou F1/F2) : Canadair / retardant\n- l'IA du drone encadre les feux sur son écran\n- l'état de la mission s'affiche sur le pupitre"
                         : "Rig XR introuvable : importe les Starter Assets puis relance ce menu."), "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- Lieux
        static float GroundY(Terrain t, Vector2 p) =>
            t.SampleHeight(new Vector3(p.x, 0f, p.y)) + t.transform.position.y;

        static void BuildLake(Transform parent, TerrainGenerator.Layout L)
        {
            Material water = SceneBuilder.Mat("Eau_Lac", new Color(0.12f, 0.25f, 0.32f));
            water.SetFloat("_Smoothness", 0.85f);
            float size = L.lakeRadius * 2.6f / 10f; // un Plane mesure 10 m
            SceneBuilder.Prim(PrimitiveType.Plane, "Lac", parent,
                new Vector3(L.lakeCenter.x, L.WaterY, L.lakeCenter.y), new Vector3(size, 1f, size), water); // collision : une balise visée sur le lac se pose à la surface
        }

        static void BuildHamlet(Transform parent, TerrainGenerator.Layout L, Terrain t)
        {
            Material walls = SceneBuilder.Mat("Maison_Murs", new Color(0.82f, 0.76f, 0.64f));
            Material roof = SceneBuilder.Mat("Maison_Toit", new Color(0.55f, 0.22f, 0.15f));
            var hamlet = new GameObject("Hameau").transform;
            hamlet.SetParent(parent, false);

            Vector2[] offsets =
            {
                new Vector2(-30f, -20f), new Vector2(-12f, -32f), new Vector2(10f, -25f), new Vector2(28f, -8f),
                new Vector2(-28f, 12f), new Vector2(-5f, 22f), new Vector2(22f, 24f)
            };
            float[] yaws = { 15f, 80f, -10f, 100f, 40f, -70f, 5f };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 p = L.hamlet + offsets[i];
                var house = new GameObject("Maison " + (i + 1)).transform;
                house.SetParent(hamlet, false);
                house.position = new Vector3(p.x, GroundY(t, p), p.y);
                house.rotation = Quaternion.Euler(0f, yaws[i], 0f);
                SceneBuilder.Prim(PrimitiveType.Cube, "Murs", house, new Vector3(0f, 2.5f, 0f), new Vector3(8f, 5f, 6f), walls);
                SceneBuilder.Prim(PrimitiveType.Cube, "Toit", house, new Vector3(0f, 5f, 0f), new Vector3(5.7f, 5.7f, 6.6f), roof,
                    keepCollider: false, euler: new Vector3(0f, 0f, 45f));
            }
        }

        static void BuildCamp(Transform parent, TerrainGenerator.Layout L, Terrain t)
        {
            Material tents = SceneBuilder.Mat("Tentes_Campement", new Color(0.9f, 0.5f, 0.1f));
            Material metal = SceneBuilder.Mat("Metal", new Color(0.55f, 0.55f, 0.58f));
            var camp = new GameObject("Campement forestier").transform;
            camp.SetParent(parent, false);
            camp.position = new Vector3(L.camp.x, GroundY(t, L.camp), L.camp.y);
            SceneBuilder.Prim(PrimitiveType.Cube, "Tente 1", camp, new Vector3(-7f, 1.5f, 0f), new Vector3(4f, 3f, 4f), tents);
            SceneBuilder.Prim(PrimitiveType.Cube, "Tente 2", camp, new Vector3(0f, 1.5f, 6f), new Vector3(4f, 3f, 4f), tents);
            SceneBuilder.Prim(PrimitiveType.Cube, "Tente 3", camp, new Vector3(7f, 1.5f, 0f), new Vector3(4f, 3f, 4f), tents);
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Citerne", camp, new Vector3(0f, 1.5f, -7f), new Vector3(2f, 1.5f, 2f), metal);
        }

        static void BuildDronePad(Transform parent, TerrainGenerator.Layout L, Terrain t)
        {
            Material pad = SceneBuilder.Mat("Base_Drone", new Color(0.95f, 0.8f, 0.1f));
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Base de ravitaillement", parent,
                new Vector3(L.dronePad.x, GroundY(t, L.dronePad) + 0.05f, L.dronePad.y), new Vector3(4f, 0.05f, 4f), pad);
        }

        static Transform BuildFoyer(Transform parent, TerrainGenerator.Layout L, Terrain t)
        {
            // Point de départ du feu (invisible) : la grille du feu s'allume ici au lancement (étape 5).
            var go = new GameObject("Point de départ du feu");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(L.foyer.x, GroundY(t, L.foyer), L.foyer.y);
            return go.transform;
        }

        // ---------------------------------------------------------------- Poste de commandement
        static FlatMapSurface BuildCommandPost(Texture2D satellite, MapFrame frame)
        {
            Material ground = SceneBuilder.Mat("Sol_Poste", new Color(0.18f, 0.17f, 0.16f));
            Material canvas = SceneBuilder.Mat("Toile_Tente", new Color(0.55f, 0.12f, 0.1f));
            Material dark = SceneBuilder.Mat("Mobilier_Sombre", new Color(0.12f, 0.12f, 0.13f));

            var post = new GameObject("Poste de commandement").transform;
            SceneBuilder.Prim(PrimitiveType.Plane, "Sol de la tente", post, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 1f, 0.6f), ground);

            var tent = new GameObject("Tente").transform;
            tent.SetParent(post, false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Mur arrière", tent, new Vector3(0f, 1.5f, -3f), new Vector3(6f, 3f, 0.1f), canvas);
            SceneBuilder.Prim(PrimitiveType.Cube, "Mur gauche", tent, new Vector3(-3f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), canvas);
            SceneBuilder.Prim(PrimitiveType.Cube, "Mur droit", tent, new Vector3(3f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), canvas);
            SceneBuilder.Prim(PrimitiveType.Cube, "Toit", tent, new Vector3(0f, 3.05f, 0f), new Vector3(6.2f, 0.1f, 6.2f), canvas);

            SceneBuilder.Prim(PrimitiveType.Cube, "Table tactique", post, new Vector3(0f, 0.45f, 1f), new Vector3(1.4f, 0.9f, 1.4f), dark, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Pupitre", post, new Vector3(1.2f, 0.5f, 0.4f), new Vector3(0.5f, 1f, 0.5f), dark);


            // Écran du drone.
            SceneBuilder.Prim(PrimitiveType.Cube, "Cadre écran", post, new Vector3(0f, 1.85f, 2.13f), new Vector3(1.75f, 1.0f, 0.04f), dark, keepCollider: false);
            var screenMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Ecran_Drone.mat");
            if (screenMat == null)
            {
                screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(screenMat, "Assets/_Project/Materials/Ecran_Drone.mat");
            }
            screenMat.SetTexture("_BaseMap", SceneBuilder.GetOrCreateRenderTexture());
            SceneBuilder.Prim(PrimitiveType.Quad, "Écran du drone", post, new Vector3(0f, 1.85f, 2.1f), new Vector3(1.6f, 0.9f, 1f), screenMat, keepCollider: false);

            var lamp = new GameObject("Lampe de la tente").AddComponent<Light>();
            lamp.transform.SetParent(post, false);
            lamp.transform.localPosition = new Vector3(0f, 2.7f, 0.3f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.65f);
            lamp.range = 6f;
            lamp.intensity = 1.5f;

            var beacon = new GameObject("Gyrophare").AddComponent<Light>();
            beacon.transform.SetParent(post, false);
            beacon.transform.localPosition = new Vector3(2.5f, 2.7f, -2.4f);
            beacon.type = LightType.Point;
            beacon.color = new Color(0.2f, 0.4f, 1f);
            beacon.range = 8f;
            var blink = beacon.gameObject.AddComponent<LightFlicker>();
            blink.blink = true;
            blink.baseIntensity = 3f;

            return null; // carte de la table remplacée par la minimap (étape 3)
        }

        // ---------------------------------------------------------------- Unités
        static DroneMover BuildUnits(TerrainGenerator.Layout L, Terrain t, MapFrame frame, RenderTexture rt,
            FlatMapSurface tableMap, out Transform truck)
        {
            Material droneMat = SceneBuilder.Mat("Drone", new Color(0.85f, 0.85f, 0.85f));
            Material dark = SceneBuilder.Mat("Mobilier_Sombre", new Color(0.12f, 0.12f, 0.13f));
            Material truckMat = SceneBuilder.Mat("Camion", new Color(0.8f, 0.05f, 0.05f));

            var units = new GameObject("Unités").transform;

            var drone = new GameObject("Drone PyroScan-1").transform;
            drone.SetParent(units, false);
            drone.position = new Vector3(L.dronePad.x, GroundY(t, L.dronePad) + 0.4f, L.dronePad.y);
            drone.rotation = Quaternion.identity; // face au nord, vers la vallée
            SceneBuilder.Prim(PrimitiveType.Cube, "Corps", drone, Vector3.zero, new Vector3(1.2f, 0.2f, 1.2f), droneMat, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Hélice AV-G", drone, new Vector3(-0.6f, 0.12f, 0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Hélice AV-D", drone, new Vector3(0.6f, 0.12f, 0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Hélice AR-G", drone, new Vector3(-0.6f, 0.12f, -0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Hélice AR-D", drone, new Vector3(0.6f, 0.12f, -0.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, keepCollider: false);

            DroneMover mover = drone.gameObject.AddComponent<DroneMover>();
            mover.terrainFrame = frame;
            mover.speed = 20f;
            mover.altitude = 40f;
            mover.climbSpeed = 12f;

            var camGo = new GameObject("Caméra drone");
            camGo.transform.SetParent(drone, false);
            camGo.transform.localPosition = new Vector3(0f, -0.2f, 0.3f);
            camGo.transform.localEulerAngles = new Vector3(45f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.farClipPlane = 700f;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SceneBuilder.FogColor;

            GameObject truckGo = SceneBuilder.Prim(PrimitiveType.Cube, "Camion de pompiers", units, Vector3.zero,
                new Vector3(2.5f, 2.5f, 6f), truckMat, keepCollider: false);
            truckGo.transform.position = new Vector3(L.truckStart.x, GroundY(t, L.truckStart) + 1.25f, L.truckStart.y);
            truck = truckGo.transform;
            return mover;
        }

        static void AddIcon(string name, MapSurface surface, Transform target, PrimitiveType shape, Vector3 scale, Material mat)
        {
            GameObject icon = GameObject.CreatePrimitive(shape);
            icon.name = name + " (carte)";
            icon.transform.localScale = scale;
            icon.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(icon.GetComponent<Collider>());
            var mi = icon.AddComponent<MapIcon>();
            mi.target = target;
            mi.surface = surface;
            if (surface.TryGetSurfacePoint(target.position, out Vector3 p)) icon.transform.position = p;
        }
    }
}
