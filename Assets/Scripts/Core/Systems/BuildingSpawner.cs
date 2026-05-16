using UnityEngine;
using UnityEngine.Tilemaps;

public class BuildingSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap buildingTilemap;
    [SerializeField] private Grid grid;
    [SerializeField] private Transform parentTransform;

    public Tilemap BuildingTilemap => buildingTilemap;

    public void SpawnBuildings()
    {
        if (buildingTilemap == null || grid == null) {
            Debug.LogError("BuildingSpawner: Missing references.");
            return;
        }
        
        if (parentTransform == null) {
            parentTransform = transform;
        }
        
        if (buildingTilemap != null)
        {
            buildingTilemap.RefreshAllTiles();
        }
    }
}