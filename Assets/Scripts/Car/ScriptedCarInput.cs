using UnityEngine;

/// <summary>Input set from code (tests, cutscenes, and the base the AI driver writes into).</summary>
public class ScriptedCarInput : CarInputSource
{
    [Range(-1f, 1f)] public float throttle;
    [Range(-1f, 1f)] public float steer;

    public override float Throttle => throttle;
    public override float Steer => steer;
}
