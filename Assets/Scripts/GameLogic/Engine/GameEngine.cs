using System;
using System.Collections.Generic;
using Astral.GameLogic.Cards;
using Astral.GameLogic.State;

namespace Astral.GameLogic.Engine
{
    /// <summary>
    /// Central coordinator for pure game logic. Unity communicates through runtime managers, not through this class directly.
    /// </summary>
    public class GameEngine
    {
        public BattleState State { get; private set; }
        public DeterministicRng Rng { get; }
        public TurnManager TurnManager { get; }
        public RuleValidator RuleValidator { get; }
        public EffectResolver EffectResolver { get; }

        public GameEngine(BattleState initialState)
        {
            State = (BattleState)initialState.Clone();
            Rng = new DeterministicRng(initialState.RandomSeed);
            RuleValidator = new RuleValidator();
            EffectResolver = new EffectResolver();
            TurnManager = new TurnManager(RuleValidator, EffectResolver);
        }

        public void ApplyAction(IGameAction action)
        {
            RuleValidator.EnsureActionIsValid(State, action);
            action.Resolve(State, Rng, EffectResolver);
            State.IncrementActionCounter();
        }

        public BattleState CloneState()
        {
            return (BattleState)State.Clone();
        }
    }

    public interface IGameAction
    {
        void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver);
    }

    /// <summary>
    /// Manages deterministic turn and phase progression.
    /// </summary>
    public class TurnManager
    {
        private readonly RuleValidator _ruleValidator;
        private readonly EffectResolver _effectResolver;

        public TurnManager(RuleValidator ruleValidator, EffectResolver effectResolver)
        {
            _ruleValidator = ruleValidator;
            _effectResolver = effectResolver;
        }

        public void AdvancePhase(BattleState state, Phase nextPhase)
        {
            _ruleValidator.ValidatePhaseTransition(state.Turn.CurrentPhase, nextPhase);
            state.Turn.AdvancePhase(nextPhase);
            _effectResolver.ResolvePhaseTriggers(state, nextPhase);
        }

        public void AdvanceTurn(BattleState state)
        {
            var nextPlayer = new PlayerId((state.Turn.ActivePlayer.Value + 1) % state.Players.Count);
            state.Turn.AdvanceTurn(nextPlayer);
            _effectResolver.ResolveTurnStart(state);
        }
    }

    /// <summary>
    /// Ensures all game actions follow the rules defined in the core documents.
    /// </summary>
    public class RuleValidator
    {
        public void EnsureActionIsValid(BattleState state, IGameAction action)
        {
            // Placeholder for full validation logic defined by the design docs.
            // Keeping this explicit avoids burying rules inside UI or MonoBehaviours.
        }

        public void ValidatePhaseTransition(Phase current, Phase next)
        {
            if (current == Phase.End && next == Phase.Start)
            {
                return;
            }

            if ((int)next < (int)current)
            {
                throw new InvalidOperationException($"Cannot go backwards from {current} to {next}.");
            }
        }
    }

    /// <summary>
    /// Interprets EffectDefinitions against the current BattleState.
    /// </summary>
    public class EffectResolver
    {
        public void ResolveEffect(BattleState state, EffectReference effect, DeterministicRng rng)
        {
            // Effect resolution is intentionally data-driven; implementations live here, not on Unity prefabs.
            state.Effects.Pop();
        }

        public void ResolvePhaseTriggers(BattleState state, Phase phase)
        {
            // Placeholder for automatic triggers (start/end of phases) as defined by authored effects.
        }

        public void ResolveTurnStart(BattleState state)
        {
            // Placeholder for start-of-turn triggers and status effect ticks.
            foreach (var player in state.Players)
            {
                foreach (var status in player.StatusEffects)
                {
                    status.TickDuration();
                }
            }
        }
    }

    /// <summary>
    /// Deterministic RNG derived from the BattleState seed.
    /// </summary>
    public class DeterministicRng
    {
        private readonly Random _random;

        public DeterministicRng(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }

    /// <summary>
    /// Factory for constructing initial battle states from authored content.
    /// </summary>
    public static class BattleStateFactory
    {
        public static BattleState Create(IReadOnlyList<CardTemplate> playerOneDeck, IReadOnlyList<CardTemplate> playerTwoDeck, int seed)
        {
            var playerStates = new List<PlayerState>
            {
                BuildPlayer(new PlayerId(0), playerOneDeck),
                BuildPlayer(new PlayerId(1), playerTwoDeck)
            };

            var turn = new TurnState(new PlayerId(0));
            var effects = new EffectStack();
            var context = new MatchContext();

            return new BattleState(playerStates, turn, effects, context, seed);
        }

        private static PlayerState BuildPlayer(PlayerId id, IReadOnlyList<CardTemplate> deckTemplates)
        {
            var deckInstances = new List<CardInstance>(deckTemplates.Count);
            foreach (var template in deckTemplates)
            {
                deckInstances.Add(new CardInstance(template));
            }

            return new PlayerState(id, startingHealth: 30, deck: deckInstances, startingResources: 0);
        }
    }
}
