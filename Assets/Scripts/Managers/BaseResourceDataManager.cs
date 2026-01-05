using System.Collections.Generic;
using UnityEngine;

public class BaseResourceDataManager : MonoBehaviour
{
    public static BaseResourceDataManager Instance { get; private set; }

    [Header("Resource Icons")]
    [SerializeField] private List<Sprite> resourceIcons;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Sprite GetResourceIcon(ResourceType type)
    {
        if (resourceIcons == null || resourceIcons.Count == 0)
        {
            return null;
        }

        int index = (int)type;
        if (index >= 0 && index < resourceIcons.Count)
        {
            return resourceIcons[index];
        }

        return null;
    }
}

