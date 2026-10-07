using UnityEngine;
using Dreamteck.Splines;
using System.Collections.Generic;
using VRDragonBoss.AI;

/// <summary>
/// Solely responsible for the breadcrumb trail of the Dragon's visual head (Dragon_Head child),
/// baking contextual serpentine undulation into recorded rotations, and physically placing
/// each body segment along that trail.
///
/// SEPARATION OF CONCERNS:
///   SegmentedDragonManager  — segment lifecycle (spawn, regrowth, gap-close on death)
///   DragonMovementManager   — movement (breadcrumbs, undulation, placement)
///   DragonSpacingManager    — spacing (cumulative segment offsets)
///   AirborneBossMovement    — intent (spline/freestyle modes, speed)
/// </summary>


public class DragonBreadCrumbManager : MonoBehaviour
{
    private struct PositionData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float distanceTraveled;
    }

    private List<PositionData> positionHistory = new List<PositionData>();
    private float headTotalDistance = 0f;
    private SplineComputer bossSpline;

    [Header("Head Reference")]
    public string headName = "DragonSegment_0";

    public Transform headTransform;
    public Transform resolvedBossRoot;

    private AirborneBossMovement Navigation;

    [Header("Serpentine Undulation")]
    public float undulationFrequency = 0.5f;
    public float maxUndulationYaw = 20f;
    public float maxUndulationRoll = 7f;

    private float currentUndulationScale = 1f;

    public float HeadTotalDistance => headTotalDistance;

    void Awake()
    {
        ResolveBossRoot();
        ResolveHeadTransform();
    }

    void Update()
    {
        if (resolvedBossRoot == null) ResolveBossRoot();
        if (headTransform == null) ResolveHeadTransform();
    }

    public void SetHeadTransform(Transform head)
    {
        if (head == null) return;
        headTransform = head;
    }

    public void SetMovementBrain(AirborneBossMovement brain)
    {
        Navigation = brain;
    }

    private void ResolveBossRoot()
    {
        if (resolvedBossRoot != null) return;

        Transform cursor = transform;
        while (cursor != null)
        {
            foreach (Transform child in cursor)
            {
                if (child.name == headName)
                {
                    resolvedBossRoot = cursor;
                    return;
                }
            }
            cursor = cursor.parent;
        }

        Transform best = null;
        int bestHops = int.MaxValue;

        foreach (Transform rootCandidate in FindAllSceneRoots())
        {
            if (rootCandidate == null) continue;
            bool hasHead = false;
            foreach (Transform child in rootCandidate)
            {
                if (child.name == headName) { hasHead = true; break; }
            }
            if (!hasHead) continue;

            int hops = HopDistance(transform, rootCandidate);
            if (hops < bestHops) { bestHops = hops; best = rootCandidate; }
        }

        if (best != null) resolvedBossRoot = best;
    }

    private void ResolveHeadTransform()
    {
        if (headTransform != null) return;

        if (resolvedBossRoot == null) ResolveBossRoot();
        if (resolvedBossRoot == null) return;

        foreach (Transform child in resolvedBossRoot)
        {
            if (child.name == headName)
            {
                headTransform = child;
                return;
            }
        }

        foreach (Transform d in resolvedBossRoot.GetComponentsInChildren<Transform>(true))
        {
            if (d.name == headName)
            {
                headTransform = d;
                return;
            }
        }
    }

    private static IEnumerable<Transform> FindAllSceneRoots()
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var t in all)
        {
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.parent != null) continue;
            yield return t;
        }
    }

    private static int HopDistance(Transform a, Transform b)
    {
        var chainA = new Dictionary<Transform, int>();
        int i = 0;
        for (Transform t = a; t != null; t = t.parent) chainA[t] = i++;
        i = 0;
        for (Transform t = b; t != null; t = t.parent)
        {
            if (chainA.TryGetValue(t, out int j)) return i + j;
            i++;
        }
        return int.MaxValue;
    }

    public void InitializeMovement(SplineComputer track, float totalExpectedLength)
    {
        bossSpline = track;
        positionHistory.Clear();
        headTotalDistance = 0f;

        ResolveBossRoot();
        ResolveHeadTransform();

        int samples = Mathf.CeilToInt((totalExpectedLength * 2f) / 0.1f) + 1;

        if (bossSpline != null)
        {
            SplineFollower rootFollower = GetComponent<SplineFollower>();
            double startPercent = rootFollower != null ? rootFollower.GetPercent() : 0.0;
            float splineLength = bossSpline.CalculateLength();

            for (int i = 0; i < samples; i++)
            {
                float distBack = i * 0.1f;
                double percent = startPercent - (distBack / splineLength);

                if (bossSpline.isClosed)
                {
                    while (percent < 0.0) percent += 1.0;
                    while (percent > 1.0) percent -= 1.0;
                }
                else
                {
                    percent = System.Math.Clamp(percent, 0.0, 1.0);
                }

                SplineSample sample = bossSpline.Evaluate(percent);

                positionHistory.Add(new PositionData
                {
                    position = sample.position,
                    rotation = sample.rotation,
                    distanceTraveled = -distBack
                });
            }
        }
        else
        {
            Vector3 startPos = headTransform != null ? headTransform.position : transform.position;
            Quaternion startRot = headTransform != null ? headTransform.rotation : transform.rotation;

            positionHistory.Add(new PositionData
            {
                position = startPos,
                rotation = startRot,
                distanceTraveled = 0f
            });
        }
    }

    public void SwitchToNewSpline(SplineComputer newTrack)
    {
        bossSpline = newTrack;
    }

    public void UpdateBreadcrumbs(float maxNeededHistoryDistance)
    {
        if (headTransform == null) ResolveHeadTransform();
        if (headTransform == null) return;

        Vector3 currentHeadPos = headTransform.position;
        Quaternion currentHeadRot = headTransform.rotation;

        if (positionHistory.Count == 0) return;

        float distMovedSinceLastFrame = Vector3.Distance(currentHeadPos, positionHistory[0].position);

        if (distMovedSinceLastFrame > 0.001f)
        {
            headTotalDistance += distMovedSinceLastFrame;

            float wavePhase = Time.time * undulationFrequency * Mathf.PI * 2f;
            float yawDeg = Mathf.Sin(wavePhase) * maxUndulationYaw * currentUndulationScale;
            float rollDeg = Mathf.Cos(wavePhase) * maxUndulationRoll * currentUndulationScale;

            Quaternion undulationOffset = Quaternion.Euler(0f, yawDeg, rollDeg);
            Quaternion recordedRot = currentHeadRot * undulationOffset;

            positionHistory.Insert(0, new PositionData
            {
                position = currentHeadPos,
                rotation = recordedRot,
                distanceTraveled = headTotalDistance
            });

            if (headTotalDistance - positionHistory[positionHistory.Count - 1].distanceTraveled > maxNeededHistoryDistance)
                positionHistory.RemoveAt(positionHistory.Count - 1);
        }
    }

    public void PlaceSegment(DragonSegment segment, float requiredDistanceBehindHead)
    {
        if (segment == null || positionHistory.Count < 2) return;

        float targetDistInHistory = headTotalDistance - requiredDistanceBehindHead;

        for (int j = 0; j < positionHistory.Count - 1; j++)
        {
            PositionData newer = positionHistory[j];
            PositionData older = positionHistory[j + 1];

            if (targetDistInHistory <= newer.distanceTraveled &&
                targetDistInHistory >= older.distanceTraveled)
            {
                float range = newer.distanceTraveled - older.distanceTraveled;
                float t = range > 0f
                    ? (newer.distanceTraveled - targetDistInHistory) / range
                    : 0f;

                Vector3 newPos = Vector3.Lerp(newer.position, older.position, t);
                Quaternion newRot = Quaternion.Slerp(newer.rotation, older.rotation, t);

                Rigidbody rb = segment.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.MovePosition(newPos);
                    rb.MoveRotation(newRot);
                }
                else
                {
                    segment.transform.position = newPos;
                    segment.transform.rotation = newRot;
                }
                break;
            }
        }
    }
}