using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Minimap en relief posée sur la table, centrée sur le drone et orientée selon son cap
    /// (comme dans COD : "devant le drone" est toujours vers le fond de la table).
    /// Disque de rayon "radius" (m, sur la table) qui couvre "rangeMeters" (m, sur le terrain).
    /// Le maillage est recalculé quand le drone bouge ou tourne ; la couleur vient de la vue satellite.
    /// C'est aussi une MapSurface : la main droite peut y envoyer le drone, la main gauche y poser des marqueurs.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class DroneMinimap : MapSurface
    {
        public Transform drone;

        [Header("Taille")]
        [Tooltip("Rayon du disque sur la table, en mètres")]
        public float radius = 0.5f;
        [Tooltip("Rayon couvert sur le terrain, en mètres")]
        public float rangeMeters = 150f;
        [Tooltip("Exagération du relief : 1 = échelle réelle (trop haut sur la table), 0.35 = relief aplati")]
        public float reliefExaggeration = 0.35f;

        [Header("Maillage")]
        public int rings = 24;
        public int segments = 48;
        [Tooltip("Délai minimum entre deux mises à jour du collider (s)")]
        public float colliderRefresh = 0.25f;

        [Header("Repères")]
        [Tooltip("Lettre N placée sur le bord du disque, dans la direction du nord")]
        public Transform northMarker;
        public float iconOffset = 0.006f;
        [Tooltip("Calque transparent (carte du feu) qui partage le maillage de la minimap")]
        public MeshFilter overlayFilter;

        Mesh mesh;
        MeshCollider meshCollider;
        Vector3[] localXZ;      // positions locales (x, 0, z) de chaque sommet
        Vector3[] vertices;
        Vector2[] uvs;
        float[] heights;
        Vector3 lastDronePos = new Vector3(float.MaxValue, 0f, 0f);
        float lastYaw = float.MaxValue;
        float heightRef;
        bool heightRefInit;
        float colliderTimer;
        bool colliderDirty;

        float WorldPerLocal => rangeMeters / radius;
        float VerticalScale => reliefExaggeration / WorldPerLocal;
        Quaternion Heading => Quaternion.Euler(0f, drone != null ? drone.eulerAngles.y : 0f, 0f);

        protected override void Awake()
        {
            base.Awake();
            meshCollider = GetComponent<MeshCollider>();
            BuildTopology();
        }

        void BuildTopology()
        {
            int count = 1 + rings * segments;
            localXZ = new Vector3[count];
            vertices = new Vector3[count];
            uvs = new Vector2[count];
            heights = new float[count];
            localXZ[0] = Vector3.zero;
            for (int i = 1; i <= rings; i++)
            {
                float r = radius * i / rings;
                for (int j = 0; j < segments; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    localXZ[Index(i, j)] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
            }

            var tris = new int[(segments + (rings - 1) * segments * 2) * 3];
            int t = 0;
            for (int j = 0; j < segments; j++)
            {
                int jn = (j + 1) % segments;
                tris[t++] = 0; tris[t++] = Index(1, jn); tris[t++] = Index(1, j);
            }
            for (int i = 1; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int jn = (j + 1) % segments;
                    int a = Index(i, j), b = Index(i, jn), c = Index(i + 1, jn), d = Index(i + 1, j);
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                }

            mesh = new Mesh { name = "Minimap relief" };
            mesh.MarkDynamic();
            mesh.vertices = localXZ;
            mesh.uv = uvs;
            mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (overlayFilter != null) overlayFilter.sharedMesh = mesh;
        }

        int Index(int ring, int seg) => 1 + (ring - 1) * segments + seg;

        void LateUpdate()
        {
            if (drone == null || frame == null) return;

            bool moved = (drone.position - lastDronePos).sqrMagnitude > 0.25f;
            bool turned = Mathf.Abs(Mathf.DeltaAngle(drone.eulerAngles.y, lastYaw)) > 0.5f;
            if (moved || turned) Rebuild();

            colliderTimer += Time.deltaTime;
            if (colliderDirty && colliderTimer >= colliderRefresh)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
                colliderDirty = false;
                colliderTimer = 0f;
            }
        }

        Vector3 LocalToWorld(Vector3 local)
        {
            Vector3 w = drone.position + Heading * new Vector3(local.x, 0f, local.z) * WorldPerLocal;
            w.y = 0f;
            return w;
        }

        void Rebuild()
        {
            lastDronePos = drone.position;
            lastYaw = drone.eulerAngles.y;

            float hMin = float.MaxValue;
            for (int k = 0; k < localXZ.Length; k++)
            {
                Vector3 w = LocalToWorld(localXZ[k]);
                heights[k] = frame.SampleHeight(w);
                if (heights[k] < hMin) hMin = heights[k];
                Vector2 n = frame.WorldToNormalized(w);
                uvs[k] = new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
            }

            // Référence de hauteur lissée : le point le plus bas du disque est posé sur la table.
            heightRef = heightRefInit ? Mathf.Lerp(heightRef, hMin, 0.25f) : hMin;
            heightRefInit = true;

            float vs = VerticalScale;
            for (int k = 0; k < localXZ.Length; k++)
                vertices[k] = new Vector3(localXZ[k].x, Mathf.Max(0f, (heights[k] - heightRef) * vs), localXZ[k].z);

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            colliderDirty = true;

            if (northMarker != null)
            {
                Vector3 dir = Quaternion.Inverse(Heading) * Vector3.forward;
                northMarker.position = transform.TransformPoint(dir * (radius + 0.045f) + Vector3.up * 0.01f);
            }
        }

        public override bool TryGetWorldPoint(Vector3 surfacePoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (drone == null || frame == null) return false;
            Vector3 local = transform.InverseTransformPoint(surfacePoint);
            if (new Vector2(local.x, local.z).magnitude > radius) return false;
            worldPoint = LocalToWorld(local);
            worldPoint.y = frame.SampleHeight(worldPoint);
            return true;
        }

        public override bool TryGetSurfacePoint(Vector3 worldPoint, out Vector3 surfacePoint)
        {
            surfacePoint = default;
            if (drone == null || frame == null) return false;
            Vector3 d = worldPoint - drone.position;
            d.y = 0f;
            Vector3 local = Quaternion.Inverse(Heading) * d / WorldPerLocal;
            if (local.magnitude > radius) return false;
            float y = Mathf.Max(0f, (frame.SampleHeight(worldPoint) - heightRef) * VerticalScale) + iconOffset;
            surfacePoint = transform.TransformPoint(new Vector3(local.x, y, local.z));
            return true;
        }
    }
}
