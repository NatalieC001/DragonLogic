using UnityEngine;
using PixelCrushers;
using Dreamteck.Splines;
using System.Collections.Generic;

public class BossNavigator : MonoBehaviour, IMessageHandler
{
    private enum MovementMode { SplineFollowing, Freestyle, MovingToSpline }

    [SerializeField] private SplineFollower splineFollower;
    private MovementMode currentMode = MovementMode.Freestyle;
    private Vector3 targetDestination;

    [Header("Movement")]
    [SerializeField] private float flightSpeed = 8f;

    [Header("Turning")]
    [SerializeField] private float maxTurnRate = 35f;
    [SerializeField] private float turnAcceleration = 25f;
    [SerializeField] private float climbLaziness = 0.5f;

    [Header("Flight Bounds")]
    [SerializeField] private DragonFlightBoundsGizmo flightBounds;

    [Header("Waypoint Arrival")]
    [SerializeField] private float arrivalRadius = 5f;

    [Header("Path Source")]
    [SerializeField] private BossPathManager pathManager;
    [SerializeField] private PathTypeTag.PathType pathType = PathTypeTag.PathType.Airborne;

    [Header("Scene Targets")]
    [SerializeField] private HealthCrystal defendCrystal;
    [SerializeField] private BossStatsAndHealth vitals;

    [Header("Defend")]
    [SerializeField] private float defendArrivalRadius = 8f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawNavGizmos = true;
    [SerializeField] private float gizmoRadius = 0.5f;

    private CreatureStatusEffects statusEffects;
    private bool defending;
    private SplineComputer pendingSpline;
    private double pendingSplineEntryPercent;
    private bool pendingSplineEntryValid;

    private float currentTurnRate;
    private float currentClimbRate;

    private float splineEntryPercent;
    private bool splineHasMoved;

    // --- Debug state ---
    private Vector3 dbgDestination;
    private Vector3 dbgSplineEntry;
    private double dbgSplinePercent;
    private bool dbgHasSplineEntry;
    private float nextTransitLog;

    private void Awake()
    {
        statusEffects = GetComponent<CreatureStatusEffects>();

        if (splineFollower == null) splineFollower = GetComponent<SplineFollower>();
        if (vitals == null) vitals = GetComponent<BossStatsAndHealth>();

        if (flightBounds == null)
        {
            Debug.LogError("[BossNavigator] flightBounds is required.");
            enabled = false;
            return;
        }

        splineFollower.follow = false;
        splineFollower.followSpeed = flightSpeed;
        transform.position = flightBounds.ClampToBounds(transform.position);

        PickNewDestination();
    }

    private void Update()
    {
        TickMovement();
        CheckDefendArrival();
    }

    private void OnDrawGizmos()
    {
        if (!drawNavGizmos) return;

        // Boss position
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);

        // Current destination (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetDestination, gizmoRadius);
        Gizmos.DrawLine(transform.position, targetDestination);

        // Projected spline entry point (magenta)
        if (dbgHasSplineEntry)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dbgSplineEntry, gizmoRadius * 1.5f);
            Gizmos.DrawLine(targetDestination, dbgSplineEntry);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(targetDestination + Vector3.up * 2f, "Dest");

        if (dbgHasSplineEntry)
        {
            UnityEditor.Handles.color = Color.magenta;
            UnityEditor.Handles.Label(dbgSplineEntry + Vector3.up * 2f,
                $"SplineEntry pct={dbgSplinePercent:F3}");
        }
