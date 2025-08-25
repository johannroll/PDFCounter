public class PdfProperty
{
    public int DocNo { get; set; }
    public int DocStartingPage { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DocPages { get; set; }
    public int DocBlankPages { get; set; }
    public string Fonts { get; set; } = string.Empty;
}