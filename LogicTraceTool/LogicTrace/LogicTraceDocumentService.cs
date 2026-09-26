using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicTrace;

public sealed record LoadResult(LogicTraceDocument? Document, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool Success => Document is not null && Errors.Count == 0;
}

public sealed class LogicTraceDocumentService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public LoadResult Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<LogicTraceDocument>(json, Options);
            if (document is null)
                return new(null, ["JSON 沒有可讀取的文件內容。"], []);
            var (errors, warnings) = Validate(document);
            return new(errors.Count == 0 ? document : null, errors, warnings);
        }
        catch (JsonException ex)
        {
            return new(null, [$"JSON 格式不合法（行 {ex.LineNumber + 1}, 位置 {ex.BytePositionInLine + 1}）：{ex.Message}"], []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(null, [$"無法讀取檔案：{ex.Message}"], []);
        }
    }

    public void Save(string path, LogicTraceDocument document)
    {
        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(path, json);
    }

    public static (List<string> Errors, List<string> Warnings) Validate(LogicTraceDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (document.SchemaVersion != "2.0") errors.Add($"不支援 Schema Version '{document.SchemaVersion}'，目前僅支援 2.0。");
        if (string.IsNullOrWhiteSpace(document.Feature.Name)) errors.Add("Feature Name 不可為空白。");

        ValidatePercent(document.Feature.Metrics.AnalysisCoverage, "Analysis Coverage", errors);
        ValidatePercent(document.Feature.Metrics.EvidenceLocCoverage, "Evidence LOC Coverage", errors);
        ValidatePercent(document.Feature.Metrics.UnresolvedWeight, "Unresolved Weight", errors);
        if (document.Feature.Metrics.TotalEffectiveLoc < 0) errors.Add("Total Effective LOC 不可為負數。");

        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in document.LogicSections)
        {
            if (string.IsNullOrWhiteSpace(section.Id) || !sectionIds.Add(section.Id)) errors.Add($"Logic Section Id 重複或空白：'{section.Id}'。");
            ValidatePercent(section.Metrics.LogicWeight, $"{section.Id} Logic Weight", errors);
            ValidatePercent(section.Metrics.CodeShare, $"{section.Id} Code Share", errors);
            ValidatePercent(section.Metrics.Coverage, $"{section.Id} Coverage", errors);
            if (section.Metrics.EffectiveLoc < 0) errors.Add($"{section.Id} Effective LOC 不可為負數。");
            foreach (var rule in section.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Id) || !ruleIds.Add(rule.Id)) errors.Add($"Rule Id 重複或空白：'{rule.Id}'。");
                foreach (var evidence in rule.Evidences)
                {
                    if (string.IsNullOrWhiteSpace(evidence.Id) || !evidenceIds.Add(evidence.Id)) errors.Add($"Evidence Id 重複或空白：'{evidence.Id}'。");
                    if (evidence.StartLine < 1 || evidence.EndLine < evidence.StartLine) errors.Add($"Evidence {evidence.Id} 行號範圍不合理。");
                }
            }
        }

        var weight = document.LogicSections.Sum(s => s.Metrics.LogicWeight);
        if (document.LogicSections.Count > 0 && Math.Abs(weight - 100m) > 0.01m) warnings.Add($"Logic Weight 加總為 {weight:0.##}%，預期為 100%。");
        var codeShare = document.LogicSections.Sum(s => s.Metrics.CodeShare);
        if (document.LogicSections.Count > 0 && Math.Abs(codeShare - 100m) > 1m) warnings.Add($"Code Share 加總為 {codeShare:0.##}%；若非 Shared Code，請確認資料。");

        foreach (var reference in document.Feature.Overview.References)
        {
            var exists = reference.TargetType switch
            {
                ReferenceTargetType.LogicSection => sectionIds.Contains(reference.TargetId),
                ReferenceTargetType.Rule => ruleIds.Contains(reference.TargetId),
                ReferenceTargetType.Evidence => evidenceIds.Contains(reference.TargetId),
                _ => false
            };
            if (!exists) errors.Add($"Overview Reference '{reference.Text}' 的 Target 不存在：{reference.TargetType} {reference.TargetId}。");
            if (string.IsNullOrWhiteSpace(reference.Text) || !document.Feature.Overview.Text.Contains(reference.Text, StringComparison.Ordinal))
                warnings.Add($"Overview 文字中找不到 Reference Text：'{reference.Text}'。");
        }

        foreach (var item in document.UnresolvedItems)
        {
            ValidatePercent(item.EstimatedWeight, $"Unresolved {item.Id} Estimated Weight", errors);
            if (!string.IsNullOrWhiteSpace(item.RelatedSectionId) && !sectionIds.Contains(item.RelatedSectionId))
                errors.Add($"Unresolved Item {item.Id} 的 Related Section 不存在：{item.RelatedSectionId}。");
        }
        return (errors, warnings.Distinct().ToList());
    }

    private static void ValidatePercent(decimal value, string name, List<string> errors)
    {
        if (value is < 0 or > 100) errors.Add($"{name} 必須介於 0 到 100。實際值：{value}。");
    }
}

public sealed record SourceReadResult(bool Success, string[] Lines, string? FullPath, string Error);

public static class SourceFileReader
{
    public static SourceReadResult Read(string projectRoot, Evidence evidence)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
                return new(false, [], null, "Project Root 不存在，請先選擇有效的資料夾。");
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, evidence.FilePath));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return new(false, [], fullPath, "Evidence FilePath 超出 Project Root，已拒絕讀取。");
            if (!File.Exists(fullPath)) return new(false, [], fullPath, $"找不到 Source File：{fullPath}");
            var lines = File.ReadAllLines(fullPath);
            if (evidence.StartLine < 1 || evidence.EndLine < evidence.StartLine || evidence.EndLine > lines.Length)
                return new(false, lines, fullPath, $"Evidence 行號 {evidence.StartLine}-{evidence.EndLine} 超出檔案範圍（共 {lines.Length} 行）。");
            return new(true, lines, fullPath, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(false, [], null, $"無法讀取 Source File：{ex.Message}");
        }
    }
}
