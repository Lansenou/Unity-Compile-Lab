namespace Ucl.Core.Graph;

/// <summary>Cycle detection and deterministic topological ordering of assembly references.</summary>
public static class GraphOrdering
{
    /// <summary>Names of every node on a cycle (Tarjan strongly connected components of size above one, or self loops).</summary>
    public static IReadOnlySet<string> FindCycles(IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string v)
        {
            indices[v] = low[v] = index++;
            stack.Push(v);
            onStack.Add(v);
            foreach (var w in edges.GetValueOrDefault(v) ?? [])
            {
                if (!edges.ContainsKey(w))
                {
                    continue;
                }

                if (!indices.ContainsKey(w))
                {
                    Visit(w);
                    low[v] = Math.Min(low[v], low[w]);
                }
                else if (onStack.Contains(w))
                {
                    low[v] = Math.Min(low[v], indices[w]);
                }
            }

            if (low[v] == indices[v])
            {
                var component = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    component.Add(w);
                }
                while (w != v);

                if (component.Count > 1 || (edges.GetValueOrDefault(v) ?? []).Contains(v))
                {
                    result.UnionWith(component);
                }
            }
        }

        foreach (var node in edges.Keys.Order(StringComparer.Ordinal))
        {
            if (!indices.ContainsKey(node))
            {
                Visit(node);
            }
        }

        return result;
    }

    /// <summary>Kahn's algorithm with ordinal tie-breaking. Edges point from an assembly to its dependencies. Input must be acyclic.</summary>
    public static IReadOnlyList<string> TopologicalOrder(IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var pending = edges.ToDictionary(e => e.Key, e => e.Value.Count(edges.ContainsKey), StringComparer.Ordinal);
        var dependents = edges.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (node, deps) in edges)
        {
            foreach (var d in deps.Where(edges.ContainsKey))
            {
                dependents[d].Add(node);
            }
        }

        var ready = new SortedSet<string>(pending.Where(p => p.Value == 0).Select(p => p.Key), StringComparer.Ordinal);
        var order = new List<string>();
        while (ready.Count > 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            order.Add(next);
            foreach (var dependent in dependents[next])
            {
                if (--pending[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        return order;
    }
}
