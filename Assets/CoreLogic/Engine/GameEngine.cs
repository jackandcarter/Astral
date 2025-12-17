using System;
using System.Collections.Generic;
using System.Linq;
using Astral.GameLogic.Cards;
using Astral.GameLogic.State;

namespace Astral.GameLogic.Engine
{
    public static class GameRulesConfig
    {
        public const int BaseActionsPerTurn = 1;
        public const int CardsDrawnPerTurn = 1;
        public const int BaseResourceGainPerTurn = 1;
    }

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
            Rng = new DeterministicRng(State);
            RuleValidator = new RuleValidator();
            EffectResolver = new EffectResolver();
            TurnManager = new TurnManager(RuleValidator, EffectResolver);
        }

        public void ApplyAction(IGameAction action)
        {
            RuleValidator.EnsureActionIsValid(State, action);
            action.Resolve(State, Rng, EffectResolver, TurnManager);
            EffectResolver.ResolvePendingEffects(State, Rng);
            State.IncrementActionCounter();
        }

        public BattleState CloneState()
        {
            return (BattleState)State.Clone();
        }
    }

    public interface IGameAction
    {
        void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver, TurnManager turnManager);
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
            if (action is IPlayerAction playerAction)
            {
                EnsureActivePlayer(state, playerAction.Player);
            }

            switch (action)
            {
                case DrawCardAction drawAction:
                    EnsurePhaseAllowsDrawing(state.Turn.CurrentPhase);
                    EnsureDeckHasCards(state, drawAction.Player, drawAction.Count);
                    break;
                case PlayCardAction playAction:
                    EnsurePhaseAllowsCardPlay(state.Turn.CurrentPhase);
                    ValidateCardPlay(state, playAction);
                    break;
                case AdvancePhaseAction advancePhaseAction:
                    EnsureActivePlayer(state, advancePhaseAction.Player);
                    ValidatePhaseTransition(state.Turn.CurrentPhase, advancePhaseAction.NextPhase);
                    break;
                case EndTurnAction:
                    EnsureEndTurnEligibility(state.Turn.CurrentPhase);
                    break;
            }
        }

        public void ValidatePhaseTransition(Phase current, Phase next)
        {
            var isForward = next switch
            {
                Phase.Start => current == Phase.End,
                Phase.Main => current == Phase.Start,
                Phase.Reaction => current == Phase.Main,
                Phase.End => current == Phase.Reaction,
                _ => false
            };

            if (!isForward)
            {
                throw new InvalidOperationException($"Cannot transition from {current} to {next}.");
            }
        }

        private static void EnsureActivePlayer(BattleState state, PlayerId player)
        {
            if (!state.Turn.ActivePlayer.Equals(player))
            {
                throw new InvalidOperationException($"It is not player {player}'s turn.");
            }
        }

        private static void EnsurePhaseAllowsDrawing(Phase currentPhase)
        {
            if (currentPhase != Phase.Start && currentPhase != Phase.Main)
            {
                throw new InvalidOperationException($"Cannot draw cards during {currentPhase} phase.");
            }
        }

        private static void EnsurePhaseAllowsCardPlay(Phase currentPhase)
        {
            if (currentPhase != Phase.Main)
            {
                throw new InvalidOperationException($"Cards can only be played during the Main phase. Current phase: {currentPhase}.");
            }
        }

        private static void ValidateCardPlay(BattleState state, PlayCardAction action)
        {
            var playerState = state.GetPlayer(action.Player);
            var card = playerState.Hand.Cards.FirstOrDefault(c => c.InstanceId == action.CardInstanceId);
            if (card == null)
            {
                throw new InvalidOperationException($"Card {action.CardInstanceId} is not in the player's hand.");
            }

            var template = card.ResolveTemplate(state.Context.CardRegistry);

            if (playerState.Resource.Current < template.Cost)
            {
                throw new InvalidOperationException($"Not enough resources to play {template.Name}.");
            }
        }

        private static void EnsureDeckHasCards(BattleState state, PlayerId player, int count)
        {
            var playerState = state.GetPlayer(player);
            if (playerState.Deck.Cards.Count < count)
            {
                throw new InvalidOperationException($"Player {player} cannot draw {count} cards; only {playerState.Deck.Cards.Count} remaining in deck.");
            }
        }

        private static void EnsureEndTurnEligibility(Phase currentPhase)
        {
            if (currentPhase != Phase.Reaction && currentPhase != Phase.End)
            {
                throw new InvalidOperationException("Turns can only end after reaching the Reaction or End phase.");
            }
        }
    }

    /// <summary>
    /// Interprets EffectDefinitions against the current BattleState.
    /// </summary>
    public class EffectResolver
    {
        public void ResolvePendingEffects(BattleState state, DeterministicRng rng)
        {
            while (state.Effects.Count > 0)
            {
                var next = state.Effects.Pop();
                ResolveEffect(state, next, rng);
            }
        }

        public void ResolveEffect(BattleState state, EffectReference effect, DeterministicRng rng)
        {
            // Effect resolution is intentionally data-driven; implementations live here, not on Unity prefabs.
        }

        public void ResolvePhaseTriggers(BattleState state, Phase phase)
        {
            // Placeholder for automatic triggers (start/end of phases) as defined by authored effects.
        }

        public void ResolveTurnStart(BattleState state)
        {
            // Placeholder for start-of-turn triggers and status effect ticks.
            var activePlayer = state.GetPlayer(state.Turn.ActivePlayer);
            activePlayer.Resource.Gain(GameRulesConfig.BaseResourceGainPerTurn);
            state.Turn.SetActions(GameRulesConfig.BaseActionsPerTurn);

            for (var i = 0; i < GameRulesConfig.CardsDrawnPerTurn; i++)
            {
                var drawnCard = activePlayer.Deck.DrawTopCard();
                activePlayer.Hand.Add(drawnCard);
            }

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
        private readonly int _seed;
        private readonly BattleState _state;

        public DeterministicRng(BattleState state)
        {
            _seed = state.RandomSeed;
            _state = state;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            var randomValue = NextRaw();
            return minInclusive + Math.Abs(randomValue % (maxExclusive - minInclusive));
        }

        public double NextDouble()
        {
            var randomValue = NextRaw();
            return (double)Math.Abs(randomValue % int.MaxValue) / int.MaxValue;
        }

        private int NextRaw()
        {
            var random = new Random(HashCode.Combine(_seed, _state.RandomCallCount));
            _state.IncrementRandomCallCount();
            return random.Next();
        }
    }

    /// <summary>
    /// Factory for constructing initial battle states from authored content.
    /// </summary>
    public static class BattleStateFactory
    {
        public static BattleState Create(IReadOnlyList<CardTemplate> playerOneDeck, IReadOnlyList<CardTemplate> playerTwoDeck, int seed)
        {
            var registry = new CardRegistry(playerOneDeck.Concat(playerTwoDeck));
            var playerStates = new List<PlayerState>
            {
                BuildPlayer(new PlayerId(0), playerOneDeck),
                BuildPlayer(new PlayerId(1), playerTwoDeck)
            };

            var turn = new TurnState(new PlayerId(0));
            var effects = new EffectStack();
            var context = new MatchContext(registry);

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
