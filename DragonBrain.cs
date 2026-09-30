using UnityEngine.Events;
using UnityEngine;
using System.Collections.Generic;
using PixelCrushers;

/// <summary>
/// Pillar C - Event-Driven Tactician
/// Uses a Utility Scoring system to intelligently evaluate tactial priorities.
/// </summary>
public enum DragonState
{
    FleeToHeal,
    DefendCrystal,
    TopplePillar,
    DenyArea,       // Shoot floor
    AttackPlayer,
    DefendMinions,
    Roam
}

public class DragonBrain : MonoBehaviour, IMessageHandler
{
    public DragonState CurrentState { get; private set; } = DragonState.Roam;

    [Header("Configuration")]
    public DragonAIConfigSO config;

    [Header("Animation Events")]
    public UnityEvent OnPlayRoar;
    public UnityEvent OnPlayAngryExpression;
    public UnityEvent OnSwoopStart;
    public UnityEvent OnSwoopEnd;

    [Header("Subsystems")]
    public BossNavigator navigator;
    public DragonFireballCaster fireball;
    public SpatialStrategyMiniGame strategyMiniGame;
    public SegmentManager segmentManager; // Using C# Actions exclusively

    private ToppleItem currentToppleTarget;
    private bool minionsNeedDefenseFlag = false;
    private float lastAttackTime;

    // Evaluated State Bools
    private bool needsHealingImperative = false;

