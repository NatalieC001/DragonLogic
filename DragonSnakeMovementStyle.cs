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
///
/// Segment add/remove is index-based. Only segments with isDestructiblePart
/// == true (body pieces) can be shed or regrown. Head, legs, and tail carry
/// isDestructiblePart == false and are protected.
/// </summary>
public class DragonSnakeMovementStyle : MonoBehaviour
{
    [Header("Body Prefabs (Head is this GameObject)")]
    public GameObject frontLegsPrefab;
    public GameObject bodyPrefab;
    public GameObject backLegsPrefab;
    public GameObject tailPrefab;

    [Header("Structure")]
    public int numberOfBodySegments = 8;
    public float segmentSpacing = 2f;
    public float totalBossPower { get; private set; }

    [Header("Ripple")]
    [Tooltip("Seconds of delay between one segment and the next. " +
             "Segment i reads the head's pose from (i * delayPerSegment) seconds ago.")]
    public float delayPerSegment = 0.15f;

    [Tooltip("How many seconds of history to keep.")]
    public float historyDuration = 5f;

    [Tooltip("Maximum samples per second recorded. Higher = smoother, more memory.")]
    public int samplesPerSecond = 60;

    private List<DragonSegment> activeSegments = new List<DragonSegment>();
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

    private bool isClosingGap = false;
    private float gapCloseTimer = 0f;
    private float gapCloseDuration = 1f;
    private Dictionary<DragonSegment, float> currentSpacings = new Dictionary<DragonSegment, float>();

    private void Start()
    {
        sampleInterval = 1f / Mathf.Max(1, samplesPerSecond);
        InitializeDragon(null);
    }

    public void InitializeDragon(SplineComputer track)
    {
        bossSpline = track;
        totalBossPower = 0f;
        activeSegments.Clear();

        headTransform = transform;

        DragonSegment headSegment = GetComponent<DragonSegment>();
        if (headSegment != null)
        {
            headSegment.isDestructiblePart = false;
            headSegment.Initialize(this, 0);

            SplineFollower headFollower = headSegment.Follower;
            if (headFollower != null)
            {
                headFollower.enabled = false;
                headFollower.follow = false;
            }

            activeSegments.Add(headSegment);
            totalBossPower += headSegment.powerContribution;
        }

        int currentIndex = activeSegments.Count;

        if (frontLegsPrefab != null)
            SpawnSegment(frontLegsPrefab, currentIndex++, false);

        for (int i = 0; i < numberOfBodySegments; i++)
            SpawnSegment(bodyPrefab, currentIndex++, true);

        if (backLegsPrefab != null)
            SpawnSegment(backLegsPrefab, currentIndex++, false);

        SpawnSegment(tailPrefab, currentIndex++, false);

        for (int i = 1; i < activeSegments.Count; i++)
        {
            if (activeSegments[i].Follower != null) activeSegments[i].Follower.enabled = false;
        }

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

        for (int i = 0; i < headHistory.Count; i++)
        {
            float age = -headHistory[i].time;
            PoseSample s = headHistory[i];
            s.position = headTransform.position + back * (age * (segmentSpacing / Mathf.Max(delayPerSegment, 0.0001f)));
            headHistory[i] = s;
        }

        UpdateSegmentPositions(1f);
    }

    private void SpawnSegment(GameObject prefab, int index, bool destructible)
    {
        if (prefab == null) return;

        GameObject segmentObj = Instantiate(prefab, transform);
        segmentObj.name = $"DragonSegment_{index}";

        SplineFollower follower = segmentObj.GetComponent<SplineFollower>();
        if (follower == null) follower = segmentObj.AddComponent<SplineFollower>();

        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

        segment.isDestructiblePart = destructible;
        segment.Initialize(this, index);
        activeSegments.Add(segment);

        totalBossPower += segment.powerContribution;
    }

    public void SwitchToNewSpline(SplineComputer newTrack)
    {
        bossSpline = newTrack;
    }

