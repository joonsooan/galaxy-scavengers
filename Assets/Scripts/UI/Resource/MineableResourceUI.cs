using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MineableResourceUI : MonoBehaviour
{
    [SerializeField] private List<Toggle> resourceToggles;

    private readonly List<ResourceType> _selectedTypes = new();

    void Start()
    {
        _selectedTypes.AddRange((ResourceType[])System.Enum.GetValues(typeof(ResourceType)));
        _selectedTypes.Remove(ResourceType.Solana);
            
        foreach (var toggle in resourceToggles)
        {
            ResourceToggle resourceInfo = toggle.GetComponent<ResourceToggle>();
            if (resourceInfo == null)
            {
                continue;
            }
            
            toggle.isOn = _selectedTypes.Contains(resourceInfo.resourceType);

            toggle.onValueChanged.AddListener((isToggled) =>
            {
                HandleToggleChange(isToggled, resourceInfo.resourceType);
            });
        }
        
        ApplyChanges();
    }

    private void HandleToggleChange(bool isToggled, ResourceType type)
    {
        if (isToggled)
        {
            if (!_selectedTypes.Contains(type)) _selectedTypes.Add(type);
        }
        else
        {
            _selectedTypes.Remove(type);
        }

        ApplyChanges();
    }

    private void ApplyChanges()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.UpdateAllLifterMineableTypes(_selectedTypes);
        }
    }
}