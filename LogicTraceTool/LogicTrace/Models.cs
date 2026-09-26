using System.Text.Json.Serialization;

namespace LogicTrace;

public sealed class LogicTraceDocument
{
    public string SchemaVersion { get; set; } = "2.0";
    public Feature Feature { get; set; } = new();
    public List<LogicSection> LogicSections { get; set; } = [];
    public List<UnresolvedItem> UnresolvedItems { get; set; } = [];
}

public sealed class Feature
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public Overview Overview { get; set; } = new();
    public EntryPoint EntryPoint { get; set; } = new();
    public List<string> MainFlow { get; set; } = [];
    public List<string> Components { get; set; } = [];
    public FeatureMetrics Metrics { get; set; } = new();
}

public sealed class Overview
{
    public string Text { get; set; } = "";
    public List<OverviewReference> References { get; set; } = [];
}

public sealed class OverviewReference
{
    public string Text { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReferenceTargetType TargetType { get; set; }
    public string TargetId { get; set; } = "";
}

public sealed class EntryPoint
{
    public string FilePath { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
}

public sealed class FeatureMetrics
{
    public decimal AnalysisCoverage { get; set; }
    public decimal EvidenceLocCoverage { get; set; }
    public int TotalEffectiveLoc { get; set; }
    public decimal UnresolvedWeight { get; set; }
}

public sealed class LogicSection
{
    public string Id { get; set; } = "";
    public int Sequence { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public SectionMetrics Metrics { get; set; } = new();
    public List<Rule> Rules { get; set; } = [];
    public override string ToString() => $"{Sequence}. {Title}";
}

public sealed class SectionMetrics
{
    public decimal LogicWeight { get; set; }
    public decimal CodeShare { get; set; }
    public decimal Coverage { get; set; }
    public int EffectiveLoc { get; set; }
}

public sealed class Rule
{
    public string Id { get; set; } = "";
    public int Sequence { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EvidenceLevel EvidenceLevel { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Confidence Confidence { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnalysisStatus AnalysisStatus { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerificationStatus VerificationStatus { get; set; }
    public List<Evidence> Evidences { get; set; } = [];
    public override string ToString() => $"{Sequence}. {Title}";
}

public sealed class Evidence
{
    public string Id { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Reason { get; set; } = "";
    public override string ToString() => $"{Id} — {FilePath}:{StartLine}-{EndLine}";
}

public sealed class UnresolvedItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal EstimatedWeight { get; set; }
    public string RelatedSectionId { get; set; } = "";
}

public enum ReferenceTargetType { LogicSection, Rule, Evidence }
public enum EvidenceLevel { Observed, Inferred, Unknown }
public enum Confidence { High, Medium, Low }
public enum AnalysisStatus { Confirmed, NeedsInvestigation }
public enum VerificationStatus { Unverified, Verified, Incorrect, NeedsReview }
