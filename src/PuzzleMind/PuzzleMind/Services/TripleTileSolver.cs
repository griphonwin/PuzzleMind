using static System.Net.WebRequestMethods;

namespace PuzzleMind.Services;

public class Tile
{
    public Guid Id { get; set; }
    public int Type { get; set; }
    public int Layer { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    // Конструктор для удобства
    public Tile(Guid id, int type, int layer, int x, int y)
    {
        Id = id;
        Type = type;
        Layer = layer;
        X = x;
        Y = y;
    }
}

public class GameState
{
    public List<Tile> Board { get; set; } = new();
    public List<Tile> Tray { get; set; } = new();
    public List<Tile> SolutionSteps { get; set; } = new();
}
public class TripleTileSolver
{
    private const int MaxTraySize = 7;
    private const int ComboSize = 3;
    private const int TileSize = 80; // Размер квадрата для проверки перекрытий

    public List<Tile>? Solve(List<Tile> board, List<Tile> initialTray)
    {
        Console.WriteLine($"[Solver] Начало поиска. Плиток на доске: {board.Count}, в лотке: {initialTray.Count}");

        var steps = new List<Tile>();
        // Используем HashSet для отслеживания посещенных состояний (мемоизация), 
        // чтобы не проверять одни и те же комбинации дважды
        var visited = new HashSet<string>();

        if (Backtrack(board, initialTray, steps, visited))
        {
            return steps;
        }

        return null;
    }

    private bool Backtrack(List<Tile> currentBoard, List<Tile> currentTray, List<Tile> steps, HashSet<string> visited)
    {
        // Условие победы
        if (currentBoard.Count == 0 && currentTray.Count == 0) return true;

        // Генерация ключа состояния для мемоизации
        var stateKey = GenerateStateKey(currentBoard, currentTray);
        if (visited.Contains(stateKey)) return false;
        visited.Add(stateKey);

        // Получаем плитки, которые не перекрыты ни одной плиткой на слое выше
        var accessibleTiles = GetAccessibleTiles(currentBoard);

        if (!accessibleTiles.Any() && currentBoard.Count > 0)
        {
            // Тупик: плитки есть, но все заблокированы (теоретически невозможно в этой игре, но для логики важно)
            return false;
        }

        // Эвристика: 
        // 1. Сначала берем те, что завершают тройку в лотке.
        // 2. Затем те, тип которых уже есть в лотке (создаем пару).
        // 3. Остальные.
        var sortedMoves = accessibleTiles.OrderByDescending(t =>
            currentTray.Count(trayTile => trayTile.Type == t.Type)).ToList();

        foreach (var tile in sortedMoves)
        {
            // Проверка: влезет ли плитка в лоток (учитываем, что если соберется 3, место освободится)
            var countInTray = currentTray.Count(t => t.Type == tile.Type);
            bool willCompleteCombo = (countInTray == ComboSize - 1);

            if (!willCompleteCombo && currentTray.Count >= MaxTraySize)
                continue;

            // Выполняем ход
            steps.Add(tile);

            var nextBoard = currentBoard.Where(t => t.Id != tile.Id).ToList();
            var nextTray = new List<Tile>(currentTray);

            if (willCompleteCombo)
            {
                // Удаляем тройку из лотка
                nextTray.RemoveAll(t => t.Type == tile.Type);
            }
            else
            {
                nextTray.Add(tile);
            }

            // Рекурсия
            if (Backtrack(nextBoard, nextTray, steps, visited)) return true;

            // Откат (Backtracking)
            steps.RemoveAt(steps.Count - 1);
        }

        return false;
    }

    private List<Tile> GetAccessibleTiles(List<Tile> board)
    {
        // Плитка доступна, если нет другой плитки на более ВЫСОКОМ слое, 
        // которая пересекается с ней по координатам X и Y.
        return board.Where(t1 => !board.Any(t2 =>
            t2.Layer > t1.Layer && IsOverlapping(t1, t2))).ToList();
    }

    private bool IsOverlapping(Tile a, Tile b)
    {
        // Проверка пересечения двух квадратов 80x80
        return a.X < b.X + TileSize &&
               a.X + TileSize > b.X &&
               a.Y < b.Y + TileSize &&
               a.Y + TileSize > b.Y;
    }

    private string GenerateStateKey(List<Tile> board, List<Tile> tray)
    {
        // Создаем уникальную строку состояния: ID оставшихся плиток + типы в лотке
        var bIds = board.Select(t => t.Id.ToString()).OrderBy(id => id);
        var tTypes = tray.Select(t => t.Type).OrderBy(t => t);
        return string.Join(",", bIds) + "|" + string.Join("-", tTypes);
    }
}