using UnityEngine;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.GameBoardSystem
{
    [CreateAssetMenu(fileName = "HazardConfig", menuName = "ScriptableObjects/HazardConfig")]
    public class HazardConfigSO : ScriptableObject
    {
        [Tooltip("Damage per second applied to anything inside the hazard.")]
        public float damagePerSecond = 10f;

        [Tooltip("Total duration before the hazard is destroyed.")]
        public float duration = 12f;

        [Tooltip("Amount of damage required to clear this hazard (Full power shot = 100).")]
        public float dispelHealth = 100f;
    }

}
