using UnityEngine;
using System.Collections;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.GameBoardSystem
{
    /// <summary>
    /// A completely standalone script attached to the VR player setup.
    /// Handles zero AI math and zero decision logic.
    /// Acts purely as a responsive feedback class that listens to spatial state changes.
    /// </summary>
    public class VRHeadsetStickyBlindness : MonoBehaviour
    {
        public PlayerDebuffConfigSO config;

        [Header("Visual Feedback")]
        [Tooltip("Particle effect instantiated directly onto the VR camera viewport.")]
        public GameObject viewportObfuscationParticles;

        [Tooltip("Streaming localized particle effects directly around the player's feet.")]
        public GameObject groundStreamParticles;

        public static event System.Action OnPlayerBlinded;
        public static event System.Action OnPlayerSightRestored;

        private bool isBlinded = false;
        private Coroutine recoveryCoroutine;

        private void Start()
        {
            if (viewportObfuscationParticles != null) viewportObfuscationParticles.SetActive(false);
            if (groundStreamParticles != null) groundStreamParticles.SetActive(false);
        }

        private int activeHazardZones = 0;

        /// <summary>
        /// Triggered when the player enters a Dark Fire zone (OnTriggerEnter).
        /// </summary>
        public void TriggerBlindness()
        {
            activeHazardZones++;

            if (recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = null;
            }

            if (!isBlinded)
            {
                isBlinded = true;
                if (viewportObfuscationParticles != null) viewportObfuscationParticles.SetActive(true);
                if (groundStreamParticles != null) groundStreamParticles.SetActive(true);

                OnPlayerBlinded?.Invoke();
                Debug.Log("[VRHeadsetStickyBlindness] Player entered dark fire. Blindness applied. Shot power halved.");
            }
        }

        /// <summary>
        /// Triggered when the player steps completely out of a dark fire boundary (OnTriggerExit).
        /// </summary>
        public void StartRecovery()
        {
            activeHazardZones = Mathf.Max(0, activeHazardZones - 1);

            if (isBlinded && activeHazardZones == 0 && recoveryCoroutine == null)
            {
                float time = config != null ? config.recoveryTime : 10f;
                recoveryCoroutine = StartCoroutine(RecoveryRoutine(time));
            }
        }

        private IEnumerator RecoveryRoutine(float recoveryTime)
        {
            Debug.Log($"[VRHeadsetStickyBlindness] Player left dark fire. Starting {recoveryTime}s recovery...");
            yield return new WaitForSeconds(recoveryTime);

            // Clean exit reset
            isBlinded = false;
            if (viewportObfuscationParticles != null) viewportObfuscationParticles.SetActive(false);
            if (groundStreamParticles != null) groundStreamParticles.SetActive(false);

            OnPlayerSightRestored?.Invoke();
            Debug.Log("[VRHeadsetStickyBlindness] Recovery complete. Full weapon power restored.");
            recoveryCoroutine = null;
        }

        /// <summary>
        /// Called by the Bow/Arrow mechanics to weaken outgoing shots.
        /// </summary>
        public float GetOutgoingDamageMultiplier()
        {
            if (isBlinded && config != null)
            {
                return config.stickyBlindnessDamageMultiplier;
            }
            return 1f; // Full power
        }
    }

}
