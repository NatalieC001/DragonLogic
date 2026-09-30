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
                var targetData = strategyMiniGame.GetStrategicToppleTarget();
                currentToppleTarget = targetData.pillar;

                if (currentToppleTarget != null && navigator != null)
                {
                    // We calculate a point "behind" the pillar along the optimal hit direction.
                    // The dragon flies to this setup point, then swoops *through* the pillar.
                    // For now, we command the navigator to fly a trajectory through the pillar.
                    // We aim for a point past the pillar in the hit direction to ensure a strong physical impact.
                    Vector3 swoopTarget = currentToppleTarget.transform.position + (targetData.optimalHitDirection * 15f);

                    navigator.Freestyle(swoopTarget);
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
            if (currentToppleTarget == null)
            {
                EvaluateState();
                return;
            }

            // The physical impact is now handled natively by Unity Physics (OnTriggerEnter) on the ToppleItem.
            // We just use this distance check to know when the "ramming run" is complete so the brain
            // can move on to its next tactical decision, preventing it from getting stuck in this state.
            float distance = Vector3.Distance(transform.position, currentToppleTarget.transform.position);

            // To prevent a soft-lock if the dragon somehow misses the pillar or gets stuck pathing:
            // 1. If it gets close enough to have completed the run OR
            // 2. If it has been stuck in this state for too long (failsafe)
            if (distance <= toppleReachDistance)
            {
                Debug.Log($"[DragonBrain] Ramming run complete (passed pillar {currentToppleTarget.name}). Evaluating next state.");
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
