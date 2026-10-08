using UnityEngine;
using VRDragonBoss.Environment; // Added to fix namespace issue for HazardType

public class DragonFireballCaster : MonoBehaviour
{
    [Header("Projectile")]
    public GameObject fireballPrefab;
    public Transform jawBone;
    public float launchSpeed = 18f;
    public float projectileLifetime = 5f;
    public GameObject muzzleFlashPrefab;

    public HazardType currentPayloadType = HazardType.Fire;

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

        DragonProjectile proj = fb.GetComponent<DragonProjectile>();
        if (proj == null)
        {
            proj = fb.AddComponent<DragonProjectile>();
        }
        proj.payloadType = currentPayloadType;

        if (muzzleFlashPrefab != null)
            Instantiate(muzzleFlashPrefab, origin, rot);

        Destroy(fb, projectileLifetime);
    }

    public void LaunchAtMoving(Rigidbody targetBody)
    {
        if (targetBody == null || jawBone == null) return;
        Vector3 origin = jawBone.position;
        Vector3 targetPos = targetBody.position;
        float dist = Vector3.Distance(origin, targetPos);
        float leadTime = dist / Mathf.Max(0.01f, launchSpeed);
        Vector3 predicted = targetPos + targetBody.linearVelocity * leadTime;
        LaunchAt(predicted);
    }
}
