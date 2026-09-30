// ==========================================
// ATTACHMENT INSTRUCTIONS:
// DO NOT attach this directly as a component!
// This is an Interface.
// Your Enemy Health scripts should implement this interface
// like this: public class EnemyHealth : MonoBehaviour, IArrowTarget
// ==========================================

using UnityEngine;

/// <summary>
/// Interface for any object that can be hit by an arrow.
/// Keeps the arrow logic strictly separated from the enemy logic.
/// </summary>
public interface IArrowTarget
{
    void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType);
}
