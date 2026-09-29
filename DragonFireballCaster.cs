using UnityEngine;

/// <summary>
/// Spawns and launches a fireball toward a world position. The brain decides
/// when and at whom; this script only knows how. It does not aim on its own,
/// does not choose targets, and does not run any state logic.
///
/// Drop this on the dragon root (same GameObject as BossNavigator and
/// DragonBrain) and assign the fireball prefab and jaw bone in the inspector.
/// </summary>
public class DragonFireballCaster : MonoBehaviour
{
    [Header("Projectile")]
    [Tooltip("Prefab spawned when firing. Should have a Rigidbody on the root.")]
    public GameObject fireballPrefab;

    [Tooltip("Bone or empty the fireball spawns from. Usually the jaw or mouth.")]
    public Transform jawBone;

    [Tooltip("Initial forward speed applied to the spawned Rigidbody.")]
    public float launchSpeed = 18f;

    [Tooltip("Seconds before the spawned fireball is destroyed if it hits nothing.")]
    public float projectileLifetime = 5f;

    [Tooltip("Optional: an effect spawned at the jaw the instant a fireball leaves.")]
    public GameObject muzzleFlashPrefab;

    /// <summary>
    /// Fires a single fireball toward a world position. Direction is normalized
    /// from the jaw to the target; no lead, no prediction, no cooldown. Those
    /// are the caller's concern.
    /// </summary>
    public void LaunchAt(Vector3 target)
    {
        if (fireballPrefab == null || jawBone == null) return;

        Vector3 origin = jawBone.position;
        Vector3 dir = target - origin;

        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Quaternion rot = Quaternion.LookRotation(dir);
        GameObject fb = Instantiate(fireballPrefab, origin, rot);

        Rigidbody rb = fb.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = dir * launchSpeed;

        if (muzzleFlashPrefab != null)
            Instantiate(muzzleFlashPrefab, origin, rot);

        Destroy(fb, projectileLifetime);
    }

    /// <summary>
    /// Convenience overload for tracking shots. Leads the target using its
    /// current Rigidbody velocity so the fireball meets a moving player
    /// rather than passing behind them.
    /// </summary>
    public void LaunchAtMoving(Rigidbody targetBody)
    {
        if (targetBody == null || jawBone == null) return;

        Vector3 origin = jawBone.position;
        Vector3 targetPos = targetBody.position;

        // Simple ballistic lead: time to reach = distance / speed, then add
        // the target's velocity * that time. Good enough for slow projectiles
        // against walking players.
        float dist = Vector3.Distance(origin, targetPos);
        float leadTime = dist / Mathf.Max(0.01f, launchSpeed);
        Vector3 predicted = targetPos + targetBody.linearVelocity * leadTime;

        LaunchAt(predicted);
    }
}