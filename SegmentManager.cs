using UnityEngine;
using System;
using System.Collections.Generic;
using PixelCrushers;

/// <summary>
/// Manages the Dragon's physical body segments.
/// Handles instantiation, shedding, regeneration, gap closure, and damage calculations.
/// Emits C# Actions for combat state changes, completely decoupled from movement.
/// </summary>
namespace VRDragonBoss.AI
{
    public class SegmentManager : MonoBehaviour, IMessageHandler
    {
        [Header("Body Prefabs")]
        public GameObject frontLegsPrefab;
    public GameObject bodyPrefab;
    public GameObject backLegsPrefab;
    public GameObject tailPrefab;

    [Header("Structure")]
    public int numberOfBodySegments = 8;
    public float segmentSpacing = 2f;
    public float delayPerSegment = 0.15f;

    [Header("Elemental Logic")]
    [Tooltip("How much damage each destructible segment can take before popping off.")]
    public float healthPerSegment = 100f; // Note: Can also pull from config

    [Header("Regeneration config overrides (optional)")]
    public DragonAIConfigSO aiConfig;

    // --- State ---
    private List<DragonSegment> activeSegments = new List<DragonSegment>();
    public IReadOnlyList<DragonSegment> ActiveSegments => activeSegments;

    public float TotalBossPower { get; private set; }

    // Gap closure tracking for DragonSnakeMovementStyle to read
    public bool IsClosingGap { get; private set; }
    public float GapCloseTimer { get; private set; }
    public float GapCloseDuration = 1f;
    public Dictionary<DragonSegment, float> CurrentSpacings { get; private set; } = new Dictionary<DragonSegment, float>();

    // Regeneration tracking
    private bool isAtCrystal = false;
    private bool isRegenLocked = false;
    private float regenTimer = 0f;
    private float timeAtCrystal = 0f;

    // --- Events (Global C# Event Bus for Combat) ---
    public event Action<int> OnSegmentCountChanged;
    public event Action OnSegmentLost;
    public event Action OnHealingImperativeReached;
    public event Action OnFullyHealed;

    private void Awake()
    {
        // Subscribe to Spline/Spatial events via MessageSystem
        MessageSystem.AddListener(this, "DragonReachedCrystal", string.Empty);
        MessageSystem.AddListener(this, "DragonLeftCrystal", string.Empty);
        MessageSystem.AddListener(this, "CrystalDestroyed", string.Empty);
    }

    private void Start()
    {
        InitializeBody();
    }

    private void OnDestroy()
    {
        MessageSystem.RemoveListener(this, "DragonReachedCrystal", string.Empty);
        MessageSystem.RemoveListener(this, "DragonLeftCrystal", string.Empty);
        MessageSystem.RemoveListener(this, "CrystalDestroyed", string.Empty);
    }

    private void Update()
    {
        if (IsClosingGap)
        {
            GapCloseTimer += Time.deltaTime;
            if (GapCloseTimer >= GapCloseDuration)
            {
                IsClosingGap = false;
            }
        }

        HandleRegeneration();
    }

    // ---------------------------------------------------------
    // Initialization
    // ---------------------------------------------------------
    public void InitializeBody()
    {
        TotalBossPower = 0f;
        activeSegments.Clear();

        int currentIndex = 0;

        // The root GameObject IS the head. It already has a DragonSegment attached.
        DragonSegment headSegment = GetComponent<DragonSegment>();
        if (headSegment != null)
        {
            headSegment.isDestructiblePart = false;
            headSegment.Initialize(this, currentIndex++);
            activeSegments.Add(headSegment);
            TotalBossPower += headSegment.powerContribution;
        }
        else
        {
            Debug.LogError("[SegmentManager] Root GameObject is missing a DragonSegment component! The head must be on the root.");
        }

        if (frontLegsPrefab != null) SpawnSegment(frontLegsPrefab, currentIndex++, false);
        for (int i = 0; i < numberOfBodySegments; i++) SpawnSegment(bodyPrefab, currentIndex++, true);
        if (backLegsPrefab != null) SpawnSegment(backLegsPrefab, currentIndex++, false);
        if (tailPrefab != null) SpawnSegment(tailPrefab, currentIndex++, false);

        OnSegmentCountChanged?.Invoke(activeSegments.Count);
    }

    private void SpawnSegment(GameObject prefab, int index, bool destructible)
    {
        if (prefab == null) return;

        GameObject segmentObj = Instantiate(prefab, transform);
        segmentObj.name = $"DragonSegment_{index}";

        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

        segment.isDestructiblePart = destructible;

        // Setup internal health if destructible
        if (destructible && aiConfig != null)
        {
            segment.health = aiConfig.healthPerSegment;
        }
        else
        {
             segment.health = healthPerSegment;
        }

        segment.Initialize(this, index);
        activeSegments.Add(segment);

        TotalBossPower += segment.powerContribution;
    }

    // ---------------------------------------------------------
    // Damage & Shedding
    // ---------------------------------------------------------

    /// <summary>
    /// Called by DragonSegment when hit by an arrow.
    /// Centralizes damage and elemental weakness logic.
    /// </summary>
    public void HandleSegmentDamage(DragonSegment segment, float amount, Vector3 hitPoint, ElementTypeOB7 element)
    {
        if (segment == null || !segment.isDestructiblePart) return;

        // FUTURE: Apply element multipliers from LevelConfigSO here!
        float finalDamage = amount;

        segment.health -= finalDamage;
        Debug.Log($"<color=orange>[SegmentManager] Segment {segment.SegmentIndex} took {finalDamage} {element} damage. Local Health: {segment.health}</color>");

        if (segment.health <= 0)
        {
            segment.ForceDestruction(); // This will visually dissolve and eventually call OnSegmentDestroyed
        }
    }

