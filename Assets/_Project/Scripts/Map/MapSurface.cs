using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Une carte que le joueur peut viser (table, carte murale, minimap).
    /// Chaque type de carte sait convertir un point visé en point du terrain, et l'inverse.
    /// </summary>
    public abstract class MapSurface : MonoBehaviour
    {
        public MapFrame frame;

        [Tooltip("La main droite peut-elle envoyer le drone depuis cette carte ?")]
        public bool allowDroneOrders = true;

        [Tooltip("La main gauche peut-elle poser des marqueurs rouges sur cette carte ? (étape 4)")]
        public bool allowMarkers = true;

        /// <summary>Point visé sur la carte -> point du terrain (sur le sol).</summary>
        public abstract bool TryGetWorldPoint(Vector3 surfacePoint, out Vector3 worldPoint);

        /// <summary>Point du terrain -> position sur la carte (légèrement au-dessus de sa surface).</summary>
        public abstract bool TryGetSurfacePoint(Vector3 worldPoint, out Vector3 surfacePoint);

        protected virtual void Awake()
        {
            if (frame == null) frame = MapFrame.Main != null ? MapFrame.Main : FindFirstObjectByType<MapFrame>();
        }
    }
}