#endif
    }

    public void Freestyle(Vector3 pos) { SetDestination(pos); }
    public void FreestyleArea() { PickNewDestination(); }
    public void FreestyleToPlayer() { SetDestination(ResolvePlayerPosition()); }
    public void MoveToNearestObservation() { RequestNearestObservationPath(); }
    public void MoveToNearestEscape() { RequestNearestEscapePath(); }
    public void DefendCrystalCommand() { HandleDefend(); }

    public void HoldPosition() { PickNewDestination(); }

    public HealthCrystal DefendCrystal => defendCrystal;

    public void OnMessage(MessageArgs messageArgs)
    {
        switch (messageArgs.message)
        {
            case "Swoop": FreestyleToPlayer(); break;
            case "TacticalWeave": MoveToNearestEscape(); break;
            case "MoveToObservation": MoveToNearestObservation(); break;
            case "MoveToSpline": if (pendingSpline != null) MoveToSpline(pendingSpline); break;
            case "Idle": HoldPosition(); break;
            case "Defend": DefendCrystalCommand(); break;
            case "CrystalDestroyed":
            case "StopDefend": ExitDefend(); break;
        }
    }

    public void SetDestination(Vector3 pos)
    {
        splineFollower.follow = false;
        targetDestination = flightBounds.ClampToBounds(pos);
        currentMode = MovementMode.Freestyle;
        pendingSpline = null;
        pendingSplineEntryValid = false;
        dbgHasSplineEntry = false;

        Debug.Log($"[BossNavigator] SetDestination {targetDestination} (mode=Freestyle)");
    }

    public void MoveToSpline(SplineComputer spline)
    {
        if (spline == null) return;

        splineFollower.follow = false;
        pendingSpline = spline;

        // Record what the boss was aiming at before we retarget.
        dbgDestination = targetDestination;

        SplineSample sample = new SplineSample();
        spline.Project(targetDestination, ref sample);

        pendingSplineEntryPercent = sample.percent;
        pendingSplineEntryValid = true;

        dbgSplineEntry = flightBounds.ClampToBounds(sample.position);
        dbgSplinePercent = sample.percent;
        dbgHasSplineEntry = true;

        targetDestination = dbgSplineEntry;
        currentMode = MovementMode.MovingToSpline;

        Debug.Log($"[BossNavigator] MoveToSpline spline='{spline.name}' " +
                  $"origDest={dbgDestination} entry={dbgSplineEntry} pct={dbgSplinePercent:F4} " +
                  $"distDestToEntry={Vector3.Distance(dbgDestination, dbgSplineEntry):F2}");

        nextTransitLog = Time.time;
    }

    public void RequestNearestEscapePath()
    {
        if (pathManager == null) return;
        var paths = pathManager.GetEscapePaths(pathType);
        GameObject pathObj = pathManager.GetNearestPathBySpline(targetDestination, paths);
        if (pathObj == null) return;

        SplineComputer spline = pathObj.GetComponentInChildren<SplineComputer>(true);
        if (spline != null) MoveToSpline(spline);
    }

    public void RequestNearestObservationPath()
    {
        if (pathManager == null) return;
        var paths = pathManager.GetObservationPaths(pathType);
        GameObject pathObj = pathManager.GetNearestPathBySpline(targetDestination, paths);
        if (pathObj == null) return;

        SplineComputer spline = pathObj.GetComponentInChildren<SplineComputer>(true);
        if (spline != null) MoveToSpline(spline);
    }

    private void HandleDefend()
    {
        if (defendCrystal == null) return;
        SetDestination(defendCrystal.transform.position);
        defending = true;
    }

    private void CheckDefendArrival()
    {
        if (!defending || defendCrystal == null || vitals == null) return;

        if (defendCrystal.IsDestroyed) { ExitDefend(); return; }

        if (Vector3.Distance(transform.position, defendCrystal.transform.position) <= defendArrivalRadius)
        {
            if (!vitals.IsShielding) { vitals.IsShielding = true; defendCrystal.BeginFeeding(vitals); PixelCrushers.MessageSystem.SendMessage(this, "DragonReachedCrystal", string.Empty); }

        }
    }

    private void ExitDefend()
    {
        if (defending) { PixelCrushers.MessageSystem.SendMessage(this, "DragonLeftCrystal", string.Empty); } defending = false;
        if (vitals != null) vitals.IsShielding = false;
        if (defendCrystal != null) defendCrystal.StopFeeding();
    }

    private Vector3 ResolvePlayerPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform.position : transform.position;
    }

    private void PickNewDestination()
    {
        Vector3 current = transform.position;

        for (int i = 0; i < 8; i++)
        {
            Vector3 candidate = flightBounds.RandomPointInside();
            if ((candidate - current).sqrMagnitude > arrivalRadius * arrivalRadius * 4f)
            {
                SetDestination(candidate);
                return;
            }
        }

        SetDestination(flightBounds.RandomPointInside());
    }

    private void TickMovement()
    {
        float speed = flightSpeed *
                      (statusEffects != null ? statusEffects.CurrentSpeedMultiplier : 1f);

        switch (currentMode)
        {
            case MovementMode.SplineFollowing: TickSplineFollowing(); break;
            case MovementMode.Freestyle: TickFreestyle(); break;
            case MovementMode.MovingToSpline: TickMoveToSpline(); break;
        }

        // Constant forward motion — exactly once per frame, never scaled, never paused.
        transform.position += transform.forward * speed * Time.deltaTime;
        transform.position = flightBounds.ClampToBounds(transform.position);
    }

    private void TickSplineFollowing()
    {
        splineFollower.followSpeed = flightSpeed;

        if (!splineFollower.follow)
        {
            PickNewDestination();
            return;
        }

        float pct = (float)splineFollower.GetPercent();

        // Do not exit until we have actually travelled away from the entry percent.
        if (!splineHasMoved && Mathf.Abs(pct - splineEntryPercent) > 0.02f)
            splineHasMoved = true;

        if (splineHasMoved && pct >= 0.98f)
        {
            splineFollower.follow = false;
            dbgHasSplineEntry = false;
            PickNewDestination();
        }
    }

    private void TickFreestyle()
    {
        Vector3 toTarget = targetDestination - transform.position;
        if (toTarget.sqrMagnitude <= arrivalRadius * arrivalRadius)
        {
            PickNewDestination();
            toTarget = targetDestination - transform.position;
        }

        if (toTarget.sqrMagnitude < 0.0001f) return;

        TurnToward(toTarget.normalized);
    }

    private void TickMoveToSpline()
    {
        splineFollower.follow = false;

        if (pendingSpline == null || !pendingSplineEntryValid)
        {
            PickNewDestination();
            return;
        }

        // Steer toward the FIXED destination captured at commit time.
        Vector3 toTarget = targetDestination - transform.position;

        if (Time.time >= nextTransitLog)
        {
            Debug.Log($"[BossNavigator] Transit boss={transform.position} dest={targetDestination} " +
                      $"dist={Mathf.Sqrt(toTarget.sqrMagnitude):F2} arrivalRadius={arrivalRadius:F2}");
            nextTransitLog = Time.time + 0.2f;
        }

        if (toTarget.sqrMagnitude > 0.01f)
            TurnToward(toTarget.normalized);

        if (toTarget.sqrMagnitude <= arrivalRadius * arrivalRadius)
        {
            Debug.Log($"[BossNavigator] Arrived at spline entry. " +
                      $"bossPos={transform.position} " +
                      $"dest={targetDestination} " +
                      $"entryPct={pendingSplineEntryPercent:F4} " +
                      $"dist={Vector3.Distance(transform.position, targetDestination):F2}");

            splineFollower.spline = pendingSpline;
            splineFollower.SetPercent(pendingSplineEntryPercent);

            splineEntryPercent = (float)pendingSplineEntryPercent;
            splineHasMoved = false;

            splineFollower.follow = true;
            pendingSpline = null;
            pendingSplineEntryValid = false;
            currentMode = MovementMode.SplineFollowing;
        }
    }

    private void TurnToward(Vector3 desiredDir)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatDesired = Vector3.ProjectOnPlane(desiredDir, Vector3.up).normalized;

        if (flatForward.sqrMagnitude < 0.0001f) flatForward = flatDesired;

        if (flatDesired.sqrMagnitude > 0.0001f)
        {
            float leftRightAngle = Vector3.SignedAngle(flatForward, flatDesired, Vector3.up);
            float wantedTurnRate = Mathf.Clamp(leftRightAngle * 2f, -maxTurnRate, maxTurnRate);

            currentTurnRate = Mathf.MoveTowards(
                currentTurnRate, wantedTurnRate, turnAcceleration * Time.deltaTime);

            transform.Rotate(Vector3.up, currentTurnRate * Time.deltaTime, Space.World);
        }

        Vector3 right = transform.right;
        Vector3 fwdForClimb = Vector3.ProjectOnPlane(transform.forward, right).normalized;
        Vector3 wantForClimb = Vector3.ProjectOnPlane(desiredDir, right).normalized;

        if (fwdForClimb.sqrMagnitude > 0.0001f && wantForClimb.sqrMagnitude > 0.0001f)
        {
            float upDownAngle = Vector3.SignedAngle(fwdForClimb, wantForClimb, right);
            float wantedClimbRate = Mathf.Clamp(
                upDownAngle * 2f, -maxTurnRate, maxTurnRate) * climbLaziness;

            currentClimbRate = Mathf.MoveTowards(
                currentClimbRate, wantedClimbRate, turnAcceleration * Time.deltaTime);

            transform.Rotate(right, currentClimbRate * Time.deltaTime, Space.World);
        }
    }
}

