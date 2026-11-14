using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Springy hover/press scale plus hover and click sounds for menu buttons. Works while paused.</summary>
public class JuicyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public float hoverScale = 1.07f;
    public float pressScale = 0.94f;

    bool hovered, pressed;
    float scale = 1f, velocity;

    public void OnPointerEnter(PointerEventData e) { hovered = true; GameAudio.Hover(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnPointerClick(PointerEventData e) { GameAudio.Click(); velocity -= 3f; }

    void OnDisable() { hovered = pressed = false; scale = 1f; velocity = 0f; transform.localScale = Vector3.one; }

    void Update()
    {
        float target = pressed ? pressScale : hovered ? hoverScale : 1f;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        // Damped spring for a little overshoot.
        velocity += (target - scale) * 260f * dt;
        velocity *= Mathf.Exp(-18f * dt);
        scale += velocity * dt;
        transform.localScale = Vector3.one * scale;
    }
}
