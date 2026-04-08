using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExtractorInputTier
{
    public ResourceType inputResource;
    [Min(1)] public int amountConsumedPerCycle = 1;
    [Min(0.01f)] public float cycleDurationSeconds = 5f;
    [Min(0f)] public float dataPercentGainedPerCycle = 1f;
    [Min(0)] public int outputDataItemsPerCycle = 1;
}

[CreateAssetMenu(fileName = "New Extractor Data", menuName = "Building/Extractor Data")]
public class ExtractorData : DisplayableData
{
    [Header("Extraction Limits")]
    [Tooltip("Maximum data % extractable in this run (e.g. planetary cap for the session).")]
    [Min(0.1f)] public float maxExtractablePercent = 25f;

    [Header("Output")]
    public ResourceType outputResourceType = ResourceType.NexusData;

    [Header("Input Tiers")]
    public List<ExtractorInputTier> inputTiers = new List<ExtractorInputTier>();

    [Header("Power")]
    [Min(0)] public int electricityConsumptionPerSecond = 1;
}
