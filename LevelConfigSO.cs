using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class CharacterConfigBase
{
    public float spawnDelay;
    public Vector3 spawnPositionOffset;
    public ElementTypeOB7 requiredArrowElement;
}

[Serializable]
public class MinionConfig : CharacterConfigBase
{
    public GameObject prefab;
    public GameObject spawnPointPrefab;
    public TargetMovementType movementType;
    public GameObject movementAssetPrefab;
}

[Serializable]
public class BossConfig : CharacterConfigBase
{
    public GameObject prefab;
    public GameObject spawnPointPrefab;
    public List<GameObject> observationPathPrefabs = new List<GameObject>();
    public List<GameObject> escapePathPrefabs = new List<GameObject>();
}

[Serializable]
public class WaveData
{
    [Header("Wave Announcement")]
    [Tooltip("Short title shown to the player when this wave is announced. e.g. 'The Siege Begins'")]
    public string waveName = "";

    [Tooltip("Brief description shown during the pre-wave countdown. Leave empty for a default objective message.")]
    [TextArea(1, 3)]
    public string waveAnnouncementText = "";

    [Header("Wave Rules")]
    public WaveProgressionType progressionType = WaveProgressionType.ClearAllTargets;
    public float waveDuration = 60f;

    [SerializeReference]
    public List<CharacterConfigBase> characters = new List<CharacterConfigBase>();
}

/// <summary>
/// Data container for a full level, consisting of multiple waves.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "Archery Range/Level Config", order = 2)]
public class LevelConfigSO : ScriptableObject
{
    [Header("Level Settings")]
    [Tooltip("The name of the level to display to the player.")]
    public string levelName = "New Archery Level";

    [Tooltip("Text displayed to the player before the level begins (e.g. teaching a mechanic).")]
    [TextArea(2, 4)]
    public string levelIntroText = "Get Ready!";

    [Tooltip("Text displayed to the player when the entire level is completed. " +
             "Segues naturally onto the next level intro — no separate outro SO needed.")]
    [TextArea(2, 4)]
    public string levelOutroText = "Level Complete!";

    [Header("Folder Paths for Picker")]
    public string minionsFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/M";
    public string bossesFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/B";
    public string pathsFolderPath = "Assets/SplinePathGenerator/GeneratedPaths";
    public string ObservationEscapeFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/ObservationAndEscapePaths";

    [Header("Waves")]
    public List<WaveData> waves = new List<WaveData>();
}




//using System;
//using System.Collections.Generic;
//using UnityEngine;

//[Serializable]
//public abstract class CharacterConfigBase
//{
//    public float spawnDelay;
//    public Vector3 spawnPositionOffset;
//    public ElementTypeOB7 requiredArrowElement;
//}

//[Serializable]
//public class MinionConfig : CharacterConfigBase
//{
//    public GameObject prefab;
//    public GameObject spawnPointPrefab;
//    public TargetMovementType movementType;
//    public GameObject movementAssetPrefab;
//}

//[Serializable]
//public class BossConfig : CharacterConfigBase
//{
//    public GameObject prefab;
//    public GameObject spawnPointPrefab;
//    public List<GameObject> observationPathPrefabs = new List<GameObject>();
//    public List<GameObject> escapePathPrefabs = new List<GameObject>();
//}

//[Serializable]
//public class WaveData
//{
//    [Header("Wave Announcement")]
//    [Tooltip("Short title shown to the player when this wave is announced. e.g. 'The Siege Begins'")]
//    public string waveName = "";

//    [Tooltip("Brief description shown during the pre-wave countdown. Leave empty for a default objective message.")]
//    [TextArea(1, 3)]
//    public string waveAnnouncementText = "";

//    [Header("Wave Rules")]
//    public WaveProgressionType progressionType = WaveProgressionType.ClearAllTargets;
//    public float waveDuration = 60f;

//    [SerializeReference]
//    public List<CharacterConfigBase> characters = new List<CharacterConfigBase>();
//}

///// <summary>
///// Data container for a full level, consisting of multiple waves.
///// </summary>
//[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "Archery Range/Level Config", order = 2)]
//public class LevelConfigSO : ScriptableObject
//{
//    [Header("Level Settings")]
//    [Tooltip("The name of the level to display to the player.")]
//    public string levelName = "New Archery Level";

//    [Tooltip("Text displayed to the player before the level begins (e.g. teaching a mechanic).")]
//    [TextArea(2, 4)]
//    public string levelIntroText = "Get Ready!";

//    [Tooltip("Text displayed to the player when the entire level is completed. " +
//             "Segues naturally onto the next level intro — no separate outro SO needed.")]
//    [TextArea(2, 4)]
//    public string levelOutroText = "Level Complete!";

//    [Header("Folder Paths for Picker")]
//    public string minionsFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/M";
//    public string bossesFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/B";
//    public string pathsFolderPath = "Assets/SplinePathGenerator/GeneratedPaths";
//    public string ObservationEscapeFolderPath = "Assets/_Project/02_Scripts/SplinePathGenerator/Entities/Bosses/ObservationAndEscapePaths";

//    [Header("Waves")]
//    public List<WaveData> waves = new List<WaveData>();
//}
