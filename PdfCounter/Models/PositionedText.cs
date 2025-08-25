public sealed class PositionedText
{
    public string Text { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }

    public double Top { get; set; }
    public double Bottom { get; set; }
    public double Height { get; set; }
    public bool IsHighlighted { get; set; }     
}