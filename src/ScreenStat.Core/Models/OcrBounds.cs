namespace ScreenStat.Core.Models;

public readonly record struct OcrBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double CenterX => Left + Width / 2;
    public double CenterY => Top + Height / 2;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public double VerticalOverlapRatio(OcrBounds other)
    {
        var overlap = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);
        var denominator = Math.Min(Height, other.Height);
        return overlap <= 0 || denominator <= 0 ? 0 : overlap / denominator;
    }
}
