using System;
using System.Linq;
using Astral.GameLogic.Cards;
using Astral.GameLogic.State;

namespace Astral.GameLogic.Engine
{
    public interface IPlayerAction : IGameAction
    {
        PlayerId Player { get; }
    }

    public class DrawCardAction : IPlayerAction
    {
        public PlayerId Player { get; }
        public int Count { get; }

        public DrawCardAction(PlayerId player, int count = 1)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count must be positive.");
            }

            Player = player;
            Count = count;
        }

        public void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver, TurnManager turnManager)
        {
            var playerState = state.GetPlayer(Player);
            for (var i = 0; i < Count; i++)
            {
                var card = playerState.Deck.DrawTopCard();
                playerState.Hand.Add(card);
            }
        }
    }

    public class PlayCardAction : IPlayerAction
    {
        public PlayerId Player { get; }
        public Guid CardInstanceId { get; }

        public PlayCardAction(PlayerId player, Guid cardInstanceId)
        {
            Player = player;
            CardInstanceId = cardInstanceId;
        }

        public void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver, TurnManager turnManager)
        {
            var playerState = state.GetPlayer(Player);
            var card = playerState.Hand.Cards.FirstOrDefault(c => c.InstanceId == CardInstanceId);
            if (card == null)
            {
                throw new InvalidOperationException($"Card {CardInstanceId} not found in player {Player}'s hand.");
            }

            var template = card.ResolveTemplate(state.Context.CardRegistry);

            playerState.Resource.Spend(template.Cost);
            playerState.Hand.Remove(card);
            playerState.Board.Play(card);

            foreach (var effect in template.BaseEffects)
            {
                var reference = new EffectReference(Player, card, effect.Id);
                state.Effects.Push(reference);
            }
        }
    }

    public class AdvancePhaseAction : IPlayerAction
    {
        public PlayerId Player { get; }
        public Phase NextPhase { get; }

        public AdvancePhaseAction(PlayerId player, Phase nextPhase)
        {
            Player = player;
            NextPhase = nextPhase;
        }

        public void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver, TurnManager turnManager)
        {
            turnManager.AdvancePhase(state, NextPhase);
        }
    }

    public class EndTurnAction : IGameAction
    {
        public void Resolve(BattleState state, DeterministicRng rng, EffectResolver resolver, TurnManager turnManager)
        {
            if (state.Turn.CurrentPhase != Phase.End)
            {
                turnManager.AdvancePhase(state, Phase.End);
            }
            turnManager.AdvanceTurn(state);
        }
    }
}
