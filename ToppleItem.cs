using UnityEngine;
using PixelCrushers;

/// <summary>
/// Attached to destructible/interactive environmental structures (pillars, scaffolding, walls).
/// </summary>
public class ToppleItem : MonoBehaviour, IMessageHandler
{
    public bool IsToppled { get; private set; } = false;
    
    [Header("Physics")]
    public float toppleForce = 15f;
    public Vector3 toppleDirectionOverride = Vector3.zero;

    /// <summary>
    /// Triggered by the Dragon when it attacks this object.
    /// </summary>
    private void OnEnable()
    {
        MessageSystem.AddListener(this, "DragonReachedPillar", string.Empty);
    }

    private void OnDisable()
    {
        MessageSystem.RemoveListener(this, "DragonReachedPillar", string.Empty);
    }

    public void OnMessage(MessageArgs messageArgs)
    {
        if (messageArgs.message == "DragonReachedPillar")
        {
            // Only topple if THIS pillar was the intended target.
            if (messageArgs.values != null && messageArgs.values.Length > 0 && messageArgs.values[0] as ToppleItem == this)
            {
                Transform dragonTransform = ((Component)messageArgs.sender).transform;
                Vector3 attackDir = (transform.position - dragonTransform.position).normalized;
                Topple(attackDir);
            }
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