/////The basic freestyling motion works. OK, but we need to move. Move it to the spline. From where it set the destination it's going to go. Uh, I can't set the destination but then jump onto the start of the spline because those are two different points. 
//using Dreamteck.Splines;
//using PixelCrushers;
//using System.Collections.Generic;
//using UnityEngine;

//public class BossNavigator : MonoBehaviour, IMessageHandler
//{
//    private enum MovementMode { SplineFollowing, Freestyle, MovingToSpline }

//    [SerializeField] private SplineFollower splineFollower;
//    private MovementMode currentMode = MovementMode.Freestyle;
//    private Vector3 targetDestination;

//    [Header("Movement")]
//    [SerializeField] private float flightSpeed = 8f;

//    [Header("Turning")]
//    [SerializeField] private float maxTurnRate = 35f;
//    [SerializeField] private float turnAcceleration = 25f;
//    [SerializeField] private float climbLaziness = 0.5f;

//    [Header("Flight Bounds")]
//    [SerializeField] private DragonFlightBoundsGizmo flightBounds;

//    [Header("Waypoint Arrival")]
//    [SerializeField] private float arrivalRadius = 5f;

//    [Header("Path Source")]
//    [SerializeField] private BossPathManager pathManager;
//    [SerializeField] private PathTypeTag.PathType pathType = PathTypeTag.PathType.Airborne;

