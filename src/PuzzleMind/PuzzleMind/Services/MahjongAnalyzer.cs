using OpenCvSharp;

namespace PuzzleMind.Services;

public class MahjongAnalyzer
{
    public AnalysisResult Analyze(byte[] imageData, List<TileTemplate> templates, MahjongGameConfig config)
    {
        using var src = Mat.FromImageData(imageData, ImreadModes.Color);
        var result = new AnalysisResult();

        if (src.Empty())
        {
            result.DebugInfo = "Ошибка: Изображение пустое.";
            return result;
        }

        // 1. Инфо о размере для лога
        result.DebugInfo = $"Размер: {src.Width}x{src.Height}. Шаблонов: {templates.Count}. ";

        // 2. Поиск плиток в пуле (внизу) - порог чуть ниже для надежности
        var poolTiles = DetectTilesInArea(src, templates, config.PoolRegion, 0.70);
        result.CurrentPoolCount = poolTiles.Count;

        // 3. Поиск плиток на поле (основная зона)
        var boardTiles = DetectTilesInArea(src, templates, config.BoardRegion, config.MatchThreshold);

        // --- ВИЗУАЛИЗАЦИЯ ЗОН ---
        Cv2.Rectangle(src, config.PoolRegion, Scalar.Blue, 2); // Синяя зона пула
        Cv2.Rectangle(src, config.BoardRegion, Scalar.Green, 2); // Зеленая зона поля

        // 4. Отрисовка того, что уже в пуле (Желтым)
        foreach (var t in poolTiles)
        {
            Cv2.Rectangle(src, t.Rect, Scalar.Yellow, 2);
            Cv2.PutText(src, t.Id, new Point(t.Rect.X, t.Rect.Y + 15), HersheyFonts.HersheyComplex, 0.4, Scalar.Yellow, 1);
        }

        // 5. ЛОГИКА ВЫБОРА ГЛАВНОГО ХОДА (Красный)
        result.TilesToClick = CalculateBestMoves(boardTiles, poolTiles, result.CurrentPoolCount);

        if (result.TilesToClick.Any())
        {
            foreach (var t in result.TilesToClick)
            {
                Cv2.Rectangle(src, t.Rect, Scalar.Red, 4); // Жирная красная рамка
                Cv2.Circle(src, t.Rect.X + t.Rect.Width / 2, t.Rect.Y + t.Rect.Height / 2, 6, Scalar.Red, -1);
            }
        }
        else if (result.CurrentPoolCount < 7)
        {
            // 6. ЕСЛИ ХОДОВ НЕТ — РИСУЕМ ПАРЫ РАЗНЫМИ ЦВЕТАМИ
            var pairColors = new List<Scalar> {
                new Scalar(255, 255, 0),   // Голубой (Cyan в BGR)
                new Scalar(255, 0, 255),   // Маджента
                new Scalar(0, 255, 255),   // Желтый
                new Scalar(0, 165, 255),   // Оранжевый
                new Scalar(200, 200, 200), // Серый
                new Scalar(180, 105, 255), // Розовый
                new Scalar(0, 255, 127),   // Салатовый
                new Scalar(255, 191, 0)    // Синий
            };

            var groups = boardTiles.GroupBy(t => t.Id).Where(g => g.Count() >= 2).ToList();
            int colorIndex = 0;

            foreach (var group in groups)
            {
                var color = pairColors[colorIndex % pairColors.Count];
                foreach (var tile in group)
                {
                    Cv2.Rectangle(src, tile.Rect, color, 2);
                    Cv2.PutText(src, "PAIR", new Point(tile.Rect.X, tile.Rect.Y - 5),
                                HersheyFonts.HersheyComplex, 0.4, color, 1);
                }
                colorIndex++; // Смена цвета для следующей пары
            }
        }

        result.ProcessedImage = src.ToBytes(".png");
        return result;
    }
    public AnalysisResult AnalyzeV1(byte[] imageData, List<TileTemplate> templates, MahjongGameConfig config)
    {
        // 1. Декодируем изображение
        using var src = Mat.FromImageData(imageData, ImreadModes.Color);
        var result = new AnalysisResult();

        if (src.Empty())
        {
            result.DebugInfo = "Ошибка: Изображение не загружено.";
            return result;
        }

        // 2. Логируем размер для отладки (выводится в ваш зеленый лог)
        result.DebugInfo = $"Размер: {src.Width}x{src.Height}. Шаблонов: {templates.Count}. ";

        // 3. Безопасно получаем области поиска (чтобы не было вылета ROI)
        using var boardRegion = GetSafeRegion(src, config.BoardRegion);
        using var poolRegion = GetSafeRegion(src, config.PoolRegion);

        // 4. Ищем плитки в ПУЛЕ (внизу)
        // Используем порог чуть ниже (0.75), так как плитки в пуле могут быть чуть другого масштаба
        var poolTiles = DetectTilesInArea(src, templates, config.PoolRegion, 0.8);
        result.CurrentPoolCount = poolTiles.Count;

        // 5. Ищем плитки на ИГРОВОМ ПОЛЕ (сверху)
        var boardTiles = DetectTilesInArea(src, templates, config.BoardRegion, config.MatchThreshold);

        // 6. РИСУЕМ ВИЗУАЛЬНУЮ ОТЛАДКУ
        // Синяя рамка — где программа ищет ПУЛ
        Cv2.Rectangle(src, config.PoolRegion, Scalar.Blue, 2);
        // Зеленая рамка — где программа ищет ИГРОВОЕ ПОЛЕ
        Cv2.Rectangle(src, config.BoardRegion, Scalar.Green, 2);

        // Желтые рамки — плитки, которые программа УЖЕ УЗНАЛА в пуле
        foreach (var t in poolTiles)
        {
            Cv2.Rectangle(src, t.Rect, Scalar.Yellow, 2);
        }

        // Зеленые маленькие рамки — плитки, узнанные на поле
        foreach (var t in boardTiles)
        {
            Cv2.Rectangle(src, t.Rect, new Scalar(0, 255, 0), 1);
        }

        // 7. ЛОГИКА ВЫБОРА ХОДА
        // Проверяем, есть ли место и что выгодно скинуть
        result.TilesToClick = CalculateBestMoves(boardTiles, poolTiles, result.CurrentPoolCount);

        // 8. РИСУЕМ РЕШЕНИЕ (КРАСНЫМ)
        foreach (var tile in result.TilesToClick)
        {
            // Жирная красная рамка для плиток, на которые надо нажать
            Cv2.Rectangle(src, tile.Rect, Scalar.Red, 4);
            // Добавляем маркер, чтобы было заметнее
            Cv2.Circle(src, tile.Rect.X + tile.Rect.Width / 2, tile.Rect.Y + tile.Rect.Height / 2, 5, Scalar.Red, -1);
        }

        // 9. Конвертируем результат в байты для отображения в Blazor
        result.ProcessedImage = src.ToBytes(".png");

        if (result.TilesToClick.Count > 0)
            result.DebugInfo += $"Найдено ходов: {result.TilesToClick.Count}.";
        else if (result.CurrentPoolCount >= 7)
            result.DebugInfo += "ПУЛ ПОЛЕН! Мест нет.";
        else
            result.DebugInfo += "Ходы не определены.";

        return result;
    }

