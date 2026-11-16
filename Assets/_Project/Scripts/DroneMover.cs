using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Vol simple du drone vers le point demandé.
    /// Si terrainFrame est renseigné, le drone suit le relief (altitude au-dessus du sol)
    /// et anticipe les crêtes devant lui. Sinon, comportement du rendu 1 (ligne droite).
    /// Sera remplacé plus tard par DroneController (vent + batterie).
    /// </summary>
    public class DroneMover : MonoBehaviour
    {
        [Tooltip("Vitesse horizontale en m/s")]
        public float speed = 12f;

        [Tooltip("Hauteur de vol au-dessus du sol, en mètres")]
        public float altitude = 15f;

        public float turnSpeed = 4f;

        [Header("Suivi du relief (terrain 1 km)")]
        public MapFrame terrainFrame;
        [Tooltip("Vitesse de montée/descente en m/s")]
        public float climbSpeed = 10f;
        [Tooltip("Distance d'anticipation du relief devant le drone, en mètres")]
        public float lookAhead = 40f;

        Vector3? target;

        public bool IsMoving => target.HasValue;

        public void GoTo(Vector3 groundPoint)
        {
            target = terrainFrame != null ? groundPoint : groundPoint + Vector3.up * altitude;
        }

        void Update()
        {
            if (!target.HasValue) return;
            if (terrainFrame != null) UpdateTerrainFollow();
            else UpdateStraight();
        }

        void UpdateStraight()
        {
            transform.position = Vector3.MoveTowards(transform.position, target.Value, speed * Time.deltaTime);
            Face(target.Value - transform.position);
            if ((transform.position - target.Value).sqrMagnitude < 0.01f) target = null;
        }

        void UpdateTerrainFollow()
        {
            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(target.Value.x - pos.x, 0f, target.Value.z - pos.z);
            float dist = flat.magnitude;
            Vector3 dir = dist > 0.001f ? flat / dist : transform.forward;

            Vector3 next = pos + dir * Mathf.Min(speed * Time.deltaTime, dist);
            float ground = Mathf.Max(terrainFrame.SampleHeight(next),
                                     terrainFrame.SampleHeight(next + dir * lookAhead));
            float desiredY = ground + altitude;
            next.y = Mathf.MoveTowards(pos.y, desiredY, climbSpeed * Time.deltaTime);
            next.y = Mathf.Max(next.y, terrainFrame.SampleHeight(next) + 3f);
            transform.position = next;

            if (dist > 0.5f) Face(dir);
            if (dist <= 0.5f && Mathf.Abs(next.y - desiredY) < 0.5f) target = null;
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }
    }
}
