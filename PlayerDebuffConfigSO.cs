using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDebuffConfig", menuName = "ScriptableObjects/PlayerDebuffConfig")]
public class PlayerDebuffConfigSO : ScriptableObject
{
    [Tooltip("How long it takes in seconds to fully recover from debuffs once leaving the hazard.")]
    public float recoveryTime = 10f;

    [Tooltip("Multiplier applied to the bow draw speed when affected by sticky debuff.")]
    public float stickyBowDrawMultiplier = 0.33f;

    [Tooltip("Multiplier applied to outgoing damage when affected by sticky blindness.")]
    public float stickyBlindnessDamageMultiplier = 0.5f;
}
