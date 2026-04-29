using System.Collections.Generic;
using UnityEngine;

public enum UnitProcessStatKind
{
    Produce,
    Spend
}

public static class UnitProcessResourceStatTracker
{
    private struct Sample
    {
        public float time;
        public int amount;
    }

    private static readonly Dictionary<ResourceType, Queue<Sample>> ProduceSamples = new Dictionary<ResourceType, Queue<Sample>>();
    private static readonly Dictionary<ResourceType, Queue<Sample>> SpendSamples = new Dictionary<ResourceType, Queue<Sample>>();
    private static readonly Dictionary<ResourceType, Queue<Sample>> PowerFuelSpendSamples = new Dictionary<ResourceType, Queue<Sample>>();

    public static void ResetAllStats()
    {
        ClearTable(ProduceSamples);
        ClearTable(SpendSamples);
        ClearTable(PowerFuelSpendSamples);
    }

    private static void ClearTable(Dictionary<ResourceType, Queue<Sample>> table)
    {
        foreach (Queue<Sample> queue in table.Values)
        {
            queue?.Clear();
        }

        table.Clear();
    }

    public static void RecordProduce(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Enqueue(ProduceSamples, type, amount);
    }

    public static void RecordSpend(ResourceType type, int amount, bool isPowerFuelSpend)
    {
        if (amount <= 0)
        {
            return;
        }

        Enqueue(SpendSamples, type, amount);
        if (isPowerFuelSpend)
        {
            Enqueue(PowerFuelSpendSamples, type, amount);
        }
    }

    public static float GetPerMinuteAverage(ResourceType type, UnitProcessStatKind kind, float windowMinutes, bool excludePowerFuelSpend)
    {
        float safeWindowMinutes = Mathf.Max(0.01f, windowMinutes);
        float now = Time.unscaledTime;
        float windowSeconds = safeWindowMinutes * 60f;
        float minTime = now - windowSeconds;

        if (kind == UnitProcessStatKind.Produce)
        {
            int produced = PruneAndSum(ProduceSamples, type, minTime);
            return produced / safeWindowMinutes;
        }

        int spent = PruneAndSum(SpendSamples, type, minTime);
        if (!excludePowerFuelSpend)
        {
            return spent / safeWindowMinutes;
        }

        int powerFuelSpent = PruneAndSum(PowerFuelSpendSamples, type, minTime);
        int effectiveSpend = Mathf.Max(0, spent - powerFuelSpent);
        return effectiveSpend / safeWindowMinutes;
    }

    private static void Enqueue(Dictionary<ResourceType, Queue<Sample>> table, ResourceType type, int amount)
    {
        if (!table.TryGetValue(type, out Queue<Sample> queue))
        {
            queue = new Queue<Sample>();
            table[type] = queue;
        }

        queue.Enqueue(new Sample
        {
            time = Time.unscaledTime,
            amount = amount
        });
    }

    private static int PruneAndSum(Dictionary<ResourceType, Queue<Sample>> table, ResourceType type, float minTime)
    {
        if (!table.TryGetValue(type, out Queue<Sample> queue) || queue.Count == 0)
        {
            return 0;
        }

        while (queue.Count > 0 && queue.Peek().time < minTime)
        {
            queue.Dequeue();
        }

        int total = 0;
        foreach (Sample sample in queue)
        {
            total += sample.amount;
        }

        return total;
    }
}
