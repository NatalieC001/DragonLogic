using System;

/// <summary>
/// Global hub for standard C# game and combat events.
/// Strictly separated from PixelCrushers MessageSystem, which is reserved exclusively for spline-based spatial logic.
/// </summary>
public static class EventManager
{
    // --- Combat / Minion Events ---
    public static event Action<StandardCreature> OnMinionUnderFire;
    public static event Action OnDragonNeedsSupport;

    // --- Boss Stats & Health Events ---
    public static event Action<float> OnPlayerShootsBoss;
    public static event Action OnBurstDamageTaken;
    public static event Action OnHealthThresholdReached;
    public static event Action OnBossDied;
    public static event Action OnStaminaDepleted;
    public static event Action OnStaminaFullyCharged;

    // --- Triggers ---
    public static void TriggerMinionUnderFire(StandardCreature minion) => OnMinionUnderFire?.Invoke(minion);
    public static void TriggerDragonNeedsSupport() => OnDragonNeedsSupport?.Invoke();

    public static void TriggerPlayerShootsBoss(float damage) => OnPlayerShootsBoss?.Invoke(damage);
    public static void TriggerBurstDamageTaken() => OnBurstDamageTaken?.Invoke();
    public static void TriggerHealthThresholdReached() => OnHealthThresholdReached?.Invoke();
    public static void TriggerBossDied() => OnBossDied?.Invoke();
    public static void TriggerStaminaDepleted() => OnStaminaDepleted?.Invoke();
    public static void TriggerStaminaFullyCharged() => OnStaminaFullyCharged?.Invoke();
}
