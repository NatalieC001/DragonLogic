using UnityEngine;
using VRDragonBoss.GameBoardSystem;

public class TestTileVisualizer : MonoBehaviour
{
    public MeshRenderer outerQuad;
    public MeshRenderer innerQuad;

    private MaterialPropertyBlock outerPropBlock;
    private MaterialPropertyBlock innerPropBlock;

    private Color defaultOuterColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Semi-transparent gray

    private void Awake()
    {
        outerPropBlock = new MaterialPropertyBlock();
        innerPropBlock = new MaterialPropertyBlock();
    }

    public void Initialize()
    {
        if (outerQuad != null)
        {
            outerQuad.GetPropertyBlock(outerPropBlock);
            outerPropBlock.SetColor("_Color", defaultOuterColor);
            outerQuad.SetPropertyBlock(outerPropBlock);
        }

        if (innerQuad != null)
        {
            innerQuad.gameObject.SetActive(false);
        }
    }

    public void HighlightAsTarget(HazardType hazardType)
    {
        if (outerQuad != null)
        {
            outerQuad.GetPropertyBlock(outerPropBlock);
            outerPropBlock.SetColor("_Color", Color.yellow);
            outerQuad.SetPropertyBlock(outerPropBlock);
        }

        if (innerQuad != null)
        {
            innerQuad.gameObject.SetActive(true);
            innerQuad.GetPropertyBlock(innerPropBlock);
            innerPropBlock.SetColor("_Color", GetColorForHazard(hazardType));
            innerQuad.SetPropertyBlock(innerPropBlock);
        }
    }

    public void ResetVisuals()
    {
        if (outerQuad != null)
        {
            outerQuad.GetPropertyBlock(outerPropBlock);
            outerPropBlock.SetColor("_Color", defaultOuterColor);
            outerQuad.SetPropertyBlock(outerPropBlock);
        }

        if (innerQuad != null)
        {
            innerQuad.gameObject.SetActive(false);
        }
    }

    // Removed OnTriggerEnter and OnCollisionEnter resets,
    // SpatialStrategyMiniGame now manages clearing the target manually.

    private Color GetColorForHazard(HazardType hazardType)
    {
        switch (hazardType)
        {
            case HazardType.Fire: return Color.red;
            case HazardType.Water: return Color.blue;
            case HazardType.Electricity: return Color.yellow;
            case HazardType.Ice: return Color.cyan;
            case HazardType.Oil: return Color.black;
            case HazardType.DarkMist: return new Color(0.2f, 0f, 0.4f);
            case HazardType.Sticky: return new Color(0.5f, 0.25f, 0f);
            default: return Color.white;
        }
    }
}
