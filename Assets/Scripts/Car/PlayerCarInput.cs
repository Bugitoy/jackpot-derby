using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Keyboard driving: W/S or Up/Down for throttle, A/D or Left/Right to steer.</summary>
public class PlayerCarInput : CarInputSource
{
    [Tooltip("How fast the steering moves toward full lock (per second). Lower = smoother, higher = twitchier.")]
    [SerializeField] float steerSpeed = 8f;
    [Tooltip("How fast steering re-centres when you let go (per second).")]
    [SerializeField] float steerReturnSpeed = 12f;

    float steer;
    float throttle;

    public override float Throttle => throttle;
    public override float Steer => steer;

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) { throttle = 0f; steer = 0f; return; }

        float t = 0f;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) t += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) t -= 1f;
        throttle = t;

        float target = 0f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) target -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) target += 1f;

        float rate = Mathf.Approximately(target, 0f) || Mathf.Sign(target) != Mathf.Sign(steer)
            ? steerReturnSpeed : steerSpeed;
        steer = Mathf.MoveTowards(steer, target, rate * Time.deltaTime);
    }
}
