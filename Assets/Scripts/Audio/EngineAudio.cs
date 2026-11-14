using UnityEngine;

/// <summary>
/// Engine loop for one car: pitch and volume follow speed and throttle, Turbo revs it up,
/// and it falls silent when the car is wrecked. Added to every car by GameAudio.
/// </summary>
public class EngineAudio : MonoBehaviour
{
    [SerializeField] float idlePitch = 0.8f;
    [SerializeField] float topPitch = 2.0f;
    [SerializeField] float turboPitch = 0.3f;
    [SerializeField] float playerVolume = 0.32f;
    [SerializeField] float enemyVolume = 0.5f;

    AudioSource source;
    CarMotor motor;
    CarInputSource input;
    CarHealth health;
    TurboAbility turbo;
    CarIdentity identity;
    float rev, pitchJitter;

    void Start()
    {
        motor = GetComponent<CarMotor>();
        input = GetComponent<CarInputSource>();
        health = GetComponent<CarHealth>();
        turbo = GetComponent<TurboAbility>();
        identity = GetComponent<CarIdentity>();
        var bank = GameAudio.I != null ? GameAudio.I.Bank : null;
        if (bank == null || bank.engineLoop == null) { enabled = false; return; }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = bank.engineLoop;
        source.loop = true;
        source.playOnAwake = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 6f;
        source.maxDistance = 55f;
        bool player = identity != null && identity.isPlayer;
        source.spatialBlend = player ? 0f : 1f;
        source.priority = player ? 32 : 96;
        source.time = Random.Range(0f, bank.engineLoop.length * 0.9f);
        source.volume = 0f;
        // Each car sounds slightly different.
        pitchJitter = player ? 1f : Random.Range(0.9f, 1.12f);
        source.Play();
    }

    void Update()
    {
        if (source == null) return;
        bool alive = health == null || health.IsAlive;
        bool running = alive && (motor == null || motor.InputEnabled || Mathf.Abs(motor.ForwardSpeed) > 0.5f);
        float top = motor != null && motor.MaxSpeed > 0f ? motor.MaxSpeed : 25f;
        float speed01 = motor != null ? Mathf.Clamp01(Mathf.Abs(motor.ForwardSpeed) / top) : 0f;
        float throttle = input != null ? Mathf.Abs(input.Throttle) : 0f;
        bool boosting = turbo != null && turbo.IsActive;

        // Revs lead the speed a little when you press the pedal, like a real engine.
        float wantRev = Mathf.Clamp01(speed01 * 0.85f + throttle * 0.2f) + (boosting ? turboPitch : 0f);
        rev = Mathf.Lerp(rev, wantRev, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float sfx = GameAudio.I != null ? GameAudio.I.SfxVolume : 1f;
        bool player = identity != null && identity.isPlayer;
        float vol = (player ? playerVolume : enemyVolume) * (0.55f + 0.45f * Mathf.Max(rev, throttle)) * sfx;
        source.volume = Mathf.MoveTowards(source.volume, running ? vol : 0f, Time.deltaTime * (running ? 2f : 1.2f));
        source.pitch = Mathf.Lerp(idlePitch, topPitch, rev) * pitchJitter;
    }
}
