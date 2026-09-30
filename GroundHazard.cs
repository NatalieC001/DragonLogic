using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ground hazard (dark mist) that applies damage-over-time to Player when inside its trigger.
/// The prefab should have a trigger Collider (e.g., SphereCollider) and optionally a particle system.
/// </summary>
[RequireComponent(typeof(Collider))]
public enum HazardType
{
    DarkMist,
    Electricity,
    Fire,
    Sticky,
    Ice
}

[RequireComponent(typeof(Collider))]
public class GroundHazard : MonoBehaviour, IArrowTarget
{
    [Tooltip("Damage per second applied to anything tagged 'Player' (or with PlayerHealth component).")]
    public float damagePerSecond = 10f;

    [Tooltip("Total duration before the hazard is destroyed.")]
    public float duration = 12f;

    [Header("Hazard Configuration")]
    public HazardType hazardType = HazardType.DarkMist;

    private Collider triggerCollider;
    private readonly HashSet<GameObject> playersInside = new HashSet<GameObject>();

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        // Ensure this is on the Enemy layer or a layer the arrow can hit
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    private void Start()
    {
        SpatialStrategyMiniGame strategy = FindFirstObjectByType<SpatialStrategyMiniGame>();
        if (strategy != null) strategy.RegisterHazardZone(transform.position);
    }

    private void OnEnable()
    {
        StartCoroutine(Lifetime());
        StartCoroutine(DamageTick());
    }

    [Header("Dispel Mechanic")]
    [Tooltip("Amount of damage required to clear this hazard. A full power shot outside the hazard should equal this.")]
    public float dispelHealth = 100f;

    // --- STUB: Dispel Mechanic ---
    // Game Designers: The player can sacrifice time to draw a full-power shot.
    // That time gives the Dragon an opportunity to stage characters or attack.
    // If the player is inside the hazard, their shot damage is halved (via sticky blindness),
    // meaning they will have to shoot the tile twice to accumulate enough damage to dispel it.
    public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
    {
        dispelHealth -= damage;
        Debug.Log($"[GroundHazard] Arrow hit dark fire. Took {damage} damage. Remaining: {dispelHealth}");

        if (dispelHealth <= 0)
        {
            Debug.Log($"<color=cyan>[GroundHazard] Dark fire dispelled!</color>");
            Dispel();
        }
    }

    private void Dispel()
    {
        // Visuals can be added here (e.g., a burst of purifying light)

        // Unregister from the spatial mini game so it knows the tile is safe again
        // (Note: The current SpatialStrategyMiniGame activeHazardZones list would need a RemoveHazardZone method
        // if it needs immediate updates, though right now it just tracks spots permanently for the duration).

        Destroy(gameObject);
    }

    private IEnumerator Lifetime()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }

    private IEnumerator DamageTick()
    {
        // Use fixed timestep for consistent damage-per-second application
        var wait = new WaitForFixedUpdate();
        while (true)
        {
            if (playersInside.Count > 0)
            {
                float damageThisTick = damagePerSecond * Time.fixedDeltaTime;

                foreach (var go in playersInside)
                {
                    if (go == null) continue;

                    // --- STUB: Elemental Hazard Logic ---
                    // Future implementation: Check hazardType here.
                    // If Sticky: Apply a 3x Bow Draw Speed reduction via SendMessage.
                    // If Electricity: Check if standing on a "Conductive Metal Sheet" tag and double damage.
                    // If Fire: Apply a DoT burn debuff.
                    // If Ice: Reduce player movement speed.
                    // If DarkMist: (Existing logic) Applies StickyBlindness.

                    // Prefer a direct PlayerHealth component if present, but call via SendMessage
                    // to be tolerant of different PlayerHealth implementations/signatures.
                    var ph = go.GetComponentInParent<PlayerHealth>();
                    if (ph != null)
                    {
                        // Use SendMessage on the component's GameObject to avoid static typing mismatches
                        // (handles projects where PlayerHealth may differ).
                        ph.gameObject.SendMessage("TakeDamage", damageThisTick, SendMessageOptions.DontRequireReceiver);

                        // Example hook for the future Sticky effect:
                        if (hazardType == HazardType.Sticky)
                        {
                            ph.gameObject.SendMessage("ApplyStickyDebuff", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                    else
                    {
                        // Fallback: try SendMessage on the collider game object
                        go.SendMessage("TakeDamage", damageThisTick, SendMessageOptions.DontRequireReceiver);

                        if (hazardType == HazardType.Sticky)
                        {
                            go.SendMessage("ApplyStickyDebuff", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                }
            }
            yield return wait;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            playersInside.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            playersInside.Remove(other.gameObject);

            // --- STUB: Remove Hazard Debuffs ---
            // If the player steps out of a Sticky hazard, restore bow draw speed.
            if (hazardType == HazardType.Sticky)
            {
                other.gameObject.SendMessage("RemoveStickyDebuff", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
