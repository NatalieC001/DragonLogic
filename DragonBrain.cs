using UnityEngine;

/// <summary>
/// Decision layer for the Asian Fire Dragon boss.
///
/// The brain does not move the dragon. BossNavigator moves the dragon. The
/// brain evaluates a small priority list every tick, picks the highest-priority
/// state whose condition is true, and — only when the winner changes — calls
/// the matching navigator intent method. That single rule (retarget on state
/// change, not every frame) is what lets the navigator finish a maneuver
/// without the brain fighting it.
///
/// Priority, highest first:
///   Retreat      -> HP below retreat threshold. Abandon everything, flee.
///   SeekCrystal  -> HP below crystal threshold. Go heal.
///   Retaliate    -> recently damaged and player in aggro range. Turn on shooter.
///   Bombard      -> player in attack range. Fire.
///   Pursue       -> player in aggro range. Close distance.
///   Defend       -> external defend order is active. Guard the crystal.
///   Roam         -> default. Wander.
///
/// Damage arriving from DragonSegment -> BossCreature should be forwarded into
/// AbsorbHit so the brain can raise the Retaliate priority and shed a segment.
/// </summary>
public class DragonBrain : MonoBehaviour
{
    public enum DragonState
    {
        Roam,
        Pursue,
        Bombard,
        Retaliate,
        SeekCrystal,
        Defend,
        Retreat
    }

    /// <summary>
    /// Priority order. Index 0 wins over index 1, etc. Do not reorder without
    /// thinking about which condition should preempt which.
    /// </summary>
    private static readonly DragonState[] PriorityOrder =
    {
        DragonState.Retreat,
        DragonState.SeekCrystal,
        DragonState.Retaliate,
        DragonState.Bombard,
        DragonState.Pursue,
        DragonState.Defend,
        DragonState.Roam
    };

    public DragonState State { get; private set; } = DragonState.Roam;

    [Header("Senses")]
    private Transform player;
    public float aggroRadius = 15f;
    public float attackRadius = 12f;

    [Header("Subsystems")]
    public BossNavigator navigator;
    public DragonFireballCaster fireball;
    public DragonSnakeMovementStyle dragon;

    [Header("Vitals")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float crystalSeekRatio = 0.6f;
    public float retreatRatio = 0.35f;

    [Header("Segment Vitals")]
    public float healthPerSegment = 10f;
    public int maxBodySegments = 8;
    public int minBodySegments = 3;
    public float regenSecondsPerSegment = 1.5f;
    public float crystalRegenRadius = 8f;

    [Header("Combat")]
    public float attackCooldown = 2f;
    public float retaliateMemorySeconds = 3f;

    [Header("Repath Throttle")]
    [Tooltip("Minimum seconds between re-issuing the same navigation command " +
             "while the current state is stable. Prevents the brain from " +
             "fighting the navigator's turn acceleration.")]
    public float repathInterval = 1.5f;

    // --- runtime ---
    private float lastAttackTime;
    private float lastRepathTime;
    private float regenAccumulator;
    private float damageAccumulator;
    private float lastDamageTime = -999f;

    // Defend order: raised externally (via message or direct call), lowered
    // when the crystal is gone or the order is explicitly cancelled.
    private bool defendOrderActive;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        Debug.Assert(player != null, $"[DragonBrain] No GameObject tagged 'Player' found in scene. Dragon will not pursue or attack.");
    }

    void Update()
    {
        DragonState winner = ResolveState();

        if (winner != State)
        {
            State = winner;
            lastRepathTime = 0f;   // force an immediate retarget on transition
            OnEnter(winner);
        }

        RunState();
        UpdateSegmentRegen();
    }

    // ---------------------------------------------------------------------
    // Priority resolution
    // ---------------------------------------------------------------------

    DragonState ResolveState()
    {
        foreach (DragonState candidate in PriorityOrder)
        {
            if (IsConditionTrue(candidate))
                return candidate;
        }
        return DragonState.Roam;
    }

    bool IsConditionTrue(DragonState s)
    {
        float hp = maxHealth > 0f ? currentHealth / maxHealth : 0f;

        switch (s)
        {
            case DragonState.Retreat:
                return hp <= retreatRatio;

            case DragonState.SeekCrystal:
                return hp <= crystalSeekRatio && HasDefendCrystal();

            case DragonState.Retaliate:
                return (Time.time - lastDamageTime) <= retaliateMemorySeconds
                       && PlayerInRange(aggroRadius);

            case DragonState.Bombard:
                return PlayerInRange(attackRadius);

            case DragonState.Pursue:
                return PlayerInRange(aggroRadius);

            case DragonState.Defend:
                return defendOrderActive && HasDefendCrystal();

            case DragonState.Roam:
                return true;

            default:
                return false;
        }
    }

