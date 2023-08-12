using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Netwolf.Server.Internal;

internal class NFA
{
    internal bool Compiled => Initial.Compiled > 0;

    private List<State> States { get; init; }

    private State Initial { get; set; }

    private State Dead { get; set; }

    internal NFA()
    {
        Initial = new State(this, 0);
        Dead = new State(this, -1);
        States = new List<State>() { Initial };
    }

    internal int NewState()
    {
        if (Compiled)
        {
            throw new InvalidOperationException("NFA is already compiled");
        }

        States.Add(new State(this, States.Count));
        return States.Count - 1;
    }

    internal void AddEpsilon(int from, int to)
    {
        if (Compiled)
        {
            throw new InvalidOperationException("NFA is already compiled");
        }

        States[from].AddEpsilon(States[to]);
    }

    internal void AddAny(int from, int to)
    {
        if (Compiled)
        {
            throw new InvalidOperationException("NFA is already compiled");
        }

        States[from].AddAny(States[to]);
    }

    internal void AddTransition(int from, char input, int to)
    {
        if (Compiled)
        {
            throw new InvalidOperationException("NFA is already compiled");
        }

        States[from].Add(input, States[to]);
    }

    internal void MarkAccepting(int state)
    {
        if (state < 0 || state >= States.Count)
        {
            throw new ArgumentException("Invalid state number", nameof(state));
        }

        States[state].Accepting = true;
    }

    internal void Compile()
    {
        // need to pre-compute the epsilon closure for every state,
        // but ultimately only the states reachable from the Initial state
        // need to be fully compiled
        foreach (var state in States)
        {
            state.Compile(1);
        }

        // Once compiled, the states used when building are no longer used.
        // Compiling to level 2 will add the actually-used states back to this.
        States.Clear();

        Initial.Compile();
        Dead.Compile();
    }