    private void LateUpdate()
    {
        if (activeSegments.Count == 0) return;

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

        if (isClosingGap)
        {
            gapCloseTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, gapCloseTimer / gapCloseDuration);
            if (t >= 1f) { t = 1f; isClosingGap = false; }
            UpdateSegmentPositions(t);
        }
        else
        {
            UpdateSegmentPositions(1f);
        }
    }

    private void UpdateSegmentPositions(float spacingLerp)
    {
        for (int i = 1; i < activeSegments.Count; i++)
        {
            DragonSegment segment = activeSegments[i];

            float delaySeconds = delayPerSegment * i;
            if (spacingLerp < 1f && currentSpacings.ContainsKey(segment))
                delaySeconds = Mathf.Lerp(currentSpacings[segment], delayPerSegment * i, spacingLerp);

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

    public void OnSegmentDestroyed(DragonSegment destroyedSegment)
    {
        if (destroyedSegment == null) return;
        if (!activeSegments.Contains(destroyedSegment)) return;
        if (!destroyedSegment.isDestructiblePart) return;

        totalBossPower -= destroyedSegment.powerContribution;
        activeSegments.Remove(destroyedSegment);

        Debug.Log($"<color=magenta>[DragonSnakeMovementStyle] A body segment fell! Boss power reduced to {totalBossPower}. Closing gap!</color>");

        if (activeSegments.Count == 0) return;

        currentSpacings.Clear();

        for (int i = 0; i < activeSegments.Count; i++)
        {
            int oldIndex = activeSegments[i].SegmentIndex;
            activeSegments[i].SegmentIndex = i;
            currentSpacings[activeSegments[i]] = oldIndex * delayPerSegment;
        }

        isClosingGap = true;
        gapCloseTimer = 0f;
    }

    public void ShedOneBodySegment()
    {
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            DragonSegment s = activeSegments[i];
            if (s != null && s.isDestructiblePart)
            {
                s.ForceDestruction();
                return;
            }
        }
    }

    public DragonSegment RegrowBodySegment()
    {
        if (bodyPrefab == null) return null;

        int insertAfter = -1;
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            if (activeSegments[i].isDestructiblePart)
            {
                insertAfter = i;
                break;
            }
        }

        if (insertAfter < 0)
        {
            for (int i = 0; i < activeSegments.Count; i++)
            {
                if (activeSegments[i].SegmentIndex == 0) { insertAfter = i; }
                else if (activeSegments[i].SegmentIndex == 1 && !activeSegments[i].isDestructiblePart)
                    insertAfter = Mathf.Max(insertAfter, i);
            }
        }

        if (insertAfter < 0) return null;

        GameObject segmentObj = Instantiate(bodyPrefab, transform);
        segmentObj.name = $"DragonSegment_regen_{activeSegments.Count}";

        SplineFollower follower = segmentObj.GetComponent<SplineFollower>();
        if (follower == null) follower = segmentObj.AddComponent<SplineFollower>();
        follower.enabled = false;

        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

        segment.isDestructiblePart = true;
        segment.Initialize(this, insertAfter + 1);

        activeSegments.Insert(insertAfter + 1, segment);
        totalBossPower += segment.powerContribution;

        currentSpacings.Clear();
        for (int i = 0; i < activeSegments.Count; i++)
        {
            int oldIndex = activeSegments[i].SegmentIndex;
            activeSegments[i].SegmentIndex = i;

            if (i > insertAfter)
                currentSpacings[activeSegments[i]] = oldIndex * delayPerSegment;
        }

        isClosingGap = true;
        gapCloseTimer = 0f;

        return segment;
    }

    public int BodySegmentCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < activeSegments.Count; i++)
                if (activeSegments[i] != null && activeSegments[i].isDestructiblePart) n++;
            return n;
        }
    }

    public void TriggerTotalDeath()
    {
        Debug.Log("<color=red>[DragonSnakeMovementStyle] The entire dragon is collapsing!</color>");
        foreach (var segment in activeSegments)
        {
            if (segment != null) segment.TriggerTotalDeath();
        }
        activeSegments.Clear();
    }
}



//using UnityEngine;  //still not great , but at least it compiles now
//using Dreamteck.Splines;
//using System.Collections.Generic;

///// <summary>
///// Controls the multi-part Asian Fire Dragon boss.
/////
///// The head writes its own history every frame: position, rotation, and time.
///// Every other segment samples that history at a fixed time delay behind the
///// head — segment i reads the head's pose from (i * delayPerSegment) seconds ago.
///// The result is a ripple: each segment literally replays what the head did a
///// fraction of a second earlier. Nothing chains, nothing lerps between segments,
///// nothing depends on segment i-1.
///// </summary>
//public class DragonSnakeMovementStyle : MonoBehaviour
//{
//    [Header("Dragon Anatomy Prefabs")]
//    public GameObject headPrefab;
//    public GameObject frontLegsPrefab;
//    public GameObject bodyPrefab;
//    public GameObject backLegsPrefab;
//    public GameObject tailPrefab;

