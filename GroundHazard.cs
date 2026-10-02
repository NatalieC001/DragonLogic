using UnityEngine.Events;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Ground hazard (dark mist) that applies damage-over-time to Player when inside its trigger.
/// The prefab should have a trigger Collider (e.g., SphereCollider) and optionally a particle system.
/// </summary>
public enum HazardType
{
    None,
    DarkMist,
    Electricity,
    Fire,
    Sticky,
    Ice,
    Water,
    Oil
}
[RequireComponent(typeof(Collider))]
public class GroundHazard : MonoBehaviour, IArrowTarget
{
    [Header("Hazard Configuration")]
    public HazardConfigSO config;
    public HazardType hazardType = HazardType.DarkMist;

    [Header("Events")]
    public UnityEvent OnDispelTriggered;

    private Collider triggerCollider;
    private readonly HashSet<GameObject> playersInside = new HashSet<GameObject>();

    private void Awake()
    {
        None,
        DarkMist,
        Electricity,
        Fire,
        Sticky,
        Ice,
        Water,
        Oil
    }
    [RequireComponent(typeof(Collider))]
    public class GroundHazard : MonoBehaviour, IArrowTarget
    {
        [Header("Hazard Configuration")]
        public HazardConfigSO config;
        public HazardType hazardType = HazardType.DarkMist;

    private float currentDispelHealth;

    private void OnEnable()
    {
        if (config != null)
        {
            currentDispelHealth = config.dispelHealth;
            StartCoroutine(Lifetime());
            StartCoroutine(DamageTick());
        }
        else
        {
            Debug.LogError("[GroundHazard] No HazardConfigSO assigned!");
        }
    }

    // --- STUB: Dispel Mechanic ---
    // Game Designers: The player can sacrifice time to draw a full-power shot.
    // That time gives the Dragon an opportunity to stage characters or attack.
    // If the player is inside the hazard, their shot damage is halved (via sticky blindness),
    // meaning they will have to shoot the tile twice to accumulate enough damage to dispel it.
    public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
    {
        currentDispelHealth -= damage;
        Debug.Log($"[GroundHazard] Arrow hit dark fire. Took {damage} damage. Remaining: {currentDispelHealth}");

        if (currentDispelHealth <= 0)
        {
            SpatialStrategyMiniGame strategy = FindFirstObjectByType<SpatialStrategyMiniGame>();
            if (strategy != null) strategy.RegisterHazardZone(transform.position);
        }

    private void Dispel()
    {
        OnDispelTriggered?.Invoke();
        ClearFromGameBoard();
        Destroy(gameObject);
    }

    private IEnumerator Lifetime()
    {
        yield return new WaitForSeconds(config.duration);
        ClearFromGameBoard();
        Destroy(gameObject);
    }

    private void ClearFromGameBoard()
    {
        GameBoard board = FindFirstObjectByType<GameBoard>();
        if (board != null)
        {
            Vector2Int pos = board.WorldToGrid(transform.position);
            // Only clear it if it's still registered to us (in case it was overwritten)
            if (board.GetTileState(pos) == hazardType)
            {
                board.ApplyElementToTile(pos, HazardType.None);
            }
        }
    }

    private IEnumerator DamageTick()
    {
        // Use fixed timestep for consistent damage-per-second application
        var wait = new WaitForFixedUpdate();
        while (true)
        {
            yield return new WaitForSeconds(config.duration);
            ClearFromGameBoard();
            Destroy(gameObject);
        }

        private void ClearFromGameBoard()
        {
            GameBoard board = FindFirstObjectByType<GameBoard>();
            if (board != null)
            {
                // We use fixedDeltaTime as intended, applying a slice of damage every fixed frame
                float damageThisTick = config.damagePerSecond * Time.fixedDeltaTime;

        private IEnumerator DamageTick()
        {
            // Use fixed timestep for consistent damage-per-second application
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                if (playersInside.Count > 0)
                {
                    // We use fixedDeltaTime as intended, applying a slice of damage every fixed frame
                    float damageThisTick = config.damagePerSecond * Time.fixedDeltaTime;

                    foreach (var go in playersInside.ToList())
                    {
                        if (go == null) continue;

                        // --- STUB: Elemental Hazard Logic ---
                        // Future implementation: Check hazardType here.
                        // If Sticky: Apply a 3x Bow Draw Speed reduction via SendMessage.
                        // If Electricity: Check if standing on a "Conductive Metal Sheet" tag and double damage.
                        // If Fire: Apply a DoT burn debuff.
                        // If Ice: Reduce player movement speed.
                        // If DarkMist: (Existing logic) Applies StickyBlindness.

                        ApplyDebuffs(ph.gameObject);
                    }
                }
                yield return wait;
            }
        }

                        ApplyDebuffs(go);
                    }
                    if (conductive) target.SendMessage("TakeDamage", config.damagePerSecond * Time.fixedDeltaTime, SendMessageOptions.DontRequireReceiver); // double dip
                    target.SendMessage("ApplyShock", SendMessageOptions.DontRequireReceiver);
                    break;
                case HazardType.DarkMist:
                    // Blindness is now triggered exactly once via OnTriggerEnter.
                    break;
            }
        }

