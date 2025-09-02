
using System.Reactive.Linq;
using ReactiveUI;
namespace PdfCounter.Models
{
    public class ExtractField : ReactiveObject
    {
        string _name = "";
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }
        bool _isfirstPageIdentifier = false;
        public bool IsFirstPageIdentifier
        {
            get => _isfirstPageIdentifier;
            set => this.RaiseAndSetIfChanged(ref _isfirstPageIdentifier, value);
        } 
        bool _isInlineValue = false;
        public bool IsInlineValue
        {
            get => _isInlineValue;
            set => this.RaiseAndSetIfChanged(ref _isInlineValue, value);
        } 
        string _matchValues = string.Empty;
        public string MatchValues
        {
            get => _matchValues;
            set => this.RaiseAndSetIfChanged(ref _matchValues, value);
        }
        bool IsMatchValuesTag => _matchValues.Length > 0 && !_isInlineValue;
        int _pageIndex;
        public int PageIndex
        {
            get => _pageIndex;
            set => this.RaiseAndSetIfChanged(ref _pageIndex, value);
        }
        // Coordinates (edit as needed: ints/doubles)
        double _x, _y, _width, _height;
        public double X { get => _x; set => this.RaiseAndSetIfChanged(ref _x, value); }
        public double Y { get => _y; set => this.RaiseAndSetIfChanged(ref _y, value); }
        public double Width { get => _width; set => this.RaiseAndSetIfChanged(ref _width, value); }
        public double Height { get => _height; set => this.RaiseAndSetIfChanged(ref _height, value); }
    }
}

