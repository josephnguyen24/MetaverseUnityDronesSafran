using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Version simple du drone pour le rendu 1 : vol en ligne droite vers le point demandé.
    /// Sera remplacé par DroneController (Rigidbody + vent + batterie) après le rendu 1.
    /// </summary>
    public class DroneMover : MonoBehaviour
    {
        [Tooltip("Vitesse en m/s")]
        public float speed = 12f;

        [Tooltip("Hauteur de vol au-dessus du point visé, en mètres")]
        public float altitude = 15f;

        [Tooltip("Vitesse de rotation vers la direction de vol")]
        public float turnSpeed = 4f;

        Vector3? target;

        public bool IsMoving => target.HasValue;

        public void GoTo(Vector3 groundPoint)
        {
            target = groundPoint + Vector3.up * altitude;
        }

        void Update()
        {
            if (!target.HasValue) return;

            transform.position = Vector3.MoveTowards(transform.position, target.Value, speed * Time.deltaTime);

            Vector3 dir = target.Value - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
            }

            if ((transform.position - target.Value).sqrMagnitude < 0.01f)
                target = null;
        }
    }
}
