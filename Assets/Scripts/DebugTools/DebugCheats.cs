using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Testing shortcuts, only in the Editor and Development builds (never in the release build):
/// F2 show chest numbers · F3 set your number to the nearest chest's · F4 set it 3 away (CLOSE) · F5 spawn a chest now.
/// </summary>
public class DebugCheats : MonoBehaviour
{
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("cheatsEnabled")] bool cheatsEnabledSetting = true;
    bool cheatsEnabled => cheatsEnabledSetting && (Application.isEditor || Debug.isDebugBuild);

    public static bool Active { get; private set; }
    public static bool ShowChestNumbers { get; private set; }

    void OnEnable() => Active = cheatsEnabled;
    void OnDisable() => Active = false;

    void Update()
    {
        Active = cheatsEnabled;
        var k = Keyboard.current;
        if (!cheatsEnabled || k == null) return;

        if (k.f2Key.wasPressedThisFrame) ShowChestNumbers = !ShowChestNumbers;

        var player = CarIdentity.Player;
        var odo = player != null ? player.GetComponent<Odometer>() : null;
        var chest = player != null ? Chest.Nearest(player.transform.position) : null;
        if (odo != null && chest != null)
        {
            if (k.f3Key.wasPressedThisFrame) { odo.SetNumber(chest.Number, false); odo.Lock(); odo.RevealHintAgainstNearestChest(); }
            if (k.f4Key.wasPressedThisFrame) { odo.SetNumber(chest.Number + 3, false); odo.Lock(); odo.RevealHintAgainstNearestChest(); }
        }
        if (k.f5Key.wasPressedThisFrame)
        {
            var spawner = FindAnyObjectByType<ChestSpawner>();
            if (spawner != null) spawner.SpawnOne();
        }

        // Combat testing.
        if (k.f6Key.wasPressedThisFrame)
            foreach (var h in CarHealth.Alive)
                if (h.Identity != null && !h.Identity.isPlayer) h.GetComponent<CarLoadout>().SetTier(BlasterTier.Small);
        if (player != null)
        {
            var ph = player.GetComponent<CarHealth>();
            if (k.f7Key.wasPressedThisFrame && ph != null) ph.TakeDamage(20f, null, DamageKind.Other, player.transform.position + Vector3.up * 2f);
            if (k.f8Key.wasPressedThisFrame) player.GetComponent<CarLoadout>().SetTier(BlasterTier.Jackpot);
            if (k.f9Key.wasPressedThisFrame)
            {
                CarHealth nearest = null;
                float best = float.MaxValue;
                foreach (var h in CarHealth.Alive)
                {
                    if (h == ph) continue;
                    float d = Vector3.Distance(h.transform.position, player.transform.position);
                    if (d < best) { best = d; nearest = h; }
                }
                if (nearest != null) nearest.TakeDamage(999f, player, DamageKind.Other, nearest.transform.position + Vector3.up * 2f);
            }
        }
    }
}