    public AnalysisResult AnalyzeV2(byte[] imageData, List<TileTemplate> templates, MahjongGameConfig config)
    {
        using var src = Mat.FromImageData(imageData, ImreadModes.Color);
        var result = new AnalysisResult();

        var poolTiles = DetectTilesInArea(src, templates, config.PoolRegion, 0.8);
        result.CurrentPoolCount = poolTiles.Count;
        var boardTiles = DetectTilesInArea(src, templates, config.BoardRegion, 0.75);

        // 1. Ищем основные ходы (красные рамки)
        result.TilesToClick = CalculateBestMoves(boardTiles, poolTiles, result.CurrentPoolCount);

        // 2. Если основных ходов нет, ищем пары для подсказки
        var highlightedPairs = new List<DetectedTile>();
        if (!result.TilesToClick.Any())
        {
            highlightedPairs = boardTiles
                .GroupBy(t => t.Id)
                .Where(g => g.Count() >= 2) // Нашли 2 или более одинаковых
                .SelectMany(g => g.Take(2)) // Берем только две для подсветки
                .ToList();
        }

        // РИСОВАНИЕ
        // Пул (желтый)
        foreach (var t in poolTiles) Cv2.Rectangle(src, t.Rect, Scalar.Yellow, 2);

        // ПАРЫ (Синий цвет - подсказка, что эти плитки можно начать собирать)
        foreach (var t in highlightedPairs)
        {
            Cv2.Rectangle(src, t.Rect, Scalar.Blue, 2);
            Cv2.PutText(src, "PAIR", new Point(t.Rect.X, t.Rect.Y - 5), HersheyFonts.HersheyComplex, 0.4, Scalar.Blue, 1);
        }

        // ГЛАВНЫЙ ХОД (Красный)
        foreach (var t in result.TilesToClick) Cv2.Rectangle(src, t.Rect, Scalar.Red, 4);

        result.ProcessedImage = src.ToBytes(".png");
        return result;
    }

