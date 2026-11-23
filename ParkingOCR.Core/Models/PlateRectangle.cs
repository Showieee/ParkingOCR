namespace ParkingOCR.Core.Models;

/// <summary>
/// Represents a bounding box in an image.
/// </summary>
public record PlateRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int Area => Width * Height;
    public double AspectRatio => Height > 0 ? (double)Width / Height : 0;
}
