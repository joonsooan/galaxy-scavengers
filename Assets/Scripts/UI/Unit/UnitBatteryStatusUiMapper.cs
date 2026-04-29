using UnityEngine;

public static class UnitBatteryStatusUiMapper
{
    public struct Result
    {
        public string StatusText;
        public bool ShowProblemIcon;
        public bool ShowOkIcon;
    }

    public static Result Map(
        UnitBattery battery,
        string textPowerEmpty,
        string textNeedCharge,
        string textGoingToCharge,
        string textQueued,
        string textCharging,
        string textOk)
    {
        Result r = new Result {
            StatusText = string.Empty,
            ShowProblemIcon = false,
            ShowOkIcon = false
        };

        if (battery == null)
        {
            return r;
        }

        float ratio = battery.NormalizedRatio;
        float thr = battery.ChargeThresholdNormalized;

        if (ratio <= 0.0001f)
        {
            r.StatusText = textPowerEmpty;
            r.ShowProblemIcon = true;
            return r;
        }

        switch (battery.FlowState)
        {
        case AllyBatteryFlowState.Charging:
            r.StatusText = textCharging;
            r.ShowOkIcon = true;
            return r;
        case AllyBatteryFlowState.QueuedAtStation:
            r.StatusText = textQueued;
            r.ShowProblemIcon = true;
            return r;
        case AllyBatteryFlowState.GoingToStation:
            r.StatusText = textGoingToCharge;
            r.ShowProblemIcon = true;
            return r;
        default:
            if (ratio <= thr)
            {
                r.StatusText = textNeedCharge;
                r.ShowProblemIcon = true;
            }
            else
            {
                r.StatusText = textOk;
                r.ShowOkIcon = true;
            }

            return r;
        }
    }
}
