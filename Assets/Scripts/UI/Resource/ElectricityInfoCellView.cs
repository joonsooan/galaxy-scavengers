using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ElectricityInfoCellView : MonoBehaviour
{
    [SerializeField] private Image buildingIcon;
    [SerializeField] private TMP_Text buildingCountText;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_Text statText;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetData(Sprite icon, int count, float normalizedRatio, float perMinuteAverage)
    {
        if (buildingIcon != null)
        {
            buildingIcon.sprite = icon;
            buildingIcon.enabled = icon != null;
        }

        if (buildingCountText != null)
        {
            buildingCountText.text = Mathf.Max(0, count).ToString();
        }

        if (amountSlider != null)
        {
            amountSlider.value = Mathf.Clamp01(normalizedRatio);
        }

        if (statText != null)
        {
            float clamped = Mathf.Max(0f, perMinuteAverage);
            statText.text = $"{clamped:0.0} / min";
        }
    }
}