    private Transform player;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        Debug.Assert(player != null, "[DragonBrain] No GameObject tagged 'Player' found.");
        Debug.Assert(strategyMiniGame != null, "[DragonBrain] StrategyMiniGame reference required.");
    }

    private void OnEnable()
    {
        VRHeadsetStickyBlindness.OnPlayerBlinded += HandlePlayerBlinded;
        MessageSystem.AddListener(this, "MinionUnderFire", string.Empty);

        if (segmentManager != null)
        {
            segmentManager.OnHealingImperativeReached += HandleHealingImperative;
            segmentManager.OnFullyHealed += HandleFullyHealed;
            segmentManager.OnSegmentLost += HandleSegmentLost;
        }
    }

    private void OnDisable()
    {
        VRHeadsetStickyBlindness.OnPlayerBlinded -= HandlePlayerBlinded;
        MessageSystem.RemoveListener(this, "MinionUnderFire", string.Empty);

        if (segmentManager != null)
        {
            segmentManager.OnHealingImperativeReached -= HandleHealingImperative;
            segmentManager.OnFullyHealed -= HandleFullyHealed;
            segmentManager.OnSegmentLost -= HandleSegmentLost;
        }
    }

    /// <summary>
    /// Utility Scoring System for determining the Dragon's tactical response.
    /// Replaces random selection with calculated desire scores.
    /// </summary>
    public void EvaluateState()
    {
        Dictionary<DragonState, float> scores = new Dictionary<DragonState, float>();

        // 1. Flee to Heal
        float healScore = 0f;
        if (needsHealingImperative && HasDefendCrystal())
        {
            healScore = 100f; // Absolute priority
        }
        else if (segmentManager != null && segmentManager.BodySegmentCount < segmentManager.numberOfBodySegments && HasDefendCrystal())
        {
            // Calculate a smaller desire to heal based on segments lost, but never overriding absolute imperatives
            float percentLost = 1f - ((float)segmentManager.BodySegmentCount / segmentManager.numberOfBodySegments);
            healScore = percentLost * 50f;
        }
        scores[DragonState.FleeToHeal] = healScore;

        // 2. Defend Minions
        scores[DragonState.DefendMinions] = MinionsNeedDefense() ? 75f : 0f;

        // 3. Attack Player (Baseline)
        scores[DragonState.AttackPlayer] = (player != null) ? 40f : 0f;
        scores[DragonState.DenyArea] = (player != null) ? 35f : 0f;

        // 4. Opportunistic Environment (Recipes / Toppling)
        float toppleScore = 0f;
        if (player != null && strategyMiniGame != null)
        {
            var targetData = strategyMiniGame.GetStrategicToppleTarget();
            if (targetData.pillar != null)
            {
                // Future expansion: If it's an Oil recipe opportunity, spike to 90f
                toppleScore = 60f;
            }
        }
        scores[DragonState.TopplePillar] = toppleScore;

        // 5. Roam (Fallback)
        scores[DragonState.Roam] = 10f;

        // Find the state with the highest score
        DragonState bestState = DragonState.Roam;
        float maxScore = -1f;

        foreach (var kvp in scores)
        {
            if (kvp.Value > maxScore)
            {
                maxScore = kvp.Value;
                bestState = kvp.Key;
            }
        }

        if (bestState != CurrentState)
        {
            TransitionToState(bestState);
        }
    }

    private void TransitionToState(DragonState newState)
    {
        CurrentState = newState;
        Debug.Log($"[DragonBrain] Transitioning to State: {newState}");

        switch (newState)
        {
            case DragonState.FleeToHeal:
                if (navigator != null) navigator.DefendCrystalCommand();
                break;
                
            case DragonState.DefendCrystal:
                if (navigator != null) navigator.DefendCrystalCommand();
                break;
                
            case DragonState.TopplePillar:
                var targetData = strategyMiniGame.GetStrategicToppleTarget();
                currentToppleTarget = targetData.pillar;

                if (currentToppleTarget != null && navigator != null)
                {
                    OnSwoopStart?.Invoke();
                    OnPlayAngryExpression?.Invoke();
                    Vector3 swoopTarget = currentToppleTarget.transform.position + (targetData.optimalHitDirection * 15f);
                    navigator.Freestyle(swoopTarget);
                }
                break;
                
            case DragonState.DenyArea:
                Vector3 hazardTarget = strategyMiniGame.GetOptimalHazardCoordinate();
                if (navigator != null) navigator.Freestyle(hazardTarget);
                TryFireball(hazardTarget);
                break;
                
            case DragonState.AttackPlayer:
                if (navigator != null) navigator.FreestyleToPlayer();
                if (player != null) TryFireball(player.position);
                break;
                
            case DragonState.DefendMinions:
                break;
                
            case DragonState.Roam:
                if (navigator != null) navigator.FreestyleArea();
                break;
        }
    }

    private void Update()
    {
        if (CurrentState == DragonState.AttackPlayer && player != null)
        {
            TryFireball(player.position);
        }
        else if (CurrentState == DragonState.TopplePillar)
        {
            if (currentToppleTarget == null)
            {
                EvaluateState();
                return;
            }
            float distance = Vector3.Distance(transform.position, currentToppleTarget.transform.position);
            float reachDist = config != null ? config.toppleReachDistance : 5f;
            if (distance <= reachDist)
            {
                Debug.Log($"[DragonBrain] Ramming run complete (passed pillar {currentToppleTarget.name}). Evaluating next state.");
                currentToppleTarget = null;
                OnSwoopEnd?.Invoke();
                EvaluateState();
            }
        }
    }

    private void TryFireball(Vector3 targetPosition)
    {
        if (fireball == null) return;
        float cooldown = config != null ? config.attackCooldown : 2f;
        if (Time.time - lastAttackTime < cooldown) return;

        fireball.LaunchAt(targetPosition);
        lastAttackTime = Time.time;
    }

    // --- Dynamic Triggers ---

    private void HandlePlayerBlinded()
    {
        Debug.Log("[DragonBrain] Player is Blinded! Triggering Minion Charge (Blind Spot)!");
        CommandMinionsToCharge();
        EvaluateState();
    }

    public void OnMessage(MessageArgs messageArgs)
    {
        if (messageArgs.message == "MinionUnderFire")
        {
            minionsNeedDefenseFlag = true;
            EvaluateState();
        }
    }

    // --- Segment Manager C# Event Hooks ---

    private void HandleHealingImperative()
    {
        Debug.Log("[DragonBrain] Healing Imperative reached! Dropping everything to survive!");
        needsHealingImperative = true;
        CommandMinionsToCharge(); // Cover retreat
        EvaluateState();
    }

    private void HandleFullyHealed()
    {
        Debug.Log("[DragonBrain] Fully healed! Back in the fight!");
        needsHealingImperative = false;
        EvaluateState();
    }

    private void HandleSegmentLost()
    {
        Debug.Log("[DragonBrain] Segment lost! Re-evaluating tactics.");
        EvaluateState();
    }

    private void CommandMinionsToCharge()
    {
        OnPlayRoar?.Invoke();
        MessageSystem.SendMessage(this, "DragonNeedsSupport", string.Empty);
    }

    private bool HasDefendCrystal()
    {
        return navigator != null && navigator.DefendCrystal != null;
    }

    private bool MinionsNeedDefense()
    {
        bool val = minionsNeedDefenseFlag;
        minionsNeedDefenseFlag = false;
        return val;
    }
}
