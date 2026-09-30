using UnityEngine;
using System.Collections.Generic;
using PixelCrushers;

/// <summary>
/// Pillar C - Event-Driven Tactician
/// Replaces the old Update-polling loop. Evaluates states dynamically based on events.
/// Relies on SpatialStrategyMiniGame for spatial targets and BossNavigator for movement execution.
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

    [Header("Subsystems")]
    public BossNavigator navigator;
    public DragonFireballCaster fireball;
    public DragonSnakeMovementStyle dragon;
    public SpatialStrategyMiniGame strategyMiniGame;
    public BossStatsAndHealth statsAndHealth;

    [Header("Senses")]
    public float aggroRadius = 15f;
    public float attackRadius = 12f;

    [Header("Topple Targeting")]
    public float toppleReachDistance = 5f;
    private ToppleItem currentToppleTarget;

    private bool minionsNeedDefenseFlag = false;

    [Header("Combat")]
    public float attackCooldown = 2f;
    private float lastAttackTime;
    
    private float damageAccumulator = 0f;
    private float healthPerSegment = 10f;

    private Transform player;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        Debug.Assert(player != null, "[DragonBrain] No GameObject tagged 'Player' found.");
        Debug.Assert(strategyMiniGame != null, "[DragonBrain] StrategyMiniGame reference required.");
    }

    private void OnEnable()
    {
        // Subscribe to relevant external events to trigger state evaluation
        VRHeadsetStickyBlindness.OnPlayerBlinded += HandlePlayerBlinded;
        MessageSystem.AddListener(this, "SegmentDestroyed", string.Empty);
        MessageSystem.AddListener(this, "MinionUnderFire", string.Empty);
    }

    private void OnDisable()
    {
        VRHeadsetStickyBlindness.OnPlayerBlinded -= HandlePlayerBlinded;
        MessageSystem.RemoveListener(this, "SegmentDestroyed", string.Empty);
        MessageSystem.RemoveListener(this, "MinionUnderFire", string.Empty);
    }

    /// <summary>
    /// Evaluates all possible states based on the current situation and transitions if needed.
    /// This is called via events (damage taken, crystals destroyed, player blinded) rather than Update.
    /// </summary>
    public void EvaluateState()
    {
        List<DragonState> validStates = new List<DragonState>();

        // 1. Check Survival Priorities (highest)
        if (NeedsHealing() && HasDefendCrystal())
        {
            validStates.Add(DragonState.FleeToHeal);
        }

        // 2. Check Minion Defense (high)
        if (MinionsNeedDefense())
        {
            validStates.Add(DragonState.DefendMinions);
        }

        // 3. Check Aggressive / Spatial Control Priorities
        if (PlayerInRange(attackRadius))
        {
            validStates.Add(DragonState.AttackPlayer);
            validStates.Add(DragonState.DenyArea); // Spatial control

            if (strategyMiniGame.GetOptimalToppleTarget() != null)
            {
                validStates.Add(DragonState.TopplePillar);
            }
        }
        else if (PlayerInRange(aggroRadius))
        {
            validStates.Add(DragonState.AttackPlayer);
        }

        // 4. Default to Roam if nothing else
        if (validStates.Count == 0)
        {
            validStates.Add(DragonState.Roam);
        }

        // Tie-breaker: Pick a random valid state to simulate "gritting it out"
        DragonState newState = validStates[Random.Range(0, validStates.Count)];

        if (newState != CurrentState)
        {
            TransitionToState(newState);
        }
    }

    private void TransitionToState(DragonState newState)
    {
        CurrentState = newState;
        Debug.Log($"[DragonBrain] Transitioning to State: {newState}");

        switch (newState)
        {
            case DragonState.FleeToHeal:
                if (navigator != null) navigator.DefendCrystalCommand(); // Use observation spline
                break;
                
            case DragonState.DefendCrystal:
                if (navigator != null) navigator.DefendCrystalCommand();
                break;
                
            case DragonState.TopplePillar:
                currentToppleTarget = strategyMiniGame.GetOptimalToppleTarget();
                if (currentToppleTarget != null && navigator != null)
                {
                    // Interrupt current path and fly directly to the pillar to knock it over
                    navigator.Freestyle(currentToppleTarget.transform.position);
                }
                break;
                
            case DragonState.DenyArea:
                Vector3 hazardTarget = strategyMiniGame.GetOptimalHazardCoordinate();
                if (navigator != null)
                {
                    navigator.Freestyle(hazardTarget); // Reposition to get a good angle
                }
                TryFireball(hazardTarget);
                break;
                
            case DragonState.AttackPlayer:
                if (navigator != null) navigator.FreestyleToPlayer();
                if (player != null) TryFireball(player.position);
                break;
                
            case DragonState.DefendMinions:
                // Move towards minion cover points to act as a shield
                break;
                
            case DragonState.Roam:
                if (navigator != null) navigator.FreestyleArea();
                break;
        }
    }

    private void Update()
    {
        // Minimal update logic: just continuous tracking or cooldown management if currently in an active attack state
        if (CurrentState == DragonState.AttackPlayer && player != null)
        {
            TryFireball(player.position);
        }
        else if (CurrentState == DragonState.TopplePillar)
        {
            if (currentToppleTarget == null || currentToppleTarget.IsToppled)
            {
                EvaluateState();
                return;
            }

            float distance = Vector3.Distance(transform.position, currentToppleTarget.transform.position);
            if (distance <= toppleReachDistance)
            {
                Debug.Log($"[DragonBrain] Reached pillar {currentToppleTarget.name}, telling it to topple!");
                MessageSystem.SendMessage(this, "DragonReachedPillar", string.Empty, currentToppleTarget);
                currentToppleTarget = null;
                EvaluateState();
            }
        }
    }

    private void TryFireball(Vector3 targetPosition)
    {
        if (fireball == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;

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
        if (messageArgs.message == "SegmentDestroyed")
        {
            OnHealthSegmentLost();
        }
        else if (messageArgs.message == "MinionUnderFire")
        {
            minionsNeedDefenseFlag = true;
            EvaluateState();
        }
    }

    public void OnHealthSegmentLost()
    {
        Debug.Log("[DragonBrain] Lost a segment! Retreating and covering retreat!");
        CommandMinionsToCharge(); // Covering retreat trigger
        EvaluateState();
    }

    public void AbsorbHit(float amount)
    {
        if (dragon != null && statsAndHealth != null)
        {
            damageAccumulator += amount;
            statsAndHealth.TakeDamage(amount, ElementTypeOB7.Normal);
            
            if (damageAccumulator >= healthPerSegment && dragon.BodySegmentCount > 3)
            {
                damageAccumulator -= healthPerSegment;
                dragon.ShedOneBodySegment();
                OnHealthSegmentLost();
            }
            EvaluateState();
        }
    }

    private void CommandMinionsToCharge()
    {
        MessageSystem.SendMessage(this, "DragonNeedsSupport", string.Empty);
    }

    // --- Condition Checks ---

    private bool NeedsHealing()
    {
        // Heal if we are below max body segments (have lost a piece)
        return dragon != null && dragon.BodySegmentCount < 8; // Assuming 8 is max as in original code
    }

    private bool HasDefendCrystal()
    {
        return navigator != null && navigator.DefendCrystal != null;
    }

    private bool MinionsNeedDefense()
    {
        bool val = minionsNeedDefenseFlag;
        minionsNeedDefenseFlag = false; // reset after checking
        return val;
    }

    private bool PlayerInRange(float radius)
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= radius;
    }
}
