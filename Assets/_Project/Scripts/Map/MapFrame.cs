using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Référentiel unique de la carte (le Terrain de 1 km).
    /// Toutes les cartes (murale, minimap, table) passent par lui pour convertir
    /// un point du terrain en position sur une carte, et inversement.
    /// Coordonnées normalisées : (0,0) = coin sud-ouest, (1,1) = coin nord-est.
    /// </summary>
    public class MapFrame : MonoBehaviour
    {
        public Terrain terrain;

        public static MapFrame Main { get; private set; }

        public Vector3 Origin => terrain.transform.position;
        public Vector3 Size => terrain.terrainData.size;

        void Awake()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            Main = this;
        }

        /// <summary>Point du monde -> coordonnées normalisées (x = est, y = nord).</summary>
        public Vector2 WorldToNormalized(Vector3 world)
        {
            return new Vector2((world.x - Origin.x) / Size.x, (world.z - Origin.z) / Size.z);
        }

        /// <summary>Coordonnées normalisées -> point du monde posé sur le sol.</summary>
        public Vector3 NormalizedToWorld(Vector2 n)
        {
            var p = new Vector3(Origin.x + n.x * Size.x, 0f, Origin.z + n.y * Size.z);
            p.y = SampleHeight(p);
            return p;
        }

        /// <summary>Altitude du sol (en Y monde) sous un point.</summary>
        public float SampleHeight(Vector3 world)
        {
            return terrain.SampleHeight(world) + Origin.y;
        }

        public static bool IsInside(Vector2 n)
        {
            return n.x >= 0f && n.x <= 1f && n.y >= 0f && n.y <= 1f;
        }
    }
}