//    [Header("Scene Targets")]
//    [SerializeField] private HealthCrystal defendCrystal;
//    [SerializeField] private BossStatsAndHealth vitals;

//    [Header("Defend")]
//    [SerializeField] private float defendArrivalRadius = 8f;

//    private CreatureStatusEffects statusEffects;
//    private bool defending;
//    private SplineComputer pendingSpline;

//    private float currentTurnRate;
//    private float currentClimbRate;

//    private void Awake()
//    {
//        statusEffects = GetComponent<CreatureStatusEffects>();

//        if (splineFollower == null) splineFollower = GetComponent<SplineFollower>();
//        if (vitals == null) vitals = GetComponent<BossStatsAndHealth>();

//        if (flightBounds == null)
//        {
//            Debug.LogError("[BossNavigator] flightBounds is required.");
//            enabled = false;
//            return;
//        }

//        splineFollower.follow = false;
//        splineFollower.followSpeed = flightSpeed;
//        transform.position = flightBounds.ClampToBounds(transform.position);

//        PickNewDestination();
//    }

//    private void Update()
//    {
//        TickMovement();
//        CheckDefendArrival();
//    }

//    public void FreestyleArea() { PickNewDestination(); }
//    public void FreestyleToPlayer() { SetDestination(ResolvePlayerPosition()); }
//    public void MoveToNearestObservation() { RequestNearestObservationPath(); }
//    public void MoveToNearestEscape() { RequestNearestEscapePath(); }
//    public void DefendCrystalCommand() { HandleDefend(); }

//    public void HoldPosition() { PickNewDestination(); }

//    public void OnMessage(MessageArgs messageArgs)
//    {
//        switch (messageArgs.message)
//        {
//            case "Swoop": FreestyleToPlayer(); break;
//            case "TacticalWeave": MoveToNearestEscape(); break;
//            case "MoveToObservation": MoveToNearestObservation(); break;
//            case "MoveToSpline": if (pendingSpline != null) MoveToSpline(pendingSpline); break;
//            case "Idle": HoldPosition(); break;
//            case "Defend": DefendCrystalCommand(); break;
//            case "CrystalDestroyed":
//            case "StopDefend": ExitDefend(); break;
//        }
//    }

//    public void SetDestination(Vector3 pos)
//    {
//        splineFollower.follow = false;
//        targetDestination = flightBounds.ClampToBounds(pos);
//        currentMode = MovementMode.Freestyle;
//    }

//    public void MoveToSpline(SplineComputer spline)
//    {
//        if (spline == null) return;

//        pendingSpline = spline;
//        splineFollower.follow = false;
//        currentMode = MovementMode.MovingToSpline;
//    }

//    public void RequestNearestEscapePath()
//    {
//        if (pathManager == null) return;
//        GameObject pathObj = pathManager.GetNearestEscapePath(transform.position, pathType);
//        if (pathObj == null) return;

//        SplineComputer spline = pathObj.GetComponent<SplineComputer>();
//        if (spline != null) MoveToSpline(spline);
//    }

//    public void RequestNearestObservationPath()
//    {
//        if (pathManager == null) return;
//        var paths = pathManager.GetObservationPaths(pathType);
//        if (paths == null || paths.Count == 0) return;

//        GameObject nearest = FindNearestPathObject(paths);
//        if (nearest == null) return;

//        SplineComputer spline = nearest.GetComponent<SplineComputer>();
//        if (spline != null) MoveToSpline(spline);
//    }

//    private void HandleDefend()
//    {
//        if (defendCrystal == null) return;
//        SetDestination(defendCrystal.transform.position);
//        defending = true;
//    }

//    private void CheckDefendArrival()
//    {
//        if (!defending || defendCrystal == null || vitals == null) return;

//        if (defendCrystal.IsDestroyed) { ExitDefend(); return; }

//        if (Vector3.Distance(transform.position, defendCrystal.transform.position) <= defendArrivalRadius)
//        {
//            if (!vitals.IsShielding) { vitals.IsShielding = true; defendCrystal.BeginFeeding(vitals); PixelCrushers.MessageSystem.SendMessage(this, "DragonReachedCrystal", string.Empty); }
//
//        }
//    }

