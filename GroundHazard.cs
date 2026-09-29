using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ground hazard (dark mist) that applies damage-over-time to Player when inside its trigger.
/// The prefab should have a trigger Collider (e.g., SphereCollider) and optionally a particle system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GroundHazard : MonoBehaviour
{
    [Tooltip("Damage per second applied to anything tagged 'Player' (or with PlayerHealth component).")]
    public float damagePerSecond = 10f;

    [Tooltip("Total duration before the hazard is destroyed.")]
    public float duration = 12f;

    private Collider triggerCollider;
    private readonly HashSet<GameObject> playersInside = new HashSet<GameObject>();

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        StartCoroutine(Lifetime());
        StartCoroutine(DamageTick());
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

                    // Prefer a direct PlayerHealth component if present, but call via SendMessage
                    // to be tolerant of different PlayerHealth implementations/signatures.
                    var ph = go.GetComponentInParent<PlayerHealth>();
                    if (ph != null)
                    {
                        // Use SendMessage on the component's GameObject to avoid static typing mismatches
                        // (handles projects where PlayerHealth may differ).
                        ph.gameObject.SendMessage("TakeDamage", damageThisTick, SendMessageOptions.DontRequireReceiver);
                    }
                    else
                    {
                        // Fallback: try SendMessage on the collider game object
                        go.SendMessage("TakeDamage", damageThisTick, SendMessageOptions.DontRequireReceiver);
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
        }
    }
}