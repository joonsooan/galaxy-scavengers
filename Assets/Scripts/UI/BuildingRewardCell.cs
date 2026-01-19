using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingRewardCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image buildingIcon;
    [SerializeField] private TMP_Text buildingNameText;
    
    public void SetBuildingData(BuildingData buildingData)
    {
        if (buildingData == null) return;
        
        if (buildingNameText != null)
        {
            buildingNameText.text = buildingData.displayName;
        }
        
        if (buildingIcon != null)
        {
            if (buildingData.icon != null)
            {
                buildingIcon.sprite = buildingData.icon;
                buildingIcon.type = Image.Type.Simple;
                buildingIcon.preserveAspect = true;
                buildingIcon.enabled = true;
            }
            else
            {
                buildingIcon.enabled = false;
            }
        }
    }
}
