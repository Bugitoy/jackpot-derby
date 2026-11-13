using System;
using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Dice, Clover }

/// <summary>
/// Drive through it to collect. Dice: re-rolls your number to a random 1-99 and unlocks it (a gamble).
/// Clover: +30 car health.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> Active = new List<Pickup>();
    /// <summary>(pickup, car that grabbed it, message for the popup)</summary>
    public static event Action<Pickup, GameObject, string> AnyCollected;

    [SerializeField] PickupKind kind;
    [SerializeField] float cloverHeal = 30f;
    [SerializeField] Transform visual;
    [SerializeField] float spinSpeed = 90f;

    public PickupKind Kind => kind;
    float age;
    Vector3 visualBase;

    void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        if (visual != null) visualBase = visual.localPosition;
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Update()
    {
        age += Time.deltaTime;
        if (visual == null) return;
        float pop = age < 0.35f ? Mathf.Sin(age / 0.35f * Mathf.PI * 0.5f) * (1f + 0.3f * Mathf.Sin(age / 0.35f * Mathf.PI)) : 1f;
        visual.localScale = Vector3.one * Mathf.Max(0.01f, pop);
        visual.localPosition = visualBase + Vector3.up * (0.4f + Mathf.Sin(age * 2.5f) * 0.35f);
        visual.localRotation = Quaternion.Euler(kind == PickupKind.Dice ? age * 40f : 0f, age * spinSpeed, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var car = other.attachedRigidbody.gameObject;
        var health = car.GetComponent<CarHealth>();
        if (health == null || !health.IsAlive) return;

        string msg;
        if (kind == PickupKind.Dice)
        {
            var odo = car.GetComponent<Odometer>();
            if (odo == null) return;
            odo.SetNumber(UnityEngine.Random.Range(1, 100), true);
            msg = "DICE! NEW NUMBER " + odo.Number;
        }
        else
        {
            health.Heal(cloverHeal);
            msg = "LUCKY CLOVER! +" + Mathf.RoundToInt(cloverHeal) + " HP";
        }

        if (FxLibrary.I != null)
        {
            Color c = kind == PickupKind.Dice ? new Color(1f, 1f, 1f) : new Color(0.4f, 1f, 0.45f);
            FxLibrary.I.HitPuff(transform.position + Vector3.up * 1.2f, c, 1.4f);
        }
        AnyCollected?.Invoke(this, car, msg);
        Destroy(gameObject);
    }
}
