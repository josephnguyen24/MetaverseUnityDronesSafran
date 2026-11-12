using UnityEngine;

namespace PyroScan
{
    /// <summary>
    /// Fait vaciller une lumière (foyer) ou la fait clignoter (gyrophare).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 6f;
        public float amplitude = 3f;
        public float speed = 6f;

        [Header("Mode gyrophare")]
        public bool blink;
        public float blinkPeriod = 0.8f;

        Light lightSource;

        void Awake() => lightSource = GetComponent<Light>();

        void Update()
        {
            if (blink)
            {
                lightSource.intensity = (Time.time % blinkPeriod) < blinkPeriod * 0.5f ? baseIntensity : 0f;
            }
            else
            {
                float noise = Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f;
                lightSource.intensity = Mathf.Max(0f, baseIntensity + noise * 2f * amplitude);
            }
        }
    }
}
