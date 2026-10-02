using UnityEngine;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.AI
{
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
    }

}
