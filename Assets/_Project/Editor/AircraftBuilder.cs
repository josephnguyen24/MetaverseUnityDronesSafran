using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Étape 6b : Canadair + avion de retardant, deux boutons sur le pupitre et un panneau radio.
    /// Les avions partent vers la dernière balise posée avec le drone.
    /// </summary>
    public static class AircraftBuilder
    {
        static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

        public static AircraftDispatcher Build(TerrainGenerator.Layout L, MarkerService markers)
        {
            var root = new GameObject("Moyens aériens");
            var dispatcher = root.AddComponent<AircraftDispatcher>();
            dispatcher.markers = markers;
            dispatcher.grid = Object.FindFirstObjectByType<FireGrid>();
            dispatcher.wind = Object.FindFirstObjectByType<WindSystem>();
            dispatcher.lakeCenter = new Vector3(L.lakeCenter.x, L.WaterY, L.lakeCenter.y);
            dispatcher.lakeRadius = L.lakeRadius;
            dispatcher.lakeWaterY = L.WaterY;

            Material smoke = FireBuilder.SmokeMaterial();
            dispatcher.canadair = BuildCanadair(root.transform, smoke);
            dispatcher.retardantPlane = BuildRetardantPlane(root.transform, smoke);

            BuildConsole(dispatcher);
            return dispatcher;
        }

        // ---------------------------------------------------------------- Avions
        static Aircraft BuildCanadair(Transform parent, Material smoke)
        {
            var go = new GameObject("Canadair");
            go.transform.SetParent(parent, false);
            Material yellow = SceneBuilder.Mat("Avion_Jaune", new Color(1f, 0.78f, 0.05f));
            Material red = SceneBuilder.Mat("Avion_Rouge", new Color(0.8f, 0.08f, 0.06f));
            Transform t = go.transform;
            SceneBuilder.Prim(PrimitiveType.Cube, "Fuselage", t, Vector3.zero, new Vector3(2.4f, 2.6f, 19f), yellow, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Nez", t, new Vector3(0f, -0.2f, 10f), new Vector3(2.2f, 2.0f, 1.6f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Aile haute", t, new Vector3(0f, 1.6f, 1.5f), new Vector3(29f, 0.4f, 3.4f), yellow, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Bande rouge", t, new Vector3(0f, 1.85f, 1.5f), new Vector3(29.2f, 0.1f, 1f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Dérive", t, new Vector3(0f, 2.8f, -8.5f), new Vector3(0.4f, 4.5f, 3f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Empennage", t, new Vector3(0f, 4.6f, -8.8f), new Vector3(9f, 0.3f, 2f), yellow, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Flotteur G", t, new Vector3(-11f, 0.4f, 1.5f), new Vector3(0.7f, 0.7f, 2.5f), yellow, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Flotteur D", t, new Vector3(11f, 0.4f, 1.5f), new Vector3(0.7f, 0.7f, 2.5f), yellow, keepCollider: false);
            var a = go.AddComponent<Aircraft>();
            a.speed = 55f;
            a.turnRate = 60f;
            a.dropFx = BuildDrop(t, "Largage eau", smoke, new Color(0.85f, 0.93f, 1f, 0.75f));
            return a;
        }

        static Aircraft BuildRetardantPlane(Transform parent, Material smoke)
        {
            var go = new GameObject("Avion de retardant");
            go.transform.SetParent(parent, false);
            Material white = SceneBuilder.Mat("Avion_Blanc", new Color(0.92f, 0.92f, 0.9f));
            Material red = SceneBuilder.Mat("Avion_Rouge", new Color(0.8f, 0.08f, 0.06f));
            Transform t = go.transform;
            SceneBuilder.Prim(PrimitiveType.Cube, "Fuselage", t, Vector3.zero, new Vector3(3f, 3f, 25f), white, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Nez", t, new Vector3(0f, 0f, 13f), new Vector3(2.4f, 2.4f, 2f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Aile basse", t, new Vector3(0f, -1f, 1f), new Vector3(34f, 0.5f, 4.5f), white, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Bouts d'aile", t, new Vector3(0f, -0.95f, 1f), new Vector3(34.2f, 0.52f, 1.2f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Dérive", t, new Vector3(0f, 3f, -11f), new Vector3(0.4f, 5f, 3.5f), red, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Empennage", t, new Vector3(0f, 1.2f, -11.5f), new Vector3(11f, 0.3f, 2.4f), white, keepCollider: false);
            SceneBuilder.Prim(PrimitiveType.Cube, "Soute", t, new Vector3(0f, -1.8f, 0f), new Vector3(1.8f, 0.6f, 8f), red, keepCollider: false);
            var a = go.AddComponent<Aircraft>();
            a.speed = 70f;
            a.turnRate = 45f;
            a.dropFx = BuildDrop(t, "Largage retardant", smoke, new Color(1f, 0.25f, 0.35f, 0.85f));
            return a;
        }

        static ParticleSystem BuildDrop(Transform plane, string name, Material mat, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(plane, false);
            go.transform.localPosition = new Vector3(0f, -2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            var em = ps.emission;
            em.rateOverTime = 160f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2f, 0.5f, 6f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        // ---------------------------------------------------------------- Pupitre
        static void BuildConsole(AircraftDispatcher dispatcher)
        {
            Transform post = GameObject.Find("Poste de commandement")?.transform;

            // Plateau incliné sur le pupitre, tourné vers le joueur.
            var pos = new Vector3(1.15f, 1.14f, 0.35f);
            var plate = new GameObject("Pupitre avions").transform;
            plate.SetParent(post, true);
            plate.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - Eye));
            Material dark = SceneBuilder.Mat("Panneau_Etat", new Color(0.04f, 0.05f, 0.06f));
            SceneBuilder.Prim(PrimitiveType.Cube, "Plateau", plate, new Vector3(0f, 0f, 0.012f), new Vector3(0.5f, 0.3f, 0.02f), dark, keepCollider: false);

            dispatcher.canadairStatus = BuildButton(plate, dispatcher, AircraftDispatcher.Kind.Canadair, -0.12f, "CANADAIR",
                SceneBuilder.Mat("Bouton_Jaune", new Color(1f, 0.75f, 0.05f), new Color(0.6f, 0.4f, 0f)),
                new[] { "<Keyboard>/f1", "<Keyboard>/numpad7" });
            dispatcher.retardantStatus = BuildButton(plate, dispatcher, AircraftDispatcher.Kind.Retardant, 0.12f, "RETARDANT",
                SceneBuilder.Mat("Bouton_Rouge", new Color(0.9f, 0.1f, 0.1f), new Color(0.6f, 0.02f, 0.02f)),
                new[] { "<Keyboard>/f2", "<Keyboard>/numpad9" });

            // Panneau radio, à droite du panneau d'état.
            var rpos = new Vector3(1.55f, 1.5f, -0.2f);
            var board = new GameObject("Panneau radio").transform;
            board.SetParent(post, true);
            board.SetPositionAndRotation(rpos, Quaternion.LookRotation(rpos - Eye));
            SceneBuilder.Prim(PrimitiveType.Cube, "Fond", board, new Vector3(0f, 0f, 0.01f), new Vector3(0.62f, 0.42f, 0.01f), dark, keepCollider: false);
            dispatcher.radio = Text(board, "Texte", new Vector3(0f, 0f, 0f), new Vector2(0.56f, 0.36f), "RADIO", 0.05f, 0.3f,
                TextAlignmentOptions.TopLeft, autoSize: true);
        }

        static TextMeshPro BuildButton(Transform plate, AircraftDispatcher dispatcher, AircraftDispatcher.Kind kind, float x,
            string label, Material mat, string[] keys)
        {
            var root = new GameObject($"Bouton {label.ToLower()}");
            root.transform.SetParent(plate, false);
            root.transform.localPosition = new Vector3(x, 0.01f, 0f);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.12f, 0.12f, 0.06f);
            box.center = new Vector3(0f, 0f, -0.02f);
            root.AddComponent<XRSimpleInteractable>();

            Material ring = SceneBuilder.Mat("Mobilier_Sombre", new Color(0.12f, 0.12f, 0.13f));
            SceneBuilder.Prim(PrimitiveType.Cylinder, "Socle", root.transform, new Vector3(0f, 0f, -0.002f), new Vector3(0.11f, 0.006f, 0.11f), ring,
                keepCollider: false, euler: new Vector3(90f, 0f, 0f));
            GameObject cap = SceneBuilder.Prim(PrimitiveType.Cylinder, "Champignon", root.transform, new Vector3(0f, 0f, -0.016f),
                new Vector3(0.085f, 0.012f, 0.085f), mat, keepCollider: false, euler: new Vector3(90f, 0f, 0f));

            var button = root.AddComponent<ConsoleButton>();
            button.dispatcher = dispatcher;
            button.kind = kind;
            button.keyboardBindings = keys;
            button.cap = cap.transform;

            Text(plate, $"Titre {label}", new Vector3(x, 0.11f, -0.003f), new Vector2(0.22f, 0.04f), label, 0.2f, 0.2f,
                TextAlignmentOptions.Center, autoSize: false);
            return Text(plate, $"État {label}", new Vector3(x, -0.095f, -0.003f), new Vector2(0.24f, 0.09f), "PRÊT", 0.16f, 0.16f,
                TextAlignmentOptions.Top, autoSize: false);
        }

        static TextMeshPro Text(Transform parent, string name, Vector3 localPos, Vector2 size, string text, float min, float max,
            TextAlignmentOptions align, bool autoSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.enableAutoSizing = autoSize;
            tmp.fontSizeMin = min;
            tmp.fontSizeMax = max;
            tmp.fontSize = max;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }
    }
}
