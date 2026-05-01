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
        public int tokenRewardAmountMin = 5;
        public int tokenRewardAmountMax = 15;
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
            ? PickTenMultipleInRange(_settings.earlyBasicAmountRange)
            : PickLateRuleAmountAsTenMultiple(targetType);

        ProceduralQuestChoiceData choice = new()
        {
            questId = questId,
            targetResourceType = targetType,
            requiredAmount = Mathf.Max(10, requiredAmount),
            createdAtQuestIndex = questIndex
        };

        choice.rewardSpecs.Add(new ProceduralQuestRewardSpec
        {
            kind = ProceduralQuestRewardKind.Token,
            amount = Mathf.Max(0, PickInt(_settings.tokenRewardAmountMin, _settings.tokenRewardAmountMax))
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

    private int PickLateRuleAmountAsTenMultiple(ResourceType targetType)
    {
        if (_settings.lateProcessedResourcePool != null && _settings.lateProcessedResourcePool.Contains(targetType))
        {
            return PickTenMultipleInRange(_settings.lateProcessedAmountRange);
        }

        return PickTenMultipleInRange(_settings.lateBasicAmountRange);
    }

    private int PickTenMultipleInRange(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        int minT = min <= 0 ? 1 : (min + 9) / 10;
        int maxT = max / 10;
        if (minT > maxT)
        {
            int snappedDown = (max / 10) * 10;
            if (snappedDown <= 0)
            {
                return Mathf.Clamp(max, 1, max);
            }

            return Mathf.Max(min, snappedDown);
        }

        return PickInt(minT, maxT) * 10;
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
