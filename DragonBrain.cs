using UnityEngine.Events;
using UnityEngine;
using System.Collections.Generic;
using PixelCrushers;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.AI
{
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
        public DragonSnakeMovementStyle dragon;
        public SegmentManager segmentManager;
        public SpatialStrategyMiniGame strategyMiniGame;
        public BossStatsAndHealth statsAndHealth;

        private ToppleItem currentToppleTarget;
        private bool minionsNeedDefenseFlag = false;
        private float lastAttackTime;
        private float damageAccumulator = 0f;

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
            // The Asian Dragon is always moving and evaluating tactical needs rather than using arbitrary radii.
            if (player != null)
            {
                // If the player exists, we are always willing to attack or deny area
                validStates.Add(DragonState.AttackPlayer);
                validStates.Add(DragonState.DenyArea);

                // If there's a good topple target (recipe or standard), prioritize it
                if (strategyMiniGame != null && strategyMiniGame.GetStrategicToppleTarget().pillar != null)
                {
                    validStates.Add(DragonState.TopplePillar);
                }
            }

            // 4. Default to Roam if nothing else
            if (validStates.Count == 0)
            {
                validStates.Add(DragonState.Roam);
            }

            // Tie-breaker: Pick a random valid state to simulate "gritting it out"
            DragonState newState = validStates[Random.Range(0, validStates.Count)];

            if (newState != CurrentState || newState == DragonState.TopplePillar || newState == DragonState.DenyArea)
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
                    if (navigator != null) navigator.FreestyleToPlayer(); // Fixed SetDestination -> FreestyleToPlayer
                    if (player != null) TryFireball(player.position);
                    break;

                case DragonState.DefendMinions:
                    // Move towards minion cover points to act as a shield
                    break;

                case DragonState.Roam:
                    if (navigator != null) navigator.FreestyleArea(); // Fixed SetDestination -> FreestyleArea
                    break;
            }
        }

        private float evaluationTimer = 0f;

        private void Update()
        {
            // Tactical evaluation mid-flight, throttled to prevent performance spikes and state thrashing
            evaluationTimer -= Time.deltaTime;
            if (evaluationTimer <= 0f)
            {
                EvaluateState();
                evaluationTimer = 1.0f; // Evaluate once per second, or instantly on event triggers
            }

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

                float hpPerSeg = config != null ? config.healthPerSegment : 10f;
                if (damageAccumulator >= hpPerSeg && segmentManager != null && segmentManager.BodySegmentCount > 3)
                {
                    damageAccumulator -= hpPerSeg;
                    segmentManager.ShedOneBodySegment();
                    OnHealthSegmentLost();
                }
                EvaluateState();
            }
        }

        private void CommandMinionsToCharge()
        {
            OnPlayRoar?.Invoke();
            MessageSystem.SendMessage(this, "DragonNeedsSupport", string.Empty);
        }

        // --- Condition Checks ---

        private bool NeedsHealing()
        {
            // Heal if we are below max body segments (have lost a piece)
            return segmentManager != null && segmentManager.BodySegmentCount < 8; // Assuming 8 is max as in original code
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

        // PlayerInRange was removed in favor of constant tactical evaluation
    }
}
