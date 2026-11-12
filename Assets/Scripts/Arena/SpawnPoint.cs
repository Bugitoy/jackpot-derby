using UnityEngine;

/// <summary>Where a car starts (inside its garage, facing out).</summary>
public class SpawnPoint : MonoBehaviour
{
    public string carName = "Lucky 7";
    public Color color = Color.white;

    void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(2.4f, 2f, 4.2f));
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 4f);
    }
}
