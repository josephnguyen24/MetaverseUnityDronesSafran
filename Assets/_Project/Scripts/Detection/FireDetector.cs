using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Étape 7 : « IA » de détection de feu sur la caméra du drone (simulée).
    /// Toutes les 0,3 s, on cherche les cases en feu visibles dans le champ de la caméra (dans le cadre, à portée, non cachées
    /// par le relief), on les regroupe en foyers et on calcule une confiance selon la taille apparente du foyer.
    /// Résultat affiché sur l'écran du drone comme un détecteur d'objets : cadres orange + étiquette « FEU 92 % »,
    /// bandeau d'alerte qui clignote, et message radio à la première détection d'un nouveau foyer.
    /// </summary>
    public class FireDetector : MonoBehaviour
    {
        public Camera droneCamera;
        public FireGrid grid;
        public AircraftDispatcher radio;

        [Header("Écran du drone (repère local : x -0.8..0.8, y -0.45..0.45)")]
        public Transform overlayRoot;
        public Vector2 screenSize = new Vector2(1.6f, 0.9f);
        public Material boxMaterial;
        public TextMeshPro banner;
        public Renderer[] alertFrame;

        [Header("Réglages")]
        public float interval = 0.3f;
        public float maxRange = 450f;
        [Tooltip("Confiance minimale pour afficher un cadre")]
        public float showThreshold = 0.55f;
        [Tooltip("Confiance minimale pour annoncer un foyer à la radio")]
        public float announceThreshold = 0.75f;
        public float announceSeparation = 120f;
        public int maxBoxes = 6;
        public int maxCellsChecked = 500;

        struct Detection { public Rect view; public float confidence; public Vector3 world; public float distance; }

        readonly List<Detection> detections = new List<Detection>();
        readonly List<Vector3> announced = new List<Vector3>();
        readonly List<LineRenderer> boxes = new List<LineRenderer>();
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly Dictionary<int, Vector3> visible = new Dictionary<int, Vector3>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly Queue<int> queue = new Queue<int>();
        float timer, blink;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= interval) { timer = 0f; Detect(); Draw(); }

            // Bandeau et cadre d'alerte qui clignotent tant qu'un feu est détecté.
            bool alert = detections.Count > 0;
            blink += Time.deltaTime;
            bool on = alert && Mathf.Repeat(blink, 0.8f) < 0.5f;
            if (alertFrame != null) foreach (Renderer r in alertFrame) if (r != null) r.enabled = on;
            if (banner != null)
            {
                if (alert)
                {
                    float best = 0f; foreach (var d in detections) best = Mathf.Max(best, d.confidence);
                    banner.text = $"<color=#FF4A2A>{(on ? "!! " : "   ")}FEU DÉTECTÉ</color>  <size=70%>{detections.Count} foyer{(detections.Count > 1 ? "s" : "")} · conf. {best * 100f:0} %</size>";
                }
                else banner.text = "<color=#5CFF6A><size=70%>IA DÉTECTION FEU : ACTIVE · aucun feu dans le champ</size></color>";
            }
        }

        void Detect()
        {
            detections.Clear();
            if (droneCamera == null || grid == null || grid.Burning == null) return;

            // 1. Cases en feu visibles par la caméra.
            visible.Clear();
            Vector3 camPos = droneCamera.transform.position;
            IReadOnlyList<int> burning = grid.Burning;
            int step = Mathf.Max(1, burning.Count / maxCellsChecked);
            for (int i = 0; i < burning.Count; i += step)
            {
                int c = burning[i];
                Vector3 p = grid.CellCenter(c) + Vector3.up * 3f;
                Vector3 v = droneCamera.WorldToViewportPoint(p);
                if (v.z <= 0f || v.z > maxRange || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                Vector3 dir = p - camPos;
                float dist = dir.magnitude;
                if (Physics.Raycast(camPos, dir / dist, dist - 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue; // caché par le relief
                visible[c] = p;
            }
            if (visible.Count == 0) return;

            // 2. Regroupement en foyers (cases voisines, avec un trou d'une case toléré).
            seen.Clear();
            int size = grid.size;
            foreach (int start in visible.Keys)
            {
                if (seen.Contains(start)) continue;
                seen.Add(start);
                queue.Enqueue(start);
                float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f, sumDist = 0f;
                Vector3 sum = Vector3.zero;
                int n = 0;
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    Vector3 p = visible[c];
                    Vector3 v = droneCamera.WorldToViewportPoint(p);
                    float pad = 6f / Mathf.Max(v.z, 1f) / (2f * Mathf.Tan(droneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
                    minX = Mathf.Min(minX, v.x - pad / droneCamera.aspect); maxX = Mathf.Max(maxX, v.x + pad / droneCamera.aspect);
                    minY = Mathf.Min(minY, v.y - pad); maxY = Mathf.Max(maxY, v.y + pad * 2.5f); // flammes + fumée au-dessus
                    sum += p; sumDist += v.z; n++;
                    int cx = c % size, cz = c / size;
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = cx + dx, nz = cz + dz;
                            if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                            int nb = nz * size + nx;
                            if (visible.ContainsKey(nb) && seen.Add(nb)) queue.Enqueue(nb);
                        }
                }
                float meanDist = sumDist / n;
                // Confiance : grandit avec la taille apparente du foyer (nombre de cases, distance), petit bruit comme un vrai détecteur.
                float apparent = n * step * Mathf.Pow(150f / Mathf.Max(meanDist, 30f), 2f);
                float conf = Mathf.Clamp01(0.45f + 0.54f * (1f - Mathf.Exp(-apparent / 4f)) + Random.Range(-0.02f, 0.02f));
                if (conf < showThreshold) continue;
                detections.Add(new Detection
                {
                    view = Rect.MinMaxRect(Mathf.Clamp01(minX), Mathf.Clamp01(minY), Mathf.Clamp01(maxX), Mathf.Clamp01(maxY)),
                    confidence = conf, world = sum / n, distance = meanDist
                });
            }
            detections.Sort((a, b) => b.confidence.CompareTo(a.confidence));
            if (detections.Count > maxBoxes) detections.RemoveRange(maxBoxes, detections.Count - maxBoxes);

            // 3. Annonce radio des nouveaux foyers.
            foreach (Detection d in detections)
            {
                if (d.confidence < announceThreshold) continue;
                bool known = false;
                foreach (Vector3 a in announced)
                    if (Vector2.Distance(new Vector2(a.x, a.z), new Vector2(d.world.x, d.world.z)) < announceSeparation) { known = true; break; }
                if (known) continue;
                announced.Add(d.world);
                Vector3 off = d.world - camPos; off.y = 0f;
                float bearing = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
                string msg = $"IA drone : feu détecté à {off.magnitude:0} m au {WindSystem.CompassName(bearing)} du drone (confiance {d.confidence * 100f:0} %). Visez-le pour poser une balise.";
                if (radio != null) radio.Radio(msg); else Debug.Log("[PyroScan] " + msg);
            }
        }

        void Draw()
        {
            if (overlayRoot == null) return;
            while (boxes.Count < detections.Count) CreateBox();
            for (int i = 0; i < boxes.Count; i++)
            {
                bool show = i < detections.Count;
                boxes[i].enabled = show;
                labels[i].gameObject.SetActive(show);
                if (!show) continue;
                Detection d = detections[i];
                Vector3 a = ToLocal(d.view.xMin, d.view.yMin), b = ToLocal(d.view.xMax, d.view.yMax);
                boxes[i].SetPositions(new[] { new Vector3(a.x, a.y), new Vector3(b.x, a.y), new Vector3(b.x, b.y), new Vector3(a.x, b.y) });
                float top = screenSize.y * 0.5f;
                float ly = b.y + 0.006f <= top - 0.035f ? b.y + 0.006f : b.y - 0.036f; // au-dessus du cadre, ou dedans s'il touche le haut
                labels[i].transform.localPosition = new Vector3(a.x + 0.004f, ly, -0.001f);
                labels[i].text = $"FEU {d.confidence * 100f:0} %  <size=75%>{d.distance:0} m</size>";
            }
        }

        Vector3 ToLocal(float vx, float vy) => new Vector3((vx - 0.5f) * screenSize.x, (vy - 0.5f) * screenSize.y, 0f);

        void CreateBox()
        {
            var go = new GameObject($"Cadre IA {boxes.Count + 1}");
            go.transform.SetParent(overlayRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            lr.widthMultiplier = 0.004f;
            lr.numCornerVertices = 0;
            lr.alignment = LineAlignment.TransformZ;
            lr.sharedMaterial = boxMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            boxes.Add(lr);

            var t = new GameObject("Étiquette").AddComponent<TextMeshPro>();
            t.transform.SetParent(overlayRoot, false);
            t.fontSize = 0.3f;
            t.color = new Color(1f, 0.55f, 0.1f);
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.pivot = new Vector2(0f, 0f);
            t.rectTransform.sizeDelta = new Vector2(0.4f, 0.04f);
            labels.Add(t);
        }
    }
}
