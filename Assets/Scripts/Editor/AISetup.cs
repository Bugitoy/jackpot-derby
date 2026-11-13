using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI setup: the three personality assets and the NavMesh bake for the arena
/// (agent sized for a car; cars themselves are left out of the bake).
/// </summary>
public static class AISetup
{
    public const string PersonalityFolder = "Assets/Data/AI";

    public static AIPersonality Personality(string prefabName)
    {
        ModelPrefabUtil.EnsureFolder(PersonalityFolder);
        string path = $"{PersonalityFolder}/{prefabName.Replace("Enemy", "")}.asset";
        var p = AssetDatabase.LoadAssetAtPath<AIPersonality>(path);
        if (p != null) return p;
        p = ScriptableObject.CreateInstance<AIPersonality>();
        switch (prefabName)
        {
            case "EnemyLucky8": // aggressive hunter, always goes for chests
                p.aggression = 0.9f; p.huntRadius = 48f; p.ramDistance = 14f; p.ramWeakHealth = 0.4f; p.sideRamLove = 0.2f;
                p.fleeHealth = 0.2f; p.riskTaking = 0.35f; p.chestGreed = 1f; p.numberSkill = 0.7f;
                p.hintPause = new Vector2(0.5f, 1.0f); p.turboEagerness = 0.7f; p.cruiseSpeed = 0.9f;
                break;
            case "EnemyBlackCat13": // sneaky ambusher, loves side rams
                p.aggression = 0.6f; p.huntRadius = 38f; p.ramDistance = 18f; p.ramWeakHealth = 0.35f; p.sideRamLove = 1f;
                p.fleeHealth = 0.35f; p.riskTaking = 0.15f; p.chestGreed = 0.5f; p.numberSkill = 0.6f;
                p.hintPause = new Vector2(0.7f, 1.4f); p.ambusher = true; p.turboEagerness = 0.6f; p.cruiseSpeed = 0.7f;
                break;
            default: // Double Dice 11: chaotic gambler
                p.aggression = 0.55f; p.huntRadius = 40f; p.ramDistance = 15f; p.ramWeakHealth = 0.3f; p.sideRamLove = 0.4f;
                p.fleeHealth = 0.25f; p.riskTaking = 0.9f; p.chestGreed = 0.7f; p.numberSkill = 0.4f;
                p.hintPause = new Vector2(0.3f, 1.6f); p.chaos = 0.6f; p.turboEagerness = 0.9f; p.cruiseSpeed = 0.85f;
                break;
        }
        AssetDatabase.CreateAsset(p, path);
        return p;
    }

    [MenuItem("Jackpot Derby/Bake AI NavMesh")]
    public static void BakeNavMesh()
    {
        // Size the default agent like a car (about 2.4 wide).
        var settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (settingsAsset.Length > 0)
        {
            var so = new SerializedObject(settingsAsset[0]);
            var settings = so.FindProperty("m_Settings");
            if (settings != null && settings.arraySize > 0)
            {
                var agent = settings.GetArrayElementAtIndex(0);
                agent.FindPropertyRelative("agentRadius").floatValue = 1.6f;
                agent.FindPropertyRelative("agentHeight").floatValue = 2.5f;
                agent.FindPropertyRelative("agentClimb").floatValue = 0.4f;
                agent.FindPropertyRelative("agentSlope").floatValue = 30f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        var grid = Object.FindAnyObjectByType<ArenaGrid>();
        if (grid == null) { Debug.LogError("[AISetup] No Arena in the scene."); return; }
        var surface = grid.GetComponent<NavMeshSurface>();
        if (surface == null) surface = grid.gameObject.AddComponent<NavMeshSurface>();
        surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        int carLayer = LayerMask.NameToLayer("Car");
        surface.layerMask = carLayer >= 0 ? ~(1 << carLayer) : ~0;
        surface.BuildNavMesh();

        ModelPrefabUtil.EnsureFolder("Assets/Scenes/Arena");
        const string path = "Assets/Scenes/Arena/NavMesh.asset";
        var old = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (old != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        Debug.Log("[AISetup] NavMesh baked.");
    }
}
