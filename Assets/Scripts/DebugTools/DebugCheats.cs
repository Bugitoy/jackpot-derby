using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Testing shortcuts (turn off "cheatsEnabled" for the final build):
/// F2 show chest numbers · F3 set your number to the nearest chest's · F4 set it 3 away (CLOSE) · F5 spawn a chest now.
/// </summary>
public class DebugCheats : MonoBehaviour
{
    [SerializeField] bool cheatsEnabled = true;

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
    }
}
