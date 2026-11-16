using UnityEngine;
using UnityEngine.InputSystem;

namespace PyroScan
{
    /// <summary>
    /// Nacelle de la caméra du drone, pilotée au joystick gauche :
    /// haut/bas = inclinaison (de l'horizon à la verticale), gauche/droite = rotation autour du drone,
    /// clic du joystick = retour face à l'avant. Sur PC : pavé numérique 8/2/4/6 (5 = recentrer) ou Page préc./suiv. + Début/Fin (F = recentrer).
    /// </summary>
    public class DroneCameraGimbal : MonoBehaviour
    {
        public float pitch = 45f;
        public float yaw = 0f;
        public float minPitch = 0f, maxPitch = 89f;
        [Tooltip("Vitesse de rotation (degrés/s) à fond de joystick")]
        public float speed = 70f;

        InputAction look, recenter;
        float defaultPitch;

        void Awake()
        {
            defaultPitch = pitch;
            look = new InputAction("Caméra drone (joystick gauche)", InputActionType.Value);
            look.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            look.AddBinding("<XRController>{LeftHand}/thumbstick");
            look.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/numpad8").With("Down", "<Keyboard>/numpad2")
                .With("Left", "<Keyboard>/numpad4").With("Right", "<Keyboard>/numpad6");
            // Secours sans pavé numérique (les flèches sont prises par le simulateur XR).
            look.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/pageUp").With("Down", "<Keyboard>/pageDown")
                .With("Left", "<Keyboard>/home").With("Right", "<Keyboard>/end");
            recenter = new InputAction("Recentrer caméra drone", InputActionType.Button);
            recenter.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
            recenter.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
            recenter.AddBinding("<Keyboard>/numpad5");
            recenter.AddBinding("<Keyboard>/f");
        }

        void OnEnable() { look.Enable(); recenter.Enable(); }
        void OnDisable() { look.Disable(); recenter.Disable(); }
        void OnDestroy() { look.Dispose(); recenter.Dispose(); }

        void Update()
        {
            Vector2 v = look.ReadValue<Vector2>();
            if (v.sqrMagnitude > 0.01f)
            {
                yaw = Mathf.Repeat(yaw + v.x * speed * Time.deltaTime + 180f, 360f) - 180f;
                pitch = Mathf.Clamp(pitch - v.y * speed * Time.deltaTime, minPitch, maxPitch);
            }
            if (recenter.WasPressedThisFrame()) { yaw = 0f; pitch = defaultPitch; }
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