    public void OnSegmentDestroyed(DragonSegment destroyedSegment)
    {
        if (destroyedSegment == null) return;
        if (!activeSegments.Contains(destroyedSegment)) return;
        if (!destroyedSegment.isDestructiblePart) return;

        TotalBossPower -= destroyedSegment.powerContribution;
        activeSegments.Remove(destroyedSegment);

        Debug.Log($"<color=magenta>[SegmentManager] A body segment fell! Boss power reduced to {TotalBossPower}. Closing gap!</color>");

        if (activeSegments.Count > 0)
        {
            CurrentSpacings.Clear();

            for (int i = 0; i < activeSegments.Count; i++)
            {
                int oldIndex = activeSegments[i].SegmentIndex;
                activeSegments[i].SegmentIndex = i;
                CurrentSpacings[activeSegments[i]] = oldIndex * delayPerSegment;
            }

            IsClosingGap = true;
            GapCloseTimer = 0f;
        }

        OnSegmentCountChanged?.Invoke(activeSegments.Count);
        OnSegmentLost?.Invoke();

        CheckHealingImperative();
    }

    public void ShedOneBodySegment()
    {
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            DragonSegment s = activeSegments[i];
            if (s != null && s.isDestructiblePart)
            {
                s.ForceDestruction();
                return;
            }
        }
    }

    // ---------------------------------------------------------
    // Regeneration
    // ---------------------------------------------------------

    public void OnMessage(MessageArgs messageArgs)
    {
        if (messageArgs.message == "DragonReachedCrystal")
        {
            isAtCrystal = true;
            Debug.Log("[SegmentManager] Dragon reached observation spline. Ready to regenerate.");
        }
        else if (messageArgs.message == "DragonLeftCrystal")
        {
            isAtCrystal = false;
            timeAtCrystal = 0f;
            regenTimer = 0f;
            Debug.Log("[SegmentManager] Dragon left observation spline. Halting regeneration.");
        }
        else if (messageArgs.message == "CrystalDestroyed")
        {
            isRegenLocked = true;
            isAtCrystal = false;
            Debug.Log("<color=red>[SegmentManager] Crystals destroyed. Regeneration permanently locked.</color>");
        }
    }

    private void HandleRegeneration()
    {
        if (!isAtCrystal || isRegenLocked) return;
        if (BodySegmentCount >= numberOfBodySegments)
        {
            // Already full
            return;
        }

        float maxRegenTime = aiConfig != null ? aiConfig.maxRegenerationTime : 20f;
        float cooldown = aiConfig != null ? aiConfig.regrowCooldown : 5f;

        timeAtCrystal += Time.deltaTime;
        if (timeAtCrystal > maxRegenTime)
        {
            // Time limit at crystal reached
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer >= cooldown)
        {
            regenTimer -= cooldown;
            RegrowBodySegment();
        }
    }

    private void RegrowBodySegment()
    {
        if (bodyPrefab == null) return;

        int insertAfter = -1;
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            if (activeSegments[i].isDestructiblePart)
            {
                insertAfter = i;
                break;
            }
        }

        if (insertAfter < 0)
        {
            for (int i = 0; i < activeSegments.Count; i++)
            {
                if (activeSegments[i].SegmentIndex == 0) { insertAfter = i; }
                else if (activeSegments[i].SegmentIndex == 1 && !activeSegments[i].isDestructiblePart)
                    insertAfter = Mathf.Max(insertAfter, i);
            }
        }

        if (insertAfter < 0) return;

        GameObject segmentObj = Instantiate(bodyPrefab, transform);
        segmentObj.name = $"DragonSegment_regen_{activeSegments.Count}";

        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

        segment.isDestructiblePart = true;
        if (aiConfig != null) segment.health = aiConfig.healthPerSegment;

        segment.Initialize(this, insertAfter + 1);

        activeSegments.Insert(insertAfter + 1, segment);
        TotalBossPower += segment.powerContribution;

        CurrentSpacings.Clear();
        for (int i = 0; i < activeSegments.Count; i++)
        {
            int oldIndex = activeSegments[i].SegmentIndex;
            activeSegments[i].SegmentIndex = i;

            if (i > insertAfter)
                CurrentSpacings[activeSegments[i]] = oldIndex * delayPerSegment;
        }

        IsClosingGap = true;
        GapCloseTimer = 0f;

        Debug.Log($"<color=green>[SegmentManager] Regrew a segment! New count: {BodySegmentCount}</color>");
        OnSegmentCountChanged?.Invoke(activeSegments.Count);

        if (BodySegmentCount >= numberOfBodySegments)
        {
            OnFullyHealed?.Invoke();
        }
    }

    private void CheckHealingImperative()
    {
        int criticalThreshold = aiConfig != null ? aiConfig.criticalSegmentThreshold : 3;
        if (BodySegmentCount <= criticalThreshold)
        {
            OnHealingImperativeReached?.Invoke();
        }
    }

    public int BodySegmentCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < activeSegments.Count; i++)
                if (activeSegments[i] != null && activeSegments[i].isDestructiblePart) n++;
            return n;
        }
    }

    public void TriggerTotalDeath()
    {
        Debug.Log("<color=red>[SegmentManager] The entire dragon is collapsing!</color>");
        foreach (var segment in activeSegments)
        {
            if (segment != null) segment.TriggerTotalDeath();
        }
        activeSegments.Clear();
        OnSegmentCountChanged?.Invoke(0);
        }
    }
}
