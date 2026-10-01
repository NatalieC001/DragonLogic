using UnityEngine;
using System.Collections;
using PixelCrushers;
using UnityEngine.Events;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;
using System;

namespace VRDragonBoss.Environment
{
    /// <summary>
    /// A puzzle lever that, when shot with a full power arrow, moves a target object
    /// (typically a minion cover) out of the way.
    /// </summary>
    public class PuzzleLever : MonoBehaviour, IArrowTarget
    {
        [Header("Target & Movement")]
        [Tooltip("The GameObject to move out of the way (e.g., a wall or pillar hiding minions).")]
        public GameObject targetCover;

    [Header("Configuration")]
    public LeverConfigSO config;

    [Header("Events")]
    public UnityEvent OnLeverActivated;
    public UnityEvent OnCoverMoved;

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
        float reqDamage = config != null ? config.requiredDamage : 50f;
        if (damage >= reqDamage)
        {
            // Ensure this lever is on the Enemy layer so the arrow can hit it
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
        {
            if (isActivated) return;

            // Check if the shot was powerful enough (full power shot requirement)
            float reqDamage = config != null ? config.requiredDamage : 50f;
            if (damage >= reqDamage)
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
        OnLeverActivated?.Invoke();
        initialCoverPosition = targetCover.transform.position;
        float distance = config != null ? config.moveDistance : 5f;
        targetCoverPosition = initialCoverPosition + (moveDirection.normalized * distance);

            StartCoroutine(MoveCoverRoutine());
        }

    private IEnumerator MoveCoverRoutine()
    {
        float t = 0f;
        float speed = config != null ? config.moveSpeed : 2f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            targetCover.transform.position = Vector3.Lerp(initialCoverPosition, targetCoverPosition, t);
            yield return null;
        }

        targetCover.transform.position = targetCoverPosition;
        OnCoverMoved?.Invoke();

            // If the cover has a CoverPoint script, tell it to trigger the minions
            global::CoverPoint coverPoint = targetCover.GetComponent<global::CoverPoint>();
            if (coverPoint != null)
            {
                coverPoint.TriggerCharge();
            }
            else
            {
                // If the CoverPoint is on a child or parent, try to find it
                coverPoint = targetCover.GetComponentInChildren<global::CoverPoint>();
                if (coverPoint != null)
                {
                    coverPoint.TriggerCharge();
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Vector3 startPos = targetCover.transform.position;
            float distance = config != null ? config.moveDistance : 5f;
            Vector3 endPos = startPos + (moveDirection.normalized * distance);

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

}
