using UnityEngine;
/// <summary>
/// Attached to destructible/interactive environmental structures (pillars, scaffolding, walls).
/// </summary>
public class ToppleItem : MonoBehaviour
{
    public bool IsToppled { get; private set; } = false;
    
    [Header("Physics")]
    public float toppleForce = 15f;
    public Vector3 toppleDirectionOverride = Vector3.zero;

    private void Awake()
    {
        // Enforce physics variables in code to prevent manual inspector errors
        gameObject.layer = LayerMask.NameToLayer("Default"); // Ensure it can be collided with

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false; // Solid physical object
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Wait for the dragon to hit it before physics takes over
        }
    }

    /// <summary>
    /// Triggered by the Dragon when it attacks this object.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // If the Dragon's physical body hits the pillar, natively topple it.
        // The dragon should have a Rigidbody (even if kinematic) and be appropriately tagged or layered.
        if (collision.collider.CompareTag("Enemy") || collision.collider.GetComponentInParent<DragonBrain>() != null)
        {
            // Calculate the physical impact vector based on the Dragon's forward momentum
            Vector3 impactDir = collision.transform.forward;

            // Fallback just in case forward is zeroed
            if (impactDir.sqrMagnitude < 0.01f)
            {
                impactDir = (transform.position - collision.transform.position).normalized;
            }

            Topple(impactDir);
        }
    }

    public void Topple(Vector3 attackDirection)
    {
        if (IsToppled) return;
        
        IsToppled = true;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            
            // Push it over based on where the dragon attacked from, or use an override
            Vector3 pushDir = toppleDirectionOverride != Vector3.zero ? toppleDirectionOverride : attackDirection.normalized;
            
            // Add a bit of downward force so it actually falls and doesn't just slide
            pushDir += Vector3.down * 0.5f;
            
            rb.AddForce(pushDir.normalized * toppleForce, ForceMode.Impulse);
            
            // Add some random torque to make the fall look chaotic
            rb.AddTorque(Random.insideUnitSphere * toppleForce, ForceMode.Impulse);
        }
        
        // Let the SpatialStrategyMiniGame know it's no longer a valid target
        SpatialStrategyMiniGame strategy = FindFirstObjectByType<SpatialStrategyMiniGame>();
        if (strategy != null)
        {
            strategy.RemoveToppleItem(this);
        }
    }
}
