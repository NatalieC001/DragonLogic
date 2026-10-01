using UnityEngine;
using System.Collections.Generic;

using VRDragonBoss.AI;
using VRDragonBoss.GameBoardSystem;
using VRDragonBoss.Environment;

namespace VRDragonBoss.GameBoardSystem
{
    /// <summary>
    /// Replaces the VRPlatformSpaceManager concept.
    /// Acts as the absolute spatial vector and matrix calculation engine.
    /// Zero AI logic. Manages the 3D plane, tracks player bounds, and handles instantiation/recipes.
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        [Header("Grid Definition")]
        public Transform arenaCenter;
        public float tileSize = 4f;
        public int gridWidth = 10;
        public int gridHeight = 10;

        [Header("Hazards")]
        public GameObject darkMistPrefab;
        public GameObject firePrefab;
        public GameObject waterPrefab;
        public GameObject oilPrefab;
        public GameObject electricityPrefab;
        public GameObject icePrefab;
        public GameObject stickyPrefab;

        private HazardType[,] gridState;
        private bool[,] isImpassable;
        private Dictionary<Vector2Int, GameObject> activeHazardObjects = new Dictionary<Vector2Int, GameObject>();

        private void Awake()
        {
            gridState = new HazardType[gridWidth, gridHeight];
            isImpassable = new bool[gridWidth, gridHeight];

            // Initialize all tiles to safe
            for(int x = 0; x < gridWidth; x++)
            {
                for(int y = 0; y < gridHeight; y++)
                {
                    gridState[x,y] = HazardType.None;
                }
            }
        }

        /// <summary>
        /// Converts a world coordinate to a logical 2D grid coordinate.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            if (arenaCenter == null) return Vector2Int.zero;

            Vector3 localPos = worldPos - arenaCenter.position;
            // Assume X and Z are the flat plane
            int x = Mathf.FloorToInt((localPos.x + (gridWidth * tileSize / 2f)) / tileSize);
            int y = Mathf.FloorToInt((localPos.z + (gridHeight * tileSize / 2f)) / tileSize);

            // Clamp to board bounds
            x = Mathf.Clamp(x, 0, gridWidth - 1);
            y = Mathf.Clamp(y, 0, gridHeight - 1);

            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Converts a logical 2D grid coordinate to a world coordinate (center of the tile).
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            if (arenaCenter == null) return Vector3.zero;

            float x = (gridPos.x * tileSize) - (gridWidth * tileSize / 2f) + (tileSize / 2f);
            float z = (gridPos.y * tileSize) - (gridHeight * tileSize / 2f) + (tileSize / 2f);

            return new Vector3(arenaCenter.position.x + x, arenaCenter.position.y, arenaCenter.position.z + z);
        }

        /// <summary>
        /// Marks a specific tile as impassable (e.g., when a barrel lands on it).
        /// </summary>
        public void MarkTileImpassable(Vector2Int gridPos)
        {
            if (IsValidGridPos(gridPos))
            {
                isImpassable[gridPos.x, gridPos.y] = true;
            }
        }

        public bool IsTileImpassable(Vector2Int gridPos)
        {
            if (IsValidGridPos(gridPos)) return isImpassable[gridPos.x, gridPos.y];
            return true;
        }

        /// <summary>
        /// Attempts to apply an element to a specific grid tile, triggering the Recipe Lookup.
        /// </summary>
        public void ApplyElementToTile(Vector2Int gridPos, HazardType newElement)
        {
            if (!IsValidGridPos(gridPos) || isImpassable[gridPos.x, gridPos.y]) return;

            HazardType currentElement = gridState[gridPos.x, gridPos.y];
            HazardType resultantElement = ResolveRecipe(currentElement, newElement);

            SetTileElement(gridPos, resultantElement);
        }

        private HazardType ResolveRecipe(HazardType current, HazardType incoming)
        {
            if (current == HazardType.None) return incoming;

            // Example Recipes
            if ((current == HazardType.Fire && incoming == HazardType.Water) ||
                (current == HazardType.Water && incoming == HazardType.Fire))
            {
                Debug.Log("[GameBoard] Recipe: Water + Fire = Steam (Nullified)");
                return HazardType.None;
            }

            if ((current == HazardType.Fire && incoming == HazardType.Oil) ||
                (current == HazardType.Oil && incoming == HazardType.Fire))
            {
                Debug.Log("[GameBoard] Recipe: Oil + Fire = Inferno (Fire)");
                // In a real scenario, this might create a larger AOE explosion before settling back to Fire
                return HazardType.Fire;
            }

            if ((current == HazardType.Water && incoming == HazardType.Electricity) ||
                (current == HazardType.Electricity && incoming == HazardType.Water))
            {
                Debug.Log("[GameBoard] Recipe: Water + Electricity = Conductive Trap (Electricity)");
                return HazardType.Electricity;
            }

            // Default overwrite if no specific recipe
            return incoming;
        }

        private void SetTileElement(Vector2Int gridPos, HazardType newElement)
        {
            // 1. Clear existing hazard if any
            if (activeHazardObjects.TryGetValue(gridPos, out GameObject oldHazard) && oldHazard != null)
            {
                Destroy(oldHazard);
                activeHazardObjects.Remove(gridPos);
            }

            gridState[gridPos.x, gridPos.y] = newElement;

            // 2. Instantiate new hazard
            if (newElement != HazardType.None)
            {
                GameObject prefabToSpawn = GetPrefabForType(newElement);
                if (prefabToSpawn != null)
                {
                    GameObject newObj = Instantiate(prefabToSpawn, GridToWorld(gridPos), Quaternion.identity);
                    activeHazardObjects[gridPos] = newObj;

                    // Scale to fit the tile size exactly
                    newObj.transform.localScale = new Vector3(tileSize, 1f, tileSize);
                }
            }
        }

        public HazardType GetTileState(Vector2Int gridPos)
        {
            if (IsValidGridPos(gridPos)) return gridState[gridPos.x, gridPos.y];
            return HazardType.None;
        }

        private GameObject GetPrefabForType(HazardType type)
        {
            switch (type)
            {
                case HazardType.DarkMist: return darkMistPrefab;
                case HazardType.Fire: return firePrefab;
                case HazardType.Water: return waterPrefab;
                case HazardType.Oil: return oilPrefab;
                case HazardType.Electricity: return electricityPrefab;
                case HazardType.Ice: return icePrefab;
                case HazardType.Sticky: return stickyPrefab;
                default: return null;
            }
        }

        private bool IsValidGridPos(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
        }
    }

}