    // Вспомогательный метод для защиты от вылета за границы изображения
    private Mat GetSafeRegion(Mat src, Rect roi)
    {
        int x1 = Math.Max(0, Math.Min(roi.X, src.Cols - 1));
        int y1 = Math.Max(0, Math.Min(roi.Y, src.Rows - 1));
        int x2 = Math.Max(0, Math.Min(roi.X + roi.Width, src.Cols));
        int y2 = Math.Max(0, Math.Min(roi.Y + roi.Height, src.Rows));

        int width = Math.Max(0, x2 - x1);
        int height = Math.Max(0, y2 - y1);

        if (width == 0 || height == 0) return new Mat(1, 1, src.Type());
        return new Mat(src, new Rect(x1, y1, width, height));
    }

    // Вспомогательный метод поиска (должен быть внутри этого же класса)
    private List<DetectedTile> DetectTilesInArea(Mat src, List<TileTemplate> templates, Rect area, double threshold)
    {
        var found = new List<DetectedTile>();
        using var region = GetSafeRegion(src, area);
        if (region.Empty()) return found;

        foreach (var temp in templates)
        {
            using var res = new Mat();
            Cv2.MatchTemplate(region, temp.Mat, res, TemplateMatchModes.CCoeffNormed);

            while (true)
            {
                Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal < threshold) break;

                // Добавляем найденную плитку
                found.Add(new DetectedTile
                {
                    Id = temp.Id,
                    Rect = new Rect(maxLoc.X + area.X, maxLoc.Y + area.Y, temp.Mat.Width, temp.Mat.Height)
                });

                // "Закрашиваем" это место в результатах, чтобы не найти его снова
                Cv2.Circle(res, maxLoc.X, maxLoc.Y, temp.Mat.Width / 2, Scalar.Black, -1);
            }
        }
        return found;
    }


    private List<DetectedTile> CalculateBestMoves(List<DetectedTile> board, List<DetectedTile> pool, int poolSize)
    {
        var moves = new List<DetectedTile>();

        // 1. Ищем, что можно ДОБИТЬ в пуле (самый важный ход)
        var tilesInPool = pool.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Count());

        foreach (var item in tilesInPool.Where(x => x.Value < 3))
        {
            int needed = 3 - item.Value;
            var candidates = board.Where(b => b.Id == item.Key).Take(needed).ToList();

            // Если нашли нужное количество и они влезут в пул
            if (candidates.Count == needed && (poolSize + needed) <= 7)
            {
                return candidates; // Срочно забираем их!
            }
        }

        // 2. Если в пуле мало места (например, больше 4), НЕ начинаем новые тройки
        if (poolSize > 4) return moves;

        // 3. Ищем целую тройку на поле
        var fullTriple = board.GroupBy(x => x.Id).FirstOrDefault(g => g.Count() >= 3);
        if (fullTriple != null)
        {
            return fullTriple.Take(3).ToList();
        }

        return moves;
    }

    public void ExtractTemplates(byte[] imageData, List<TileTemplate> existingTemplates)
    {
        var config = new MahjongGameConfig();
        using var src = Mat.FromImageData(imageData, ImreadModes.Color);
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates");

        // Зоны для поиска новых плиток
        var zones = new[] { config.BoardRegion, config.PoolRegion };
        int newTilesCount = 0;

        foreach (var zone in zones)
        {
            using var region = GetSafeRegion(src, zone);
            if (region.Empty()) continue;

            using var gray = new Mat();
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Canny(gray, gray, 70, 200);

            Cv2.FindContours(gray, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                if (rect.Width > 50 && rect.Width < 100 && rect.Height > 50 && rect.Height < 100)
                {
                    using var tileMat = new Mat(region, rect);

                    // ПРОВЕРКА: Знаем ли мы эту плитку?
                    bool isKnown = false;
                    foreach (var temp in existingTemplates)
                    {
                        // ЗАЩИТА: Проверяем, что шаблон не больше вырезанного фрагмента
                        if (temp.Mat.Width > tileMat.Width || temp.Mat.Height > tileMat.Height)
                        {
                            continue; // Пропускаем, этот шаблон слишком велик для этого фрагмента
                        }

                        using var res = new Mat();
                        Cv2.MatchTemplate(tileMat, temp.Mat, res, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);

                        if (maxVal > 0.85)
                        {
                            isKnown = true;
                            break;
                        }
                    }

                    // Если плитка незнакомая — сохраняем
                    if (!isKnown)
                    {
                        string name = $"unknown_{Guid.NewGuid().ToString().Substring(0, 4)}.png";
                        tileMat.SaveImage(Path.Combine(templatePath, name));
                        newTilesCount++;
                    }
                }
            }
        }
    }

    public void AutoCollectTemplates(byte[] imageData, MahjongGameConfig config)
    {
        using var src = Mat.FromImageData(imageData, ImreadModes.Color);
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates");
        if (!Directory.Exists(templatePath)) Directory.CreateDirectory(templatePath);

        // Объединяем зоны поиска (поле + пул)
        var regions = new[] { config.BoardRegion, config.PoolRegion };

        foreach (var region in regions)
        {
            using var roi = new Mat(src, region);
            using var gray = new Mat();
            Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Canny(gray, gray, 50, 150); // Ищем границы плиток

            Cv2.FindContours(gray, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                // Фильтр по размеру (плитки на вашем скрине примерно 50-80 пикселей)
                if (rect.Width > 40 && rect.Width < 100 && rect.Height > 40 && rect.Height < 100)
                {
                    using var tile = new Mat(roi, rect);
                    string id = Guid.NewGuid().ToString().Substring(0, 4);
                    tile.SaveImage(Path.Combine(templatePath, $"auto_{id}.png"));
                }
            }
        }
    }
}

