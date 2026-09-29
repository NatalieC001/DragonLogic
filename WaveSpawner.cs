using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Handles the physical instantiation and tracking of targets.
/// Subscribes to LevelProgressionManager to know when to start.
/// Informs LevelProgressionManager when the wave is cleared.
/// Completely avoids Coroutines for internal loops and delays (except standard Unity delays if absolutely needed).
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelProgressionManager progressionManager;
    public MinionManager minionManager;

    [Header("Spawn Settings")]
    [Tooltip("The central point where targets are spawned around. If null, uses this object's position.")]
    public Transform spawnCenter;

    // --- State ---
    private bool isWaveActive = false;
    private bool currentWaveIsBoss = false;
    private WaveData currentWaveData;
    
    // Tracks environmental paths instantiated exclusively for this wave so they can be destroyed
    private List<GameObject> activePaths = new List<GameObject>();
    private LevelConfigSO currentLevelConfig;
    private int currentWaveIndex;
    private int totalWaveCount;

    // Explicit tracking driven by MinionManager
    private int activeEnemyCount = 0;

    // Timer logic for TimeBased waves
    private float waveTimer = 0f;
    
    private BossPathManager pathManager;

    // --- Queue for delayed spawning (replacing Coroutines) ---
    private class PendingSpawn
    {
        public CharacterConfigBase Config;
        public float TimeToSpawn;
    }
    private List<PendingSpawn> spawnQueue = new List<PendingSpawn>();

    // --- Public Events ---
    public event Action<string> OnWaveProgressUpdated;

    private void OnEnable()
    {
        if (progressionManager == null)
        {
            progressionManager = FindFirstObjectByType<LevelProgressionManager>();
        }

        if (progressionManager != null)
        {
            progressionManager.OnWaveStartRequested += HandleWaveStartRequested;
            progressionManager.OnStateChanged += HandleStateChanged;
        }
        
        if (spawnCenter == null)
        {
            spawnCenter = this.transform;
        }

        if (minionManager == null)
        {
            minionManager = FindFirstObjectByType<MinionManager>();
        }

        pathManager = FindFirstObjectByType<BossPathManager>();
    }

    private void OnDisable()
    {
        if (progressionManager != null)
        {
            progressionManager.OnWaveStartRequested -= HandleWaveStartRequested;
            progressionManager.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(LevelProgressionManager.GameState state)
    {
        // If state shifts away from wave active, forcefully clean up
        if (state != LevelProgressionManager.GameState.WaveActive && state != LevelProgressionManager.GameState.BossActive)
        {
            CleanupTargets();
            isWaveActive = false;
            spawnQueue.Clear();
        }
    }

    private void HandleWaveStartRequested(LevelConfigSO levelConfig, int waveIndex)
    {
        if (waveIndex >= levelConfig.waves.Count) return;

        currentLevelConfig = levelConfig;
        currentWaveData = levelConfig.waves[waveIndex];
        currentWaveIndex = waveIndex;
        totalWaveCount = levelConfig.waves.Count;
        
        activePaths.Clear();
        spawnQueue.Clear();
        waveTimer = 0f;
        isWaveActive = true;
        currentWaveIsBoss = false;
        activeEnemyCount = 0;

        // Queue all characters using absolute time to avoid Coroutine cascades
        float currentTime = Time.time;

        foreach (var charConfig in currentWaveData.characters)
        {
            if (charConfig is BossConfig boss)
            {
                currentWaveIsBoss = true;
                InstantiateAndRegisterPaths(boss.observationPathPrefabs);
                InstantiateAndRegisterPaths(boss.escapePathPrefabs);
            }

            spawnQueue.Add(new PendingSpawn
            {
                Config = charConfig,
                TimeToSpawn = currentTime + charConfig.spawnDelay
            });
        }
        
        if (currentWaveIsBoss)
        {
            Debug.Log($"[WaveSpawner] Boss Wave {waveIndex + 1}/{totalWaveCount} started. Tracking Boss Colliders on Enemy layer.");
            progressionManager.NotifyBossWaveStarted();
        }
        else
        {
            Debug.Log($"[WaveSpawner] Standard Wave {waveIndex + 1}/{totalWaveCount} started. {spawnQueue.Count} targets queued.");
        }
    }

    private void Update()
    {
        ProcessSpawns();

        if (!isWaveActive) return;
        UpdateWaveLogic();
    }

    private void ProcessSpawns()
    {
        for (int i = spawnQueue.Count - 1; i >= 0; i--)
        {
            if (Time.time >= spawnQueue[i].TimeToSpawn)
            {
                SpawnCharacter(spawnQueue[i].Config);
                spawnQueue.RemoveAt(i);
            }
        }
    }

    private void UpdateWaveLogic()
    {
        bool allTargetsCleared = (activeEnemyCount == 0) && (spawnQueue.Count == 0);

        if (currentWaveData.progressionType == WaveProgressionType.TimeBased)
        {
            waveTimer += Time.deltaTime;
            float timeLeft = Mathf.Max(0, currentWaveData.waveDuration - waveTimer);
            OnWaveProgressUpdated?.Invoke($"Wave {currentWaveIndex + 1}/{totalWaveCount} | Survive: {timeLeft:F1}s");

            if (waveTimer >= currentWaveData.waveDuration || allTargetsCleared)
            {
                CompleteWave();
            }
        }
        else if (currentWaveData.progressionType == WaveProgressionType.ClearAllTargets)
        {
            int targetsRemaining = activeEnemyCount + spawnQueue.Count;
            
            // Only update HUD if it's not a boss fight (Bosses usually have custom UI)
            if (!currentWaveIsBoss)
            {
                OnWaveProgressUpdated?.Invoke($"Wave {currentWaveIndex + 1}/{totalWaveCount} | Targets Left: {targetsRemaining}");
            }

            if (allTargetsCleared)
            {
                CompleteWave();
            }
        }
    }

    /// <summary>
    /// Explicitly called by MinionManager when a minion broadcasts its spawn event.
    /// </summary>
    public void NotifyTargetRegistered()
    {
        if (isWaveActive)
        {
            activeEnemyCount++;
        }
    }

    /// <summary>
    /// Explicitly called by MinionManager when a minion dies and dissolves.
    /// </summary>
    public void NotifyTargetDestroyed()
    {
        if (isWaveActive)
        {
            activeEnemyCount--;
            if (activeEnemyCount < 0) activeEnemyCount = 0;
            Debug.Log($"[WaveSpawner] Target destroyed. Remaining: {activeEnemyCount}");
        }
    }

    private void CompleteWave()
    {
        isWaveActive = false;
        CleanupTargets();
        spawnQueue.Clear();

        if (progressionManager != null)
        {
            if (currentWaveIsBoss)
            {
                Debug.Log($"[WaveSpawner] Boss Wave {currentWaveIndex + 1} completed. Boss Defeated!");
                progressionManager.ReceiveBossDefeated();
            }
            else
            {
                Debug.Log($"[WaveSpawner] Standard Wave {currentWaveIndex + 1} completed.");
                progressionManager.ReceiveWaveCompleted();
            }
        }
    }

    private void CleanupTargets()
    {
        // Clean up the environmental paths spawned for Bosses.
        foreach (var path in activePaths)
        {
            if (path != null)
            {
                Destroy(path);
            }
        }
        activePaths.Clear();

        if (pathManager != null)
        {
            pathManager.ClearAllPaths();
        }
    }
    
    private void InstantiateAndRegisterPaths(List<GameObject> pathPrefabs)
    {
        if (pathManager == null)
        {
            Debug.LogWarning("[WaveSpawner] No BossPathManager found in scene. Paths will be spawned but un-registered.");
        }

        foreach (var prefab in pathPrefabs)
        {
            if (prefab != null)
            {
                // Instantiate the path into the scene
                GameObject spawnedPath = Instantiate(prefab, spawnCenter.position, Quaternion.identity);
                
                // Track it locally so it can be physically destroyed at wave end
                activePaths.Add(spawnedPath); 

                // Register it with the global source of truth
                if (pathManager != null)
                {
                    pathManager.RegisterPath(spawnedPath);
                }
            }
        }
    }

    private void SpawnCharacter(CharacterConfigBase config)
    {
        Vector3 spawnPos = spawnCenter.position + config.spawnPositionOffset;

        // VR Constraint: Ensure target never spawns below the ground (Y < 0 relative to center)
        if (spawnPos.y < spawnCenter.position.y)
        {
            spawnPos.y = spawnCenter.position.y;
        }

        // VR Constraint: Ensure target is perfectly upright, facing the spawn center
        Vector3 directionToCenter = spawnCenter.position - spawnPos;
        directionToCenter.y = 0;

        Quaternion spawnRot = Quaternion.identity;
        if (directionToCenter.sqrMagnitude > 0.001f)
        {
            spawnRot = Quaternion.LookRotation(directionToCenter);
        }

        GameObject prefabToSpawn = null;

        if (config is MinionConfig minionConfig)
        {
            prefabToSpawn = minionConfig.prefab;
        }
        else if (config is BossConfig bossConfig)
        {
            prefabToSpawn = bossConfig.prefab;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[WaveSpawner] Prefab is missing from the Character Config!");
            return;
        }

        GameObject spawnedEntity = Instantiate(prefabToSpawn, spawnPos, spawnRot);

        // --- Diagnostic: confirm StandardCreature self-registration will happen ---
        if (config is MinionConfig)
        {
            StandardCreature[] creatures = spawnedEntity.GetComponentsInChildren<StandardCreature>(true);
            if (creatures.Length == 0)
            {
                Debug.LogError($"[WaveSpawner] Spawned '{spawnedEntity.name}' but found NO StandardCreature components in its hierarchy! " +
                               "Wave tracking will fail — this prefab will never register with MinionManager. " +
                               "Ensure StandardCreature is on the root OR on each child entity inside the swarm prefab.");
            }
            else
            {
                Debug.Log($"[WaveSpawner] Spawned '{spawnedEntity.name}' with {creatures.Length} StandardCreature(s) — self-registration will proceed via Awake.");
            }
        }

        // Native self-registration explicitly natively registers with MinionManager. 
        // MinionManager handles tracking active counts and physical destruction natively.
        
        if (config is BossConfig)
        {
            // Inject the WaveSpawner so the Boss can notify it upon death
            BossCreature boss = spawnedEntity.GetComponent<BossCreature>();
            if (boss != null)
            {
                boss.Initialize(this);
            }

            // Bosses don't natively register through MinionManager yet, so we manually track the root entity
            NotifyTargetRegistered();
        }
    }
}