//    private void ExitDefend()
//    {
//        if (defending) { PixelCrushers.MessageSystem.SendMessage(this, "DragonLeftCrystal", string.Empty); } defending = false;
//        if (vitals != null) vitals.IsShielding = false;
//        if (defendCrystal != null) defendCrystal.StopFeeding();
//    }

//    private Vector3 ResolvePlayerPosition()
//    {
//        GameObject player = GameObject.FindGameObjectWithTag("Player");
//        return player != null ? player.transform.position : transform.position;
//    }

//    private GameObject FindNearestPathObject(List<GameObject> paths)
//    {
//        GameObject nearest = null;
//        float minDist = float.MaxValue;

//        foreach (var p in paths)
//        {
//            if (p == null) continue;
//            float dist = Vector3.Distance(transform.position, p.transform.position);
//            if (dist < minDist) { minDist = dist; nearest = p; }
//        }
//        return nearest;
//    }

//    private void PickNewDestination()
//    {
//        Vector3 current = transform.position;

//        for (int i = 0; i < 8; i++)
//        {
//            Vector3 candidate = flightBounds.RandomPointInside();
//            if ((candidate - current).sqrMagnitude > arrivalRadius * arrivalRadius * 4f)
//            {
//                SetDestination(candidate);
//                return;
//            }
//        }

//        SetDestination(flightBounds.RandomPointInside());
//    }

//    private void TickMovement()
//    {
//        float speed = flightSpeed *
//                      (statusEffects != null ? statusEffects.CurrentSpeedMultiplier : 1f);

//        switch (currentMode)
//        {
//            case MovementMode.SplineFollowing: TickSplineFollowing(); break;
//            case MovementMode.Freestyle: TickFreestyle(); break;
//            case MovementMode.MovingToSpline: TickMoveToSpline(); break;
//        }

//        // Constant forward motion — exactly once per frame, never scaled, never paused.
//        transform.position += transform.forward * speed * Time.deltaTime;
//        transform.position = flightBounds.ClampToBounds(transform.position);
//    }

//    private void TickSplineFollowing()
//    {
//        splineFollower.followSpeed = flightSpeed;

//        if (!splineFollower.follow || splineFollower.GetPercent() >= 0.98)
//        {
//            splineFollower.follow = false;
//            PickNewDestination();
//        }
//    }

//    private void TickFreestyle()
//    {
//        Vector3 toTarget = targetDestination - transform.position;
//        if (toTarget.sqrMagnitude <= arrivalRadius * arrivalRadius)
//        {
//            PickNewDestination();
//            toTarget = targetDestination - transform.position;
//        }

//        if (toTarget.sqrMagnitude < 0.0001f) return;

//        TurnToward(toTarget.normalized);
//    }

//    private void TickMoveToSpline()
//    {
//        splineFollower.follow = false;

//        SplineSample sample = pendingSpline.Project(transform.position);
//        Vector3 splinePoint = flightBounds.ClampToBounds(sample.position);

//        Vector3 toPoint = splinePoint - transform.position;
//        if (toPoint.sqrMagnitude > 0.01f)
//            TurnToward(toPoint.normalized);

//        if (Vector3.Distance(transform.position, splinePoint) <= arrivalRadius)
//        {
//            splineFollower.spline = pendingSpline;
//            splineFollower.follow = true;
//            pendingSpline = null;
//            currentMode = MovementMode.SplineFollowing;
//        }
//    }

//    private void TurnToward(Vector3 desiredDir)
//    {
//        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
//        Vector3 flatDesired = Vector3.ProjectOnPlane(desiredDir, Vector3.up).normalized;

//        if (flatForward.sqrMagnitude < 0.0001f) flatForward = flatDesired;

//        if (flatDesired.sqrMagnitude > 0.0001f)
//        {
//            float leftRightAngle = Vector3.SignedAngle(flatForward, flatDesired, Vector3.up);
//            float wantedTurnRate = Mathf.Clamp(leftRightAngle * 2f, -maxTurnRate, maxTurnRate);

//            currentTurnRate = Mathf.MoveTowards(
//                currentTurnRate, wantedTurnRate, turnAcceleration * Time.deltaTime);

//            transform.Rotate(Vector3.up, currentTurnRate * Time.deltaTime, Space.World);
//        }

//        Vector3 right = transform.right;
//        Vector3 fwdForClimb = Vector3.ProjectOnPlane(transform.forward, right).normalized;
//        Vector3 wantForClimb = Vector3.ProjectOnPlane(desiredDir, right).normalized;

//        if (fwdForClimb.sqrMagnitude > 0.0001f && wantForClimb.sqrMagnitude > 0.0001f)
//        {
//            float upDownAngle = Vector3.SignedAngle(fwdForClimb, wantForClimb, right);
//            float wantedClimbRate = Mathf.Clamp(
//                upDownAngle * 2f, -maxTurnRate, maxTurnRate) * climbLaziness;