public class AnalysisResult
{
    public int CurrentPoolCount { get; set; }
    public List<DetectedTile> TilesToClick { get; set; } = new();
    public byte[]? ProcessedImage { get; set; }

    // Добавьте эту строку:
    public string DebugInfo { get; set; } = "";
}

public class DetectedTile
{
    // Идентификатор типа плитки (например, "bamboo_1", "dragon_red")
    public string Id { get; set; } = string.Empty;

    // Координаты на скриншоте для отрисовки рамки
    public Rect Rect { get; set; }
}

public class TileTemplate
{
    public string Id { get; set; } = string.Empty;

    // Объект OpenCV с изображением-эталоном
    public Mat Mat { get; set; } = new();
}
//public class MahjongGameConfig
//{
//    // Область игрового поля (где лежат все плитки)
//    // Координаты: X, Y, Ширина, Высота
//    // Эти значения нужно будет подогнать под разрешение вашего экрана/игры
//    public Rect BoardRegion { get; set; } = new Rect(0, 0, 1920, 800);

//    // Область пула внизу (где лежат те 7 плиток, что вы уже выбрали)
//    public Rect PoolRegion { get; set; } = new Rect(400, 850, 1100, 200);

//    // Порог точности распознавания (0.0 - 1.0)
//    // 0.85 — это хороший баланс, чтобы не путать похожие плитки
//    public double MatchThreshold { get; set; } = 0.85;
//}
public class MahjongGameConfig
{
    public Rect BoardRegion { get; set; } = new Rect(20, 100, 510, 600);
    public Rect PoolRegion { get; set; } = new Rect(25, 720, 500, 120); 
    public double MatchThreshold { get; set; } = 0.8;
}