//    [Header("Structure")]
//    public int numberOfBodySegments = 8;
//    public float segmentSpacing = 2f;
//    public float totalBossPower { get; private set; }

//    [Header("Ripple")]
//    [Tooltip("Seconds of delay between one segment and the next. " +
//             "Segment i reads the head's pose from (i * delayPerSegment) seconds ago.")]
//    public float delayPerSegment = 0.15f;

//    [Tooltip("How many seconds of history to keep.")]
//    public float historyDuration = 5f;

//    [Tooltip("Maximum samples per second recorded. Higher = smoother, more memory.")]
//    public int samplesPerSecond = 60;

//    private List<DragonSegment> activeSegments = new List<DragonSegment>();
//    private SplineComputer bossSpline;

//    private Transform headTransform;
//    private BossCreature bossBrain;

//    private struct PoseSample
//    {
//        public float time;
//        public Vector3 position;
//        public Quaternion rotation;
//    }

//    private List<PoseSample> headHistory = new List<PoseSample>();
//    private float recordTimer = 0f;
//    private float sampleInterval;
//    private float currentTime = 0f;

//    private bool isClosingGap = false;
//    private float gapCloseTimer = 0f;
//    private float gapCloseDuration = 1f;
//    private Dictionary<DragonSegment, float> currentSpacings = new Dictionary<DragonSegment, float>();

//    private void Start()
//    {
//        sampleInterval = 1f / Mathf.Max(1, samplesPerSecond);
//        InitializeDragon(null);
//    }

//    public void InitializeDragon(SplineComputer track)
//    {
//        bossSpline = track;
//        totalBossPower = 0f;
//        activeSegments.Clear();
//        bossBrain = GetComponent<BossCreature>();

//        int currentIndex = 0;

//        SpawnSegment(headPrefab, currentIndex);
//        headTransform = activeSegments[0].transform;

//        SplineFollower headFollower = activeSegments[0].Follower;
//        if (headFollower != null)
//        {
//            headFollower.enabled = false;
//            headFollower.follow = false;
//        }
//        currentIndex++;

//        if (frontLegsPrefab != null) { SpawnSegment(frontLegsPrefab, currentIndex); currentIndex++; }
//        for (int i = 0; i < numberOfBodySegments; i++) { SpawnSegment(bodyPrefab, currentIndex); currentIndex++; }
//        if (backLegsPrefab != null) { SpawnSegment(backLegsPrefab, currentIndex); currentIndex++; }
//        SpawnSegment(tailPrefab, currentIndex);

//        for (int i = 1; i < activeSegments.Count; i++)
//        {
//            if (activeSegments[i].Follower != null) activeSegments[i].Follower.enabled = false;
//        }

//        // Seed the history with the head's current pose repeated for historyDuration seconds,
//        // so the body starts laid out straight behind the head.
//        headTransform.position = transform.position;
//        headTransform.rotation = transform.rotation;

//        headHistory.Clear();
//        currentTime = 0f;
//        recordTimer = 0f;

//        Vector3 back = -headTransform.forward;
//        int totalSamples = Mathf.CeilToInt(historyDuration / sampleInterval);

//        for (int i = totalSamples - 1; i >= 0; i--)
//        {
//            float t = -i * sampleInterval;
//            headHistory.Add(new PoseSample
//            {
//                time = t,
//                position = headTransform.position + back * (i * sampleInterval * -1f) * 0f, // straight back handled below
//                rotation = headTransform.rotation
//            });
//        }

//        // Reposition the seeded history so segment spacing matches segmentSpacing at t=0:
//        // older samples are further back along -forward.
//        for (int i = 0; i < headHistory.Count; i++)
//        {
//            float age = -headHistory[i].time; // seconds before now
//            PoseSample s = headHistory[i];
//            s.position = headTransform.position + back * (age * (segmentSpacing / Mathf.Max(delayPerSegment, 0.0001f)));
//            headHistory[i] = s;
//        }

//        UpdateSegmentPositions(1f);
//    }

//    private void SpawnSegment(GameObject prefab, int index)
//    {
//        if (prefab == null) return;