    bool PlayerInRange(float radius)
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= radius;
    }

    bool HasDefendCrystal()
    {
        return navigator != null && navigator.DefendCrystal != null;
    }

    // ---------------------------------------------------------------------
    // State execution
    // ---------------------------------------------------------------------

    void OnEnter(DragonState s)
    {
        switch (s)
        {
            case DragonState.Retreat:
                if (navigator != null) navigator.MoveToNearestEscape();
                lastRepathTime = Time.time;
                break;

            case DragonState.SeekCrystal:
                if (navigator != null) navigator.DefendCrystalCommand();
                lastRepathTime = Time.time;
                break;

            case DragonState.Retaliate:
                if (navigator != null) navigator.FreestyleToPlayer();
                lastRepathTime = Time.time;
                break;

            case DragonState.Bombard:
                if (navigator != null) navigator.FreestyleToPlayer();
                lastAttackTime = Time.time - attackCooldown; // fire immediately on arrival
                lastRepathTime = Time.time;
                break;

            case DragonState.Pursue:
                if (navigator != null) navigator.FreestyleToPlayer();
                lastRepathTime = Time.time;
                break;

            case DragonState.Defend:
                if (navigator != null) navigator.DefendCrystalCommand();
                lastRepathTime = Time.time;
                break;

            case DragonState.Roam:
                if (navigator != null) navigator.FreestyleArea();
                lastRepathTime = Time.time;
                break;
        }
    }

    void RunState()
    {
        switch (State)
        {
            case DragonState.Retaliate:
            case DragonState.Bombard:
            case DragonState.Pursue:
                // Keep the navigator pointed at the moving player, but throttled.
                if (navigator != null && Time.time - lastRepathTime > repathInterval)
                {
                    navigator.FreestyleToPlayer();
                    lastRepathTime = Time.time;
                }
                if (State == DragonState.Bombard)
                    TryFireball();
                break;

            case DragonState.SeekCrystal:
            case DragonState.Defend:
                if (navigator != null && Time.time - lastRepathTime > repathInterval)
                {
                    navigator.DefendCrystalCommand();
                    lastRepathTime = Time.time;
                }
                break;

            case DragonState.Retreat:
                if (navigator != null && Time.time - lastRepathTime > repathInterval)
                {
                    navigator.MoveToNearestEscape();
                    lastRepathTime = Time.time;
                }
                break;

            case DragonState.Roam:
                // Let the navigator finish its freestyle leg; only re-issue
                // when it has arrived and picked nothing new, or after the
                // throttle elapses.
                if (navigator != null && Time.time - lastRepathTime > repathInterval * 2f)
                {
                    navigator.FreestyleArea();
                    lastRepathTime = Time.time;
                }
                break;
        }
    }

    void TryFireball()
    {
        if (fireball == null || player == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;

        fireball.LaunchAt(player.position);
        lastAttackTime = Time.time;
    }

    void UpdateSegmentRegen()
    {
        if (dragon == null) return;

        bool nearCrystal = false;
        HealthCrystal crystal = navigator != null ? navigator.DefendCrystal : null;
        if (crystal != null)
        {
            float d = Vector3.Distance(transform.position, crystal.transform.position);
            nearCrystal = d <= crystalRegenRadius;
        }

        if (!nearCrystal) { regenAccumulator = 0f; return; }
        if (dragon.BodySegmentCount >= maxBodySegments) { regenAccumulator = 0f; return; }

        regenAccumulator += Time.deltaTime / regenSecondsPerSegment;
        while (regenAccumulator >= 1f && dragon.BodySegmentCount < maxBodySegments)
        {
            regenAccumulator -= 1f;
            dragon.RegrowBodySegment();
        }
    }

    // ---------------------------------------------------------------------
    // External hooks
    // ---------------------------------------------------------------------

    /// <summary>
    /// BossCreature should forward the amount of damage it actually applied
    /// here. Raises Retaliate priority and sheds a segment every
    /// healthPerSegment worth of accumulated damage.
    /// </summary>
    public void AbsorbHit(float amount)
    {
        lastDamageTime = Time.time;
        damageAccumulator += amount;

        if (dragon != null &&
            damageAccumulator >= healthPerSegment &&
            dragon.BodySegmentCount > minBodySegments)
        {
            damageAccumulator -= healthPerSegment;
            dragon.ShedOneBodySegment();
        }
    }

    /// <summary>
    /// Raise a defend order. The brain will hold Defend priority until either
    /// the crystal is destroyed or ClearDefendOrder is called.
    /// </summary>
    public void IssueDefendOrder()
    {
        defendOrderActive = true;
    }

    public void ClearDefendOrder()
    {
        defendOrderActive = false;
    }

    /// <summary>
    /// Notify the brain that the defended crystal is gone. Called from the
    /// crystal itself, or from BossNavigator when it exits defend mode.
    /// </summary>
    public void OnDefendCrystalDestroyed()
    {
        defendOrderActive = false;
    }
}