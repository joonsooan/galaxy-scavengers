using System.Collections.Generic;
using UnityEngine;

public class ProceduralQuestGenerator
{
    public class GenerationSettings
    {
        public List<ResourceType> earlyBasicResourcePool = new();
        public List<ResourceType> lateBasicResourcePool = new();
        public List<ResourceType> lateProcessedResourcePool = new();
        public Vector2Int earlyBasicAmountRange = new(20, 50);
        public Vector2Int lateBasicAmountRange = new(80, 140);
        public Vector2Int lateProcessedAmountRange = new(10, 30);
        public int switchToLateRuleQuestIndex = 4;
        public float lateBasicWeight = 0.65f;
        public int choicesPerCycle = 3;
        public int rewardAmountMin = 5;
        public int rewardAmountMax = 15;
        public bool useDeterministicSeed;
        public int deterministicSeed;
    }

    private readonly GenerationSettings _settings;
    private readonly System.Random _deterministicRandom;

    public ProceduralQuestGenerator(GenerationSettings settings)
    {
        _settings = settings;
        if (_settings.useDeterministicSeed)
        {
            _deterministicRandom = new System.Random(_settings.deterministicSeed);
        }
    }

    public List<ProceduralQuestChoiceData> GenerateChoices(int nextQuestIndex, ref int nextQuestId)
    {
        List<ProceduralQuestChoiceData> choices = new();
        HashSet<ResourceType> usedResources = new();

        int count = Mathf.Max(1, _settings.choicesPerCycle);
        for (int i = 0; i < count; i++)
        {
            ProceduralQuestChoiceData choice = BuildChoice(nextQuestIndex, nextQuestId, usedResources);
            choices.Add(choice);
            usedResources.Add(choice.targetResourceType);
            nextQuestId++;
        }

        return choices;
    }

    private ProceduralQuestChoiceData BuildChoice(int questIndex, int questId, HashSet<ResourceType> usedResources)
    {
        bool useEarlyRule = questIndex < _settings.switchToLateRuleQuestIndex;
        ResourceType targetType = useEarlyRule
            ? PickUniqueType(_settings.earlyBasicResourcePool, usedResources)
            : PickLateRuleResourceType(usedResources);
        int requiredAmount = useEarlyRule
            ? PickRange(_settings.earlyBasicAmountRange)
            : PickLateRuleAmount(targetType);

        ProceduralQuestChoiceData choice = new()
        {
            questId = questId,
            targetResourceType = targetType,
            requiredAmount = Mathf.Max(1, requiredAmount),
            createdAtQuestIndex = questIndex
        };

        choice.rewardSpecs.Add(new ProceduralQuestRewardSpec
        {
            resourceType = targetType,
            amount = Mathf.Max(0, PickInt(_settings.rewardAmountMin, _settings.rewardAmountMax))
        });

        return choice;
    }

    private ResourceType PickLateRuleResourceType(HashSet<ResourceType> usedResources)
    {
        bool useBasic = PickFloat01() <= Mathf.Clamp01(_settings.lateBasicWeight);
        List<ResourceType> firstPool = useBasic ? _settings.lateBasicResourcePool : _settings.lateProcessedResourcePool;
        List<ResourceType> fallbackPool = useBasic ? _settings.lateProcessedResourcePool : _settings.lateBasicResourcePool;

        ResourceType selected = PickUniqueType(firstPool, usedResources);
        if (selected != ResourceType.None)
        {
            return selected;
        }

        selected = PickUniqueType(fallbackPool, usedResources);
        if (selected != ResourceType.None)
        {
            return selected;
        }

        return PickUniqueType(_settings.earlyBasicResourcePool, usedResources);
    }

    private int PickLateRuleAmount(ResourceType targetType)
    {
        if (_settings.lateProcessedResourcePool != null && _settings.lateProcessedResourcePool.Contains(targetType))
        {
            return PickRange(_settings.lateProcessedAmountRange);
        }

        return PickRange(_settings.lateBasicAmountRange);
    }

    private ResourceType PickUniqueType(List<ResourceType> pool, HashSet<ResourceType> usedResources)
    {
        if (pool == null || pool.Count == 0)
        {
            return ResourceType.None;
        }

        List<ResourceType> available = new();
        for (int i = 0; i < pool.Count; i++)
        {
            ResourceType type = pool[i];
            if (type == ResourceType.None)
            {
                continue;
            }

            if (!usedResources.Contains(type))
            {
                available.Add(type);
            }
        }

        if (available.Count == 0)
        {
            return pool[PickInt(0, pool.Count - 1)];
        }

        return available[PickInt(0, available.Count - 1)];
    }

    private int PickRange(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return PickInt(min, max);
    }

    private int PickInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        if (_settings.useDeterministicSeed)
        {
            return _deterministicRandom.Next(minInclusive, maxInclusive + 1);
        }

        return Random.Range(minInclusive, maxInclusive + 1);
    }

    private float PickFloat01()
    {
        if (_settings.useDeterministicSeed)
        {
            return (float)_deterministicRandom.NextDouble();
        }

        return Random.value;
    }
}