//        GameObject segmentObj = Instantiate(prefab);
//        segmentObj.name = $"DragonSegment_{index}";

//        SplineFollower follower = segmentObj.GetComponent<SplineFollower>();
//        if (follower == null) follower = follower = segmentObj.AddComponent<SplineFollower>();

//        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
//        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

//        segment.Initialize(this, bossBrain, index);
//        activeSegments.Add(segment);

//        totalBossPower += segment.powerContribution;
//    }

//    public void SwitchToNewSpline(SplineComputer newTrack)
//    {
//        bossSpline = newTrack;
//    }

//    private void LateUpdate()
//    {
//        if (activeSegments.Count == 0) return;

//        // 1. Head mirrors the root (navigator drives the root).
//        headTransform.position = transform.position;
//        headTransform.rotation = transform.rotation;

//        // 2. Advance time, record the head's pose at fixed intervals.
//        currentTime += Time.deltaTime;
//        recordTimer += Time.deltaTime;

//        if (recordTimer >= sampleInterval)
//        {
//            recordTimer -= sampleInterval;
//            headHistory.Add(new PoseSample
//            {
//                time = currentTime,
//                position = headTransform.position,
//                rotation = headTransform.rotation
//            });

//            // Trim old samples.
//            float cutoff = currentTime - historyDuration;
//            while (headHistory.Count > 1 && headHistory[0].time < cutoff)
//                headHistory.RemoveAt(0);
//        }

//        // 3. Place each follower segment by sampling the head's history.
//        if (isClosingGap)
//        {
//            gapCloseTimer += Time.deltaTime;
//            float t = Mathf.SmoothStep(0f, 1f, gapCloseTimer / gapCloseDuration);
//            if (t >= 1f) { t = 1f; isClosingGap = false; }
//            UpdateSegmentPositions(t);
//        }
//        else
//        {
//            UpdateSegmentPositions(1f);
//        }
//    }

//    /// <summary>
//    /// Segment i samples the head's history from (i * delayPerSegment) seconds ago.
//    /// Position and rotation come from that exact historical sample — nothing else.
//    /// Gap-closing lerps the effective delay from its old value down to the target.
//    /// </summary>
//    private void UpdateSegmentPositions(float spacingLerp)
//    {
//        for (int i = 1; i < activeSegments.Count; i++)
//        {
//            DragonSegment segment = activeSegments[i];

//            float delaySeconds = delayPerSegment * i;
//            if (spacingLerp < 1f && currentSpacings.ContainsKey(segment))
//                delaySeconds = Mathf.Lerp(currentSpacings[segment], delayPerSegment * i, spacingLerp);

//            float sampleTime = currentTime - delaySeconds;

//            PoseSample pose = SampleHistory(sampleTime);

//            segment.transform.position = pose.position;
//            segment.transform.rotation = pose.rotation;
//        }
//    }

//    /// <summary>
//    /// Find the two recorded samples that bracket the requested time and lerp between them.
//    /// If the requested time is older than anything we have, return the oldest sample.
//    /// </summary>
//    private PoseSample SampleHistory(float time)
//    {
//        if (headHistory.Count == 0)
//        {
//            return new PoseSample
//            {
//                time = time,
//                position = headTransform.position,
//                rotation = headTransform.rotation
//            };
//        }

//        if (time <= headHistory[0].time) return headHistory[0];
//        if (time >= headHistory[headHistory.Count - 1].time) return headHistory[headHistory.Count - 1];

//        for (int i = 0; i < headHistory.Count - 1; i++)
//        {
//            PoseSample a = headHistory[i];
//            PoseSample b = headHistory[i + 1];

//            if (time >= a.time && time <= b.time)
//            {
//                float range = b.time - a.time;
//                float t = range > 0.0001f ? (time - a.time) / range : 0f;

//                return new PoseSample
//                {
//                    time = time,
//                    position = Vector3.Lerp(a.position, b.position, t),
//                    rotation = Quaternion.Slerp(a.rotation, b.rotation, t)
//                };
//            }
//        }

//        return headHistory[headHistory.Count - 1];
//    }

//    public void PauseSplineFollow() { }
//    public void ResumeSplineFollow() { }

//    public void OnSegmentDestroyed(DragonSegment destroyedSegment)
//    {
//        totalBossPower -= destroyedSegment.powerContribution;
//        activeSegments.Remove(destroyedSegment);

