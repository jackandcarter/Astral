using System;
using System.Collections.Generic;

namespace Astral.GameLogic.Cards
{
    /// <summary>
    /// Immutable definition for a card that can appear in decks.
    /// </summary>
    [Serializable]
    public class CardTemplate
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public IReadOnlyDictionary<string, int> BaseStats { get; }
        public IReadOnlyList<EffectDefinition> BaseEffects { get; }
        public IReadOnlyCollection<string> Tags { get; }
        public IReadOnlyList<UpgradeDefinition> UpgradePaths { get; }

        public CardTemplate(
            string id,
            string name,
            int cost,
            IReadOnlyDictionary<string, int> baseStats,
            IReadOnlyList<EffectDefinition> baseEffects,
            IReadOnlyCollection<string> tags,
            IReadOnlyList<UpgradeDefinition> upgradePaths)
        {
            Id = id;
            Name = name;
            Cost = cost;
            BaseStats = baseStats;
            BaseEffects = baseEffects;
            Tags = tags;
            UpgradePaths = upgradePaths;
        }
    }

    /// <summary>
    /// Runtime instance of a card, including any mutations or temporary state.
    /// </summary>
    [Serializable]
    public class CardInstance : ICloneable
    {
        public Guid InstanceId { get; }
        public CardTemplate Template { get; }
        public Dictionary<string, int> CurrentStats { get; }
        public List<Mutation> Mutations { get; }
        public List<EffectDefinition> TemporaryEffects { get; }

        public CardInstance(CardTemplate template)
        {
            InstanceId = Guid.NewGuid();
            Template = template;
            CurrentStats = new Dictionary<string, int>(template.BaseStats);
            Mutations = new List<Mutation>();
            TemporaryEffects = new List<EffectDefinition>();
        }

        private CardInstance(CardInstance source)
        {
            InstanceId = source.InstanceId;
            Template = source.Template;
            CurrentStats = new Dictionary<string, int>(source.CurrentStats);
            Mutations = new List<Mutation>(source.Mutations.Count);
            foreach (var mutation in source.Mutations)
            {
                Mutations.Add(mutation.Clone());
            }

            TemporaryEffects = new List<EffectDefinition>(source.TemporaryEffects);
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

    [Serializable]
    public class EffectDefinition
    {
        public string Id { get; }
        public string Description { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }

        public EffectDefinition(string id, string description, IReadOnlyDictionary<string, object> parameters)
        {
            Id = id;
            Description = description;
            Parameters = parameters;
        }
    }

    [Serializable]
    public class UpgradeDefinition
    {
        public string Id { get; }
        public string TargetTemplateId { get; }
        public string RequirementDescription { get; }

        public UpgradeDefinition(string id, string targetTemplateId, string requirementDescription)
        {
            Id = id;
            TargetTemplateId = targetTemplateId;
            RequirementDescription = requirementDescription;
        }
    }
}
