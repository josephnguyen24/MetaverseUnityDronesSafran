using System;
using System.Collections.Generic;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Liste unique des marqueurs rouges, enregistrés en position réelle sur le terrain.
    /// Chaque marqueur est un objet vide placé dans la vallée ; les cartes l'affichent via MarkerViews.
    /// Plus tard (étape 6), les avions prendront leurs cibles dans cette liste.
    /// </summary>
    public class MarkerService : MonoBehaviour
    {
        public static MarkerService Instance { get; private set; }

        public int maxMarkers = 10;

        public event Action<Transform> MarkerAdded;
        public event Action<Transform> MarkerRemoved;

        readonly List<Transform> markers = new List<Transform>();
        int counter;

        public IReadOnlyList<Transform> Markers => markers;

        /// <summary>Dernière balise posée (cible des avions), ou null.</summary>
        public Transform Latest => markers.Count > 0 ? markers[markers.Count - 1] : null;

        void Awake() => Instance = this;

        public Transform Add(Vector3 worldPoint)
        {
            if (markers.Count >= maxMarkers)
            {
                Debug.Log($"[PyroScan] Maximum de {maxMarkers} marqueurs atteint : enlève-en un avant d'en poser un autre.");
                return null;
            }
            var go = new GameObject($"Marqueur rouge {++counter}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPoint;
            markers.Add(go.transform);
            MarkerAdded?.Invoke(go.transform);
            Debug.Log($"[PyroScan] Marqueur posé en {worldPoint} ({markers.Count}/{maxMarkers})");
            return go.transform;
        }

        public bool Remove(Transform marker)
        {
            if (marker == null || !markers.Remove(marker)) return false;
            MarkerRemoved?.Invoke(marker);
            Destroy(marker.gameObject);
            Debug.Log($"[PyroScan] Marqueur enlevé ({markers.Count}/{maxMarkers})");
            return true;
        }

        /// <summary>Marqueur le plus proche (distance horizontale) dans un rayon donné, ou null.</summary>
        public Transform FindNearest(Vector3 worldPoint, float maxDistance)
        {
            Transform best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (Transform m in markers)
            {
                Vector3 d = m.position - worldPoint;
                d.y = 0f;
                if (d.sqrMagnitude <= bestSqr) { bestSqr = d.sqrMagnitude; best = m; }
            }
            return best;
        }
    }
}
