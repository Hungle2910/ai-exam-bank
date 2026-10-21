namespace AiExamBank.Modules.Knowledge.Contracts;

/// <summary>Traceable source location for a generated draft, before human review.</summary>
public sealed record SourceCitation
{
    private SourceCitation(string sourceId, string locator)
    {
        SourceId = sourceId;
        Locator = locator;
    }

    public string SourceId { get; }
    public string Locator { get; }

    public static SourceCitation Create(string sourceId, string locator)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source ID is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(locator)) throw new ArgumentException("A locator is required.", nameof(locator));
        return new SourceCitation(sourceId.Trim(), locator.Trim());
    }
}
