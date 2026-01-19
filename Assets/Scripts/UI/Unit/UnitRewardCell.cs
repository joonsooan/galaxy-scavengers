using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitRewardCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image unitIcon;
    [SerializeField] private TMP_Text unitNameText;
    
    public void SetUnitData(UnitData unitData)
    {
        if (unitData == null) return;
        
        if (unitNameText != null)
        {
            unitNameText.text = unitData.unitName;
        }
        
        if (unitIcon != null)
        {
            if (unitData.unitIcon != null)
            {
                unitIcon.sprite = unitData.unitIcon;
                unitIcon.type = Image.Type.Simple;
                unitIcon.preserveAspect = true;
                unitIcon.enabled = true;
            }
            else
            {
                unitIcon.enabled = false;
            }
        }
    }
}
