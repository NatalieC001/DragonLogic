using UnityEngine.Events;
using UnityEngine;
using System.Collections.Generic;
using PixelCrushers;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.AI
{
    public enum DragonState
    {
        FleeToHeal,
        DefendCrystal,
        TopplePillar,
        DenyArea,
        AttackPlayer,
        DefendMinions,
        Roam
    }

    public class DragonBrain : MonoBehaviour, IMessageHandler
    {
        public DragonState CurrentState { get; private set; } = DragonState.Roam;

        public Dictionary<DragonState, float> UtilityScores { get; private set; } = new Dictionary<DragonState, float>();

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

        private bool hasFiredInCurrentState = false;
        private Vector3 currentCombatTarget;
        public float firingVantageRadius = 15f;
        private float combatStateTimeout = 0f; // NEW: Prevent stalling

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            Debug.Assert(player != null, "[DragonBrain] No GameObject tagged 'Player' found.");
            Debug.Assert(strategyMiniGame != null, "[DragonBrain] StrategyMiniGame reference required.");
            InitializeUtilityScores();
        }

        private void InitializeUtilityScores()
        {
            foreach (DragonState state in System.Enum.GetValues(typeof(DragonState)))
            {
                UtilityScores[state] = 0f;
            }
        }

        private void OnEnable()
        {
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

        public void EvaluateState()
        {
            InitializeUtilityScores();

            if (NeedsHealing() && HasDefendCrystal()) UtilityScores[DragonState.FleeToHeal] += 100f;
            if (MinionsNeedDefense()) UtilityScores[DragonState.DefendMinions] += 80f;

            if (player != null)
            {
                UtilityScores[DragonState.Roam] += 10f;
                UtilityScores[DragonState.AttackPlayer] += 40f;
                UtilityScores[DragonState.DenyArea] += 50f;

                if (strategyMiniGame != null)
                {
                    var toppleTarget = strategyMiniGame.GetStrategicToppleTarget();
                    if (toppleTarget.pillar != null)
                    {
                        float toppleScore = toppleTarget.isRecipeOpportunity ? 90f : 60f;
                        UtilityScores[DragonState.TopplePillar] += toppleScore;
                    }
                }
            }

            float cooldown = config != null ? config.attackCooldown : 2f;
            if (Time.time - lastAttackTime < cooldown || hasFiredInCurrentState)
            {
                UtilityScores[DragonState.DenyArea] -= 100f;
                UtilityScores[DragonState.AttackPlayer] -= 100f;
            }

            DragonState bestState = DragonState.Roam;
            float highestScore = -999f;
            foreach (var kvp in UtilityScores)
            {
                if (kvp.Value > highestScore)
                {
                    highestScore = kvp.Value;
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
            hasFiredInCurrentState = false;
            combatStateTimeout = 10f; // Max 10 seconds to complete a firing run
            Debug.Log($"[DragonBrain] Transitioning to State: {newState}");

            switch (newState)
            {
                case DragonState.FleeToHeal:
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
                    currentCombatTarget = strategyMiniGame.GetOptimalHazardCoordinate();
                    if (navigator != null)
                    {
                        Vector3 vantagePoint = currentCombatTarget + (Vector3.up * 10f) + (Random.onUnitSphere * firingVantageRadius);
                        vantagePoint.y = Mathf.Max(vantagePoint.y, 10f);
                        navigator.Freestyle(vantagePoint);
                    }
                    break;

                case DragonState.AttackPlayer:
                    if (player != null)
                    {
                        currentCombatTarget = player.position;
                        if (navigator != null)
                        {
                            Vector3 vantagePoint = currentCombatTarget + (Vector3.up * 10f) + (Random.onUnitSphere * firingVantageRadius);
                            vantagePoint.y = Mathf.Max(vantagePoint.y, 10f);
                            navigator.Freestyle(vantagePoint);
                        }
                    }
                    break;

                case DragonState.DefendMinions:
                    break;

                case DragonState.Roam:
                    if (navigator != null) navigator.FreestyleArea();
                    break;
            }
        }

        private float evaluationTimer = 0f;

        private void Update()
        {
            evaluationTimer -= Time.deltaTime;
            if (evaluationTimer <= 0f)
            {
                EvaluateState();
                evaluationTimer = 1.0f;
            }

            HandleCombatStateUpdate();
        }

        private void HandleCombatStateUpdate()
        {
            if (CurrentState == DragonState.DenyArea || CurrentState == DragonState.AttackPlayer)
            {
                if (hasFiredInCurrentState) return;

                combatStateTimeout -= Time.deltaTime;
                if (combatStateTimeout <= 0f)
                {
                    Debug.Log("[DragonBrain] Combat state timed out (failed to reach vantage point). Re-evaluating.");
                    EvaluateState();
                    return;
                }

                if (CurrentState == DragonState.AttackPlayer && player != null)
                {
                    currentCombatTarget = player.position;
                    // Periodically update vantage point if player moves too far
                    if (navigator != null && Vector3.Distance(transform.position, currentCombatTarget) > firingVantageRadius * 2f)
                    {
                        Vector3 vantagePoint = currentCombatTarget + (Vector3.up * 10f) + (Random.onUnitSphere * firingVantageRadius);
                        vantagePoint.y = Mathf.Max(vantagePoint.y, 10f);
                        navigator.Freestyle(vantagePoint);
                    }
                }

                float distanceToTarget = Vector3.Distance(transform.position, currentCombatTarget);

                if (distanceToTarget <= firingVantageRadius * 1.5f)
                {
                    TryFireball(currentCombatTarget);
                }
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
                    Debug.Log($"[DragonBrain] Ramming run complete. Evaluating next state.");
                    currentToppleTarget = null;
                    OnSwoopEnd?.Invoke();
                    EvaluateState();
                }
            }
        }

        private void TryFireball(Vector3 targetPosition)
        {
            if (fireball == null || hasFiredInCurrentState) return;
            float cooldown = config != null ? config.attackCooldown : 2f;
            if (Time.time - lastAttackTime < cooldown) return;

            fireball.LaunchAt(targetPosition);
            lastAttackTime = Time.time;
            hasFiredInCurrentState = true;

            EvaluateState();
        }

        private void HandlePlayerBlinded()
        {
            CommandMinionsToCharge();
            EvaluateState();
        }

        public void OnMessage(MessageArgs messageArgs)
        {
            if (messageArgs.message == "SegmentDestroyed") OnHealthSegmentLost();
            else if (messageArgs.message == "MinionUnderFire")
            {
                minionsNeedDefenseFlag = true;
                EvaluateState();
            }
        }

        public void OnHealthSegmentLost()
        {
            CommandMinionsToCharge();
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

        private bool NeedsHealing()
        {
            return segmentManager != null && segmentManager.BodySegmentCount < 8;
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
}
