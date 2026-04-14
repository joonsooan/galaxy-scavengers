using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitProcessorActivityCellView : MonoBehaviour
{
    [SerializeField] private Image resourceIcon;
    [SerializeField] private TMP_Text perMinuteText;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetData(Sprite icon, float perMinute)
    {
        if (resourceIcon != null)
        {
            resourceIcon.sprite = icon;
            resourceIcon.enabled = icon != null;
        }

        if (perMinuteText != null)
        {
            perMinuteText.text = perMinute.ToString("0.##");
        }
    }
}
