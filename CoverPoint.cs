using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A marker in the world where minions can hide.
/// </summary>
public class CoverPoint : MonoBehaviour
{
    [Tooltip("Maximum number of minions that can hide behind this cover before they charge.")]
    public int capacity = 3;

    private List<StandardCreature> hiddenMinions = new List<StandardCreature>();
    
    public bool IsFull => hiddenMinions.Count >= capacity;

    public void RegisterMinionArrival(StandardCreature minion)
    {
        if (minion == null || hiddenMinions.Contains(minion)) return;
        
        hiddenMinions.Add(minion);
        
        if (IsFull)
        {
            TriggerCharge();
        }
    }

    public void RemoveMinion(StandardCreature minion)
    {
        hiddenMinions.Remove(minion);
    }

    /// <summary>
    /// Forces all minions hiding here to attack the player.
    /// Typically called when the cover is destroyed (e.g. via puzzle lever) or capacity is reached.
    /// </summary>
    public void TriggerCharge()
    {
        if (hiddenMinions.Count == 0) return;
        
        Debug.Log($"<color=red>[CoverPoint] {name} is exposed/full! Minions are charging!</color>");
        
        Transform playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        foreach (var minion in hiddenMinions)
        {
            if (minion != null)
            {
                minion.ChargePlayer(playerTransform);
            }
        }
        
        hiddenMinions.Clear(); // Reset as they leave
    }

    /// <summary>
    /// Called when a lever or puzzle mechanic destroys this cover.
    /// </summary>
    public void DestroyCover()
    {
        TriggerCharge();
        Destroy(gameObject);
    }
}
