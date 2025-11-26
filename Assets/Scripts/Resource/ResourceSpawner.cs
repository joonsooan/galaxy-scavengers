using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ResourceSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap resourceTilemap;
    [SerializeField] private TileBase[] resourceTiles;
    [SerializeField] private GameObject[] resourcePrefabs;
    [SerializeField] private Grid grid;
    [SerializeField] private Transform parentTransform;

    private readonly List<GameObject> _spawnedResources = new List<GameObject>();

    public Tilemap ResourceTilemap {
        get {
            return resourceTilemap;
        }
    }

    public void SpawnResources()
    {
        if (parentTransform == null) {
            parentTransform = transform;
        }

        _spawnedResources.Clear();

        foreach (Vector3Int pos in resourceTilemap.cellBounds.allPositionsWithin) {
            TileBase currentTile = resourceTilemap.GetTile(pos);

            if (currentTile == null) continue;

            for (int i = 0; i < resourceTiles.Length; i++) {
                TileBase t = resourceTiles[i];
                if (currentTile != t) continue;

                Vector3 worldPos = grid.GetCellCenterWorld(pos);
                GameObject resourceNodeObj = Instantiate(resourcePrefabs[i], worldPos, Quaternion.identity, parentTransform);
                ResourceNode nodeComponent = resourceNodeObj.GetComponent<ResourceNode>();

                if (nodeComponent != null) {
                    nodeComponent.cellPosition = pos;
                    nodeComponent.spawner = this;
                }

                _spawnedResources.Add(resourceNodeObj);
                break;
            }
        }
    }


    public void NotifyResourceDestroyed(ResourceNode node)
    {
        if (node == null) return;
        _spawnedResources.Remove(node.gameObject);
    }
}
