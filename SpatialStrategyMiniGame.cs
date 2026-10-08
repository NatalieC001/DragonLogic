using UnityEngine;
using System.Collections.Generic;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.GameBoardSystem
{
    public class SpatialStrategyMiniGame : MonoBehaviour
    {
        private Transform player;

        [Header("Arena Boundaries")]
        public Transform arenaCenter;
        public MeshFilter arenaPlane;

        [Header("Grid Strategy Settings")]
        public int gridDivisions = 10;
        public float wallBuildingWeight = 2.0f;
        public float escapeBlockingWeight = 1.5f;
        public float playerProximityWeight = 1.0f;

        private TestTileVisualizer[,] testGrid;
        private bool hasGeneratedGrid = false;

        [Header("Topple Objects")]
        private List<ToppleItem> availableToppleItems = new List<ToppleItem>();
        private List<Vector3> activeHazardZones = new List<Vector3>();
        public float hazardRadius = 5f;

        private GameBoard cachedBoard;
        private Vector2Int currentTargetedTile = new Vector2Int(-1, -1);

        public void Initialize(Transform playerTransform)
        {
            player = playerTransform;
            cachedBoard = FindFirstObjectByType<GameBoard>();
            UpdateToppleItemsList();
            GenerateTestGrid();
        }

        private void GenerateTestGrid()
        {
            if (arenaPlane == null)
            {
                Debug.LogWarning("[SpatialStrategyMiniGame] Arena Plane is not assigned! Cannot generate test grid.");
                return;
            }

            if (testGrid != null)
            {
                for (int x = 0; x < gridDivisions; x++)
                {
                    for (int y = 0; y < gridDivisions; y++)
                    {
                        if (testGrid[x, y] != null) Destroy(testGrid[x, y].gameObject);
                    }
                }
            }

            testGrid = new TestTileVisualizer[gridDivisions, gridDivisions];

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
                    Vector3 tilePos = startPos
                        + (arenaPlane.transform.right * (x * tileSizeX + (tileSizeX / 2f)))
                        + (arenaPlane.transform.forward * (y * tileSizeZ + (tileSizeZ / 2f)));

                    tilePos.y += 0.05f;

                    GameObject tileObj = new GameObject($"TestTile_{x}_{y}");
                    tileObj.transform.position = tilePos;
                    tileObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    tileObj.transform.parent = gridContainer.transform;

                    GameObject outerQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    outerQuad.name = "OuterQuad";
                    outerQuad.transform.parent = tileObj.transform;
                    outerQuad.transform.localPosition = Vector3.zero;
                    outerQuad.transform.localRotation = Quaternion.identity;
                    outerQuad.transform.localScale = new Vector3(tileSizeX, tileSizeZ, 1f);
                    Destroy(outerQuad.GetComponent<MeshCollider>());

                    GameObject innerQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    innerQuad.name = "InnerQuad";
                    innerQuad.transform.parent = tileObj.transform;
                    innerQuad.transform.localPosition = new Vector3(0, 0, -0.01f);
                    innerQuad.transform.localRotation = Quaternion.identity;
                    innerQuad.transform.localScale = new Vector3(tileSizeX * 0.6f, tileSizeZ * 0.6f, 1f);
                    Destroy(innerQuad.GetComponent<MeshCollider>());

                    TestTileVisualizer visualizer = tileObj.AddComponent<TestTileVisualizer>();
                    visualizer.outerQuad = outerQuad.GetComponent<MeshRenderer>();
                    visualizer.innerQuad = innerQuad.GetComponent<MeshRenderer>();
                    visualizer.Initialize();

                    // Constantly leave quads enabled to trace grid floor status
                    outerQuad.GetComponent<MeshRenderer>().enabled = true;

                    testGrid[x, y] = visualizer;
                }
            }
            hasGeneratedGrid = true;
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

        public void ClearTargetHighlights()
        {
            if (currentTargetedTile.x >= 0 && currentTargetedTile.y >= 0)
            {
                if (testGrid[currentTargetedTile.x, currentTargetedTile.y] != null)
                {
                    testGrid[currentTargetedTile.x, currentTargetedTile.y].ResetVisuals();
                }
            }
            currentTargetedTile = new Vector2Int(-1, -1);
        }

        public Vector3 GetOptimalHazardCoordinate()
        {
            if (player == null || arenaCenter == null || !hasGeneratedGrid)
                return transform.position;

            Vector3 playerPos = player.position;
            playerPos.y = 0;
            Vector3 centerPos = arenaCenter.position;
            centerPos.y = 0;

            Vector3 escapeVector = (centerPos - playerPos).normalized;

            float highestScore = -1f;
            Vector2Int bestTileIndices = new Vector2Int(0, 0);
            Vector3 bestWorldPos = transform.position;

            for (int x = 0; x < gridDivisions; x++)
            {
                for (int y = 0; y < gridDivisions; y++)
                {
                    if (testGrid[x, y] == null) continue;

                    Vector3 tileWorldPos = testGrid[x, y].transform.position;
                    tileWorldPos.y = 0;
                    float score = 0f;

                    float distToPlayer = Vector3.Distance(tileWorldPos, playerPos);
                    if (distToPlayer < hazardRadius * 2f && distToPlayer > hazardRadius * 0.5f)
                    {
                        score += playerProximityWeight * (10f / Mathf.Max(distToPlayer, 0.1f));
                    }

                    Vector3 toTile = (tileWorldPos - playerPos).normalized;
                    float dotProduct = Vector3.Dot(escapeVector, toTile);
                    if (dotProduct > 0.5f && distToPlayer < hazardRadius * 3f)
                    {
                        score += escapeBlockingWeight * 5f * dotProduct;
                    }

                    int nearbyHazards = 0;
                    foreach (Vector3 hazard in activeHazardZones)
                    {
                        float dist = Vector3.Distance(tileWorldPos, new Vector3(hazard.x, 0, hazard.z));
                        if (dist < hazardRadius * 0.5f)
                        {
                            score = -100f;
                            break;
                        }
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
                            if (dist < hazardRadius * 2f) nearbyHazards++;
                        }
                    }

                    score += nearbyHazards * wallBuildingWeight * 5f;

                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestTileIndices = new Vector2Int(x, y);
                        bestWorldPos = testGrid[x, y].transform.position;
                    }
                }
            }

            if (Physics.Raycast(bestWorldPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                bestWorldPos.y = hit.point.y;
            }
            else
            {
                bestWorldPos.y = arenaCenter.position.y;
            }

            ClearTargetHighlights();
            currentTargetedTile = bestTileIndices;

            HazardType requestedHazard = DetermineHazardTypeForTile(bestWorldPos);
            if (testGrid[bestTileIndices.x, bestTileIndices.y] != null)
            {
                testGrid[bestTileIndices.x, bestTileIndices.y].HighlightAsTarget(requestedHazard);
            }

            return bestWorldPos;
        }

        private HazardType DetermineHazardTypeForTile(Vector3 targetPos)
        {
            if (cachedBoard != null)
            {
                Vector2Int gridPos = cachedBoard.WorldToGrid(targetPos);
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        Vector2Int adj = gridPos + new Vector2Int(x, y);
                        if (cachedBoard.GetTileState(adj) == HazardType.Oil) return HazardType.Fire;
                        else if (cachedBoard.GetTileState(adj) == HazardType.Water) return HazardType.Electricity;
                    }
                }
            }
            return HazardType.Fire;
        }

        public ToppleItem GetOptimalToppleTarget()
        {
            if (player == null || arenaCenter == null) return null;
            UpdateToppleItemsList();
            ToppleItem bestItem = null;
            float minScore = float.MaxValue;

            Vector3 playerPos = player.position;
            playerPos.y = 0;
            Vector3 escapeVector = (arenaCenter.position - playerPos).normalized;
            Vector3 idealBlockPoint = playerPos + (escapeVector * 10f);

            foreach (var item in availableToppleItems)
            {
                if (item == null || item.IsToppled) continue;
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

        public ToppleTargetData GetStrategicToppleTarget()
        {
            ToppleTargetData result = new ToppleTargetData { pillar = null, optimalHitDirection = Vector3.forward, isRecipeOpportunity = false };
            GameBoard board = cachedBoard;
            if (board != null)
            {
                foreach (var item in availableToppleItems)
                {
                    if (item == null || item.IsToppled) continue;

                    if (item.spillType == HazardType.Oil)
                    {
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                Vector2Int gridPos = board.WorldToGrid(item.transform.position) + new Vector2Int(x, y);
                                if (board.GetTileState(gridPos) == HazardType.Fire)
                                {
                                    Vector3 fallDir = (board.GridToWorld(gridPos) - item.transform.position).normalized;
                                    result.pillar = item;
                                    result.optimalHitDirection = fallDir;
                                    result.isRecipeOpportunity = true;
                                    return result;
                                }
                            }
                        }
                    }

                    if (item.spillType == HazardType.Water)
                    {
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                Vector2Int gridPos = board.WorldToGrid(item.transform.position) + new Vector2Int(x, y);
                                if (board.GetTileState(gridPos) == HazardType.Fire)
                                {
                                    Vector3 fallDir = (board.GridToWorld(gridPos) - item.transform.position).normalized;
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
            Vector3 playerPos = player.position;
            playerPos.y = 0;
            Vector3 centerPos = arenaCenter.position;
            centerPos.y = 0;
            Vector3 blockPoint = playerPos + (centerPos - playerPos).normalized * 5f;
            Vector3 fallDirection = (blockPoint - optimalPillar.transform.position).normalized;

            if (fallDirection.sqrMagnitude < 0.01f)
            {
                fallDirection = (playerPos - optimalPillar.transform.position).normalized;
            }

            result.optimalHitDirection = fallDirection;
            return result;
        }

        public void RemoveToppleItem(ToppleItem item)
        {
            if (availableToppleItems.Contains(item)) availableToppleItems.Remove(item);
        }
    }
}