//        Debug.Log($"<color=magenta>[DragonSnakeMovementStyle] A segment fell! Boss power reduced to {totalBossPower}. Closing gap!</color>");

//        if (activeSegments.Count == 0) return;

//        currentSpacings.Clear();

//        // Each segment behind the destroyed one now has a smaller target delay.
//        // Capture its OLD delay so we can lerp it down over gapCloseDuration.
//        for (int i = 0; i < activeSegments.Count; i++)
//        {
//            int oldIndex = activeSegments[i].SegmentIndex;
//            activeSegments[i].SegmentIndex = i;
//            currentSpacings[activeSegments[i]] = oldIndex * delayPerSegment;
//        }

//        isClosingGap = true;
//        gapCloseTimer = 0f;
//    }

//    public void TriggerTotalDeath()
//    {
//        Debug.Log("<color=red>[DragonSnakeMovementStyle] The entire dragon is collapsing!</color>");
//        foreach (var segment in activeSegments)
//        {
//            if (segment != null) segment.TriggerTotalDeath();
//        }
//        activeSegments.Clear();
//    }
//}


//using UnityEngine;
//using Dreamteck.Splines;
//using System.Collections.Generic;

///// <summary>
///// Controls the multi-part Asian Fire Dragon boss.
/////
///// Every segment is a link in a chain. Segment i is positioned by reading the
///// transform of segment i-1 (the one in front of it) and placing itself a fixed
///// distance behind that segment, along the direction from that segment back
///// toward itself. Rotation is inherited from the segment in front.
/////
///// When a segment is destroyed, the ones behind it are re-indexed, so the chain
///// re-links automatically and the body closes the gap.
///// </summary>
//public class DragonSnakeMovementStyle : MonoBehaviour
//{
//    [Header("Dragon Anatomy Prefabs")]
//    public GameObject headPrefab;
//    public GameObject frontLegsPrefab;
//    public GameObject bodyPrefab;
//    public GameObject backLegsPrefab;
//    public GameObject tailPrefab;

//    [Header("Structure")]
//    public int numberOfBodySegments = 8;
//    public float segmentSpacing = 2f;
//    public float totalBossPower { get; private set; }

//    [Header("Follow")]
//    [Tooltip("How quickly a body segment swings around to align with its leader.")]
//    public float followLerp = 15f;

//    private List<DragonSegment> activeSegments = new List<DragonSegment>();
//    private SplineComputer bossSpline;

//    private Transform headTransform;
//    private BossCreature bossBrain;

//    private bool isClosingGap = false;
//    private float gapCloseTimer = 0f;
//    private float gapCloseDuration = 1f;
//    private Dictionary<DragonSegment, float> currentSpacings = new Dictionary<DragonSegment, float>();

//    private void Start()
//    {
//        InitializeDragon(null);
//    }

//    public void InitializeDragon(SplineComputer track)
//    {
//        bossSpline = track;
//        totalBossPower = 0f;
//        activeSegments.Clear();
//        bossBrain = GetComponent<BossCreature>();

//        int currentIndex = 0;

//        SpawnSegment(headPrefab, currentIndex);
//        headTransform = activeSegments[0].transform;

//        SplineFollower headFollower = activeSegments[0].Follower;
//        if (headFollower != null)
//        {
//            headFollower.enabled = false;
//            headFollower.follow = false;
//        }
//        currentIndex++;

//        if (frontLegsPrefab != null) { SpawnSegment(frontLegsPrefab, currentIndex); currentIndex++; }
//        for (int i = 0; i < numberOfBodySegments; i++) { SpawnSegment(bodyPrefab, currentIndex); currentIndex++; }
//        if (backLegsPrefab != null) { SpawnSegment(backLegsPrefab, currentIndex); currentIndex++; }
//        SpawnSegment(tailPrefab, currentIndex);

//        for (int i = 1; i < activeSegments.Count; i++)
//        {
//            if (activeSegments[i].Follower != null) activeSegments[i].Follower.enabled = false;
//        }

//        // Lay the body out straight behind the head for the first frame.
//        headTransform.position = transform.position;
//        headTransform.rotation = transform.rotation;

//        Vector3 back = -headTransform.forward;
//        for (int i = 1; i < activeSegments.Count; i++)
//        {
//            activeSegments[i].transform.position = headTransform.position + back * (segmentSpacing * i);
//            activeSegments[i].transform.rotation = headTransform.rotation;
//        }
//    }

