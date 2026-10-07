using UnityEngine;
using PixelCrushers;
using Dreamteck.Splines;

/// <summary>
/// Attached to individual segments (Head, Body, Tail) of the Asian Dragon Boss.
/// Handles segment health and reports destruction to the main SegmentManager.
/// </summary>
namespace VRDragonBoss.AI
{
    [RequireComponent(typeof(SplineFollower))]
    public class DragonSegment : MonoBehaviour, IArrowTarget
    {
        [Header("Segment Stats")]
    [Tooltip("Can this individual piece be destroyed mid-fight? (Check True for body segments, False for Head/Legs/Tail).")]
    public bool isDestructiblePart = true;

    [HideInInspector]
    public float health = 100f; // Managed and initialized by SegmentManager

    [Tooltip("The amount of power/energy this specific segment contributes to the boss's total power.")]
    public float powerContribution = 10f;

    [Header("Visuals & Physics")]
    [Tooltip("Reference to the child mesh renderer (useful for triggering visual effects).")]
    [SerializeField] private Renderer segmentRenderer;
    [Tooltip("Reference to the child collider (useful for disabling physics upon death).")]
    [SerializeField] public Collider segmentCollider;

    [Tooltip("The physics layer this segment will be forced onto so arrows can detect it. Displayed here as a reminder!")]
    [SerializeField] private string targetLayer = "Enemy";

    private SegmentManager segmentManager;
    private BossStatsAndHealth vitals;
    private SplineFollower follower;

    // The index of this segment in the manager's list (Head = 0)
    public int SegmentIndex { get; set; }
    public SplineFollower Follower => follower;
    private bool isDead = false;


    private void Awake()
    {
        follower = GetComponent<SplineFollower>();
        vitals = GetComponentInParent<BossStatsAndHealth>();

        // Force the physics layer so arrows detect this segment, even if the dev forgot to set it!
        int layerIndex = LayerMask.NameToLayer(targetLayer);
        if (layerIndex != -1)
        {
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider col in allColliders)
            {
                col.gameObject.layer = layerIndex;
            }

            gameObject.layer = layerIndex;
            if (segmentCollider != null)
            {
                segmentCollider.gameObject.layer = layerIndex;
            }
        }
        else
        {
            Debug.LogWarning($"[DragonSegment] Layer '{targetLayer}' does not exist in your project settings!");
        }
    }

    public void Initialize(SegmentManager manager, int index)
    {
        segmentManager = manager;
        SegmentIndex = index;

        if (vitals == null)
            vitals = GetComponentInParent<BossStatsAndHealth>();
    }

    public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
    {
        TakeDamage(damage, impactPoint, elementType);
    }

    /// <summary>
    /// Called when the player shoots this specific segment.
    /// </summary>
    public virtual void TakeDamage(float amount, Vector3 hitPoint, ElementTypeOB7 arrowType = ElementTypeOB7.Normal)
    {
        if (isDead) return;

        // Pass damage up to the boss vitals so the overall health bar drops and
        // the message system can fire PlayerShootsBoss / BurstDamageTaken / etc.
        if (vitals != null)
        {
            vitals.TakeDamage(amount, arrowType);
        }

        // Delegate destruction calculation/elemental weaknesses to SegmentManager
        if (segmentManager != null)
        {
            segmentManager.HandleSegmentDamage(this, amount, hitPoint, arrowType);
        }
        else
        {
            Debug.LogWarning("[DragonSegment] Missing SegmentManager reference!");
        }
    }

    // Extracted out so SegmentManager can force it to die based on centralized damage calculations
    protected virtual void Die()
    {
        isDead = true;

        if (!isDestructiblePart) return;

        Debug.Log($"<color=red>[DragonSegment] Segment {SegmentIndex} health reached 0!</color>");

        if (segmentCollider != null)
        {
            segmentCollider.enabled = false;
        }

        StickingArrow[] attachedArrows = GetComponentsInChildren<StickingArrow>(true);
        foreach (StickingArrow arrow in attachedArrows)
        {
            if (arrow != null)
            {
                DissolveEffect arrowDissolve = arrow.GetComponentInChildren<DissolveEffect>();
                if (arrowDissolve != null)
                {
                    arrowDissolve.TriggerDissolve();
                }
            }
        }

        DissolveEffect dissolve = GetComponentInChildren<DissolveEffect>();
        if (dissolve != null)
        {
            dissolve.TriggerDissolve(FinalizeDestruction);
        }
        else
        {
            FinalizeDestruction();
        }
    }

    private void FinalizeDestruction()
    {
        if (segmentManager != null)
        {
            segmentManager.OnSegmentDestroyed(this);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// External entry point so the movement script or manager can force-shed a body segment.
    /// </summary>
    public void ForceDestruction()
    {
        if (isDead) return;
        if (!isDestructiblePart) return;

        health = 0f;
        Die();
    }

    public float GetSegmentSize()
    {
        Renderer r = segmentRenderer != null ? segmentRenderer : GetComponentInChildren<Renderer>();
        if (r != null)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return mf.sharedMesh.bounds.size.z * mf.transform.lossyScale.z;
            }
            return r.bounds.size.z;
        }
        return 2.0f;
    }

        public virtual void TriggerTotalDeath()
        {
            if (segmentCollider != null)
            {
                segmentCollider.enabled = false;
            }

            Destroy(gameObject);
        }
    }
}
