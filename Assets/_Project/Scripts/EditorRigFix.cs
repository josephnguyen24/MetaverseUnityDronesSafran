using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace PyroScan
{
    /// <summary>
    /// Correctif pour le simulateur XR dans l'éditeur (sans casque) :
    /// le simulateur laisse la caméra au ras du sol (hauteur 0), ce qui la place derrière la table.
    /// Ce script remonte le "Camera Offset" du rig à hauteur des yeux et garde le joueur à sa position de départ.
    /// Il ne fait rien quand un vrai casque est actif (Quest, Quest Link).
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class EditorRigFix : MonoBehaviour
    {
        public float eyeHeight = 1.6f;
        [Tooltip("Durée pendant laquelle le rig est maintenu à sa position de départ (s)")]
        public float holdDuration = 1.5f;

        XROrigin origin;
        Vector3 startPos;
        Quaternion startRot;
        float elapsed;

        void Awake()
        {
            origin = GetComponent<XROrigin>();
            startPos = transform.position;
            startRot = transform.rotation;
        }

        void Start()
        {
            if (!Application.isEditor || XRSettings.isDeviceActive) enabled = false;
        }

        void LateUpdate()
        {
            // Hauteur des yeux : on place directement l'objet Camera Offset.
            if (origin.CameraFloorOffsetObject != null)
            {
                Transform offset = origin.CameraFloorOffsetObject.transform;
                Vector3 p = offset.localPosition;
                if (Mathf.Abs(p.y - eyeHeight) > 0.001f)
                    offset.localPosition = new Vector3(p.x, eyeHeight, p.z);
            }

            if (elapsed <= holdDuration)
            {
                elapsed += Time.deltaTime;
                transform.SetPositionAndRotation(startPos, startRot);
            }
        }
    }
}
