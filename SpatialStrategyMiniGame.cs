using UnityEngine;
using System.Collections.Generic;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.GameBoardSystem
{
    /// <summary>
    /// Pillar A - Spatial Strategy Mini-Game
    /// Acts as a chess-master AI. Evaluates the flat 3D plane and the player's position
    /// to orchestrate environmental hazards that box the player into a corner.
    /// </summary>
    public class SpatialStrategyMiniGame : MonoBehaviour
    {
        private Transform player;

        [Header("Arena Boundaries")]
        [Tooltip("The center of the playable flat 3D plane. (Fallback if plane is missing)")]
        public Transform arenaCenter;
        [Tooltip("The plane object representing the arena. The mesh bounds will define the total area.")]
        public MeshFilter arenaPlane;

        [Header("Grid Strategy Settings")]
        [Tooltip("How many tiles the arena should be divided into along one axis (e.g., 3 means a 3x3 grid).")]
        public int gridDivisions = 10;

        [Tooltip("Toggle the visual test grid in the editor/game.")]
        public bool testMode = false;

        [Tooltip("Weight for choosing tiles that connect to existing obstacles.")]
        public float wallBuildingWeight = 2.0f;
        [Tooltip("Weight for choosing tiles that block the player's path to the center.")]
        public float escapeBlockingWeight = 1.5f;
        [Tooltip("Weight for choosing tiles immediately surrounding the player (herding).")]
        public float playerProximityWeight = 1.0f;

        private TestTileVisualizer[,] testGrid;
        private bool hasGeneratedGrid = false;

        [Header("Topple Objects")]
        private List<ToppleItem> availableToppleItems = new List<ToppleItem>();

        // Track active hazards to avoid shooting the same spot twice
        private List<Vector3> activeHazardZones = new List<Vector3>();
        public float hazardRadius = 5f;

        private GameBoard cachedBoard;

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        cachedBoard = FindFirstObjectByType<GameBoard>();
        UpdateToppleItemsList();

        GenerateTestGrid();
    }

    private bool lastTestModeState = false;

    private void Update()
    {
        if (hasGeneratedGrid && testMode != lastTestModeState)
        {
            lastTestModeState = testMode;
            ToggleTestGrid(testMode);
        }
    }

    private void ToggleTestGrid(bool show)
    {
        if (testGrid == null) return;

        for (int x = 0; x < gridDivisions; x++)
        {
            for (int y = 0; y < gridDivisions; y++)
            {
                if (testGrid[x, y] != null && testGrid[x, y].outerQuad != null)
                {
                    testGrid[x, y].outerQuad.enabled = show;
                }
            }
        }
    }

    private void GenerateTestGrid()
    {
        if (arenaPlane == null)
        {
            Debug.LogWarning("[SpatialStrategyMiniGame] Arena Plane is not assigned! Cannot generate test grid.");
            return;
        }

        // Clean up old grid if it exists
        if (testGrid != null)
        {
            for (int x = 0; x < gridDivisions; x++)
            {
                for (int y = 0; y < gridDivisions; y++)
                {
                    if (testGrid[x, y] != null)
                        Destroy(testGrid[x, y].gameObject);
                }
            }
        }

        testGrid = new TestTileVisualizer[gridDivisions, gridDivisions];

        // Calculate size based on mesh and scale
        Bounds meshBounds = arenaPlane.mesh.bounds;
        Vector3 planeScale = arenaPlane.transform.lossyScale;

        float planeWidth = meshBounds.size.x * planeScale.x;
        float planeDepth = meshBounds.size.z * planeScale.z;

        float tileSizeX = planeWidth / gridDivisions;
        float tileSizeZ = planeDepth / gridDivisions;

        Vector3 startPos = arenaPlane.transform.position
            - (arenaPlane.transform.right * (planeWidth / 2f))
            - (arenaPlane.transform.forward * (planeDepth / 2f));

        GameObject gridContainer = new GameObject("TestGridContainer");
        gridContainer.transform.parent = this.transform;

        for (int x = 0; x < gridDivisions; x++)
        {
            for (int y = 0; y < gridDivisions; y++)
            {
                // Calculate position for the center of this tile
                Vector3 tilePos = startPos
                    + (arenaPlane.transform.right * (x * tileSizeX + (tileSizeX / 2f)))
                    + (arenaPlane.transform.forward * (y * tileSizeZ + (tileSizeZ / 2f)));

                // Slight offset to prevent Z-fighting with the arena plane
                tilePos.y += 0.05f;

                GameObject tileObj = new GameObject($"TestTile_{x}_{y}");
                tileObj.transform.position = tilePos;
                tileObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Make quad lay flat
                tileObj.transform.parent = gridContainer.transform;

                // Outer Quad (Opaque Gray)
                GameObject outerQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                outerQuad.name = "OuterQuad";
                outerQuad.transform.parent = tileObj.transform;
                outerQuad.transform.localPosition = Vector3.zero;
                outerQuad.transform.localRotation = Quaternion.identity;
                // Scale outer quad to fit the tile size exactly
                outerQuad.transform.localScale = new Vector3(tileSizeX, tileSizeZ, 1f);
                Destroy(outerQuad.GetComponent<MeshCollider>()); // Use BoxCollider instead

                // Inner Quad (Hidden, smaller)
                GameObject innerQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                innerQuad.name = "InnerQuad";
                innerQuad.transform.parent = tileObj.transform;
                innerQuad.transform.localPosition = new Vector3(0, 0, -0.01f); // Slightly above outer quad
                innerQuad.transform.localRotation = Quaternion.identity;
                // Scale inner quad to be slightly smaller
                innerQuad.transform.localScale = new Vector3(tileSizeX * 0.6f, tileSizeZ * 0.6f, 1f);
                Destroy(innerQuad.GetComponent<MeshCollider>());

                // Box Collider for collision resets
                BoxCollider boxColl = tileObj.AddComponent<BoxCollider>();
                boxColl.isTrigger = true; // Use trigger to avoid physically stopping the dragon's body
                boxColl.size = new Vector3(tileSizeX, tileSizeZ, 0.5f);

                // Setup visualizer script
                TestTileVisualizer visualizer = tileObj.AddComponent<TestTileVisualizer>();
                visualizer.outerQuad = outerQuad.GetComponent<MeshRenderer>();
                visualizer.innerQuad = innerQuad.GetComponent<MeshRenderer>();
                visualizer.Initialize();

                testGrid[x, y] = visualizer;
            }
        }

        hasGeneratedGrid = true;
        lastTestModeState = testMode;
        ToggleTestGrid(testMode);
    }

        private void UpdateToppleItemsList()
        {
            availableToppleItems.Clear();
            availableToppleItems.AddRange(FindObjectsByType<ToppleItem>(FindObjectsSortMode.None));
        }
        
        public void RegisterHazardZone(Vector3 pos)
        {
            activeHazardZones.Add(pos);
        }

        /// <summary>
        /// The Enclosure Algorithm (VR Herding Strategy)
        /// Uses a utility scoring system across the dynamic grid to trap the VR player
        /// by building "soft" and "hard" walls, herding them into taking debuffs or wasting time.
        /// </summary>
        public Vector3 GetOptimalHazardCoordinate()
        {
            if (player == null || arenaCenter == null || !hasGeneratedGrid)
                return transform.position;

            Vector3 playerPos = player.position;
            playerPos.y = 0;
            Vector3 centerPos = arenaCenter.position;
            centerPos.y = 0;

            // Player's path to safety
            Vector3 escapeVector = (centerPos - playerPos).normalized;

            float highestScore = -1f;
            Vector2Int bestTileIndices = new Vector2Int(0, 0);
            Vector3 bestWorldPos = transform.position;

            // 1. Evaluate Every Tile
            for (int x = 0; x < gridDivisions; x++)
            {
                for (int y = 0; y < gridDivisions; y++)
                {
                    if (testGrid[x, y] == null) continue;

                    Vector3 tileWorldPos = testGrid[x, y].transform.position;
                    tileWorldPos.y = 0;

                    float score = 0f;

                    // Rule A: Proximity to Player (Herding)
                    float distToPlayer = Vector3.Distance(tileWorldPos, playerPos);
                    if (distToPlayer < hazardRadius * 2f && distToPlayer > hazardRadius * 0.5f)
                    {
                        // High score for tiles immediately around the player (but not directly on them)
                        score += playerProximityWeight * (10f / Mathf.Max(distToPlayer, 0.1f));
                    }

                    // Rule B: Blocking the Escape Route
                    // Is this tile generally in the direction the player wants to run?
                    Vector3 toTile = (tileWorldPos - playerPos).normalized;
                    float dotProduct = Vector3.Dot(escapeVector, toTile);
                    if (dotProduct > 0.5f && distToPlayer < hazardRadius * 3f)
                    {
                        score += escapeBlockingWeight * 5f * dotProduct;
                    }

                    // Rule C: Wall Building (Connecting to existing obstacles)
                    int nearbyHazards = 0;
                    foreach (Vector3 hazard in activeHazardZones)
                    {
                        float dist = Vector3.Distance(tileWorldPos, new Vector3(hazard.x, 0, hazard.z));
                        // If it's too close to another hazard, it's a waste (overlap)
                        if (dist < hazardRadius * 0.5f)
                        {
                            score = -100f; // Invalid spot
                            break;
                        }
                        // If it's adjacent, it forms a wall!
                        else if (dist < hazardRadius * 2f)
                        {
                            nearbyHazards++;
                        }
                    }

                    foreach (ToppleItem pillar in availableToppleItems)
                    {
                        if (pillar != null && pillar.IsToppled)
                        {
                            float dist = Vector3.Distance(tileWorldPos, new Vector3(pillar.transform.position.x, 0, pillar.transform.position.z));
                            if (dist < hazardRadius * 2f)
                            {
                                nearbyHazards++; // Connect to fallen pillars
                            }
                        }
                    }

                    score += nearbyHazards * wallBuildingWeight * 5f;

                    // Selection
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestTileIndices = new Vector2Int(x, y);
                        bestWorldPos = testGrid[x, y].transform.position;
                    }
                }
            }

            // Snap to ground level
            if (Physics.Raycast(bestWorldPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                bestWorldPos.y = hit.point.y;
            }
            else
            {
                bestWorldPos.y = arenaCenter.position.y;
            }

            // Highlight the tile visually as the target, and determine element
            HazardType requestedHazard = DetermineHazardTypeForTile(bestWorldPos);

            if (testGrid[bestTileIndices.x, bestTileIndices.y] != null)
            {
                testGrid[bestTileIndices.x, bestTileIndices.y].HighlightAsTarget(requestedHazard);
            }

            return bestWorldPos;
        }

        private HazardType DetermineHazardTypeForTile(Vector3 targetPos)
        {
            // Synergy check: look for nearby elements to combo
            if (cachedBoard != null)
            {
                Vector2Int gridPos = cachedBoard.WorldToGrid(targetPos);

                // Check surrounding tiles for Oil to ignite
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        Vector2Int adj = gridPos + new Vector2Int(x, y);
                        if (cachedBoard.GetTileState(adj) == HazardType.Oil)
                        {
                            return HazardType.Fire; // Combo!
                        }
                        else if (cachedBoard.GetTileState(adj) == HazardType.Water)
                        {
                            return HazardType.Electricity; // Combo!
                        }
                    }
                }
            }

            // Default fallback
            return HazardType.Fire;
        }

        /// <summary>
        /// Returns the nearest ToppleItem that hasn't already been destroyed or knocked over,
        /// prioritizing items that sit between the player and the center of the arena.
        /// </summary>
        public ToppleItem GetOptimalToppleTarget()
        {
            if (player == null || arenaCenter == null) return null;

            UpdateToppleItemsList();

            ToppleItem bestItem = null;
            float minScore = float.MaxValue;

            Vector3 playerPos = player.position;
            playerPos.y = 0;

            // The ideal pillar to knock over is one that blocks the player's path inward.
            Vector3 escapeVector = (arenaCenter.position - playerPos).normalized;
            Vector3 idealBlockPoint = playerPos + (escapeVector * 10f);

            foreach (var item in availableToppleItems)
            {
                if (item == null || item.IsToppled) continue;

                // Score is based on how close the pillar is to the ideal blocking point
                float dist = Vector3.Distance(item.transform.position, idealBlockPoint);
                if (dist < minScore)
                {
                    minScore = dist;
                    bestItem = item;
                }
            }

            return bestItem;
        }


        public struct ToppleTargetData
        {
            public ToppleItem pillar;
            public Vector3 optimalHitDirection;
            public bool isRecipeOpportunity;
        }

        /// <summary>
        /// Returns the optimal pillar to topple AND the strategic direction the Dragon should hit it from.
        /// The goal is to push the pillar so it blocks the player's path toward the arena center,
        /// effectively corralling them and knocking floor tiles out of action.
        /// </summary>
        public ToppleTargetData GetStrategicToppleTarget()
        {
            ToppleTargetData result = new ToppleTargetData { pillar = null, optimalHitDirection = Vector3.forward, isRecipeOpportunity = false };

            // Recipe Opportunity Check using GameBoard
            GameBoard board = cachedBoard;
            if (board != null)
            {
                foreach (var item in availableToppleItems)
                {
                    if (item == null || item.IsToppled) continue;

                    // Example Recipe: Oil into Fire
                    if (item.spillType == HazardType.Oil)
                    {
                        // Check if there is a Fire hazard within topple distance
                        // (Roughly 2 tiles away from the barrel)
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                Vector2Int gridPos = board.WorldToGrid(item.transform.position) + new Vector2Int(x, y);
                                if (board.GetTileState(gridPos) == HazardType.Fire)
                                {
                                    Vector3 fireWorldPos = board.GridToWorld(gridPos);
                                    Vector3 fallDir = (fireWorldPos - item.transform.position).normalized;

                                    result.pillar = item;
                                    result.optimalHitDirection = fallDir;
                                    result.isRecipeOpportunity = true;
                                    return result;
                                }
                            }
                        }
                    }

                    // Example Recipe: Water into Fire
                    if (item.spillType == HazardType.Water)
                    {
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                Vector2Int gridPos = board.WorldToGrid(item.transform.position) + new Vector2Int(x, y);
                                if (board.GetTileState(gridPos) == HazardType.Fire)
                                {
                                    Vector3 fireWorldPos = board.GridToWorld(gridPos);
                                    Vector3 fallDir = (fireWorldPos - item.transform.position).normalized;

                                    result.pillar = item;
                                    result.optimalHitDirection = fallDir;
                                    result.isRecipeOpportunity = true;
                                    return result;
                                }
                            }
                        }
                    }
                }
            }

            ToppleItem optimalPillar = GetOptimalToppleTarget();
            if (optimalPillar == null) return result;

            result.pillar = optimalPillar;

            // Calculate the vector we want the pillar to FALL toward.
            // We want it to fall between the player and the center, creating a wall.
            Vector3 playerPos = player.position;
            playerPos.y = 0;
            Vector3 centerPos = arenaCenter.position;
            centerPos.y = 0;

            // The point we want to block
            Vector3 blockPoint = playerPos + (centerPos - playerPos).normalized * 5f;

            // The direction the pillar needs to fall to land on that block point
            Vector3 fallDirection = (blockPoint - optimalPillar.transform.position).normalized;

            // If the pillar is already exactly on the block point (rare), just push it towards the player.
            if (fallDirection.sqrMagnitude < 0.01f)
            {
                fallDirection = (playerPos - optimalPillar.transform.position).normalized;
            }

            // The dragon must hit the pillar from the OPPOSITE direction of where it should fall.
            // E.g., if we want it to fall North, the dragon must hit it flying North (from the South).
            result.optimalHitDirection = fallDirection;

            return result;
        }

        public void RemoveToppleItem(ToppleItem item)
        {
            if (availableToppleItems.Contains(item))
            {
                availableToppleItems.Remove(item);
            }
        }
    }

}
