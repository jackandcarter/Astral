using System;
using System.Collections.Generic;
using System.Linq;
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
            action.Resolve(State, Rng, EffectResolver, TurnManager);
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

        private const int BaseActionsPerTurn = 1;
        private const int CardsDrawnPerTurn = 1;
        private const int BaseResourceGainPerTurn = 1;

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

            if (playerState.Resource.Current < card.Template.Cost)
            {
                throw new InvalidOperationException($"Not enough resources to play {card.Template.Name}.");
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
        public void ResolveEffect(BattleState state, EffectReference effect, DeterministicRng rng)
        {
            // Effect resolution is intentionally data-driven; implementations live here, not on Unity prefabs.
            if (state.Effects.Count == 0)
            {
                throw new InvalidOperationException("Cannot resolve an effect when the stack is empty.");
            }

            state.Effects.Pop();
        }

        public void ResolvePhaseTriggers(BattleState state, Phase phase)
        {
            // Placeholder for automatic triggers (start/end of phases) as defined by authored effects.
        }

        public void ResolveTurnStart(BattleState state)
        {
            // Placeholder for start-of-turn triggers and status effect ticks.
            var activePlayer = state.GetPlayer(state.Turn.ActivePlayer);
            activePlayer.Resource.Gain(BaseResourceGainPerTurn);
            state.Turn.SetActions(BaseActionsPerTurn);

            for (var i = 0; i < CardsDrawnPerTurn; i++)
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
