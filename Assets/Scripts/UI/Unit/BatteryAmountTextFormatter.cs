using UnityEngine;

public static class BatteryAmountTextFormatter
{
    public static string Format(float current, float max)
    {
        return $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }
}
