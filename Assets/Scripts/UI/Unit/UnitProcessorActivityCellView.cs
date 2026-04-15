using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitProcessorActivityCellView : MonoBehaviour
{
    [SerializeField] private Image resourceIcon;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_Text perMinuteText;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetData(Sprite icon, float normalizedRatio, float perMinuteAverage)
    {
        if (resourceIcon != null)
        {
            resourceIcon.sprite = icon;
            resourceIcon.enabled = icon != null;
        }

        if (amountSlider != null)
        {
            amountSlider.value = Mathf.Clamp01(normalizedRatio);
        }

        if (perMinuteText != null)
        {
            float clamped = Mathf.Max(0f, perMinuteAverage);
            if (clamped > 0f && clamped < 1f)
            {
                perMinuteText.text = $"{clamped:0.0} / min";
            }
            else
            {
                int rounded = Mathf.RoundToInt(clamped);
                perMinuteText.text = $"{rounded} / min";
            }
        }
    }
}
