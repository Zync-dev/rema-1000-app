using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rema.App.Data.Entities;

/// <summary>
/// En frihånds-streg, linje eller firkant på en gulvplan (fx butikkens vægge).
/// Gemmes som del af <see cref="FloorPlan.ShapesJson"/>.
/// </summary>
public sealed class FloorShape
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>"pen" (frihånd), "line" eller "rect".</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "pen";

    /// <summary>Hex-farve fra den tilladte palet.</summary>
    [JsonPropertyName("color")] public string Color { get; set; } = FloorShapes.DefaultColor;

    /// <summary>Stregtykkelse i planenheder.</summary>
    [JsonPropertyName("width")] public int Width { get; set; } = 4;

    /// <summary>Punkter [[x,y], …]. pen: mange; line/rect: præcis 2.</summary>
    [JsonPropertyName("points")] public List<double[]> Points { get; set; } = [];
}

/// <summary>Parsning, sanering og validering af gulvplanens former.</summary>
public static class FloorShapes
{
    public const string DefaultColor = "#1f2733";

    public static readonly string[] Palette =
        ["#1f2733", "#0a4d9c", "#e2231a", "#8b93a0", "#177245"];

    private const int MaxShapes = 120;
    private const int MaxPointsPerShape = 600;
    private const int CoordMax = 6000;

    private static readonly JsonSerializerOptions Opts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static List<FloorShape> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FloorShape>>(json, Opts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Renser klient-input: kendte typer, palet-farver, klampede koordinater.</summary>
    public static List<FloorShape> Sanitize(IEnumerable<FloorShape>? input)
    {
        var result = new List<FloorShape>();
        if (input is null) return result;

        foreach (var s in input)
        {
            if (result.Count >= MaxShapes) break;

            var kind = s.Kind?.ToLowerInvariant() switch
            {
                "line" => "line",
                "rect" => "rect",
                _ => "pen",
            };

            var pts = (s.Points ?? [])
                .Where(p => p is { Length: 2 })
                .Take(MaxPointsPerShape)
                .Select(p => new[]
                {
                    Math.Round(Math.Clamp(p[0], 0, CoordMax), 1),
                    Math.Round(Math.Clamp(p[1], 0, CoordMax), 1),
                })
                .ToList();

            if (kind is "line" or "rect")
                pts = pts.Take(2).ToList();

            if (pts.Count < 2) continue;

            result.Add(new FloorShape
            {
                Id = string.IsNullOrWhiteSpace(s.Id) ? Guid.NewGuid().ToString("N") : s.Id.Trim()[..Math.Min(s.Id.Trim().Length, 40)],
                Kind = kind,
                Color = Palette.Contains(s.Color) ? s.Color : DefaultColor,
                Width = Math.Clamp(s.Width, 1, 60),
                Points = pts,
            });
        }

        return result;
    }

    public static string? Serialize(List<FloorShape> shapes) =>
        shapes.Count == 0 ? null : JsonSerializer.Serialize(shapes, Opts);
}
