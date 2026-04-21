using UnityEngine;
using UnityEngine.UI;

public static class BatterySliderFillColorUtility
{
    public static void ApplyDiscreteByRatio(
        Slider slider,
        float normalizedCharge01,
        Color colorEmpty,
        Color colorFull)
    {
        if (slider == null || slider.fillRect == null)
        {
            return;
        }

        Image img = slider.fillRect.GetComponent<Image>();
        if (img == null)
        {
            return;
        }

        img.color = normalizedCharge01 <= 0.0001f ? colorEmpty : colorFull;
    }

    public static void ApplyDiscreteFillColor(
        Slider slider,
        UnitBattery battery,
        Color colorEmpty,
        Color colorNeedCharge,
        Color colorCharging,
        Color colorFull)
    {
        if (slider == null || slider.fillRect == null)
        {
            return;
        }

        Image img = slider.fillRect.GetComponent<Image>();
        if (img == null)
        {
            return;
        }

        if (battery == null)
        {
            img.color = colorEmpty;
            return;
        }

        if (battery.NormalizedRatio <= 0.0001f)
        {
            img.color = colorEmpty;
            return;
        }

        switch (battery.FlowState)
        {
        case AllyBatteryFlowState.Charging:
            img.color = colorCharging;
            return;
        case AllyBatteryFlowState.QueuedAtStation:
        case AllyBatteryFlowState.GoingToStation:
            img.color = colorNeedCharge;
            return;
        default:
            img.color = battery.NormalizedRatio <= battery.ChargeThresholdNormalized
                ? colorNeedCharge
                : colorFull;
            return;
        }
    }
}
