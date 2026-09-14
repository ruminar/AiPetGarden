using System.Text.Json;
using AiPetGarden.Models;
using System.IO;
namespace AiPetGarden.Services;

/// <summary>
/// Reads Codex pet assets without creating, changing, or deleting their files.
/// Metadata variants are intentionally handled conservatively until adapters are added.
/// </summary>
public sealed class PetAssetLoader
{
    private static readonly HashSet<string> SpriteExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".webp", ".gif", ".jpg", ".jpeg" };

    public async Task<PetAssetScanResult> ScanAsync(string petRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(petRoot);
        return await Task.Run(() => Scan(petRoot, cancellationToken), cancellationToken);
    }

    private static PetAssetScanResult Scan(string petRoot, CancellationToken cancellationToken)
    {
        var assets = new List<PetAsset>();
        var issues = new List<PetAssetScanIssue>();

        if (!Directory.Exists(petRoot))
        {
            issues.Add(new PetAssetScanIssue(petRoot, "Codex pet directory was not found."));
            return new PetAssetScanResult(petRoot, assets, issues);
        }

        IEnumerable<string> metadataPaths;
        try
        {
            metadataPaths = Directory.EnumerateFiles(petRoot, "pet.json", SearchOption.AllDirectories).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new PetAssetScanIssue(petRoot, exception.Message));
            return new PetAssetScanResult(petRoot, assets, issues);
        }

        foreach (var metadataPath in metadataPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                assets.Add(LoadAsset(metadataPath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                // A broken asset must not prevent healthy pets from being discovered.
                issues.Add(new PetAssetScanIssue(metadataPath, exception.Message));
            }
        }

        return new PetAssetScanResult(petRoot, assets, issues);
    }

    private static PetAsset LoadAsset(string metadataPath)
    {
        using var stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("pet.json must contain a JSON object.");
        }

        var directoryPath = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidDataException("The asset directory could not be determined.");
        var assetId = GetFirstString(document.RootElement, "id", "petId", "assetId")
            ?? Path.GetFileName(directoryPath);
        var displayName = GetFirstString(document.RootElement, "displayName", "name", "label")
            ?? assetId;
        var spritePaths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SpriteExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var spriteDefinition = TryLoadSpriteDefinition(directoryPath);

        return new PetAsset(assetId, displayName, directoryPath, metadataPath, spritePaths, spriteDefinition);
    }

    private static PetSpriteDefinition? TryLoadSpriteDefinition(string directoryPath)
    {
        var manifestPath = Path.Combine(directoryPath, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var relativeSpritePath = GetRequiredString(root, "spritesheetPath");

        if (!root.TryGetProperty("spritesheetLayout", out var layout) || layout.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("manifest.json does not contain a spritesheetLayout object.");
        }

        if (!layout.TryGetProperty("neutralLookFrame", out var neutralFrame) || neutralFrame.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("manifest.json does not contain a neutralLookFrame object.");
        }

        var columns = GetRequiredPositiveInt(layout, "columns");
        var rows = GetRequiredPositiveInt(layout, "rows");
        var cellWidth = GetRequiredPositiveInt(layout, "cellWidth");
        var cellHeight = GetRequiredPositiveInt(layout, "cellHeight");
        var neutralRow = GetRequiredNonNegativeInt(neutralFrame, "rowIndex");
        var neutralColumn = GetRequiredNonNegativeInt(neutralFrame, "columnIndex");
        if (neutralRow >= rows || neutralColumn >= columns)
        {
            throw new InvalidDataException("neutralLookFrame lies outside the declared spritesheet grid.");
        }

        var spriteSheetPath = ResolveAssetPath(directoryPath, relativeSpritePath);
        if (!File.Exists(spriteSheetPath))
        {
            throw new InvalidDataException($"The spritesheet was not found: {relativeSpritePath}");
        }

        return new PetSpriteDefinition(
            spriteSheetPath,
            columns,
            rows,
            cellWidth,
            cellHeight,
            neutralRow,
            neutralColumn);
    }

    private static string ResolveAssetPath(string directoryPath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Asset paths must be relative to the pet directory.");
        }

        var directoryRoot = Path.GetFullPath(directoryPath) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(Path.Combine(directoryPath, relativePath));
        if (!resolvedPath.StartsWith(directoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("An asset path points outside the pet directory.");
        }

        return resolvedPath;
    }

    private static string GetRequiredString(JsonElement element, string name)
    {
        var value = GetFirstString(element, name);
        return value ?? throw new InvalidDataException($"{name} must be a non-empty string.");
    }

    private static int GetRequiredPositiveInt(JsonElement element, string name)
    {
        var value = GetRequiredNonNegativeInt(element, name);
        return value > 0 ? value : throw new InvalidDataException($"{name} must be greater than zero.");
    }

    private static int GetRequiredNonNegativeInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value) && value >= 0)
        {
            return value;
        }

        throw new InvalidDataException($"{name} must be a non-negative integer.");
    }

    private static string? GetFirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        return null;
    }
}
