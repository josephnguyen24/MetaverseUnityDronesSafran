using System;
using System.Collections.Generic;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Propagation du feu sur une grille (automate cellulaire) posée sur le terrain.
    /// Chaque case a un combustible (0 aucun : eau, route, roche ; 1 herbe ; 2 broussailles ; 3 forêt).
    /// Toutes les "tickInterval" secondes, chaque case en feu peut allumer ses 8 voisines,
    /// avec une probabilité plus forte dans le sens du vent. Une case brûle un temps donné puis devient brûlée.
    /// Prévu pour l'étape 6 : Extinguish (eau du Canadair) et ApplyRetardant (barrière de retardant).
    /// </summary>
    public class FireGrid : MonoBehaviour
    {
        public enum CellState : byte { Incombustible, Combustible, EnFeu, Brule }

        public static FireGrid Instance { get; private set; }

        public MapFrame frame;
        public WindSystem wind;

        [Header("Grille")]
        public int size = 100;
        [HideInInspector] public byte[] fuel;

        [Header("Départ de feu")]
        public Transform[] ignitionPoints;

        [Header("Réglages de propagation")]
        public float tickInterval = 0.5f;
        [Tooltip("Multiplicateur global de vitesse (1 = normal)")]
        public float spreadSpeed = 0.4f;
        [Tooltip("Probabilité d'allumer une voisine, par tick, selon le combustible (aucun, herbe, broussailles, forêt)")]
        public float[] spreadChance = { 0f, 0.05f, 0.04f, 0.03f };
        [Tooltip("Durée de combustion d'une case (s), selon le combustible")]
        public float[] burnSeconds = { 0f, 8f, 14f, 25f };
        [Tooltip("Influence du vent sur la propagation")]
        public float windInfluence = 1.6f;

        [Header("Cartes")]
        [Tooltip("Rendus qui affichent la carte du feu (calques de la carte murale et de la minimap)")]
        public Renderer[] overlayRenderers;

        public event Action<int> CellIgnited;
        public event Action<int> CellBurnt;
        public event Action<int> CellExtinguished;
        public event Action Ticked;

        public int BurningCount => burning.Count;
        /// <summary>Cases actuellement en feu (lecture seule).</summary>
        public IReadOnlyList<int> Burning => burning;
        public int BurntCount { get; private set; }
        public Texture2D Overlay { get; private set; }
        public float CellSize => frame != null ? frame.Size.x / size : 10f;

        CellState[] state;
        float[] burnLeft;
        float[] protectedUntil;
        byte[] protectionKind; // 0 aucune, 1 eau, 2 retardant
        readonly List<int> burning = new List<int>();
        readonly List<int> protectedCells = new List<int>();
        Color32[] pixels;
        bool overlayDirty;
        float timer;

        static readonly Color32 ColNone = new Color32(0, 0, 0, 0);
        static readonly Color32 ColFire = new Color32(255, 110, 0, 235);
        static readonly Color32 ColBurnt = new Color32(25, 18, 12, 210);
        static readonly Color32 ColWater = new Color32(40, 130, 255, 150);
        static readonly Color32 ColRetardant = new Color32(255, 60, 150, 170);

        void Awake()
        {
            Instance = this;
            int n = size * size;
            state = new CellState[n];
            burnLeft = new float[n];
            protectedUntil = new float[n];
            protectionKind = new byte[n];
            pixels = new Color32[n];
            for (int i = 0; i < n; i++)
                state[i] = fuel != null && i < fuel.Length && fuel[i] > 0 ? CellState.Combustible : CellState.Incombustible;

            Overlay = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Carte du feu"
            };
            Overlay.SetPixels32(pixels);
            Overlay.Apply();
        }

        void Start()
        {
            foreach (Renderer r in overlayRenderers)
                if (r != null) r.material.SetTexture("_BaseMap", Overlay);

            foreach (Transform p in ignitionPoints)
            {
                if (p == null) continue;
                int c = CellIndex(p.position);
                if (c < 0) continue;
                // Foyer de départ garanti : 3x3 cellules de broussailles au minimum, toutes allumées
                // (sinon un foyer posé sur un sol pauvre peut s'éteindre tout seul en quelques secondes).
                SeedIgnition(c);
                foreach (int nb in Neighbours(c)) SeedIgnition(nb);
            }
            ApplyOverlay();
        }

        void Update()
        {
            timer += Time.deltaTime;
            while (timer >= tickInterval)
            {
                timer -= tickInterval;
                Tick(tickInterval);
            }
            if (overlayDirty) ApplyOverlay();
        }

        void Tick(float dt)
        {
            Vector3 w = wind != null ? wind.Vector : Vector3.forward;
            float strength = wind != null ? wind.Strength : 0.5f;
            var snapshot = burning.ToArray();

            foreach (int c in snapshot)
            {
                int cx = c % size, cz = c / size;
                float chance = spreadChance[fuel[c]] * spreadSpeed;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                        int nb = nz * size + nx;
                        if (state[nb] != CellState.Combustible || Time.time < protectedUntil[nb]) continue;
                        Vector2 dir = new Vector2(dx, dz).normalized;
                        float windFactor = Mathf.Max(0.15f, 1f + windInfluence * strength * Vector2.Dot(dir, new Vector2(w.x, w.z)));
                        float diag = (dx != 0 && dz != 0) ? 0.7f : 1f;
                        float p = chance * windFactor * diag * Mathf.Lerp(0.6f, 1.2f, fuel[nb] / 3f);
                        if (UnityEngine.Random.value < p) Ignite(nb);
                    }

                burnLeft[c] -= dt;
                if (burnLeft[c] <= 0f && state[c] == CellState.EnFeu)
                {
                    state[c] = CellState.Brule;
                    burning.Remove(c);
                    BurntCount++;
                    SetPixel(c, ColBurnt);
                    CellBurnt?.Invoke(c);
                }
            }

            // Fin des protections (eau qui sèche, retardant qui s'use).
            for (int i = protectedCells.Count - 1; i >= 0; i--)
            {
                int c = protectedCells[i];
                if (Time.time >= protectedUntil[c])
                {
                    protectionKind[c] = 0;
                    protectedCells.RemoveAt(i);
                    if (state[c] == CellState.Combustible) SetPixel(c, ColNone);
                }
            }
            Ticked?.Invoke();
        }

        void SeedIgnition(int c)
        {
            if (fuel[c] < 2) fuel[c] = 2;
            if (state[c] == CellState.Incombustible) state[c] = CellState.Combustible;
            Ignite(c);
        }

        void Ignite(int c)
        {
            if (state[c] != CellState.Combustible) return;
            state[c] = CellState.EnFeu;
            burnLeft[c] = burnSeconds[fuel[c]] * UnityEngine.Random.Range(0.8f, 1.2f);
            burning.Add(c);
            SetPixel(c, ColFire);
            CellIgnited?.Invoke(c);
        }

        void SetPixel(int c, Color32 col)
        {
            pixels[c] = col;
            overlayDirty = true;
        }

        void ApplyOverlay()
        {
            Overlay.SetPixels32(pixels);
            Overlay.Apply(false);
            overlayDirty = false;
        }

        IEnumerable<int> Neighbours(int c)
        {
            int cx = c % size, cz = c / size;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx >= 0 && nz >= 0 && nx < size && nz < size) yield return nz * size + nx;
                }
        }

        // ---------------------------------------------------------------- Outils publics

        public int CellIndex(Vector3 world)
        {
            Vector2 n = frame.WorldToNormalized(world);
            int x = Mathf.FloorToInt(n.x * size), z = Mathf.FloorToInt(n.y * size);
            if (x < 0 || z < 0 || x >= size || z >= size) return -1;
            return z * size + x;
        }

        public Vector3 CellCenter(int c)
        {
            var n = new Vector2((c % size + 0.5f) / size, (c / size + 0.5f) / size);
            return frame.NormalizedToWorld(n);
        }

        public CellState GetState(int c) => state[c];

        public bool AnyBurningWithin(Vector3 center, float radius)
        {
            float r2 = radius * radius;
            foreach (int c in burning)
            {
                Vector3 d = CellCenter(c) - center;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>Centre (moyenne) des cases en feu, ou false s'il n'y en a pas.</summary>
        public bool TryGetFireCentroid(out Vector3 centroid)
        {
            centroid = default;
            if (burning.Count == 0) return false;
            foreach (int c in burning) centroid += CellCenter(c);
            centroid /= burning.Count;
            return true;
        }

        /// <summary>Étape 6 – eau du Canadair : éteint les cases en feu du cercle et les mouille un moment.</summary>
        public int Extinguish(Vector3 center, float radius, float wetSeconds = 60f)
        {
            int count = 0;
            ForEachCellInRadius(center, radius, c =>
            {
                if (state[c] == CellState.EnFeu)
                {
                    state[c] = CellState.Combustible;
                    burning.Remove(c);
                    count++;
                    CellExtinguished?.Invoke(c);
                }
                Protect(c, wetSeconds, 1, ColWater);
            });
            return count;
        }

        /// <summary>Étape 6 – retardant : bande que le feu ne peut pas traverser pendant "seconds".</summary>
        public void ApplyRetardant(Vector3 center, Vector3 direction, float length, float width, float seconds = 240f)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            Vector3 side = new Vector3(direction.z, 0f, -direction.x);
            ForEachCellInRadius(center, Mathf.Max(length, width), c =>
            {
                Vector3 d = CellCenter(c) - center;
                d.y = 0f;
                if (Mathf.Abs(Vector3.Dot(d, direction)) <= length * 0.5f && Mathf.Abs(Vector3.Dot(d, side)) <= width * 0.5f)
                    if (state[c] == CellState.Combustible) Protect(c, seconds, 2, ColRetardant);
            });
        }

        void Protect(int c, float seconds, byte kind, Color32 col)
        {
            if (state[c] == CellState.Incombustible || state[c] == CellState.Brule) return;
            if (protectedUntil[c] <= Time.time) protectedCells.Add(c);
            protectedUntil[c] = Mathf.Max(protectedUntil[c], Time.time + seconds);
            protectionKind[c] = kind;
            SetPixel(c, col);
        }

        void ForEachCellInRadius(Vector3 center, float radius, Action<int> action)
        {
            int c0 = CellIndex(center);
            if (c0 < 0) return;
            int r = Mathf.CeilToInt(radius / CellSize);
            int cx = c0 % size, cz = c0 / size;
            for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= size || z >= size) continue;
                    int c = z * size + x;
                    Vector3 d = CellCenter(c) - center;
                    d.y = 0f;
                    if (d.magnitude <= radius) action(c);
                }
        }
    }
}
