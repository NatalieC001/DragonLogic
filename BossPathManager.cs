using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Dreamteck.Splines;

/// <summary>
/// The single source of truth for "what paths exist in this level right now."
/// Boss movement scripts ask this manager for paths, removing direct coupling to Spawners.
/// </summary>
public class BossPathManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> airborneObservationPaths = new List<GameObject>();
    [SerializeField] private List<GameObject> airborneEscapePaths = new List<GameObject>();
    [SerializeField] private List<GameObject> terrestrialObservationPaths = new List<GameObject>();
    [SerializeField] private List<GameObject> terrestrialEscapePaths = new List<GameObject>();

    private void Awake()
    {
        // Auto-register any PathTypeTag objects already in the scene at startup.
        // This covers pre-placed paths that aren't spawned by the WaveSpawner.
        FallbackScanPaths();
    }

    /// <summary>
    /// Manually trigger a re-scan of the scene for any newly placed paths.
    /// Call this after dynamically instantiating paths if needed.
    /// </summary>
    public void RegisterPrePlacedPaths()
    {
        FallbackScanPaths();
    }

    /// <summary>
    /// Called by the Spawner (or the Path itself via Start) to register it globally.
    /// </summary>
    public void RegisterPath(GameObject pathObj)
    {
        PathTypeTag tag = pathObj.GetComponent<PathTypeTag>();
        if (tag == null)
        {
            Debug.LogWarning($"[BossPathManager] Attempted to register a path '{pathObj.name}' without a PathTypeTag. Defaulting to Airborne Observation.");
            airborneObservationPaths.Add(pathObj);
            return;
        }

        if (tag.pathType == PathTypeTag.PathType.Airborne)
        {
            if (tag.isEscapeRoute) airborneEscapePaths.Add(pathObj);
            else airborneObservationPaths.Add(pathObj);
        }
        else // Terrestrial
        {
            if (tag.isEscapeRoute) terrestrialEscapePaths.Add(pathObj);
            else terrestrialObservationPaths.Add(pathObj);
        }
    }

    /// <summary>
    /// Clears all stored paths. Called when a level/wave ends.
    /// </summary>
    public void ClearAllPaths()
    {
        airborneObservationPaths.Clear();
        airborneEscapePaths.Clear();
        terrestrialObservationPaths.Clear();
        terrestrialEscapePaths.Clear();
    }

    // --- Query Methods for Bosses ---

    /// <summary>
    /// Fallback scan if the Spawner hasn't explicitly registered paths yet.
    /// This fixes the 1-frame race condition where a Boss spawns before the paths are tracked!
    /// </summary>
    private void FallbackScanPaths()
    {
        PathTypeTag[] allTagsInScene = FindObjectsByType<PathTypeTag>(FindObjectsSortMode.None);
        foreach (PathTypeTag tag in allTagsInScene)
        {
            if (!airborneObservationPaths.Contains(tag.gameObject) && !airborneEscapePaths.Contains(tag.gameObject) &&
                !terrestrialObservationPaths.Contains(tag.gameObject) && !terrestrialEscapePaths.Contains(tag.gameObject))
            {
                RegisterPath(tag.gameObject);
            }
        }
    }

    public List<GameObject> GetObservationPaths(PathTypeTag.PathType type)
    {
        List<GameObject> paths = type == PathTypeTag.PathType.Airborne ? new List<GameObject>(airborneObservationPaths) : new List<GameObject>(terrestrialObservationPaths);

        if (paths.Count == 0)
        {
            FallbackScanPaths();
            paths = type == PathTypeTag.PathType.Airborne ? new List<GameObject>(airborneObservationPaths) : new List<GameObject>(terrestrialObservationPaths);
        }

        return paths;
    }

    public List<GameObject> GetEscapePaths(PathTypeTag.PathType type)
    {
        List<GameObject> paths = type == PathTypeTag.PathType.Airborne ? new List<GameObject>(airborneEscapePaths) : new List<GameObject>(terrestrialEscapePaths);

        if (paths.Count == 0)
        {
            FallbackScanPaths();
            paths = type == PathTypeTag.PathType.Airborne ? new List<GameObject>(airborneEscapePaths) : new List<GameObject>(terrestrialEscapePaths);
        }

        return paths;
    }

    /// <summary>
    /// Finds the closest escape path to the given position based on distance to the path's transform.
    /// Note: In a full Spline integration, this might check nearest points on the spline rather than just the root transform.
    /// </summary>
    public GameObject GetNearestEscapePath(Vector3 position, PathTypeTag.PathType type)
    {
        List<GameObject> paths = GetEscapePaths(type);
        if (paths.Count == 0) return null;

        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (var p in paths)
        {
            if (p == null) continue;

            float dist = Vector3.Distance(position, p.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = p;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Finds the closest path from the supplied candidate list by projecting the
    /// given candidate point onto each spline and comparing distance to the nearest
    /// point on the spline (not the path root transform). The SplineComputer is
    /// searched in children, because paths are prefabs whose root only holds the
    /// PathTypeTag.
    /// </summary>
    public GameObject GetNearestPathBySpline(Vector3 candidatePoint, List<GameObject> paths)
    {
        if (paths == null || paths.Count == 0)
        {
            Debug.Log("[BossPathManager] GetNearestPathBySpline: paths null/empty.");
            return null;
        }

        GameObject nearest = null;
        float minDistance = float.MaxValue;
        string log = $"[BossPathManager] GetNearestPathBySpline candidate={candidatePoint} count={paths.Count}\n";

        foreach (var p in paths)
        {
            if (p == null) continue;

            SplineComputer sc = p.GetComponentInChildren<SplineComputer>(true);
            if (sc == null)
            {
                log += $"  '{p.name}': no SplineComputer in children\n";
                continue;
            }

            SplineSample sample = new SplineSample();
            sc.Project(candidatePoint, ref sample);

            float dist = Vector3.Distance(candidatePoint, sample.position);
            log += $"  '{p.name}': proj={sample.position} pct={sample.percent:F4} dist={dist:F2}\n";

            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = p;
            }
        }

        log += $"  CHOSEN: {(nearest != null ? nearest.name : "<none>")} dist={minDistance:F2}";
        Debug.Log(log);

        return nearest;
    }
}