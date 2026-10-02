using UnityEngine;

[CreateAssetMenu(fileName = "LeverConfig", menuName = "ScriptableObjects/LeverConfig")]
public class LeverConfigSO : ScriptableObject
{
    [Tooltip("Damage required to trigger the lever. A full power shot should equal or exceed this.")]
    public float requiredDamage = 50f;

    [Tooltip("How far to move the cover in the specified direction.")]
    public float moveDistance = 5f;

    [Tooltip("How fast the cover moves to its target position.")]
    public float moveSpeed = 2f;
}