//            currentClimbRate = Mathf.MoveTowards(
//                currentClimbRate, wantedClimbRate, turnAcceleration * Time.deltaTime);

//            transform.Rotate(right, currentClimbRate * Time.deltaTime, Space.World);
//        }
//    }
//}



















//using UnityEngine;
//using PixelCrushers;
//using Dreamteck.Splines;

///// <summary>
///// Pillar C — Movement executor.
/////
///// The dragon is always in one of two motion states: on a spline, or freestyling.
///// Freestyle covers travel between splines and any direct destination the brain
///// sends. Receives action messages from the brain via the Message System and
///// executes them instantly — the brain can interrupt at any time.
/////
///// Movement is always incremental. The transform never teleports. Only one
///// code path writes transform.position per frame: Vector3.MoveTowards in the
///// freestyle and blend ticks. The Dreamteck SplineFollower only drives the
///// transform when explicitly enabled, and is only enabled once the dragon is
///// already within blendArrivalTolerance of the spline.
///// </summary>
//public class BossNavigator : MonoBehaviour, IMessageHandler
//{
//    private enum MovementMode { Idle, SplineFollowing, Freestyle, BlendingToSpline }

//    [SerializeField] private SplineFollower splineFollower;
//    private MovementMode currentMode = MovementMode.Idle;
//    private Vector3 targetDestination;

//    [Header("Movement")]
//    [SerializeField] private float baseFlightSpeed = 8f;
//    [SerializeField] private float rotationSpeed = 120f;

//    [Header("Territory")]
//    [Tooltip("Centre of the dragon's territory. Wander points stay near here.")]
//    [SerializeField] private Transform territoryCenter;

//    [Tooltip("Maximum distance from territory centre that wander points may be placed.")]
//    [SerializeField] private float territoryRadius = 60f;

//    [Tooltip("Minimum height above territory centre for wander points.")]
//    [SerializeField] private float wanderMinHeight = 10f;

//    [Tooltip("Maximum height above territory centre for wander points.")]
//    [SerializeField] private float wanderMaxHeight = 40f;

//    [Header("Blend")]
//    [Tooltip("Distance at which the dragon is considered 'on' the spline. " +
//             "The follower is only enabled once the dragon is within this distance, " +
//             "so SetPercent cannot cause a visible jump.")]
//    [SerializeField] private float blendArrivalTolerance = 0.5f;

//    [Header("Path Source")]
//    [SerializeField] private BossPathManager pathManager;
//    [SerializeField] private PathTypeTag.PathType pathType = PathTypeTag.PathType.Airborne;

//    [Header("Scene Targets")]
//    [SerializeField] private HealthCrystal defendCrystal;
//    [SerializeField] private BossStatsAndHealth vitals;

//    [Header("Defend")]
//    [SerializeField] private float defendArrivalRadius = 8f;

//    private CreatureStatusEffects statusEffects;
//    private bool defending;
//    private SplineComputer pendingSpline;

//    // ── Unity Lifecycle ───────────────────────────────────────────────────────

//    private void Awake()
//    {
//        statusEffects = GetComponent<CreatureStatusEffects>();

//        if (splineFollower == null)
//            splineFollower = GetComponent<SplineFollower>();

//        if (vitals == null)
//            vitals = GetComponent<BossStatsAndHealth>();

//        splineFollower.follow = false;
//    }

//    private void Update()
//    {
//        TickMovement();
//        CheckDefendArrival();
//    }

//    // ── Message Handler ───────────────────────────────────────────────────────

//    /// <summary>
//    /// Entry point for the brain. Interrupts whatever the dragon is doing and
//    /// starts the requested motion immediately. No waiting for arrival, no
//    /// notification back — the brain is expected to send the next action when
//    /// it wants one.
//    /// </summary>
//    public void OnMessage(MessageArgs messageArgs)
//    {
//        switch (messageArgs.message)
//        {
//            case "Swoop":
//                Freestyle(ResolvePlayerPosition());
//                break;

//            case "TacticalWeave":
//                RequestNearestEscapePath();
//                break;

//            case "BlendToSpline":
//                RequestNearestObservationPath();
//                break;

//            case "Idle":
//                HoldPosition();
//                break;

//            case "Defend":
//                HandleDefend();
//                break;

//            case "CrystalDestroyed":
//            case "StopDefend":
//                ExitDefend();
//                break;
//        }
//    }

//    // ── Movement requests ─────────────────────────────────────────────────────

