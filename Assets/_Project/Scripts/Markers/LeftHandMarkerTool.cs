using UnityEngine;
using UnityEngine.InputSystem;

namespace PyroScan
{
    /// <summary>
    /// Gâchette gauche :
    /// - rayon gauche sur une carte (murale ou minimap) : enlève la balise visée ;
    /// - sinon : pose une balise au point visé par la caméra du drone (viseur au centre de l'écran).
    /// Touche B en secours pour tester sur PC.
    /// </summary>
    public class LeftHandMarkerTool : MonoBehaviour
    {
        public MarkerService service;

        [Tooltip("Transform de la manette gauche (origine et direction du rayon)")]
        public Transform rayOrigin;

        [Tooltip("Caméra du drone : la balise se pose au point visé par son centre")]
        public Camera droneCamera;

        [Tooltip("Portée du balisage depuis le drone (m)")]
        public float beaconRange = 600f;

        public float mapRayDistance = 15f;

        [Tooltip("Distance sur le terrain (m) en dessous de laquelle on vise une balise existante pour l'enlever")]
        public float removeRadiusMeters = 15f;

        [Tooltip("Autoriser aussi la pose directement sur les cartes (désactivé : on balise avec le drone)")]
        public bool allowMapPlacement = false;

        InputAction trigger;

        void Awake()
        {
            if (service == null) service = GetComponent<MarkerService>();
            trigger = new InputAction("Balise (gâchette gauche)", InputActionType.Button);
            trigger.AddBinding("<XRController>{LeftHand}/triggerPressed");
            trigger.AddBinding("<XRController>{LeftHand}/triggerButton");
            trigger.AddBinding("<XRController>{LeftHand}/trigger").WithInteraction("press(pressPoint=0.6)");
            trigger.AddBinding("<Keyboard>/b");
        }

        void OnEnable() => trigger.Enable();
        void OnDisable() => trigger.Disable();
        void OnDestroy() => trigger.Dispose();

        void Update()
        {
            if (trigger.WasPressedThisFrame()) Fire();
        }

        void Fire()
        {
            if (service == null) return;
            if (TryMapAction()) return;
            DropBeaconFromDrone();
        }

        bool TryMapAction()
        {
            if (rayOrigin == null) return false;
            MapSurface surface = null;
            Vector3 hitPoint = default;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, mapRayDistance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var s = hit.collider.GetComponentInParent<MapSurface>();
                if (s != null && s.allowMarkers && hit.distance < best)
                {
                    best = hit.distance;
                    surface = s;
                    hitPoint = hit.point;
                }
            }
            if (surface == null) return false;
            if (surface.TryGetWorldPoint(hitPoint, out Vector3 world))
            {
                Transform existing = service.FindNearest(world, removeRadiusMeters);
                if (existing != null) service.Remove(existing);
                else if (allowMapPlacement) service.Add(world);
            }
            return true; // le rayon était sur une carte : on ne balise pas avec le drone
        }

        void DropBeaconFromDrone()
        {
            if (droneCamera == null) return;
            Transform cam = droneCamera.transform;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, beaconRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                service.Add(hit.point);
            else
                Debug.Log("[PyroScan] Balise : la caméra du drone ne vise pas le sol (trop loin ou vers le ciel).");
        }
    }
}
