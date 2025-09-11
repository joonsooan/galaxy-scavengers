using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public int size;
        public GameObject prefab;
        public Transform parent;
    }

    public static ObjectPooler Instance;

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> _poolDictionary;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            Transform poolParent = pool.parent;
            
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, poolParent);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            _poolDictionary.Add(pool.tag, objectPool);
        }
    }

    public GameObject SpawnFromPool(string objTag, Vector3 position, Quaternion rotation)
    {
        if (!_poolDictionary.ContainsKey(objTag))
        {
            Debug.LogWarning("Pool with tag " + objTag + " doesn't exist.");
            return null;
        }

        GameObject objectToSpawn = _poolDictionary[objTag].Dequeue();

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        _poolDictionary[objTag].Enqueue(objectToSpawn);

        return objectToSpawn;
    }
}