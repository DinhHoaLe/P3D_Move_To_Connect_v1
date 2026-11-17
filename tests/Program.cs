using System;
using System.Collections.Generic;
using PlanCad_MoveToConnect_ver1;

public static class Program
{
    public static void Main()
    {
        // A tee splits into two branches; one branch loops back through a connector.
        var graph = new Dictionary<int, int[]> {
            [1] = new[] { 2 }, [2] = new[] { 1, 3 }, [3] = new[] { 2, 4, 5 },
            [4] = new[] { 3, 6 }, [5] = new[] { 3, 6 }, [6] = new[] { 4, 5 },
            [99] = new int[0] };
        var visits = new Dictionary<int, int>();
        var result = GraphWalk.Component(1, n => {
            visits[n] = visits.ContainsKey(n) ? visits[n] + 1 : 1;
            return graph[n];
        }, 6);
        Check(new HashSet<int>(result).SetEquals(new[] { 1, 2, 3, 4, 5, 6 }), "branches, connectors and closed loop included; other cluster excluded");
        foreach (var visit in visits.Values) if (visit != 1) throw new Exception("Node visited twice");
        Check(result.Count == 6, "no double transform of cyclic nodes");
        Check(GraphWalk.Component(99, n => graph[n], 1).Count == 1, "isolated pipe");
        Throws(() => GraphWalk.Component(1, n => graph[n], 5), "oversized cluster rejected, not truncated");
        Throws(() => GraphWalk.Component(1, n => n == 4 ? throw new InvalidOperationException("equipment boundary") : graph[n], 100), "blocked boundary aborts entire traversal");
        Console.WriteLine("All 5 graph checks passed.");
    }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static void Throws(Action action, string text)
    {
        try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS: " + text); return; }
        throw new Exception("Expected failure: " + text);
    }
}
