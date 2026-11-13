using UnityEngine;

/// <summary>
/// Tunable character for an AI driver. Lucky 8 = aggressive hunter who always goes for chests;
/// Black Cat 13 = sneaky corner ambusher who loves side rams; Double Dice 11 = chaotic gambler.
/// </summary>
[CreateAssetMenu(menuName = "Jackpot Derby/AI Personality")]
public class AIPersonality : ScriptableObject
{
    [Header("Fighting")]
    [Tooltip("0 = timid, 1 = hunts anything it can see.")]
    [Range(0f, 1f)] public float aggression = 0.6f;
    [Tooltip("How far away it notices cars to hunt.")]
    public float huntRadius = 40f;
    [Tooltip("Rams when this close to a weak or side-on target.")]
    public float ramDistance = 14f;
    [Tooltip("Targets below this health fraction count as 'weak' (worth ramming).")]
    [Range(0f, 1f)] public float ramWeakHealth = 0.35f;
    [Tooltip("Extra keenness to ram cars that show their side.")]
    [Range(0f, 1f)] public float sideRamLove = 0.3f;
    [Tooltip("Flee from armed cars when own health falls below this fraction.")]
    [Range(0f, 1f)] public float fleeHealth = 0.3f;
    [Tooltip("0 = always plays safe, 1 = ignores danger half the time.")]
    [Range(0f, 1f)] public float riskTaking = 0.2f;

    [Header("Chests")]
    [Tooltip("Chance to go for a chest even when it already has a blaster.")]
    [Range(0f, 1f)] public float chestGreed = 0.5f;
    [Tooltip("1 = perfect guessing search, 0 = sloppy guesses and badly judged stops.")]
    [Range(0f, 1f)] public float numberSkill = 0.6f;
    [Tooltip("Seconds it 'thinks' after each hint (lets the player win races).")]
    public Vector2 hintPause = new Vector2(0.5f, 1.2f);

    [Header("Style")]
    [Tooltip("Waits at street corners near enemies, then side-rams them.")]
    public bool ambusher;
    [Tooltip("Random changes of plan (Double Dice).")]
    [Range(0f, 1f)] public float chaos = 0f;
    [Tooltip("How readily it uses Turbo.")]
    [Range(0f, 1f)] public float turboEagerness = 0.5f;
    [Tooltip("Throttle when cruising around (0..1).")]
    [Range(0.3f, 1f)] public float cruiseSpeed = 0.75f;
}
