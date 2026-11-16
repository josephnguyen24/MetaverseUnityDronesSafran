using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Icône qui suit un objet du terrain (drone, camion...) sur une carte.
    /// Elle se cache quand l'objet sort de la zone affichée par la carte.
    /// </summary>
    public class MapIcon : MonoBehaviour
    {
        public Transform target;
        public MapSurface surface;

        Renderer[] renderers;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void LateUpdate()
        {
            if (target == null || surface == null) return;
            bool visible = surface.TryGetSurfacePoint(target.position, out Vector3 p);
            foreach (Renderer r in renderers) r.enabled = visible;
            if (visible) transform.position = p;
        }
    }
}
