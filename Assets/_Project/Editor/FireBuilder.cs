using TMPro;
using UnityEditor;
using UnityEngine;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Étape 5 : le feu. Vent, grille de propagation (combustible tiré de la végétation), flammes,
    /// sol brûlé, fumée, calques "carte du feu" sur la carte murale et la minimap, panneau d'état sur le pupitre.
    /// </summary>
    public static class FireBuilder
    {
        public const int GridSize = 100; // cases de 10 m

        public static void Build(TerrainGenerator.Layout layout, Terrain terrain, MapFrame frame, Transform ignition,
            FlatMapSurface wallMap, DroneMinimap minimap)
        {
            var root = new GameObject("Feu et météo");

            var wind = root.AddComponent<WindSystem>();
            wind.baseDirection = 40f;   // souffle vers le nord-est : pousse le feu vers le hameau
            wind.baseStrength = 0.55f;

            var grid = root.AddComponent<FireGrid>();
            grid.frame = frame;
            grid.wind = wind;
            grid.size = GridSize;
            grid.fuel = TerrainGenerator.ComputeFuel(layout, terrain, GridSize);
            grid.ignitionPoints = new[] { ignition };
            grid.spreadSpeed = 0.4f; // plus lent : plusieurs minutes avant d'atteindre le campement

            // Calques "carte du feu" : carte murale (quad devant la carte) et minimap (même maillage, juste au-dessus).
            Material overlay = OverlayMaterial();
            GameObject wallOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wallOverlay.name = "Calque feu (carte murale)";
            Object.DestroyImmediate(wallOverlay.GetComponent<Collider>());
            wallOverlay.transform.SetParent(wallMap.transform, false);
            wallOverlay.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            wallOverlay.GetComponent<Renderer>().sharedMaterial = overlay;

            var miniOverlay = new GameObject("Calque feu (minimap)");
            miniOverlay.transform.SetParent(minimap.transform, false);
            miniOverlay.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            minimap.overlayFilter = miniOverlay.AddComponent<MeshFilter>();
            miniOverlay.AddComponent<MeshRenderer>().sharedMaterial = overlay;

            grid.overlayRenderers = new Renderer[] { wallOverlay.GetComponent<Renderer>(), miniOverlay.GetComponent<Renderer>() };

            // Flammes, sol brûlé, fumée.
            var visuals = root.AddComponent<FireVisuals>();
            visuals.grid = grid;
            visuals.flameMaterial = UnlitColor("Flamme", new Color(1f, 0.45f, 0.05f));
            var burnt = SceneBuilder.Mat("Sol_Brule", new Color(0.07f, 0.06f, 0.05f));
            burnt.enableInstancing = true;
            visuals.burntMaterial = burnt;
            visuals.smoke = BuildSmoke(root.transform);

            // Panneau d'état sur le pupitre + règles de victoire/défaite.
            var status = root.AddComponent<MissionStatus>();
            status.grid = grid;
            status.wind = wind;
            status.display = BuildPanel();
            status.protectedZones = new[]
            {
                new MissionStatus.Zone { name = "le hameau", center = Ground(terrain, layout.hamlet), radius = 60f },
                new MissionStatus.Zone { name = "le campement", center = Ground(terrain, layout.camp), radius = 35f },
            };
        }

        static Vector3 Ground(Terrain t, Vector2 p) =>
            new Vector3(p.x, t.SampleHeight(new Vector3(p.x, 0f, p.y)) + t.transform.position.y, p.y);

        static TextMeshPro BuildPanel()
        {
            var pos = new Vector3(1.25f, 1.5f, 0.5f);
            Quaternion rot = Quaternion.LookRotation(pos - new Vector3(0f, 1.6f, 0f));
            var panel = new GameObject("Panneau d'état (pupitre)").transform;
            panel.SetPositionAndRotation(pos, rot);

            SceneBuilder.Prim(PrimitiveType.Cube, "Fond", panel, new Vector3(0f, 0f, 0.01f), new Vector3(0.62f, 0.4f, 0.01f),
                SceneBuilder.Mat("Panneau_Etat", new Color(0.04f, 0.05f, 0.06f)), keepCollider: false);

            var go = new GameObject("Texte");
            go.transform.SetParent(panel, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "MISSION EN COURS";
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 0.4f;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(0.56f, 0.34f);
            return tmp;
        }

        static ParticleSystem BuildSmoke(Transform parent)
        {
            var go = new GameObject("Fumée");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 5f;
            main.startSize = new ParticleSystem.MinMaxCurve(25f, 55f);
            main.startColor = new Color(0.35f, 0.33f, 0.31f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 20f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();
            return ps;
        }

        internal static Material SmokeMaterial()
        {
            const string path = "Assets/_Project/Materials/Fumee.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            MakeTransparent(m);
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material OverlayMaterial()
        {
            const string path = "Assets/_Project/Materials/Calque_Feu.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            MakeTransparent(m);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitColor(string name, Color c)
        {
            string path = "Assets/_Project/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
