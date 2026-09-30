using UnityEngine;
using System.Collections;
using PixelCrushers;

/// <summary>
/// A puzzle lever that, when shot with a full power arrow, moves a target object
/// (typically a minion cover) out of the way.
/// </summary>
public class PuzzleLever : MonoBehaviour, IArrowTarget
{
    [Header("Target & Movement")]
    [Tooltip("The GameObject to move out of the way (e.g., a wall or pillar hiding minions).")]
    public GameObject targetCover;

    [Tooltip("The local direction to move the cover.")]
    public Vector3 moveDirection = Vector3.down;

    [Tooltip("How far to move the cover in the specified direction.")]
    public float moveDistance = 5f;

    [Tooltip("How fast the cover moves to its target position.")]
    public float moveSpeed = 2f;

    [Header("Dispel Mechanic")]
    [Tooltip("Damage required to trigger the lever. A full power shot should equal or exceed this.")]
    public float requiredDamage = 50f;

    private bool isActivated = false;
    private Vector3 initialCoverPosition;
    private Vector3 targetCoverPosition;

    private void Awake()
    {
        // Ensure this lever is on the Enemy layer so the arrow can hit it
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
    {
        if (isActivated) return;

        // Check if the shot was powerful enough (full power shot requirement)
        if (damage >= requiredDamage)
        {
            Debug.Log($"<color=cyan>[PuzzleLever] Lever hit with enough force! Activating.</color>");
            ActivateLever();
        }
        else
        {
            Debug.Log($"[PuzzleLever] Arrow hit lever, but not enough force. Damage: {damage}");
        }
    }

    private void ActivateLever()
    {
        if (targetCover == null)
        {
            Debug.LogWarning("[PuzzleLever] Activated, but no target cover assigned.");
            return;
        }

        isActivated = true;
        initialCoverPosition = targetCover.transform.position;
        targetCoverPosition = initialCoverPosition + (moveDirection.normalized * moveDistance);

        StartCoroutine(MoveCoverRoutine());
    }

    private IEnumerator MoveCoverRoutine()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            targetCover.transform.position = Vector3.Lerp(initialCoverPosition, targetCoverPosition, t);
            yield return null;
        }

        targetCover.transform.position = targetCoverPosition;

        // If the cover has a CoverPoint script, tell it to trigger the minions
        CoverPoint coverPoint = targetCover.GetComponent<CoverPoint>();
        if (coverPoint != null)
        {
            coverPoint.TriggerCharge();
        }
        else
        {
            // If the CoverPoint is on a child or parent, try to find it
            coverPoint = targetCover.GetComponentInChildren<CoverPoint>();
            if (coverPoint != null)
            {
                coverPoint.TriggerCharge();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (targetCover != null)
        {
            Gizmos.color = Color.magenta;
            Vector3 startPos = targetCover.transform.position;
            Vector3 endPos = startPos + (moveDirection.normalized * moveDistance);

            // Draw a line indicating the movement path
            Gizmos.DrawLine(startPos, endPos);

            // Draw a wire cube at the destination
            Gizmos.DrawWireCube(endPos, targetCover.GetComponent<Collider>() != null ? targetCover.GetComponent<Collider>().bounds.size : Vector3.one);

            // Draw a line from the lever to the target for visual connection
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, startPos);
        }
    }
}
