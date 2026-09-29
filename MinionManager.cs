using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A centralized manager to track all active StandardCreatures (Minions).
/// When a minion finishes its dissolve sequence, it notifies this manager,
/// which then explicitly notifies the WaveSpawner
/// before obliterating the minion GameObject AND its empty root wrappers.
/// </summary>
public class MinionManager : MonoBehaviour
{
    [SerializeField] private List<StandardCreature> activeMinions = new List<StandardCreature>();
    
    [Tooltip("The spawner that needs to be notified when a minion dies to progress the wave.")]
    [SerializeField] private WaveSpawner waveSpawner;

    private void Awake()
    {
        // Failsafe in case it's not wired in the inspector
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
        }
    }

    /// <summary>
    /// Registers a newly spawned minion explicitly.
    /// </summary>
    public void RegisterMinion(StandardCreature minion)
    {
        if (minion != null && !activeMinions.Contains(minion))
        {
            activeMinions.Add(minion);
            Debug.Log($"<color=cyan>[MinionManager] Registered: {minion.gameObject.name}. Active count: {activeMinions.Count}</color>");

            if (waveSpawner != null)
            {
                waveSpawner.NotifyTargetRegistered();
            }
            else
            {
                Debug.LogWarning("[MinionManager] WaveSpawner is null — cannot notify target registered! Wave will never complete.");
            }
        }
    }

    /// <summary>
    /// Called EXACTLY when a minion's dissolve animation finishes.
    /// Safely purges it from tracking, notifies the wave manager explicitly, and destroys it.
    /// </summary>
    public void OnMinionDestroyed(StandardCreature destroyedMinion)
    {
        // Only process the death if the minion is actually registered.
        // This completely prevents edge cases where rapid multi-hits send multiple death signals.
        if (activeMinions.Contains(destroyedMinion))
        {
            activeMinions.Remove(destroyedMinion);

            Debug.Log($"<color=magenta>[MinionManager] Minion visually completed death sequence. Destroying object. Remaining globally: {activeMinions.Count}</color>");

            // Explicitly notify the WaveSpawner so it can evaluate wave completion instantly!
            if (waveSpawner != null)
            {
                waveSpawner.NotifyTargetDestroyed();
            }

            // ONE source of truth: The manager executes the final physical destruction of the specific minion GameObject.
            if (destroyedMinion != null && destroyedMinion.gameObject != null)
            {
                // Cache the parent before we destroy this minion
                Transform parentTransform = destroyedMinion.transform.parent;

                // 1. Destroy the specific minion (and its nested meshes/colliders)
                Destroy(destroyedMinion.gameObject);

                // 2. Smart Cleanup: Check if the parent Swarm/Path is now completely empty
                if (parentTransform != null)
                {
                    // We check if the parent has any remaining active minions in its hierarchy
                    StandardCreature[] remainingSiblings = parentTransform.GetComponentsInChildren<StandardCreature>(false);
                    
                    // If this destroyed minion was the absolute last one in the parent structure...
                    if (remainingSiblings == null || remainingSiblings.Length <= 1) 
                    {
                        // Destroy the parent wrapper too, leaving zero garbage behind in the scene!
                        Debug.Log($"<color=magenta>[MinionManager] Parent Swarm {parentTransform.name} is now empty. Destroying it!</color>");
                        Destroy(parentTransform.gameObject);
                    }
                }
            }
        }
    }
}
