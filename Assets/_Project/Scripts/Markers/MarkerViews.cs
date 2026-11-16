using System.Collections.Generic;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Affiche chaque marqueur de MarkerService sur toutes les cartes qui acceptent les marqueurs :
    /// une punaise rouge sur les cartes plates (carte murale), une colonne rouge sur la minimap.
    /// </summary>
    public class MarkerViews : MonoBehaviour
    {
        public MarkerService service;
        public MapSurface[] surfaces;
        public Material markerMaterial;

        [Header("Fumigène dans la vallée")]
        public bool worldBeacons = true;
        public Material smokeMaterial;

        readonly Dictionary<Transform, List<GameObject>> views = new Dictionary<Transform, List<GameObject>>();

        void OnEnable()
        {
            if (service == null) service = GetComponent<MarkerService>();
            service.MarkerAdded += OnAdded;
            service.MarkerRemoved += OnRemoved;
        }

        void OnDisable()
        {
            service.MarkerAdded -= OnAdded;
            service.MarkerRemoved -= OnRemoved;
        }

        void OnAdded(Transform marker)
        {
            var list = new List<GameObject>();
            foreach (MapSurface s in surfaces)
                if (s != null && s.allowMarkers) list.Add(CreateIcon(marker, s));
            if (worldBeacons) list.Add(CreateBeacon(marker));
            views[marker] = list;
        }

        void OnRemoved(Transform marker)
        {
            if (!views.TryGetValue(marker, out List<GameObject> list)) return;
            foreach (GameObject go in list) Destroy(go);
            views.Remove(marker);
        }

        GameObject CreateIcon(Transform marker, MapSurface surface)
        {
            var root = new GameObject($"{marker.name} - {surface.name}");
            root.transform.SetParent(transform, false);
            root.transform.rotation = surface.transform.rotation;

            if (surface is FlatMapSurface)
            {
                // Punaise : tête ronde collée à la carte.
                Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.035f);
            }
            else
            {
                // Colonne debout sur le relief, avec une boule au sommet.
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.01f));
                Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.065f, 0f), Vector3.one * 0.022f);
            }

            var icon = root.AddComponent<MapIcon>();
            icon.target = marker;
            icon.surface = surface;
            return root;
        }

        /// <summary>Fumigène rouge planté sur le terrain : poteau lumineux, lueur et fumée rouge.</summary>
        GameObject CreateBeacon(Transform marker)
        {
            var root = new GameObject($"{marker.name} - fumigène");
            root.transform.SetParent(marker, false);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.6f, 2f, 0.6f));

            var light = new GameObject("Lueur").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 5f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.1f, 0.1f);
            light.range = 35f;
            light.intensity = 6f;

            var smokeGo = new GameObject("Fumée rouge");
            smokeGo.transform.SetParent(root.transform, false);
            smokeGo.transform.localPosition = new Vector3(0f, 4f, 0f);
            smokeGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = smokeGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 4f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startColor = new Color(0.9f, 0.15f, 0.12f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (smokeMaterial != null) smokeGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
            ps.Play();
            return root;
        }

        void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (markerMaterial != null) go.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        }
    }
}
