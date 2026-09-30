using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pillar A - Spatial Strategy Mini-Game
/// Acts as a chess-master AI. Evaluates the flat 3D plane and the player's position
/// to orchestrate environmental hazards that box the player into a corner.
/// </summary>
public class SpatialStrategyMiniGame : MonoBehaviour
{
    private Transform player;

    [Header("Arena Boundaries")]
    [Tooltip("The center of the playable flat 3D plane.")]
    public Transform arenaCenter;
    [Tooltip("The size of the square arena for quadrant calculation.")]
    public float arenaSize = 40f;

    [Header("Topple Objects")]
    private List<ToppleItem> availableToppleItems = new List<ToppleItem>();
    
    // Track active hazards to avoid shooting the same spot twice
    private List<Vector3> activeHazardZones = new List<Vector3>();
    public float hazardRadius = 5f;

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        UpdateToppleItemsList();
    }

    private void UpdateToppleItemsList()
    {
        availableToppleItems.Clear();
        availableToppleItems.AddRange(FindObjectsByType<ToppleItem>(FindObjectsSortMode.None));
    }
    
    public void RegisterHazardZone(Vector3 pos)
    {
        activeHazardZones.Add(pos);
    }

    /// <summary>
    /// Returns the best calculated world position to cast dark fire to trap the player.
    /// This acts like chess: it looks at where the player is, and attempts to block their
    /// path to the center, forcing them into a corner or edge.
    /// </summary>
    public Vector3 GetOptimalHazardCoordinate()
    {
        if (player == null || arenaCenter == null) return transform.position;

        // The goal of the AI is to cut off the player's escape to the center of the room.
        // It wants to push them toward the edges.
        
        Vector3 playerPos = player.position;
        Vector3 centerPos = arenaCenter.position;
        playerPos.y = 0;
        centerPos.y = 0;

        // Calculate the vector from the player to the center of the arena (their primary escape route)
        Vector3 escapeVector = (centerPos - playerPos).normalized;
        
        // Target a spot directly in their path to the center
        Vector3 targetCoordinate = playerPos + (escapeVector * (hazardRadius * 1.5f));
        
        // Prevent stacking hazards exactly on top of each other
        bool isClear = false;
        int attempts = 0;
        
        while (!isClear && attempts < 5)
        {
            isClear = true;
            foreach (var hazard in activeHazardZones)
            {
                if (Vector3.Distance(targetCoordinate, hazard) < hazardRadius)
                {
                    // Shift the target left or right if a hazard is already there
                    Vector3 cross = Vector3.Cross(escapeVector, Vector3.up);
                    targetCoordinate += cross * (hazardRadius * 1.2f);
                    isClear = false;
                    break;
                }
            }
            attempts++;
        }

        // Snap to ground level
        if (Physics.Raycast(targetCoordinate + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
        {
            targetCoordinate.y = hit.point.y;
        }
        else
        {
            targetCoordinate.y = arenaCenter.position.y;
        }

        return targetCoordinate;
    }

    /// <summary>
    /// Returns the nearest ToppleItem that hasn't already been destroyed or knocked over,
    /// prioritizing items that sit between the player and the center of the arena.
    /// </summary>
    public ToppleItem GetOptimalToppleTarget()
    {
        if (player == null || arenaCenter == null) return null;
        
        UpdateToppleItemsList();

        ToppleItem bestItem = null;
        float minScore = float.MaxValue;
        
        Vector3 playerPos = player.position;
        playerPos.y = 0;
        
        // The ideal pillar to knock over is one that blocks the player's path inward.
        Vector3 escapeVector = (arenaCenter.position - playerPos).normalized;
        Vector3 idealBlockPoint = playerPos + (escapeVector * 10f);

        foreach (var item in availableToppleItems)
        {
            if (item == null || item.IsToppled) continue;

            // Score is based on how close the pillar is to the ideal blocking point
            float dist = Vector3.Distance(item.transform.position, idealBlockPoint);
            if (dist < minScore)
            {
                minScore = dist;
                bestItem = item;
            }
        }

        return bestItem;
    }


    public struct ToppleTargetData
    {
        public ToppleItem pillar;
        public Vector3 optimalHitDirection;
    }

    /// <summary>
    /// Returns the optimal pillar to topple AND the strategic direction the Dragon should hit it from.
    /// The goal is to push the pillar so it blocks the player's path toward the arena center,
    /// effectively corralling them and knocking floor tiles out of action.
    /// </summary>
    public ToppleTargetData GetStrategicToppleTarget()
    {
        ToppleTargetData result = new ToppleTargetData { pillar = null, optimalHitDirection = Vector3.forward };

        ToppleItem optimalPillar = GetOptimalToppleTarget();
        if (optimalPillar == null) return result;

        result.pillar = optimalPillar;

        // Calculate the vector we want the pillar to FALL toward.
        // We want it to fall between the player and the center, creating a wall.
        Vector3 playerPos = player.position;
        playerPos.y = 0;
        Vector3 centerPos = arenaCenter.position;
        centerPos.y = 0;

        // The point we want to block
        Vector3 blockPoint = playerPos + (centerPos - playerPos).normalized * 5f;

        // The direction the pillar needs to fall to land on that block point
        Vector3 fallDirection = (blockPoint - optimalPillar.transform.position).normalized;

        // If the pillar is already exactly on the block point (rare), just push it towards the player.
        if (fallDirection.sqrMagnitude < 0.01f)
        {
            fallDirection = (playerPos - optimalPillar.transform.position).normalized;
        }

        // The dragon must hit the pillar from the OPPOSITE direction of where it should fall.
        // E.g., if we want it to fall North, the dragon must hit it flying North (from the South).
        result.optimalHitDirection = fallDirection;

        return result;
    }

    public void RemoveToppleItem(ToppleItem item)
    {
        if (availableToppleItems.Contains(item))
        {
            availableToppleItems.Remove(item);
        }
    }
}
