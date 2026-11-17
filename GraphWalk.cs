using System;
using System.Collections.Generic;

namespace PlanCad_MoveToConnect_ver1
{
    internal static class GraphWalk
    {
        // Visit every node once, including branches and closed loops. Exceptions from
        // the boundary validator propagate before the caller modifies the drawing.
        internal static List<T> Component<T>(T seed, Func<T, IEnumerable<T>> neighbours, int limit)
        {
            var seen = new HashSet<T>();
            var pending = new Queue<T>();
            var result = new List<T>();
            seen.Add(seed);
            pending.Enqueue(seed);
            while (pending.Count != 0)
            {
                if (result.Count >= limit) throw new InvalidOperationException("Cum vuot gioi han " + limit + " doi tuong.");
                var node = pending.Dequeue();
                result.Add(node);
                foreach (var next in neighbours(node)) if (seen.Add(next)) pending.Enqueue(next);
            }
            return result;
        }
    }
}
