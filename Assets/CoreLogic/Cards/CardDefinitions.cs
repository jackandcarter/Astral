using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astral.GameLogic.Cards
{
    /// <summary>
    /// Inspector-authorable definition for a card that can appear in decks.
    /// </summary>
    [CreateAssetMenu(menuName = "Astral/Card Template")]
    public class CardTemplate : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int cost;
        [SerializeField] private List<CardStatDefinition> baseStats = new();
        [SerializeField] private List<EffectDefinition> baseEffects = new();
        [SerializeField] private List<string> tags = new();
        [SerializeField] private List<UpgradeDefinition> upgradePaths = new();

        public string Id => id;
        public string Name => displayName;
        public int Cost => cost;
        public IReadOnlyList<CardStatDefinition> BaseStats => baseStats;
        public IReadOnlyList<EffectDefinition> BaseEffects => baseEffects;
        public IReadOnlyCollection<string> Tags => tags;
        public IReadOnlyList<UpgradeDefinition> UpgradePaths => upgradePaths;

        public Dictionary<string, int> BuildBaseStatDictionary()
        {
            return baseStats.ToDictionary(stat => stat.StatId, stat => stat.Value);
        }
    }

    /// <summary>
    /// Runtime instance of a card, including any mutations or temporary state.
    /// </summary>
    [Serializable]
    public class CardInstance : ICloneable
    {
        public Guid InstanceId { get; }
        public string TemplateId { get; }
        public Dictionary<string, int> CurrentStats { get; }
        public List<Mutation> Mutations { get; }
        public List<EffectDefinition> TemporaryEffects { get; }

        public CardInstance(CardTemplate template)
        {
            InstanceId = Guid.NewGuid();
            TemplateId = template.Id;
            CurrentStats = template.BuildBaseStatDictionary();
            Mutations = new List<Mutation>();
            TemporaryEffects = new List<EffectDefinition>();
        }

        private CardInstance(CardInstance source)
        {
            InstanceId = source.InstanceId;
            TemplateId = source.TemplateId;
            CurrentStats = new Dictionary<string, int>(source.CurrentStats);
            Mutations = new List<Mutation>(source.Mutations.Count);
            foreach (var mutation in source.Mutations)
            {
                Mutations.Add(mutation.Clone());
            }

            TemporaryEffects = new List<EffectDefinition>(source.TemporaryEffects);
        }

        public CardTemplate ResolveTemplate(CardRegistry registry)
        {
            return registry.GetTemplate(TemplateId);
        }

        public object Clone()
        {
            return new CardInstance(this);
        }
    }

    [Serializable]
    public class Mutation
    {
        public string Id { get; }
        public string Source { get; }
        public Dictionary<string, int> StatAdjustments { get; }

        public Mutation(string id, string source, Dictionary<string, int> statAdjustments)
        {
            Id = id;
            Source = source;
            StatAdjustments = statAdjustments;
        }

        public Mutation Clone()
        {
            return new Mutation(Id, Source, new Dictionary<string, int>(StatAdjustments));
        }
    }

    [CreateAssetMenu(menuName = "Astral/Effect Definition")]
    public class EffectDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField, TextArea] private string description;
        [SerializeField] private List<EffectParameter> parameters = new();

        public string Id => id;
        public string Description => description;
        public IReadOnlyList<EffectParameter> Parameters => parameters;

        public Dictionary<string, EffectParameterValue> GetParameterValues()
        {
            return parameters.ToDictionary(p => p.Key, p => p.Value);
        }
    }

    [CreateAssetMenu(menuName = "Astral/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string targetTemplateId;
        [SerializeField, TextArea] private string requirementDescription;

        public string Id => id;
        public string TargetTemplateId => targetTemplateId;
        public string RequirementDescription => requirementDescription;
    }

    [Serializable]
    public class CardStatDefinition
    {
        [SerializeField] private string statId;
        [SerializeField] private int value;

        public string StatId => statId;
        public int Value => value;
    }

    public enum EffectParameterType
    {
        Integer,
        Float,
        Boolean,
        String
    }

    [Serializable]
    public class EffectParameter
    {
        [SerializeField] private string key;
        [SerializeField] private EffectParameterType type;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private string stringValue;

        public string Key => key;
        public EffectParameterType Type => type;
        public EffectParameterValue Value => type switch
        {
            EffectParameterType.Integer => EffectParameterValue.FromInt(intValue),
            EffectParameterType.Float => EffectParameterValue.FromFloat(floatValue),
            EffectParameterType.Boolean => EffectParameterValue.FromBool(boolValue),
            EffectParameterType.String => EffectParameterValue.FromString(stringValue ?? string.Empty),
            _ => EffectParameterValue.None
        };
    }

    [Serializable]
    public struct EffectParameterValue
    {
        public EffectParameterType Type { get; private set; }
        public int IntValue { get; private set; }
        public float FloatValue { get; private set; }
        public bool BoolValue { get; private set; }
        public string StringValue { get; private set; }

        public static EffectParameterValue FromInt(int value) => new() { Type = EffectParameterType.Integer, IntValue = value };
        public static EffectParameterValue FromFloat(float value) => new() { Type = EffectParameterType.Float, FloatValue = value };
        public static EffectParameterValue FromBool(bool value) => new() { Type = EffectParameterType.Boolean, BoolValue = value };
        public static EffectParameterValue FromString(string value) => new() { Type = EffectParameterType.String, StringValue = value };

        public static EffectParameterValue None => new() { Type = EffectParameterType.String, StringValue = string.Empty };
    }

    [Serializable]
    public class CardRegistry : ICloneable
    {
        private readonly Dictionary<string, CardTemplate> _templates;

        public CardRegistry(IEnumerable<CardTemplate> templates)
        {
            _templates = templates?.ToDictionary(t => t.Id) ?? new Dictionary<string, CardTemplate>();
        }

        private CardRegistry(CardRegistry source)
        {
            _templates = new Dictionary<string, CardTemplate>(source._templates);
        }

        public CardTemplate GetTemplate(string id)
        {
            if (!_templates.TryGetValue(id, out var template))
            {
                throw new InvalidOperationException($"Card template '{id}' was not registered.");
            }

            return template;
        }

        public object Clone()
        {
            return new CardRegistry(this);
        }
    }
}