//    /// <summary>
//    /// Freestyle through world space toward the given point. Disables the
//    /// spline follower first so the plugin stops touching the transform.
//    /// Movement happens in TickFreestyle via Vector3.MoveTowards — incremental,
//    /// no teleport. When the destination is reached, Wander() picks a new one
//    /// and the dragon keeps moving without notifying the brain.
//    /// </summary>
//    public void Freestyle(Vector3 pos)
//    {
//        splineFollower.follow = false;
//        targetDestination = pos;
//        currentMode = MovementMode.Freestyle;
//    }

//    /// <summary>
//    /// Begin a blend onto the given spline. The follower stays OFF during the
//    /// blend — TickBlendToSpline drives the transform with Vector3.MoveTowards
//    /// toward the nearest point on the spline (re-projected every frame).
//    /// The follower is only enabled once the dragon is within
//    /// blendArrivalTolerance of the spline, so there is no visible jump.
//    /// </summary>
//    public void BlendToSpline(SplineComputer spline)
//    {
//        if (spline == null) return;

//        pendingSpline = spline;
//        splineFollower.follow = false;
//        currentMode = MovementMode.BlendingToSpline;
//    }

//    /// <summary>
//    /// Ask BossPathManager for the nearest escape path of this navigator's
//    /// pathType, then BlendToSpline onto it.
//    /// </summary>
//    public void RequestNearestEscapePath()
//    {
//        if (pathManager == null) return;
//        GameObject pathObj = pathManager.GetNearestEscapePath(transform.position, pathType);
//        if (pathObj == null) return;

//        SplineComputer spline = pathObj.GetComponent<SplineComputer>();
//        if (spline != null) BlendToSpline(spline);
//    }

//    /// <summary>
//    /// Ask BossPathManager for all observation paths of this navigator's pathType,
//    /// pick the nearest by transform distance, then BlendToSpline onto it.
//    /// </summary>
//    public void RequestNearestObservationPath()
//    {
//        if (pathManager == null) return;
//        var paths = pathManager.GetObservationPaths(pathType);
//        if (paths == null || paths.Count == 0) return;

//        GameObject nearest = FindNearestPathObject(paths);
//        if (nearest == null) return;

//        SplineComputer spline = nearest.GetComponent<SplineComputer>();
//        if (spline != null) BlendToSpline(spline);
//    }

//    /// <summary>
//    /// Stop and wait. Disables the follower and does nothing per frame until
//    /// the brain sends another message.
//    /// </summary>
//    public void HoldPosition()
//    {
//        splineFollower.follow = false;
//        currentMode = MovementMode.Idle;
//    }

//    // ── Defend ────────────────────────────────────────────────────────────────

//    /// <summary>
//    /// Freestyle toward the defend crystal. Sets the defending flag so
//    /// CheckDefendArrival can watch for arrival.
//    /// </summary>
//    private void HandleDefend()
//    {
//        if (defendCrystal == null) return;
//        Freestyle(defendCrystal.transform.position);
//        defending = true;
//    }

//    /// <summary>
//    /// Runs every frame. If defending and within defendArrivalRadius of the
//    /// crystal, enables shielding on vitals and tells the crystal to start
//    /// feeding. Exits automatically if the crystal is destroyed.
//    /// </summary>
//    private void CheckDefendArrival()
//    {
//        if (!defending || defendCrystal == null || vitals == null) return;

//        if (defendCrystal.IsDestroyed)
//        {
//            ExitDefend();
//            return;
//        }

//        if (Vector3.Distance(transform.position, defendCrystal.transform.position) <= defendArrivalRadius)
//        {
//            if (!vitals.IsShielding) { vitals.IsShielding = true; defendCrystal.BeginFeeding(vitals); PixelCrushers.MessageSystem.SendMessage(this, "DragonReachedCrystal", string.Empty); }
//
//        }
//    }

//    private void ExitDefend()
//    {
//        if (defending) { PixelCrushers.MessageSystem.SendMessage(this, "DragonLeftCrystal", string.Empty); } defending = false;
//        if (vitals != null) vitals.IsShielding = false;
//        if (defendCrystal != null) defendCrystal.StopFeeding();
//    }

//    private Vector3 ResolvePlayerPosition()
//    {
//        GameObject player = GameObject.FindGameObjectWithTag("Player");
//        return player != null ? player.transform.position : transform.position;
//    }

//    private GameObject FindNearestPathObject(System.Collections.Generic.List<GameObject> paths)
//    {
//        GameObject nearest = null;
//        float minDist = float.MaxValue;

//        foreach (var p in paths)
//        {
//            if (p == null) continue;
//            float dist = Vector3.Distance(transform.position, p.transform.position);
//            if (dist < minDist)
//            {
//                minDist = dist;
//                nearest = p;
//            }
//        }
//        return nearest;
//    }

