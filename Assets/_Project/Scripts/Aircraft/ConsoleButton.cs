using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PyroScan
{
    /// <summary>
    /// Bouton du pupitre : viser + grip (ou appuyer du doigt / de la manette) appelle un avion.
    /// Touches de secours sur PC (F1 / F2, ou pavé numérique 7 / 9).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ConsoleButton : MonoBehaviour
    {
        public AircraftDispatcher dispatcher;
        public AircraftDispatcher.Kind kind;
        public string[] keyboardBindings;
        [Tooltip("Partie mobile du bouton (s'enfonce à l'appui)")]
        public Transform cap;
        public float pressDepth = 0.008f;

        XRSimpleInteractable interactable;
        InputAction key;
        Vector3 capRest;
        float lastPress = -10f;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (cap != null) capRest = cap.localPosition;
            key = new InputAction($"Bouton {kind}", InputActionType.Button);
            if (keyboardBindings != null) foreach (string b in keyboardBindings) key.AddBinding(b);
        }

        void OnEnable() { interactable.selectEntered.AddListener(OnSelect); key.Enable(); }
        void OnDisable() { interactable.selectEntered.RemoveListener(OnSelect); key.Disable(); }
        void OnDestroy() => key.Dispose();

        void Update() { if (key.WasPressedThisFrame()) Press(); }

        void OnSelect(SelectEnterEventArgs args) => Press();

        public void Press()
        {
            if (Time.time - lastPress < 0.4f) return; // anti double appui
            lastPress = Time.time;
            if (cap != null) StartCoroutine(Animate());
            if (dispatcher != null) dispatcher.Request(kind);
        }

        IEnumerator Animate()
        {
            cap.localPosition = capRest + cap.parent.InverseTransformDirection(transform.forward) * pressDepth;
            yield return new WaitForSeconds(0.15f);
            cap.localPosition = capRest;
        }
    }
}
