using System.Collections.ObjectModel;
using System.Text.Json;

namespace AiExamBank.Modules.Review.Domain;

/// <summary>
/// Validates untrusted model output before the question writer receives it.
/// Provider metadata, identity, revision state and citations must come from trusted application code.
/// </summary>
public static class AiDraftSafetyGate
{
    public const string PolicyVersion = "m3-ai-draft-v1";
    private static readonly HashSet<string> AllowedFields = ["stem", "options", "correctOption", "explanation"];
    private static readonly string[] OptionLabels = ["A", "B", "C", "D"];

    public static AiDraftSafetyResult Parse(string? modelJson)
    {
        if (string.IsNullOrWhiteSpace(modelJson) || modelJson.Length > 16_384)
            return Invalid("Model response is empty or too large.");

        try
        {
            using var document = JsonDocument.Parse(modelJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasOnlyFields(root, AllowedFields))
                return Invalid("Model response contains unsupported fields.");

            if (!TryText(root, "stem", 4_000, out var stem) ||
                !TryText(root, "correctOption", 1, out var correctOption) ||
                !OptionLabels.Contains(correctOption, StringComparer.Ordinal))
                return Invalid("Question stem or answer is invalid.");

            if (!root.TryGetProperty("options", out var optionObject) ||
                optionObject.ValueKind != JsonValueKind.Object ||
                !HasOnlyFields(optionObject, OptionLabels))
                return Invalid("Options must contain exactly A, B, C and D.");

            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var label in OptionLabels)
            {
                if (!TryText(optionObject, label, 1_000, out var option))
                    return Invalid("An option is missing or invalid.");
                options.Add(label, option);
            }

            if (options.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                return Invalid("Options must be distinct.");

            string? explanation = null;
            if (root.TryGetProperty("explanation", out _))
            {
                if (!TryText(root, "explanation", 2_000, out var value))
                    return Invalid("Explanation is invalid.");
                explanation = value;
            }

            return new(new AiDraftContent(stem, new ReadOnlyDictionary<string, string>(options), correctOption, explanation,
                RevisionReviewState.Draft), null);
        }
        catch (JsonException)
        {
            return Invalid("Model response is not valid JSON.");
        }
    }

    private static bool HasOnlyFields(JsonElement element, IEnumerable<string> allowed)
    {
        var expected = new HashSet<string>(allowed, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name) || !seen.Add(property.Name))
                return false;
        }
        return true;
    }

    private static bool TryText(JsonElement element, string name, int maxLength, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        var candidate = property.GetString()?.Trim();
        if (string.IsNullOrEmpty(candidate) || candidate.Length > maxLength ||
            candidate.Any(char.IsControl))
            return false;
        value = candidate;
        return true;
    }

    private static AiDraftSafetyResult Invalid(string error) => new(null, error);
}

public sealed record AiDraftContent(
    string Stem,
    IReadOnlyDictionary<string, string> Options,
    string CorrectOption,
    string? Explanation,
    RevisionReviewState State);

public sealed record AiDraftSafetyResult(AiDraftContent? Draft, string? Error)
{
    public bool Succeeded => Draft is not null;
    public string PolicyVersion => AiDraftSafetyGate.PolicyVersion;
    public string Action => Succeeded ? "REVIEW_REQUIRED" : "BLOCK";
}