//    private void SpawnSegment(GameObject prefab, int index)
//    {
//        if (prefab == null) return;

//        // Unparented, so nothing else fights their world positions.
//        GameObject segmentObj = Instantiate(prefab);
//        segmentObj.name = $"DragonSegment_{index}";

//        SplineFollower follower = segmentObj.GetComponent<SplineFollower>();
//        if (follower == null) follower = segmentObj.AddComponent<SplineFollower>();

//        DragonSegment segment = segmentObj.GetComponent<DragonSegment>();
//        if (segment == null) segment = segmentObj.AddComponent<DragonSegment>();

//        segment.Initialize(this, bossBrain, index);
//        activeSegments.Add(segment);

//        totalBossPower += segment.powerContribution;
//    }

//    public void SwitchToNewSpline(SplineComputer newTrack)
//    {
//        bossSpline = newTrack;
//    }

//    private void LateUpdate()
//    {
//        if (activeSegments.Count == 0) return;

//        // 1. Head mirrors the root. The navigator moves the root.
//        headTransform = activeSegments[0].transform;
//        headTransform.position = transform.position;
//        headTransform.rotation = transform.rotation;

//        // 2. Each following segment links to the one in front of it.
//        for (int i = 1; i < activeSegments.Count; i++)
//        {
//            Transform leader = activeSegments[i - 1].transform;
//            Transform follower = activeSegments[i].transform;

//            float spacing = segmentSpacing;
//            if (isClosingGap && currentSpacings.ContainsKey(activeSegments[i]))
//            {
//                float t = Mathf.SmoothStep(0f, 1f, gapCloseTimer / gapCloseDuration);
//                spacing = Mathf.Lerp(currentSpacings[activeSegments[i]], segmentSpacing, t);
//            }

//            // Direction from leader back toward where the follower currently is.
//            Vector3 back = follower.position - leader.position;

//            // If they're coincident, fall back to the leader's backward axis so the
//            // link has a direction to work with.
//            if (back.sqrMagnitude < 0.0001f)
//                back = -leader.forward;
//            else
//                back.Normalize();

//            // Place this segment a fixed distance behind the leader along that direction.
//            Vector3 targetPos = leader.position + back * spacing;

//            // Rotation: inherit the leader's rotation.
//            Quaternion targetRot = leader.rotation;

//            // Smooth the swing so the body bends like a string, not a rigid rod.
//            follower.position = Vector3.Lerp(follower.position, targetPos, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
//            follower.rotation = Quaternion.Slerp(follower.rotation, targetRot, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
//        }

//        // 3. Gap close timer.
//        if (isClosingGap)
//        {
//            gapCloseTimer += Time.deltaTime;
//            if (gapCloseTimer >= gapCloseDuration) isClosingGap = false;
//        }
//    }

//    public void PauseSplineFollow() { }
//    public void ResumeSplineFollow() { }

//    public void OnSegmentDestroyed(DragonSegment destroyedSegment)
//    {
//        totalBossPower -= destroyedSegment.powerContribution;
//        activeSegments.Remove(destroyedSegment);

//        Debug.Log($"<color=magenta>[DragonSnakeMovementStyle] A segment fell! Boss power reduced to {totalBossPower}. Closing gap!</color>");

//        if (activeSegments.Count == 0) return;

//        currentSpacings.Clear();

//        // Every segment behind the destroyed one is now one slot closer to the head.
//        // Capture how far each is currently trailing so we can lerp it back over gapCloseDuration.
//        for (int i = 0; i < activeSegments.Count; i++)
//        {
//            int oldIndex = activeSegments[i].SegmentIndex;
//            activeSegments[i].SegmentIndex = i;

//            // Currently, this segment is roughly (oldIndex * spacing) behind the head.
//            currentSpacings[activeSegments[i]] = oldIndex * segmentSpacing;
//        }

//        isClosingGap = true;
//        gapCloseTimer = 0f;
//    }

//    public void TriggerTotalDeath()
//    {
//        Debug.Log("<color=red>[DragonSnakeMovementStyle] The entire dragon is collapsing!</color>");
//        foreach (var segment in activeSegments)
//        {
//            if (segment != null) segment.TriggerTotalDeath();
//        }
//        activeSegments.Clear();
//    }
//}