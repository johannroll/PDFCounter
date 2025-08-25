using PdfCounter.Models;

public enum FieldEditorResult { Cancel, Save, Remove }

public sealed class FieldEditorOutcome
{
    public FieldEditorResult Result { get; init; }
    public ExtractField? Edited { get; init; } // populated when Save
}
