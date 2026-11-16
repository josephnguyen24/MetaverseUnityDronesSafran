using System;
using TMPro;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// État de la mission, affiché sur le pupitre :
    /// échec si le feu atteint une zone protégée (hameau, campement), réussite quand plus aucune case ne brûle.
    /// (La défaite par batterie vide arrivera avec le contrôleur de drone complet.)
    /// </summary>
    public class MissionStatus : MonoBehaviour
    {
        public enum State { EnCours, Reussie, Echec }

        [Serializable]
        public class Zone
        {
            public string name;
            public Vector3 center;
            public float radius = 50f;
        }

        public FireGrid grid;
        public WindSystem wind;
        public TextMeshPro display;
        public Zone[] protectedZones;

        public State Current { get; private set; } = State.EnCours;
        public float Elapsed { get; private set; }
        public event Action<State> StateChanged;

        string failReason;
        float checkTimer, displayTimer;

        void Update()
        {
            if (Current == State.EnCours)
            {
                Elapsed += Time.deltaTime;
                checkTimer += Time.deltaTime;
                if (checkTimer >= 0.5f)
                {
                    checkTimer = 0f;
                    foreach (Zone z in protectedZones)
                        if (grid.AnyBurningWithin(z.center, z.radius))
                        {
                            failReason = $"le feu a atteint {z.name}";
                            SetState(State.Echec);
                            break;
                        }
                    if (Current == State.EnCours && Elapsed > 5f && grid.BurningCount == 0)
                        SetState(State.Reussie);
                }
            }

            displayTimer += Time.deltaTime;
            if (displayTimer >= 0.25f && display != null)
            {
                displayTimer = 0f;
                display.text = BuildText();
            }
        }

        void SetState(State s)
        {
            Current = s;
            Debug.Log(s == State.Echec ? $"[PyroScan] ÉCHEC : {failReason}" : "[PyroScan] MISSION RÉUSSIE : feu éteint");
            StateChanged?.Invoke(s);
        }

        string BuildText()
        {
            int min = (int)(Elapsed / 60f), sec = (int)(Elapsed % 60f);
            string header = Current switch
            {
                State.EnCours => $"<b>MISSION EN COURS</b>  {min:00}:{sec:00}",
                State.Reussie => "<color=#5CFF6A><b>MISSION RÉUSSIE</b></color>",
                _ => $"<color=#FF5050><b>ÉCHEC</b></color>\n<size=70%>{failReason}</size>",
            };
            string windText = wind != null ? $"vers {WindSystem.CompassName(wind.Direction)}, {wind.SpeedKmh:0} km/h" : "-";
            return $"{header}\n<size=80%>Feu actif : {grid.BurningCount * 0.01f:0.00} ha\nSurface brûlée : {grid.BurntCount * 0.01f:0.00} ha\nVent : {windText}\n" +
                   "<color=#BBBBBB>Protéger le hameau et le campement</color></size>";
        }
    }
}
