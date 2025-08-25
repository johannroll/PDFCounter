namespace PdfCounter.Models
{
    public sealed class AddPropertyItem
    {
        public static readonly AddPropertyItem Instance = new();
        private AddPropertyItem() { }
        public override string ToString() => "Add property…";
    }
}