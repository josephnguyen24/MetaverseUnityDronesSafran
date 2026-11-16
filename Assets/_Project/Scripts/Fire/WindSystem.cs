using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Vent global : direction (vers laquelle il souffle, en degrés depuis le nord, sens horaire)
    /// et force 0..1 qui varie par rafales. Utilisé par le feu (et plus tard par le drone et la batterie).
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        public static WindSystem Instance { get; private set; }

        [Tooltip("Direction vers laquelle souffle le vent (0 = nord, 90 = est)")]
        public float baseDirection = 40f;
        [Range(0f, 1f)] public float baseStrength = 0.55f;
        [Tooltip("Variation de direction des rafales (degrés)")]
        public float directionVariation = 20f;
        [Tooltip("Variation de force des rafales")]
        public float strengthVariation = 0.25f;
        public float gustSpeed = 0.05f;

        public float Direction { get; private set; }
        public float Strength { get; private set; }
        public float SpeedKmh => 10f + 45f * Strength;
        public Vector3 Vector => new Vector3(Mathf.Sin(Direction * Mathf.Deg2Rad), 0f, Mathf.Cos(Direction * Mathf.Deg2Rad));

        void Awake()
        {
            Instance = this;
            Direction = baseDirection;
            Strength = baseStrength;
        }

        void Update()
        {
            float t = Time.time * gustSpeed;
            Direction = baseDirection + (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * directionVariation;
            Strength = Mathf.Clamp01(baseStrength + (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * strengthVariation);
        }

        public static string CompassName(float deg)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
            int i = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8;
            return names[i];
        }
    }
}
