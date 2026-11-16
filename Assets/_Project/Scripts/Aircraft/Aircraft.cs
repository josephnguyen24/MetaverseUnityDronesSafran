using System.Collections;
using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Vol simple d'un avion au-dessus du terrain : ligne droite vers un point, altitude qui suit le relief
    /// (avec anticipation), virage et inclinaison lissés. Sert au Canadair et à l'avion de retardant.
    /// </summary>
    public class Aircraft : MonoBehaviour
    {
        public float speed = 55f;
        public float climbSpeed = 25f;
        public float turnRate = 70f;          // degrés/s
        public float lookAhead = 120f;
        [Tooltip("Particules du largage (eau ou retardant)")]
        public ParticleSystem dropFx;

        float bank;

        public static float GroundY(Vector3 p)
        {
            var f = MapFrame.Main;
            return f != null ? f.SampleHeight(p) : 0f;
        }

        /// <summary>Altitude de sécurité au point p dans la direction dir (relief sous l'avion et devant lui).</summary>
        public float SafeY(Vector3 p, Vector3 dir, float height)
        {
            float g = GroundY(p);
            for (int i = 1; i <= 3; i++) g = Mathf.Max(g, GroundY(p + dir * (lookAhead * i / 3f)));
            return g + height;
        }

        /// <summary>Vole jusqu'à 'target' (on ignore son Y) à 'height' m au-dessus du relief. fixedY : altitude imposée (écopage).</summary>
        public IEnumerator FlyTo(Vector3 target, float height, float arriveDistance = 25f, float? fixedY = null)
        {
            while (true)
            {
                Vector3 pos = transform.position;
                Vector3 to = target - pos; to.y = 0f;
                if (to.magnitude <= arriveDistance) yield break;
                Step(to.normalized, height, fixedY);
                yield return null;
            }
        }

        /// <summary>Avance tout droit dans une direction pendant 'seconds'.</summary>
        public IEnumerator FlyStraight(Vector3 dir, float seconds, float height)
        {
            dir.y = 0f; dir.Normalize();
            for (float t = 0f; t < seconds; t += Time.deltaTime) { Step(dir, height, null); yield return null; }
        }

        /// <summary>Un pas de vol vers la direction voulue (virage limité).</summary>
        public void Step(Vector3 wantedDir, float height, float? fixedY)
        {
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = wantedDir;
            fwd.Normalize();
            float angle = Vector3.SignedAngle(fwd, wantedDir, Vector3.up);
            float turn = Mathf.Clamp(angle, -turnRate * Time.deltaTime, turnRate * Time.deltaTime);
            Vector3 dir = Quaternion.Euler(0f, turn, 0f) * fwd;

            Vector3 pos = transform.position + dir * speed * Time.deltaTime;
            float wantY = fixedY ?? SafeY(pos, dir, height);
            pos.y = Mathf.MoveTowards(transform.position.y, wantY, climbSpeed * Time.deltaTime);
            transform.position = pos;

            float targetBank = -Mathf.Clamp(angle, -35f, 35f);
            bank = Mathf.Lerp(bank, targetBank, 3f * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
        }

        /// <summary>Tourne en rond autour d'un point (attente).</summary>
        public void Orbit(Vector3 center, float radius, float height)
        {
            Vector3 d = transform.position - center; d.y = 0f;
            if (d.sqrMagnitude < 1f) d = Vector3.forward * radius;
            Vector3 tangent = Vector3.Cross(Vector3.up, d.normalized);       // sens anti-horaire
            float correction = (d.magnitude - radius) / radius;              // revient vers le cercle
            Vector3 want = (tangent - d.normalized * Mathf.Clamp(correction, -1f, 1f)).normalized;
            Step(want, height, null);
        }

        public void SetVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (dropFx == null || !r.transform.IsChildOf(dropFx.transform)) r.enabled = visible;
        }
    }
}