//    /// <summary>
//    /// Freestyle to a random point inside the dragon's territory. Called
//    /// internally when a spline ends or a freestyle destination is reached,
//    /// so the dragon keeps moving without the brain having to say anything.
//    /// </summary>
//    private void Wander()
//    {
//        Vector3 centre = territoryCenter != null ? territoryCenter.position : transform.position;

//        Vector2 circle = Random.insideUnitCircle * territoryRadius;
//        float height = Random.Range(wanderMinHeight, wanderMaxHeight);
//        Vector3 point = new Vector3(centre.x + circle.x, centre.y + height, centre.z + circle.y);

//        Freestyle(point);
//    }

//    // ── Movement Tick ─────────────────────────────────────────────────────────

//    private void TickMovement()
//    {
//        float effectiveSpeed = baseFlightSpeed *
//                               (statusEffects != null ? statusEffects.CurrentSpeedMultiplier : 1f);

//        switch (currentMode)
//        {
//            case MovementMode.SplineFollowing:
//                TickSplineFollowing(effectiveSpeed);
//                break;

//            case MovementMode.Freestyle:
//                TickFreestyle(effectiveSpeed);
//                break;

//            case MovementMode.BlendingToSpline:
//                TickBlendToSpline(effectiveSpeed);
//                break;

//            case MovementMode.Idle:
//                break;
//        }
//    }

//    /// <summary>
//    /// Hands full transform control to the Dreamteck SplineFollower. We only
//    /// set followSpeed; the plugin moves the transform along the spline. When
//    /// the spline reaches its end (percent >= 1.0), the dragon wanders to a
//    /// new point. The spline end is handled here, silently, because the
//    /// navigator owns its own routing.
//    /// </summary>
//    private void TickSplineFollowing(float effectiveSpeed)
//    {
//        splineFollower.followSpeed = effectiveSpeed;

//        if (splineFollower.GetPercent() >= 1.0)
//            Wander();
//    }

//    /// <summary>
//    /// Freestyle movement. Every frame: move the transform toward the target
//    /// by at most effectiveSpeed * deltaTime using Vector3.MoveTowards (this
//    /// is what prevents teleporting). Rotate toward the direction of travel
//    /// using Quaternion.RotateTowards (angle-limited per frame). When the
//    /// destination is reached, Wander() picks a new one.
//    /// </summary>
//    private void TickFreestyle(float effectiveSpeed)
//    {
//        Vector3 toTarget = targetDestination - transform.position;

//        if (toTarget.sqrMagnitude < 0.01f)
//        {
//            Wander();
//            return;
//        }

//        transform.position = Vector3.MoveTowards(
//            transform.position, targetDestination, effectiveSpeed * Time.deltaTime);

//        Quaternion look = Quaternion.LookRotation(toTarget);
//        transform.rotation = Quaternion.RotateTowards(
//            transform.rotation, look, rotationSpeed * Time.deltaTime);
//    }

//    /// <summary>
//    /// Blend movement. Every frame: project the dragon's current position onto
//    /// the pending spline to find the nearest point on it, then move toward
//    /// that point with Vector3.MoveTowards — incremental, no teleport. Because
//    /// the spline point is re-projected each frame, the dragon homes in on the
//    /// spline smoothly even if the nearest point shifts as it moves.
//    ///
//    /// When the dragon is within blendArrivalTolerance of the spline, hand
//    /// control to the follower: set its spline, set its percent to the closest
//    /// point we just found (which is where the dragon already is, so no jump),
//    /// enable follow, and switch to SplineFollowing.
//    /// </summary>
//    private void TickBlendToSpline(float effectiveSpeed)
//    {
//        splineFollower.follow = false;

//        SplineSample sample = pendingSpline.Project(transform.position);
//        Vector3 splinePoint = sample.position;
//        float closestPercent = (float)sample.percent;

//        transform.position = Vector3.MoveTowards(
//            transform.position, splinePoint, effectiveSpeed * Time.deltaTime);

//        Vector3 blendDir = splinePoint - transform.position;
//        if (blendDir.sqrMagnitude > 0.01f)
//        {
//            Quaternion blendLook = Quaternion.LookRotation(blendDir);
//            transform.rotation = Quaternion.RotateTowards(
//                transform.rotation, blendLook, rotationSpeed * Time.deltaTime);
//        }

//        if (Vector3.Distance(transform.position, splinePoint) <= blendArrivalTolerance)
//        {
//            splineFollower.spline = pendingSpline;
//            splineFollower.SetPercent(closestPercent);
//            splineFollower.follow = true;
//            pendingSpline = null;
//            currentMode = MovementMode.SplineFollowing;
//        }
//    }
//}