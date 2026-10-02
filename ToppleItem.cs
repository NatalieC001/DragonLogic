using UnityEngine;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.Environment
{
    /// <summary>
    /// Attached to destructible/interactive environmental structures (pillars, scaffolding, walls).
    /// </summary>
    public class ToppleItem : MonoBehaviour
    {
        public bool IsToppled { get; private set; } = false;

        [Header("Physics")]
        public float toppleForce = 15f;
        public Vector3 toppleDirectionOverride = Vector3.zero;

        [Header("Elemental Spills")]
        [Tooltip("If true, this object acts like a barrel (Oil, Water, Sticky) and spills its contents upon toppling.")]
        public bool spillsContents = false;
        public HazardType spillType = HazardType.Water;
        [Tooltip("The GroundHazard prefab to instantiate for the spill.")]
        public GameObject spillPrefab;

        private void Awake()
        {
            // Enforce physics variables in code to prevent manual inspector errors
            gameObject.layer = LayerMask.NameToLayer("Default"); // Ensure it can be collided with

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                // Main collider stays solid so player can't walk through it
                col.isTrigger = false;

                // Dynamically add a slightly larger trigger collider to detect the kinematic boss
                BoxCollider bossTrigger = gameObject.AddComponent<BoxCollider>();
                bossTrigger.isTrigger = true;

                // Use local size if it's already a box, or default to a safe local scale approximation
                if (col is BoxCollider box)
                {
                    bossTrigger.size = box.size * 1.2f;
                    bossTrigger.center = box.center;
                }
                else
                {
                    // Fallback for MeshColliders or Capsules, assume local scale of 1 is the base
                    bossTrigger.size = Vector3.one * 1.2f;
                }
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Wait for the dragon to hit it before physics takes over
            }
        }

        /// <summary>
        /// Triggered by the Dragon when it swoops through this object.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // If the Dragon's physical body enters the trigger, natively topple it.
            if (other.CompareTag("Enemy") || other.GetComponentInParent<DragonBrain>() != null)
            {
                // Calculate the physical impact vector based on the Dragon's forward momentum
                Vector3 impactDir = other.transform.forward;

                // Fallback just in case forward is zeroed
                if (impactDir.sqrMagnitude < 0.01f)
                {
                    impactDir = (transform.position - other.transform.position).normalized;
                }

                Topple(impactDir);
            }
        }

        public void Topple(Vector3 attackDirection)
        {
            if (IsToppled) return;
            
            IsToppled = true;
            
            Vector3 pushDir = toppleDirectionOverride != Vector3.zero ? toppleDirectionOverride : attackDirection.normalized;
            pushDir += Vector3.down * 0.5f;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;

                // Main collider is already solid.

                rb.AddForce(pushDir.normalized * toppleForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * toppleForce, ForceMode.Impulse);
            }
            
            // Let the SpatialStrategyMiniGame know it's no longer a valid target
            SpatialStrategyMiniGame strategy = FindFirstObjectByType<SpatialStrategyMiniGame>();
            if (strategy != null)
            {
                strategy.RemoveToppleItem(this);
            }

            // Wait a brief moment for physics to settle before calculating footprint
            StartCoroutine(CalculateFootprintCoroutine(pushDir.normalized));
        }

        private System.Collections.IEnumerator CalculateFootprintCoroutine(Vector3 fallDirection)
        {
            yield return new WaitForSeconds(1.5f);

            GameBoard board = FindFirstObjectByType<GameBoard>();
            if (board != null)
            {
                // The physical barrel itself becomes an impassable obstacle
                Vector2Int gridPos = board.WorldToGrid(transform.position);
                board.MarkTileImpassable(gridPos);
                Debug.Log($"[ToppleItem] Marked {gridPos} as impassable.");
            }

            if (spillsContents)
            {
                SpillContents(fallDirection, board);
            }
        }

        /// <summary>
        /// Handles generating elemental surface hazards when a barrel topples, triggering gameboard recipes.
        /// </summary>
        private void SpillContents(Vector3 fallDirection, GameBoard board)
        {
            Debug.Log($"<color=cyan>[ToppleItem] Barrel toppled! Spilling {spillType} in direction {fallDirection}</color>");

            if (board == null) return;

            // Spill extends roughly 2 tiles in the direction of the fall
            Vector3 flatFallDirection = new Vector3(fallDirection.x, 0, fallDirection.z).normalized;

            Vector3 spillPos1 = transform.position + (flatFallDirection * board.tileSize);
            Vector3 spillPos2 = transform.position + (flatFallDirection * board.tileSize * 2f);

            Vector2Int grid1 = board.WorldToGrid(spillPos1);
            Vector2Int grid2 = board.WorldToGrid(spillPos2);

            board.ApplyElementToTile(grid1, spillType);
            board.ApplyElementToTile(grid2, spillType);
        }
    }

}
