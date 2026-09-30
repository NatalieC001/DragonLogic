using UnityEngine;

[CreateAssetMenu(fileName = "DragonAIConfig", menuName = "ScriptableObjects/DragonAIConfig")]
public class DragonAIConfigSO : ScriptableObject
{
    [Header("Combat Variables")]
    public float toppleReachDistance = 5f;
    public float attackCooldown = 2f;

    [Header("Health & Scaling")]
    public float healthPerSegment = 10f;

    [Tooltip("The percentage strength buff applied to the Dragon for every living minion. (e.g. 0.02 = 2%)")]
    public float buffPerLivingMinion = 0.02f;

    [Header("Regeneration")]
    [Tooltip("Seconds to wait between regrowing each segment while at the crystal.")]
    public float regrowCooldown = 5f;

    [Tooltip("Maximum time the dragon can spend regenerating at the crystal before it must resume attacking.")]
    public float maxRegenerationTime = 20f;

    [Tooltip("If the number of living segments falls to this or lower, survival becomes the highest priority.")]
    public int criticalSegmentThreshold = 3;
}
