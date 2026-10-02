using UnityEngine;
using Dreamteck.Splines;
using System.Collections.Generic;

/// <summary>
/// Controls the multi-part Asian Fire Dragon boss visuals.
///
/// The head (this GameObject) writes its own history every frame: position,
/// rotation, and time. Every other segment samples that history at a fixed
/// time delay behind the head — segment i reads the head's pose from
/// (i * delayPerSegment) seconds ago. The result is a ripple: each segment
/// literally replays what the head did a fraction of a second earlier.
///
/// BossNavigator moves this transform. This script never moves anything
/// forward — it only lays the body out behind the head.
/// </summary>
public class DragonSnakeMovementStyle : MonoBehaviour
{
    [Header("Managers")]
    public SegmentManager segmentManager;

    [Header("Ripple")]
    [Tooltip("How many seconds of history to keep.")]
    public float historyDuration = 5f;

    [Tooltip("Maximum samples per second recorded. Higher = smoother, more memory.")]
    public int samplesPerSecond = 60;

    private SplineComputer bossSpline;
    private Transform headTransform;

    private struct PoseSample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private List<PoseSample> headHistory = new List<PoseSample>();
    private float recordTimer = 0f;
    private float sampleInterval;
    private float currentTime = 0f;

    private void Start()
    {
        sampleInterval = 1f / Mathf.Max(1, samplesPerSecond);
        headTransform = transform;

        if (segmentManager == null)
            segmentManager = GetComponent<SegmentManager>();

        InitializeMovement(null);
    }

    public void InitializeMovement(SplineComputer track)
    {
        bossSpline = track;

        // Note: The segments themselves are spawned by SegmentManager

        // Wait for segment manager to be populated if needed, but normally SegmentManager initializes its body in Start too
        // In a real execution order we'd want SegmentManager to go first. But we can just seed history.

        headHistory.Clear();
        currentTime = 0f;
        recordTimer = 0f;

        Vector3 back = -headTransform.forward;
        int totalSamples = Mathf.CeilToInt(historyDuration / sampleInterval);

        for (int i = totalSamples - 1; i >= 0; i--)
        {
            float t = -i * sampleInterval;
            headHistory.Add(new PoseSample
            {
                time = t,
                position = headTransform.position,
                rotation = headTransform.rotation
            });
        }

        // Reposition initial history straight back
        if (segmentManager != null)
        {
            float segSpacing = segmentManager.segmentSpacing;
            float delay = segmentManager.delayPerSegment;

            for (int i = 0; i < headHistory.Count; i++)
            {
                float age = -headHistory[i].time;
                PoseSample s = headHistory[i];
                s.position = headTransform.position + back * (age * (segSpacing / Mathf.Max(delay, 0.0001f)));
                headHistory[i] = s;
            }
        }
    }

    public void SwitchToNewSpline(SplineComputer newTrack)
    {
        bossSpline = newTrack;
    }

    private void LateUpdate()
    {
        if (segmentManager == null || segmentManager.ActiveSegments == null || segmentManager.ActiveSegments.Count == 0) return;

        currentTime += Time.deltaTime;
        recordTimer += Time.deltaTime;

        if (recordTimer >= sampleInterval)
        {
            recordTimer -= sampleInterval;
            headHistory.Add(new PoseSample
            {
                time = currentTime,
                position = headTransform.position,
                rotation = headTransform.rotation
            });

            float cutoff = currentTime - historyDuration;
            while (headHistory.Count > 1 && headHistory[0].time < cutoff)
                headHistory.RemoveAt(0);
        }

        float spacingLerp = 1f;
        if (segmentManager.IsClosingGap)
        {
            float t = Mathf.SmoothStep(0f, 1f, segmentManager.GapCloseTimer / segmentManager.GapCloseDuration);
            if (t >= 1f) { t = 1f; }
            spacingLerp = t;
        }

        UpdateSegmentPositions(spacingLerp);
    }

    private void UpdateSegmentPositions(float spacingLerp)
    {
        var activeSegments = segmentManager.ActiveSegments;
        for (int i = 1; i < activeSegments.Count; i++)
        {
            DragonSegment segment = activeSegments[i];

            float delaySeconds = segmentManager.delayPerSegment * i;
            if (spacingLerp < 1f && segmentManager.CurrentSpacings.ContainsKey(segment))
                delaySeconds = Mathf.Lerp(segmentManager.CurrentSpacings[segment], segmentManager.delayPerSegment * i, spacingLerp);

            float sampleTime = currentTime - delaySeconds;

            PoseSample pose = SampleHistory(sampleTime);

            segment.transform.position = pose.position;
            segment.transform.rotation = pose.rotation;
        }
    }

    private PoseSample SampleHistory(float time)
    {
        if (headHistory.Count == 0)
        {
            return new PoseSample
            {
                time = time,
                position = headTransform.position,
                rotation = headTransform.rotation
            };
        }

        if (time <= headHistory[0].time) return headHistory[0];
        if (time >= headHistory[headHistory.Count - 1].time) return headHistory[headHistory.Count - 1];

        for (int i = 0; i < headHistory.Count - 1; i++)
        {
            PoseSample a = headHistory[i];
            PoseSample b = headHistory[i + 1];

            if (time >= a.time && time <= b.time)
            {
                float range = b.time - a.time;
                float t = range > 0.0001f ? (time - a.time) / range : 0f;

                return new PoseSample
                {
                    time = time,
                    position = Vector3.Lerp(a.position, b.position, t),
                    rotation = Quaternion.Slerp(a.rotation, b.rotation, t)
                };
            }
        }

        return headHistory[headHistory.Count - 1];
    }

    public void PauseSplineFollow() { }
    public void ResumeSplineFollow() { }
}
