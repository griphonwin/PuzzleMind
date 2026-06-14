namespace PuzzleMind.Services;

public class WaterSortState
{
    public List<List<string>> Tubes { get; set; } = new();
    public int Capacity { get; set; } = 4;

    public static WaterSortState CreateEmpty(int tubeCount, int capacity) => new()
    {
        Capacity = capacity,
        Tubes = [.. Enumerable.Range(0, tubeCount).Select(_ => new List<string>())]
    };

    public WaterSortState Clone() => new()
    {
        Capacity = Capacity,
        Tubes = [.. Tubes.Select(t => t.ToList())]
    };

    public string GetHash() => string.Join("|", Tubes.Select(t => string.Join(",", t)));
    public bool IsSolved()
    {
        return Tubes.All(t => t.Count == 0 || t.Distinct().Count() == 1);
    }
}

public class WaterSolver
{
    public async Task<List<WaterSortState>?> SolveAsync(WaterSortState initial)
    {
        var path = new List<WaterSortState>();
        var visited = new HashSet<string>();
        return DFS(initial, path, visited) ? path : null;
    }

    private bool DFS(WaterSortState current, List<WaterSortState> path, HashSet<string> visited)
    {
        var hash = current.GetHash();
        if (visited.Contains(hash)) return false;
        visited.Add(hash);
        path.Add(current);

        if (current.IsSolved()) return true;

        for (int i = 0; i < current.Tubes.Count; i++)
        {
            for (int j = 0; j < current.Tubes.Count; j++)
            {
                if (i == j || current.Tubes[i].Count == 0) continue;
                if (current.Tubes[j].Count < current.Capacity &&
                   (current.Tubes[j].Count == 0 || current.Tubes[i].Last() == current.Tubes[j].Last()))
                {
                    var next = current.Clone();
                    var color = next.Tubes[i].Last();
                    next.Tubes[i].RemoveAt(next.Tubes[i].Count - 1);
                    next.Tubes[j].Add(color);

                    if (DFS(next, path, visited)) return true;
                }
            }
        }
        path.RemoveAt(path.Count - 1);
        return false;
    }
}