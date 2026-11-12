using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PyroScan
{
    /// <summary>
    /// Interaction du rendu 1 : le joueur vise la maquette (MiniValley) avec la manette et appuie
    /// sur le bouton de sélection (grip par défaut dans les Starter Assets) ; un marqueur apparaît et le drone part vers le même point dans la vraie vallée.
    /// À placer sur la racine de MiniValley (copie réduite de Valley).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class TacticalTable : MonoBehaviour
    {
        [Tooltip("La vraie vallée (objet Valley)")]
        public Transform valley;

        public DroneMover drone;

        [Tooltip("Petite sphère posée sur la maquette à chaque ordre")]
        public GameObject markerPrefab;

        public int maxMarkers = 8;

        [Tooltip("Portée du rayon de secours, en mètres")]
        public float fallbackRayLength = 15f;

        XRSimpleInteractable interactable;
        readonly Queue<GameObject> markers = new Queue<GameObject>();

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelect);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelect);
        }

        void OnSelect(SelectEnterEventArgs args)
        {
            if (TryGetPointOnTable(args.interactorObject, out Vector3 point))
                SendDroneTo(point);
            else
                Debug.LogWarning("[PyroScan] Sélection reçue mais aucun point trouvé sur la maquette.");
        }

        /// <summary>Convertit un point de la maquette en point de la vallée et y envoie le drone.</summary>
        public void SendDroneTo(Vector3 pointOnTable)
        {
            Vector3 local = transform.InverseTransformPoint(pointOnTable);
            Vector3 target = valley.TransformPoint(local);

            if (markerPrefab != null)
            {
                // Pas de parent : sinon le marqueur hériterait de l'échelle de la maquette.
                GameObject marker = Instantiate(markerPrefab, pointOnTable, Quaternion.identity);
                markers.Enqueue(marker);
                if (markers.Count > maxMarkers)
                    Destroy(markers.Dequeue());
            }

            if (drone != null)
                drone.GoTo(target);

            Debug.Log($"[PyroScan] Waypoint : maquette {pointOnTable} -> vallée {target}");
        }

        bool TryGetPointOnTable(IXRSelectInteractor interactor, out Vector3 point)
        {
            // 1) Rayon classique (XRRayInteractor)
            if (interactor is XRRayInteractor ray &&
                ray.TryGetCurrent3DRaycastHit(out RaycastHit rayHit) &&
                rayHit.collider.transform.IsChildOf(transform))
            {
                point = rayHit.point;
                return true;
            }

            // 2) Secours (Near-Far Interactor des Starter Assets XRI 3, ou autre) :
            //    on lance notre propre rayon depuis la manette.
            Transform origin = interactor.transform;
            RaycastHit[] hits = Physics.RaycastAll(origin.position, origin.forward, fallbackRayLength,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            point = default;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) && hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            return best < float.MaxValue;
        }
    }
}
