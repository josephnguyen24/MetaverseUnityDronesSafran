using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PyroScan
{
    /// <summary>
    /// Main droite : viser une carte et appuyer sur le bouton de sélection (grip)
    /// envoie le drone au point correspondant du terrain.
    /// Fonctionne avec n'importe quelle MapSurface dont allowDroneOrders est coché.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class DroneOrderOnMap : MonoBehaviour
    {
        public MapSurface surface;
        public DroneMover drone;

        [Tooltip("Petite sphère posée sur la carte à chaque ordre")]
        public GameObject markerPrefab;
        [Tooltip("Objet placé au point visé sur le terrain (suivi par une icône sur les cartes) ; utile sur la minimap qui défile")]
        public Transform destination;
        public int maxMarkers = 6;
        public float fallbackRayLength = 15f;

        XRSimpleInteractable interactable;
        readonly Queue<GameObject> markers = new Queue<GameObject>();

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (surface == null) surface = GetComponent<MapSurface>();
        }

        void OnEnable() => interactable.selectEntered.AddListener(OnSelect);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnSelect);

        void OnSelect(SelectEnterEventArgs args)
        {
            if (surface == null || !surface.allowDroneOrders || drone == null) return;
            if (args.interactorObject.handedness == InteractorHandedness.Left) return; // main gauche = marqueurs
            if (!TryGetHitPoint(args.interactorObject, out Vector3 hit)) return;
            if (!surface.TryGetWorldPoint(hit, out Vector3 world)) return;

            drone.GoTo(world);
            if (destination != null) destination.position = world;

            if (markerPrefab != null)
            {
                markers.Enqueue(Instantiate(markerPrefab, hit, Quaternion.identity));
                if (markers.Count > maxMarkers) Destroy(markers.Dequeue());
            }
            Debug.Log($"[PyroScan] Ordre drone : carte {hit} -> terrain {world}");
        }

        bool TryGetHitPoint(IXRSelectInteractor interactor, out Vector3 point)
        {
            if (interactor is XRRayInteractor ray &&
                ray.TryGetCurrent3DRaycastHit(out RaycastHit rayHit) &&
                rayHit.collider.transform.IsChildOf(transform))
            {
                point = rayHit.point;
                return true;
            }

            Transform origin = interactor.transform;
            float best = float.MaxValue;
            point = default;
            foreach (RaycastHit hit in Physics.RaycastAll(origin.position, origin.forward, fallbackRayLength,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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
