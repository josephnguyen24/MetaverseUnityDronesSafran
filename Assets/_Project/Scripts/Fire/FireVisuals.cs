using System.Collections.Generic;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Affichage du feu dans la vallée, pensé pour le Quest :
    /// flammes low-poly (pool limité), sol brûlé (carrés sombres), une seule colonne de fumée au centre du feu.
    /// </summary>
    public class FireVisuals : MonoBehaviour
    {
        public FireGrid grid;
        public Material flameMaterial;
        public Material burntMaterial;
        public ParticleSystem smoke;
        public int maxFlames = 120;
        public int maxBurntPatches = 2500;

        readonly Dictionary<int, Transform> flames = new Dictionary<int, Transform>();
        readonly Stack<Transform> flamePool = new Stack<Transform>();
        int burntPatches;
        Mesh flameMesh;
        Transform flameRoot, burntRoot;

        void Awake()
        {
            flameRoot = new GameObject("Flammes").transform;
            flameRoot.SetParent(transform, false);
            burntRoot = new GameObject("Sol brûlé").transform;
            burntRoot.SetParent(transform, false);
            flameMesh = BuildFlameMesh();
        }

        void OnEnable()
        {
            grid.CellIgnited += OnIgnited;
            grid.CellBurnt += OnBurnt;
            grid.CellExtinguished += RemoveFlame;
            grid.Ticked += UpdateSmoke;
        }

        void OnDisable()
        {
            grid.CellIgnited -= OnIgnited;
            grid.CellBurnt -= OnBurnt;
            grid.CellExtinguished -= RemoveFlame;
            grid.Ticked -= UpdateSmoke;
        }

        void OnIgnited(int c)
        {
            if (flames.Count >= maxFlames) return;
            Transform f = flamePool.Count > 0 ? flamePool.Pop() : CreateFlame();
            f.gameObject.SetActive(true);
            float s = grid.CellSize;
            f.position = grid.CellCenter(c) + new Vector3(Random.Range(-0.3f, 0.3f) * s, 0f, Random.Range(-0.3f, 0.3f) * s);
            f.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            flames[c] = f;
        }

        void OnBurnt(int c)
        {
            RemoveFlame(c);
            if (burntPatches >= maxBurntPatches) return;
            Vector3 p = grid.CellCenter(c);
            Vector2 n = grid.frame.WorldToNormalized(p);
            Vector3 normal = grid.frame.terrain.terrainData.GetInterpolatedNormal(n.x, n.y);
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Brûlé";
            q.transform.SetParent(burntRoot, false);
            q.transform.position = p + normal * 0.3f;
            q.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            q.transform.localScale = Vector3.one * grid.CellSize * 1.25f;
            q.GetComponent<Renderer>().sharedMaterial = burntMaterial;
            burntPatches++;
        }

        void RemoveFlame(int c)
        {
            if (!flames.TryGetValue(c, out Transform f)) return;
            flames.Remove(c);
            f.gameObject.SetActive(false);
            flamePool.Push(f);
        }

        Transform CreateFlame()
        {
            var go = new GameObject("Flamme");
            go.transform.SetParent(flameRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = flameMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = flameMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            // Vacillement des flammes (peu coûteux : au plus maxFlames objets).
            float t = Time.time;
            foreach (KeyValuePair<int, Transform> kv in flames)
            {
                float k = Mathf.PerlinNoise(kv.Key * 0.37f, t * 3f);
                kv.Value.localScale = new Vector3(1f, 0.6f + k * 0.9f, 1f);
            }
        }

        void UpdateSmoke()
        {
            if (smoke == null) return;
            var emission = smoke.emission;
            if (grid.TryGetFireCentroid(out Vector3 c))
            {
                smoke.transform.position = c + Vector3.up * 8f;
                emission.rateOverTime = Mathf.Min(40f, 4f + grid.BurningCount * 0.3f);
                if (!smoke.isPlaying) smoke.Play();
            }
            else
            {
                emission.rateOverTime = 0f;
            }
        }

        static Mesh BuildFlameMesh()
        {
            // Deux cônes croisés : flamme low-poly de 6 m de haut.
            var v = new List<Vector3>();
            var t = new List<int>();
            AddCone(v, t, 3f, 6f, 6);
            AddCone(v, t, 1.8f, 8f, 5);
            var m = new Mesh { name = "Flamme" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void AddCone(List<Vector3> v, List<int> t, float r, float h, int n)
        {
            int b = v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            int apex = v.Count;
            v.Add(new Vector3(0f, h, 0f));
            for (int i = 0; i < n; i++) t.AddRange(new[] { b + i, apex, b + (i + 1) % n });
        }
    }
}
