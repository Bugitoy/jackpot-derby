using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Small frame-rate readout in the corner (F1 toggles). Used to check WebGL performance.
/// Shown by default in the Editor and Development builds; hidden (F1 still works) in the release build.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    [SerializeField] bool visible = true;
    float smoothed = 60f;

    void Awake() { if (!Application.isEditor && !Debug.isDebugBuild) visible = false; }
    GUIStyle style;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
        if (Time.unscaledDeltaTime > 0f)
            smoothed = Mathf.Lerp(smoothed, 1f / Time.unscaledDeltaTime, 0.05f);
    }

    void OnGUI()
    {
        if (!visible) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        style.normal.textColor = smoothed >= 50f ? Color.green : smoothed >= 30f ? Color.yellow : Color.red;
        GUI.Label(new Rect(10, 8, 200, 24), Mathf.RoundToInt(smoothed) + " FPS", style);
    }
}
