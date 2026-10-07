using UnityEngine;
using PixelCrushers;

/// <summary>
/// Pillar B — Vitals container.
///
/// Holds health, stamina, and threshold state. Emits signals to the
/// Message System so the brain (QM node tree) can decide what to do.
/// No AI, no movement, no navigation.
/// </summary>
public class BossStatsAndHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 1000f;
    [Range(0f, 1f)]
    [SerializeField] private float criticalHealthThreshold = 0.25f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;

    [Header("Burst Detection")]
    [Tooltip("Amount of damage within burstWindow that triggers BurstDamageTaken.")]
    [SerializeField] private float burstDamageThreshold = 150f;
    [SerializeField] private float burstWindow = 2f;

    [Header("Shielding")]
    [Tooltip("While true, incoming damage is ignored.")]
    [SerializeField] private bool isShielding;

    private float currentHealth;
    private float currentStamina;
    private bool isDead;
    private bool criticalFired;

    private float burstAccumulator;
    private float burstTimer;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;
    public bool IsDead => isDead;
    public bool IsCritical => currentHealth <= maxHealth * criticalHealthThreshold;
    public bool IsShielding { get => isShielding; set => isShielding = value; }

    // ADDED — read-only accessors so the custom Editor can draw the bars.
    public float MaxStamina => maxStamina;
    public float CriticalHealthThreshold => criticalHealthThreshold;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
    }

    private void Update()
    {
        if (burstTimer > 0f)
        {
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0f) burstAccumulator = 0f;
        }
    }

    public void TakeDamage(float amount, ElementTypeOB7 type)
    {
        if (isDead || amount <= 0f) return;
        if (isShielding) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        EventManager.TriggerPlayerShootsBoss(amount);

        burstAccumulator += amount;
        burstTimer = burstWindow;
        if (burstAccumulator >= burstDamageThreshold)
        {
            burstAccumulator = 0f;
            burstTimer = 0f;
            EventManager.TriggerBurstDamageTaken();
        }

        if (!criticalFired && IsCritical)
        {
            criticalFired = true;
            EventManager.TriggerHealthThresholdReached();
        }

        if (currentHealth <= 0f)
        {
            isDead = true;
            EventManager.TriggerBossDied();
        }
    }

    public void DrainStamina(float amount)
    {
        if (amount <= 0f) return;
        currentStamina = Mathf.Max(0f, currentStamina - amount);

        if (currentStamina <= 0f)
            EventManager.TriggerStaminaDepleted();
    }

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f) return;
        bool wasBelowFull = currentStamina < maxStamina;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);

        if (wasBelowFull && currentStamina >= maxStamina)
            EventManager.TriggerStaminaFullyCharged();
    }

    public void RestoreHealth(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (criticalFired && !IsCritical) criticalFired = false;
    }
}