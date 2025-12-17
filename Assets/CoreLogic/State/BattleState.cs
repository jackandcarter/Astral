using System;
using System.Collections.Generic;
using System.Linq;
using Astral.GameLogic.Cards;

namespace Astral.GameLogic.State
{
    public enum Phase
    {
        Start,
        Main,
        Reaction,
        End
    }

    [Serializable]
    public struct PlayerId : IEquatable<PlayerId>
    {
        public int Value { get; }

        public PlayerId(int value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    [Serializable]
    public class TurnState : ICloneable
    {
        public int TurnNumber { get; private set; }
        public PlayerId ActivePlayer { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int ActionsRemaining { get; private set; }

        public TurnState(PlayerId activePlayer)
        {
            TurnNumber = 1;
            ActivePlayer = activePlayer;
            CurrentPhase = Phase.Start;
            ActionsRemaining = 0;
        }

        private TurnState(TurnState source)
        {
            TurnNumber = source.TurnNumber;
            ActivePlayer = source.ActivePlayer;
            CurrentPhase = source.CurrentPhase;
            ActionsRemaining = source.ActionsRemaining;
        }

        public void SetActions(int count)
        {
            ActionsRemaining = count;
        }

        public void AdvancePhase(Phase nextPhase)
        {
            CurrentPhase = nextPhase;
        }

        public void AdvanceTurn(PlayerId nextActivePlayer)
        {
            TurnNumber++;
            ActivePlayer = nextActivePlayer;
            CurrentPhase = Phase.Start;
            ActionsRemaining = 0;
        }

        public object Clone() => new TurnState(this);
    }

    [Serializable]
    public class BattleState : ICloneable
    {
        public List<PlayerState> Players { get; }
        public TurnState Turn { get; }
        public EffectStack Effects { get; }
        public MatchContext Context { get; }
        public int RandomSeed { get; }
        public int RandomCallCount { get; private set; }
        public int ActionCounter { get; private set; }

        public BattleState(
            IEnumerable<PlayerState> players,
            TurnState turn,
            EffectStack effects,
            MatchContext context,
            int randomSeed,
            int randomCallCount = 0)
        {
            Players = players.Select(p => (PlayerState)p.Clone()).ToList();
            Turn = (TurnState)turn.Clone();
            Effects = (EffectStack)effects.Clone();
            Context = (MatchContext)context.Clone();
            RandomSeed = randomSeed;
            RandomCallCount = randomCallCount;
        }

        private BattleState(BattleState source)
        {
            Players = source.Players.Select(p => (PlayerState)p.Clone()).ToList();
            Turn = (TurnState)source.Turn.Clone();
            Effects = (EffectStack)source.Effects.Clone();
            Context = (MatchContext)source.Context.Clone();
            RandomSeed = source.RandomSeed;
            RandomCallCount = source.RandomCallCount;
            ActionCounter = source.ActionCounter;
        }

        public void IncrementActionCounter()
        {
            ActionCounter++;
        }

        public void IncrementRandomCallCount()
        {
            RandomCallCount++;
        }

        public PlayerState GetPlayer(PlayerId playerId)
        {
            var player = Players.FirstOrDefault(p => p.Id.Equals(playerId));
            if (player == null)
            {
                throw new InvalidOperationException($"Player with id {playerId} does not exist in this battle state.");
            }

            return player;
        }

        public PlayerState GetOpponent(PlayerId playerId)
        {
            var opponent = Players.FirstOrDefault(p => !p.Id.Equals(playerId));
            if (opponent == null)
            {
                throw new InvalidOperationException("Battle state does not contain an opponent.");
            }

            return opponent;
        }

        public object Clone()
        {
            return new BattleState(this);
        }
    }

    [Serializable]
    public class PlayerState : ICloneable
    {
        public PlayerId Id { get; }
        public int Health { get; set; }
        public DeckState Deck { get; }
        public HandState Hand { get; }
        public DiscardState Discard { get; }
        public BoardState Board { get; }
        public ResourceState Resource { get; }
        public List<StatusEffect> StatusEffects { get; }

        public PlayerState(PlayerId id, int startingHealth, IEnumerable<CardInstance> deck, int startingResources)
        {
            Id = id;
            Health = startingHealth;
            Deck = new DeckState(deck);
            Hand = new HandState();
            Discard = new DiscardState();
            Board = new BoardState();
            Resource = new ResourceState(startingResources);
            StatusEffects = new List<StatusEffect>();
        }

        private PlayerState(PlayerState source)
        {
            Id = source.Id;
            Health = source.Health;
            Deck = (DeckState)source.Deck.Clone();
            Hand = (HandState)source.Hand.Clone();
            Discard = (DiscardState)source.Discard.Clone();
            Board = (BoardState)source.Board.Clone();
            Resource = (ResourceState)source.Resource.Clone();
            StatusEffects = source.StatusEffects.Select(s => s.Clone()).ToList();
        }

        public object Clone()
        {
            return new PlayerState(this);
        }
    }

    [Serializable]
    public class DeckState : ICloneable
    {
        public List<CardInstance> Cards { get; }

        public DeckState(IEnumerable<CardInstance> cards)
        {
            Cards = cards.Select(card => (CardInstance)card.Clone()).ToList();
        }

        private DeckState(DeckState source)
        {
            Cards = source.Cards.Select(card => (CardInstance)card.Clone()).ToList();
        }

        public CardInstance DrawTopCard()
        {
            if (Cards.Count == 0)
            {
                throw new InvalidOperationException("Cannot draw from an empty deck.");
            }

            var card = Cards[0];
            Cards.RemoveAt(0);
            return card;
        }

        public object Clone() => new DeckState(this);
    }

    [Serializable]
    public class HandState : ICloneable
    {
        public List<CardInstance> Cards { get; }

        public HandState()
        {
            Cards = new List<CardInstance>();
        }

        private HandState(HandState source)
        {
            Cards = source.Cards.Select(card => (CardInstance)card.Clone()).ToList();
        }

        public void Add(CardInstance card)
        {
            Cards.Add(card);
        }

        public void Remove(CardInstance card)
        {
            Cards.Remove(card);
        }

        public object Clone() => new HandState(this);
    }

    [Serializable]
    public class DiscardState : ICloneable
    {
        public List<CardInstance> Cards { get; }

        public DiscardState()
        {
            Cards = new List<CardInstance>();
        }

        private DiscardState(DiscardState source)
        {
            Cards = source.Cards.Select(card => (CardInstance)card.Clone()).ToList();
        }

        public void Add(CardInstance card)
        {
            Cards.Add(card);
        }

        public object Clone() => new DiscardState(this);
    }

    [Serializable]
    public class BoardState : ICloneable
    {
        public List<CardInstance> Cards { get; }

        public BoardState()
        {
            Cards = new List<CardInstance>();
        }

        private BoardState(BoardState source)
        {
            Cards = source.Cards.Select(card => (CardInstance)card.Clone()).ToList();
        }

        public void Play(CardInstance card)
        {
            Cards.Add(card);
        }

        public void Remove(CardInstance card)
        {
            Cards.Remove(card);
        }

        public object Clone() => new BoardState(this);
    }

    [Serializable]
    public class ResourceState : ICloneable
    {
        public int Current { get; private set; }

        public ResourceState(int startingAmount)
        {
            Current = startingAmount;
        }

        private ResourceState(ResourceState source)
        {
            Current = source.Current;
        }

        public void Spend(int amount)
        {
            if (amount > Current)
            {
                throw new InvalidOperationException("Cannot spend more resources than available.");
            }

            Current -= amount;
        }

        public void Gain(int amount)
        {
            Current += amount;
        }

        public object Clone() => new ResourceState(this);
    }

    [Serializable]
    public class StatusEffect
    {
        public string Id { get; }
        public string Source { get; }
        public int RemainingDuration { get; private set; }

        public StatusEffect(string id, string source, int remainingDuration)
        {
            Id = id;
            Source = source;
            RemainingDuration = remainingDuration;
        }

        public void TickDuration()
        {
            if (RemainingDuration > 0)
            {
                RemainingDuration--;
            }
        }

        public StatusEffect Clone()
        {
            return new StatusEffect(Id, Source, RemainingDuration);
        }
    }

    [Serializable]
    public class EffectStack : ICloneable
    {
        private readonly Stack<EffectReference> _stack;

        public EffectStack()
        {
            _stack = new Stack<EffectReference>();
        }

        private EffectStack(EffectStack source)
        {
            _stack = new Stack<EffectReference>(source._stack.Reverse());
        }

        public void Push(EffectReference effect)
        {
            _stack.Push(effect);
        }

        public EffectReference Pop()
        {
            return _stack.Pop();
        }

        public EffectReference Peek()
        {
            return _stack.Peek();
        }

        public int Count => _stack.Count;

        public object Clone()
        {
            return new EffectStack(this);
        }
    }

    [Serializable]
    public class EffectReference
    {
        public PlayerId Owner { get; }
        public CardInstance SourceCard { get; }
        public string EffectId { get; }

        public EffectReference(PlayerId owner, CardInstance sourceCard, string effectId)
        {
            Owner = owner;
            SourceCard = sourceCard;
            EffectId = effectId;
        }
    }

    [Serializable]
    public class MatchContext : ICloneable
    {
        public IReadOnlyDictionary<string, int> RuleModifiers { get; }
        public CardRegistry CardRegistry { get; }

        public MatchContext(CardRegistry cardRegistry, IReadOnlyDictionary<string, int> ruleModifiers = null)
        {
            CardRegistry = (CardRegistry)cardRegistry.Clone();
            RuleModifiers = ruleModifiers ?? new Dictionary<string, int>();
        }

        private MatchContext(MatchContext source)
        {
            RuleModifiers = new Dictionary<string, int>(source.RuleModifiers);
            CardRegistry = (CardRegistry)source.CardRegistry.Clone();
        }

        public object Clone()
        {
            return new MatchContext(this);
        }
    }
}
