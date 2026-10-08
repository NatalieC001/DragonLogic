using UnityEngine;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

public class DragonProjectile : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("How much health this projectile has (so players can shoot it down).")]
    public float health = 1f;

    [Tooltip("The hazard type applied if it hits the floor.")]
    public HazardType payloadType = HazardType.Fire;

    [Header("Effects")]
    public GameObject explosionEffectPrefab;

    private bool hasTriggered = false;

    // Called if player shoots the projectile (assuming player deals damage via similar method)
    public void TakeDamage(float amount)
    {
        if (hasTriggered) return;
        health -= amount;
        if (health <= 0)
        {
            DestroyProjectile(true);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.collider, collision.contacts[0].point);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other, transform.position);
    }

    private void HandleImpact(Collider other, Vector3 hitPoint)
    {
        if (hasTriggered) return;

        // Check if we hit the player
        if (other.CompareTag("Player"))
        {
            hasTriggered = true;
            // Player hit: triggers VR blindness or damage logic.
            // In original code VRHeadsetStickyBlindness triggers itself, but we can call an event here or rely on the player's collider taking damage.
            // For now, let's just make it do damage or let the player scripts handle it natively via tag.
            Debug.Log("[DragonProjectile] Hit the Player directly!");
            DestroyProjectile(true);
            return;
        }

        // Otherwise, assume we hit the environment/floor.
        // Convert the hit point to a grid tile and apply the element.
        GameBoard board = FindFirstObjectByType<GameBoard>();
        if (board != null)
        {
            Vector2Int gridPos = board.WorldToGrid(hitPoint);
            board.ApplyElementToTile(gridPos, payloadType);
        }

        hasTriggered = true;
        DestroyProjectile(true);
    }

    private void DestroyProjectile(bool spawnExplosion)
    {
        // Fallback: If no explosion effect was assigned via script dynamically, try to load one from Resources or ignore it
        if (spawnExplosion && explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