    private Collider[] overlapBuffer = new Collider[10];

    private void ApplyDebuffs(GameObject target)
    {
        switch (hazardType)
        {
            case HazardType.Sticky:
                target.SendMessage("ApplyStickyDebuff", SendMessageOptions.DontRequireReceiver);
                break;
            case HazardType.Fire:
                target.SendMessage("ApplyBurn", SendMessageOptions.DontRequireReceiver);
                break;
            case HazardType.Ice:
                target.SendMessage("ApplySlow", SendMessageOptions.DontRequireReceiver);
                break;
            case HazardType.Electricity:
                // Check if standing on metal/water for double damage using NonAlloc
                int numOverlaps = Physics.OverlapSphereNonAlloc(transform.position, transform.localScale.x, overlapBuffer);
                bool conductive = false;
                for (int i = 0; i < numOverlaps; i++)
                {
                    if (overlapBuffer[i].CompareTag("ConductiveMetal") || overlapBuffer[i].CompareTag("WaterPuddle"))
                    {
                        conductive = true;
                        break;
                    }
                }
                if (conductive) target.SendMessage("TakeDamage", config.damagePerSecond * Time.fixedDeltaTime, SendMessageOptions.DontRequireReceiver); // double dip
                target.SendMessage("ApplyShock", SendMessageOptions.DontRequireReceiver);
                break;
            case HazardType.DarkMist:
                // Blindness is now triggered exactly once via OnTriggerEnter.
                break;
        }
    }

    private void OnDestroy()
    {
        // Failsafe: If hazard is destroyed while players are inside, clear their debuffs
        foreach (var go in playersInside)
        {
            if (go != null)
            {
                switch (hazardType)
                {
                    case HazardType.Sticky:
                        go.SendMessage("RemoveStickyDebuff", SendMessageOptions.DontRequireReceiver);
                        break;
                    case HazardType.Fire:
                        go.SendMessage("RemoveBurn", SendMessageOptions.DontRequireReceiver);
                        break;
                    case HazardType.Ice:
                        go.SendMessage("RemoveSlow", SendMessageOptions.DontRequireReceiver);
                        break;
                    case HazardType.DarkMist:
                        var blindness = go.GetComponentInChildren<VRHeadsetStickyBlindness>();
                        if (blindness != null) blindness.StartRecovery();
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            playersInside.Add(other.gameObject);
            // Trigger immediately upon entry for responsive feedback
            if (hazardType == HazardType.DarkMist)
            {
                var blindness = other.gameObject.GetComponentInChildren<VRHeadsetStickyBlindness>();
                if (blindness != null) blindness.TriggerBlindness();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                playersInside.Add(other.gameObject);
                // Trigger immediately upon entry for responsive feedback
                if (hazardType == HazardType.DarkMist)
                {
                    var blindness = other.gameObject.GetComponentInChildren<VRHeadsetStickyBlindness>();
                    if (blindness != null) blindness.TriggerBlindness();
                }
            }
        }

            switch (hazardType)
            {
                case HazardType.Sticky:
                    other.gameObject.SendMessage("RemoveStickyDebuff", SendMessageOptions.DontRequireReceiver);
                    break;
                case HazardType.Fire:
                    other.gameObject.SendMessage("RemoveBurn", SendMessageOptions.DontRequireReceiver);
                    break;
                case HazardType.Ice:
                    other.gameObject.SendMessage("RemoveSlow", SendMessageOptions.DontRequireReceiver);
                    break;
                case HazardType.DarkMist:
                    var blindness = other.gameObject.GetComponentInChildren<VRHeadsetStickyBlindness>();
                    if (blindness != null) blindness.StartRecovery();
                    break;
            }
        }
    }

}
