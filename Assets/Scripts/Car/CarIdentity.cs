using System.Collections.Generic;
using UnityEngine;

/// <summary>Who this car is: name, colour, and whether it's the player. Keeps a list of all cars.</summary>
public class CarIdentity : MonoBehaviour
{
    public string displayName = "Lucky 7";
    public Color color = new Color(0.15f, 0.70f, 0.70f);
    public bool isPlayer;

    public static readonly List<CarIdentity> All = new List<CarIdentity>();
    public static CarIdentity Player { get; private set; }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (isPlayer) Player = this;
    }

    void OnDisable()
    {
        All.Remove(this);
        if (Player == this) Player = null;
    }
}
