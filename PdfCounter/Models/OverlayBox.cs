using System.Drawing;
using ReactiveUI;

public class OverlayBox : ReactiveObject
{
    string _label = "";
    string _borderBoxColor = "Red";
    double _leftPx, _topPx, _widthPx, _heightPx;
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

     public string BorderBoxColor
    {
        get => _borderBoxColor;
        set => this.RaiseAndSetIfChanged(ref _borderBoxColor, value);
    }

    public double LeftPx
    {
        get => _leftPx;
        set => this.RaiseAndSetIfChanged(ref _leftPx, value);
    }

    public double TopPx
    {
        get => _topPx;
        set => this.RaiseAndSetIfChanged(ref _topPx, value);
    }

    public double WidthPx
    {
        get => _widthPx;
        set => this.RaiseAndSetIfChanged(ref _widthPx, value);
    }

    public double HeightPx
    {
        get => _heightPx;
        set => this.RaiseAndSetIfChanged(ref _heightPx, value);
    }
}
