using System.Runtime.CompilerServices;

namespace Netwolf.Server.Internal;

internal class NFA
{
    internal bool Compiled => Initial.Compiled > 0;

    private List<State> States { get; init; }

    private State Initial { get; set; }

    private HashSet<int> Accepting { get; set; } = new();

    internal NFA()
    {
        Initial = new State(0);
        States = new List<State>() { Initial };
    }

    internal int NewState()
    {
        if (Compiled)
        {
            throw new InvalidOperationException("NFA is already compiled");
        }

        States.Add(new State(States.Count));
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

        _ = Accepting.Add(state);
    }

    internal void Compile()
    {
        Initial.Compile();
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
            cur = Initial.Next(c);
        }

        return Accepting.Intersect(cur.Names).Any();
    }

    private class State
    {
        public static State Dead = new(-1) { Compiled = 2 };

        public HashSet<int> Names { get; init; } = new();

        public int Compiled { get; private set; } = 0;

        private List<State> EpsilonTransitions { get; set; } = new();

        private State? AnyCharacter { get; set; }

        private Dictionary<char, State> Transitions { get; init; } = new();

        public State(int name)
        {
            _ = Names.Add(name);
        }

        public State(params State?[] states)
        {
            EpsilonTransitions.AddRange(states.Where(s => s != null)!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public State Next(char c)
        {
            return Transitions.GetValueOrDefault(c) ?? AnyCharacter ?? Dead;
        }

        public void AddEpsilon(State next)
        {
            if (Compiled > 0)
            {
                throw new InvalidOperationException("State is already compiled");
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

            AnyCharacter = new State(AnyCharacter, next);
        }

        public void Add(char input, State next)
        {
            if (Compiled > 0)
            {
                throw new InvalidOperationException("State is already compiled");
            }

            Transitions[input] = new State(Transitions.GetValueOrDefault(input), next);
        }

        public void Compile(int to = 2)
        {
            if (Compiled < 1 && to >= 1)
            {
                // recursion prevention
                Compiled = 1;

                // roll any character transitions into other transition tables
                if (AnyCharacter != null)
                {
                    foreach (var (input, state) in Transitions)
                    {
                        Transitions[input] = new State(AnyCharacter, state);
                    }
                }

                // compute the epsilon closure for this state
                var closure = new HashSet<State>(EpsilonTransitions);
                var queue = new Queue<State>(closure);
                while (queue.Count > 0)
                {
                    var front = queue.Dequeue();
                    foreach (var state in front.EpsilonTransitions)
                    {
                        if (!closure.Contains(state) && state != this)
                        {
                            _ = closure.Add(state);
                            queue.Enqueue(state);
                        }
                    }
                }

                EpsilonTransitions = closure.ToList();
                Names.UnionWith(EpsilonTransitions.SelectMany(s => s.Names));
            }

            if (Compiled < 2 && to >= 2)
            {
                Compiled = 2;

                // roll epsilon transitions into the other transition tables
                foreach (var state in EpsilonTransitions)
                {
                    state.Compile(1);

                    foreach (var (input, next) in state.Transitions)
                    {
                        Transitions[input] = Transitions.TryGetValue(input, out var existing) ? new State(next, existing) : next;
                    }

                    AnyCharacter = new State(AnyCharacter, state);
                }

                EpsilonTransitions.Clear();
                AnyCharacter?.Compile();
                foreach (var (_, state) in Transitions)
                {
                    state.Compile();
                }
            }
        }
    }
}
