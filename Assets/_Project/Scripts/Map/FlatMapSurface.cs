using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Carte plate vue du dessus, affichée sur un Quad (table provisoire, carte murale).
    /// Le Quad couvre toute la carte : son coin bas-gauche = sud-ouest, haut-droit = nord-est.
    /// </summary>
    public class FlatMapSurface : MapSurface
    {
        [Tooltip("Décalage des icônes au-dessus de la carte, en unités locales du Quad")]
        public float surfaceOffset = 0.004f;

        public override bool TryGetWorldPoint(Vector3 surfacePoint, out Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(surfacePoint);
            var n = new Vector2(local.x + 0.5f, local.y + 0.5f);
            worldPoint = default;
            if (frame == null || !MapFrame.IsInside(n)) return false;
            worldPoint = frame.NormalizedToWorld(n);
            return true;
        }

        public override bool TryGetSurfacePoint(Vector3 worldPoint, out Vector3 surfacePoint)
        {
            surfacePoint = default;
            if (frame == null) return false;
            Vector2 n = frame.WorldToNormalized(worldPoint);
            if (!MapFrame.IsInside(n)) return false;
            // La face visible d'un Quad regarde vers -Z local : on décale les icônes de ce côté.
            surfacePoint = transform.TransformPoint(new Vector3(n.x - 0.5f, n.y - 0.5f, -surfaceOffset));
            return true;
        }
    }
}
