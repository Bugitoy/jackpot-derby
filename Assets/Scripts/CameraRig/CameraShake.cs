using UnityEngine;

/// <summary>Small screen shake. Anything can call CameraShake.Add(amount); it decays quickly.</summary>
[RequireComponent(typeof(ChaseCamera))]
public class CameraShake : MonoBehaviour
{
    [SerializeField] float maxOffset = 1.2f;
    [SerializeField] float decay = 2.5f;
    [SerializeField] float frequency = 28f;

    static CameraShake instance;
    ChaseCamera chase;
    float trauma;

    void Awake()
    {
        instance = this;
        chase = GetComponent<ChaseCamera>();
    }

    /// <summary>0..1, stacked and clamped. 0.2 = light bump, 0.6 = big hit.</summary>
    public static void Add(float amount)
    {
        if (instance != null) instance.trauma = Mathf.Clamp01(instance.trauma + amount);
    }

    void Update()
    {
        trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
        float s = trauma * trauma * maxOffset;
        float t = Time.time * frequency;
        chase.ShakeOffset = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 0.6f, Mathf.PerlinNoise(t, 0.7f) - 0.5f) * 2f * s;
    }
}
