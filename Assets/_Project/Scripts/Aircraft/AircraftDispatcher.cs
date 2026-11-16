using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Étape 6b : moyens aériens déclenchés depuis le pupitre, vers la dernière balise posée.
    /// - Canadair : largue de l'eau sur un cercle de 40 m (feu éteint, sol mouillé 60 s), écope au lac, recharge ~60 s. Largages illimités.
    /// - Avion de retardant : ligne de 120 m x 30 m en travers du vent (barrière 4 min). 3 largages par mission, ~90 s entre deux.
    /// </summary>
    public class AircraftDispatcher : MonoBehaviour
    {
        public enum Kind { Canadair, Retardant }

        public MarkerService markers;
        public FireGrid grid;
        public WindSystem wind;

        [Header("Canadair")]
        public Aircraft canadair;
        public Vector3 lakeCenter;
        public float lakeRadius = 55f;
        public float lakeWaterY = -121f;
        public float canadairCruise = 70f;
        public float canadairDropHeight = 35f;
        public float waterRadius = 20f;        // cercle de 40 m
        public float wetSeconds = 60f;
        public float canadairReload = 60f;

        [Header("Avion de retardant")]
        public Aircraft retardantPlane;
        public Vector3 retardantBase = new Vector3(0f, 150f, -700f);
        public float retardantCruise = 60f;
        public float stripLength = 120f;
        public float stripWidth = 30f;
        public float retardantSeconds = 240f;
        public float retardantCooldown = 90f;
        public int retardantDrops = 3;

        [Header("Pupitre")]
        public TextMeshPro canadairStatus;
        public TextMeshPro retardantStatus;
        public TextMeshPro radio;
        public int radioLines = 5;

        string canadairState = "", retardantState = "";
        bool canadairBusy, retardantBusy;
        float canadairReadyAt, retardantReadyAt;
        int retardantLeft;
        readonly List<string> radioLog = new List<string>();
        float displayTimer;

        void Start()
        {
            retardantLeft = retardantDrops;
            if (canadair != null)
            {
                canadair.transform.position = lakeCenter + new Vector3(lakeRadius + 120f, 0f, 0f);
                canadair.transform.position = new Vector3(canadair.transform.position.x,
                    canadair.SafeY(canadair.transform.position, Vector3.forward, canadairCruise), canadair.transform.position.z);
            }
            if (retardantPlane != null)
            {
                retardantPlane.transform.position = retardantBase;
                retardantPlane.SetVisible(false);
            }
            Radio("Moyens aériens en attente. Posez une balise avec le drone, puis appelez un avion.");
            Refresh();
        }

        void Update()
        {
            // Canadair au repos : il tourne au-dessus du lac.
            if (canadair != null && !canadairBusy)
                canadair.Orbit(lakeCenter, lakeRadius + 120f, canadairCruise);

            displayTimer += Time.deltaTime;
            if (displayTimer >= 0.25f) { displayTimer = 0f; Refresh(); }
        }

        // ---------------------------------------------------------------- Appels depuis le pupitre
        public void Request(Kind kind)
        {
            Transform beacon = markers != null ? markers.Latest : null;
            string name = kind == Kind.Canadair ? "Canadair" : "Avion de retardant";
            if (beacon == null) { Radio($"{name} : aucune balise. Posez une balise avec le drone (gâchette gauche)."); return; }

            if (kind == Kind.Canadair)
            {
                if (canadair == null) return;
                if (canadairBusy) { Radio("Canadair : déjà en mission."); return; }
                if (Time.time < canadairReadyAt) { Radio($"Canadair : en recharge, disponible dans {Mathf.CeilToInt(canadairReadyAt - Time.time)} s."); return; }
                StartCoroutine(CanadairMission(beacon.position, beacon.name));
            }
            else
            {
                if (retardantPlane == null) return;
                if (retardantLeft <= 0) { Radio("Retardant : plus de largage disponible pour cette mission."); return; }
                if (retardantBusy) { Radio("Retardant : déjà en vol."); return; }
                if (Time.time < retardantReadyAt) { Radio($"Retardant : prochain avion dans {Mathf.CeilToInt(retardantReadyAt - Time.time)} s."); return; }
                StartCoroutine(RetardantMission(beacon.position, beacon.name));
            }
        }

        // ---------------------------------------------------------------- Canadair
        IEnumerator CanadairMission(Vector3 target, string beaconName)
        {
            canadairBusy = true;
            Aircraft a = canadair;
            Radio($"Canadair : bien reçu, en route vers {Short(beaconName)}.");

            // Approche : on se place 400 m avant la cible, dans l'axe lac -> cible, puis passage bas au-dessus.
            Vector3 axis = Flat(target - lakeCenter).normalized;
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.forward;
            canadairState = "EN VOL vers la balise";
            yield return a.FlyTo(target - axis * 400f, canadairCruise, 60f);
            yield return a.FlyTo(target - axis * 120f, canadairDropHeight, 30f);
            canadairState = "LARGAGE";
            yield return a.FlyTo(target - axis * 30f, canadairDropHeight, 10f);
            if (a.dropFx != null) a.dropFx.Play();
            yield return a.FlyTo(target, canadairDropHeight, 10f);
            int n = grid != null ? grid.Extinguish(target, waterRadius, wetSeconds) : 0;
            Radio(n > 0 ? $"Canadair : largage effectué, {n * 0.01f:0.00} ha éteints. Retour au lac pour écoper."
                        : "Canadair : largage effectué (pas de flammes sous la balise). Retour au lac.");
            canadairReadyAt = Time.time + canadairReload;
            yield return a.FlyStraight(a.transform.forward, 3f, canadairCruise);
            if (a.dropFx != null) a.dropFx.Stop();

            // Écopage : passage au ras de l'eau à travers le lac.
            canadairState = "RETOUR au lac";
            Vector3 dirToLake = Flat(lakeCenter - a.transform.position).normalized;
            Vector3 entry = lakeCenter - dirToLake * (lakeRadius + 250f);
            yield return a.FlyTo(entry, canadairCruise, 50f);
            canadairState = "ÉCOPAGE";
            Vector3 skimDir = Flat(lakeCenter - a.transform.position).normalized;
            yield return a.FlyTo(lakeCenter - skimDir * (lakeRadius * 0.8f), canadairCruise, 15f, lakeWaterY + 30f);
            yield return a.FlyTo(lakeCenter + skimDir * (lakeRadius * 0.8f), canadairCruise, 15f, lakeWaterY + 1.5f);
            Radio("Canadair : écopage terminé, remplissage en cours.");
            canadairBusy = false; // reprend son attente au-dessus du lac
        }

        // ---------------------------------------------------------------- Retardant
        IEnumerator RetardantMission(Vector3 target, string beaconName)
        {
            retardantBusy = true;
            Aircraft a = retardantPlane;
            Vector3 windDir = wind != null ? Flat(wind.Vector).normalized : Vector3.forward;
            if (windDir.sqrMagnitude < 0.01f) windDir = Vector3.forward;
            Vector3 strip = new Vector3(windDir.z, 0f, -windDir.x);            // en travers du vent
            if (Vector3.Dot(strip, Flat(target - retardantBase)) < 0f) strip = -strip; // arrive du côté de la base
            Radio($"Retardant : avion parti, ligne de {stripLength:0} m en travers du vent sur {Short(beaconName)}.");

            Vector3 start = target - strip * 900f;
            a.transform.position = new Vector3(start.x, a.SafeY(start, strip, retardantCruise) + 40f, start.z);
            a.transform.rotation = Quaternion.LookRotation(strip);
            a.SetVisible(true);

            retardantState = "EN VOL vers la balise";
            yield return a.FlyTo(target - strip * (stripLength * 0.5f + 60f), retardantCruise, 20f);
            retardantState = "LARGAGE";
            if (a.dropFx != null) a.dropFx.Play();
            yield return a.FlyTo(target, retardantCruise, 8f);
            if (grid != null) grid.ApplyRetardant(target, strip, stripLength, stripWidth, retardantSeconds);
            yield return a.FlyStraight(strip, stripLength * 0.5f / a.speed, retardantCruise);
            if (a.dropFx != null) a.dropFx.Stop();
            retardantLeft--;
            retardantReadyAt = Time.time + retardantCooldown;
            Radio(retardantLeft > 0
                ? $"Retardant : ligne posée, tient {retardantSeconds / 60f:0} min. Prochain avion dans {retardantCooldown:0} s ({retardantLeft}/{retardantDrops})."
                : $"Retardant : ligne posée, tient {retardantSeconds / 60f:0} min. Plus de largage pour cette mission.");

            retardantState = "RETOUR base";
            yield return a.FlyStraight(strip, 12f, retardantCruise + 30f);
            a.SetVisible(false);
            a.transform.position = retardantBase;
            retardantBusy = false;
        }

        // ---------------------------------------------------------------- Affichage
        void Refresh()
        {
            if (canadairStatus != null)
            {
                string s;
                if (canadairBusy) s = $"<color=#FFC34D>{canadairState}</color>";
                else if (Time.time < canadairReadyAt) s = $"<color=#FFC34D>RECHARGE {Mathf.CeilToInt(canadairReadyAt - Time.time)} s</color>";
                else s = "<color=#5CFF6A>PRÊT</color>";
                canadairStatus.text = s;
            }
            if (retardantStatus != null)
            {
                string s;
                if (retardantBusy) s = $"<color=#FFC34D>{retardantState}</color>";
                else if (retardantLeft <= 0) s = "<color=#AAAAAA>TERMINÉ (0/" + retardantDrops + ")</color>";
                else if (Time.time < retardantReadyAt) s = $"<color=#FFC34D>DISPO DANS {Mathf.CeilToInt(retardantReadyAt - Time.time)} s</color>";
                else s = "<color=#5CFF6A>PRÊT</color>";
                retardantStatus.text = $"{s}\n<size=80%>{retardantLeft}/{retardantDrops} largages</size>";
            }
        }

        public void Radio(string message)
        {
            float t = Time.timeSinceLevelLoad;
            radioLog.Add($"<color=#8FD3FF>{(int)(t / 60f):00}:{(int)(t % 60f):00}</color> {message}");
            while (radioLog.Count > radioLines) radioLog.RemoveAt(0);
            if (radio != null) radio.text = "<b>RADIO</b>\n<size=85%>" + string.Join("\n", radioLog) + "</size>";
            Debug.Log("[PyroScan] Radio : " + message);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static string Short(string beaconName) => beaconName.Replace("Marqueur rouge", "la balise");
    }
}
