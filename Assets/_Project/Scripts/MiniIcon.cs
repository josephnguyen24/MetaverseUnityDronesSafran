using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Place une petite icône sur la maquette à l'endroit où se trouve un objet de la vraie vallée
    /// (drone, camion...). La maquette étant une copie réduite de la vallée, la conversion est exacte.
    /// </summary>
    public class MiniIcon : MonoBehaviour
    {
        public Transform target;
        public Transform valley;
        public Transform miniValley;

        void LateUpdate()
        {
            if (target == null || valley == null || miniValley == null) return;
            transform.position = miniValley.TransformPoint(valley.InverseTransformPoint(target.position));
            transform.rotation = miniValley.rotation * Quaternion.Inverse(valley.rotation) * target.rotation;
        }
    }
}
