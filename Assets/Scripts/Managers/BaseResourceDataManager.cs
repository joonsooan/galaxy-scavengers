using System.Collections.Generic;
using UnityEngine;

public class BaseResourceDataManager : MonoBehaviour
{
    [Header("Resource Icons")]
    [SerializeField] private List<Sprite> resourceIcons;

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
    
    public int GetResourceIconCount()
    {
        return resourceIcons != null ? resourceIcons.Count : 0;
    }
}

