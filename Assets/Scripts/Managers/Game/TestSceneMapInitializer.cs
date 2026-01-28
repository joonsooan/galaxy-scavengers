using UnityEngine;

public class TestSceneMapInitializer : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private bool useFixedSeed = true;
    [SerializeField] private int fixedSeed = 0;

    private void Start()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        if (mapGenerator == null)
        {
            return;
        }

        if (useFixedSeed)
        {
            mapGenerator.SetFixedSeed(fixedSeed);
        }

        mapGenerator.GenerateMap();
    }
}

