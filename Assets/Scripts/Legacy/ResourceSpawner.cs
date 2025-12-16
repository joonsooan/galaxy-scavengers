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

    private readonly List<GameObject> _spawnedResources = new ();

    public Tilemap ResourceTilemap => resourceTilemap;

    public void NotifyResourceDestroyed(ResourceNode node)
    {
        if (node == null) return;
        _spawnedResources.Remove(node.gameObject);
    }
}
