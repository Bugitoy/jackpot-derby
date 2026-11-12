using UnityEngine;

public enum HintKind { None, Lucky, Close, TooHigh, TooLow }

/// <summary>
/// The answer you get when you compare your number with a chest's hidden number.
/// exact = LUCKY!, within ±5 = CLOSE, otherwise TOO HIGH / TOO LOW ("WAY" if more than 25 off).
/// </summary>
public struct HintResult
{
    public const int CloseRange = 5;
    public const int WayRange = 25;

    public HintKind kind;
    /// <summary>Your number minus the chest's number.</summary>
    public int difference;
    public bool way;

    public static HintResult NoChest => new HintResult { kind = HintKind.None };

    public static HintResult Evaluate(int number, int target)
    {
        int d = number - target;
        var r = new HintResult { difference = d };
        if (d == 0) r.kind = HintKind.Lucky;
        else if (Mathf.Abs(d) <= CloseRange) r.kind = HintKind.Close;
        else
        {
            r.kind = d > 0 ? HintKind.TooHigh : HintKind.TooLow;
            r.way = Mathf.Abs(d) > WayRange;
        }
        return r;
    }

    public bool OpensChest => kind == HintKind.Lucky || kind == HintKind.Close;

    public string Text
    {
        get
        {
            switch (kind)
            {
                case HintKind.Lucky: return "LUCKY!";
                case HintKind.Close: return "CLOSE";
                case HintKind.TooHigh: return way ? "WAY TOO HIGH" : "TOO HIGH";
                case HintKind.TooLow: return way ? "WAY TOO LOW" : "TOO LOW";
                default: return "NO CHEST YET";
            }
        }
    }

    public Color Color
    {
        get
        {
            switch (kind)
            {
                case HintKind.Lucky: return new Color(1f, 0.82f, 0.15f);
                case HintKind.Close: return new Color(0.35f, 0.95f, 0.40f);
                case HintKind.TooHigh: return new Color(1f, 0.45f, 0.30f);
                case HintKind.TooLow: return new Color(0.40f, 0.70f, 1f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        }
    }
}
