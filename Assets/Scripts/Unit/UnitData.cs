using UnityEngine;

[CreateAssetMenu(fileName = "New Unit Data", menuName = "Unit/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("Base Information")]
    public string unitName;
    public Sprite unitIcon;
    public GameObject unitPrefab;

    [Header("Production Costs")]
    public float productionTime = 3f;
    public ResourceCost[] productionCosts;

    [Header("Noise Coefficient")]
    [Tooltip("Noise coefficient value for this unit (0-100)")]
    [Range(0f, 100f)]
    public float noiseCoefficient = 0f;
}