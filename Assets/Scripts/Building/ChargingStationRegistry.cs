using System.Collections.Generic;
using UnityEngine;

public static class ChargingStationRegistry
{
    private static readonly List<ChargingStation> Stations = new List<ChargingStation>();
    private static readonly HashSet<ChargingStation> RejectForApproachPass = new HashSet<ChargingStation>();

    public static void Register(ChargingStation station)
    {
        if (station == null || Stations.Contains(station))
        {
            return;
        }

        Stations.Add(station);
    }

    public static void Unregister(ChargingStation station)
    {
        if (station == null)
        {
            return;
        }

        Stations.Remove(station);
    }

    public static ChargingStation GetNearest(Vector3 worldPosition)
    {
        ChargingStation best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < Stations.Count; i++)
        {
            ChargingStation s = Stations[i];
            if (s == null || !s.isActiveAndEnabled)
            {
                continue;
            }

            float sq = (s.transform.position - worldPosition).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = s;
            }
        }

        return best;
    }

    public static bool TryGetNearestStationForApproach(Vector3 worldPosition, UnitAllyBatteryDriver driver, out ChargingStation station)
    {
        station = null;
        if (driver == null)
        {
            return false;
        }

        RejectForApproachPass.Clear();

        while (true)
        {
            ChargingStation best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Stations.Count; i++)
            {
                ChargingStation s = Stations[i];
                if (s == null || !s.isActiveAndEnabled || RejectForApproachPass.Contains(s))
                {
                    continue;
                }

                if (!s.WouldAcceptNewApproach())
                {
                    continue;
                }

                float sq = (s.transform.position - worldPosition).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = s;
                }
            }

            if (best == null)
            {
                return false;
            }

            if (best.TryBeginApproach(driver))
            {
                station = best;
                return true;
            }

            RejectForApproachPass.Add(best);
        }
    }
}
