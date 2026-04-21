using System.Collections.Generic;
using UnityEngine;

public static class ChargingStationRegistry
{
    private static readonly List<ChargingStation> Stations = new List<ChargingStation>();

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
}