    internal bool Parse(string input)
    {
        if (!Compiled)
        {
            throw new InvalidOperationException("NFA must be compiled");
        }

        var cur = Initial;
        foreach (char c in input)
        {
            cur = cur.Next(c);
        }

        return cur.Accepting;
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class State
    {
        public HashSet<int> Names { get; init; } = new();

        public int Compiled { get; private set; } = 0;

        private bool _accepting;
        public bool Accepting
        {
            get => _accepting;
            set
            {
                if (Compiled > 0)
                {
                    throw new InvalidOperationException("State is already compiled");
                }

                _accepting = value;
            }
        }

        private NFA NFA { get; init; }

        private HashSet<State> EpsilonTransitions { get; set; } = new();

        private State? AnyCharacter { get; set; }

        private Dictionary<char, State> Transitions { get; init; } = new();

        private State? ProxyFor { get; set; }

        private bool InternalState { get; init; }

        private string DebuggerDisplay
        {
            get
            {
                if (this == NFA.Dead)
                {
                    return "Dead State";
                }

                return String.Format("State {{{0}}}{1}{2}",
                    String.Join(", ", Names.Order()),
                    this == NFA.Initial ? " Initial" : String.Empty,
                    Accepting ? " Accepting" : String.Empty);
            }
        }

        public State(NFA nfa, int name)
        {
            NFA = nfa;
            Names.Add(name);
            InternalState = false;
        }

        public State(NFA nfa, params State[] states)
        {
            NFA = nfa;
            EpsilonTransitions.UnionWith(states);
            InternalState = true;
        }

        public State Next(char c)
        {
            return Transitions.GetValueOrDefault(c) ?? AnyCharacter ?? NFA.Dead;
        }

        public void AddEpsilon(State next)
        {
            if (Compiled > 0)
            {
                throw new InvalidOperationException("State is already compiled");
            }

            if (next.InternalState)
            {
                throw new ArgumentException("Trying to add transition to an internal state");
            }

            if (next == this)
            {
                // pointless
                return;
            }

            EpsilonTransitions.Add(next);
        }

        public void AddAny(State next)
        {
            if (Compiled > 0)
            {
                throw new InvalidOperationException("State is already compiled");
            }

            if (next.InternalState)
            {
                throw new ArgumentException("Trying to add transition to an internal state");
            }

            if (InternalState)
            {
                throw new InvalidOperationException("Cannot add non-epsilon transitions to internal states");
            }

            if (AnyCharacter != null)
            {
                AnyCharacter.AddEpsilon(next);
            }
            else
            {
                AnyCharacter = new State(NFA, next);
            }
        }

        public void Add(char input, State next)
        {
            if (Compiled > 0)
            {
                throw new InvalidOperationException("State is already compiled");
            }

            if (next.InternalState)
            {
                throw new ArgumentException("Trying to add transition to an internal state");
            }

            if (InternalState)
            {
                throw new InvalidOperationException("Cannot add non-epsilon transitions to internal states");
            }

            if (Transitions.ContainsKey(input))
            {
                Transitions[input].AddEpsilon(next);
            }
            else
            {
                Transitions[input] = new State(NFA, next);
            }
        }

        private void AddEpsilonRange(IEnumerable<State> states)
        {
            foreach (var state in states)
            {
                AddEpsilon(state);
            }
        }

        internal void Compile(int to = 3)
        {
            if (Compiled < 1 && to >= 1)
            {
                // recursion prevention
                Compiled = 1;

                // compute the epsilon closure for this state
                var queue = new Queue<State>(EpsilonTransitions);
                while (queue.Count > 0)
                {
                    var front = queue.Dequeue();
                    foreach (var state in front.EpsilonTransitions)
                    {
                        if (!EpsilonTransitions.Contains(state) && state != this)
                        {
                            EpsilonTransitions.Add(state);
                            queue.Enqueue(state);
                        }
                    }
                }

                // kill any internal states we may have picked up in the closure calculation
                EpsilonTransitions = EpsilonTransitions.Where(s => !s.InternalState).ToHashSet();

                // update the name of this state with the epsilon closure
                Names.UnionWith(EpsilonTransitions.SelectMany(s => s.Names));

                // if any of the states reachable in the epsilon closure are accepting,
                // make this state an accepting state
                _accepting = _accepting || EpsilonTransitions.Any(s => s._accepting);
            }

            if (Compiled < 2 && to >= 2)
            {
                Compiled = 2;

                // check if we already exist in the State table (aka we're a duplicate state)
                // if so, mark that we're a proxy to the state that's already there
                if (NFA.States.FirstOrDefault(s => s.Names.SetEquals(Names)) is State dup)
                {
                    ProxyFor = dup;
                    dup.Compile(2);
                    return;
                }

                NFA.States.Add(this);

                // roll epsilon transitions into the other transition tables
                foreach (var state in EpsilonTransitions)
                {
                    foreach (var (input, next) in state.Transitions)
                    {
                        if (Transitions.ContainsKey(input))
                        {
                            Transitions[input].AddEpsilonRange(next.EpsilonTransitions);
                        }
                        else
                        {
                            Transitions[input] = new State(NFA, next.EpsilonTransitions.ToArray());
                        }
                    }

                    if (state.AnyCharacter != null)
                    {
                        if (AnyCharacter != null)
                        {
                            AnyCharacter.AddEpsilonRange(state.AnyCharacter.EpsilonTransitions);
                        }
                        else
                        {
                            AnyCharacter = new State(NFA, state.AnyCharacter.EpsilonTransitions.ToArray());
                        }
                    }
                }

                EpsilonTransitions.Clear();

                // roll any character transitions into other transition tables
                if (AnyCharacter != null)
                {
                    foreach (var state in Transitions.Values)
                    {
                        state.AddEpsilonRange(AnyCharacter.EpsilonTransitions);
                    }
                }

                // compile adjacent internal states
                foreach (var state in Transitions.Values)
                {
                    state.Compile(2);
                }

                AnyCharacter?.Compile(2);
            }

            if (Compiled < 3 && to >= 3)
            {
                Compiled = 3;
                foreach (var (input, state) in Transitions)
                {
                    var s = state;
                    while (s.ProxyFor != null)
                    {
                        s = s.ProxyFor;
                    }

                    Transitions[input] = s;
                    s.Compile(3);
                }

                if (AnyCharacter != null)
                {
                    while (AnyCharacter.ProxyFor != null)
                    {
                        AnyCharacter = AnyCharacter.ProxyFor;
                    }

                    AnyCharacter.Compile(3);
                }
            }
        }
    }
}
