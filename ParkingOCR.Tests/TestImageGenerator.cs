using OpenCvSharp;

namespace ParkingOCR.Tests;

/// <summary>
/// Generates realistic synthetic Romanian and European license plates and vehicle crops for offline testing.
/// </summary>
public static class TestImageGenerator
{
    public static Mat CreatePlateImage(string plateText, bool includeEuroband = true, bool embedInBumper = false)
    {
        return CreateEuropeanPlateImage(plateText, includeEuroband ? "RO" : null, yellowBackground: false, isSwiss: false, embedInBumper: embedInBumper);
    }

    public static Mat CreateEuropeanPlateImage(
        string plateText,
        string? eurobandCountry = "RO",
        bool yellowBackground = false,
        bool isSwiss = false,
        bool embedInBumper = false)
    {
        int plateW = 420;
        int plateH = 90;

        Scalar bg = yellowBackground
            ? new Scalar(30, 215, 255) // British / Dutch yellow in BGR
            : Scalar.White;

        Mat plate = new(new Size(plateW, plateH), MatType.CV_8UC3, bg);

        // 1. Black outer border and frame
        Cv2.Rectangle(plate, new Rect(0, 0, plateW, plateH), Scalar.Black, 3);

        int textStartX = 25;

        // 2. Left side: Euroband or Swiss Shield
        if (isSwiss)
        {
            // Swiss plate: Red shield with white cross on the left (NO Euroband!)
            int shieldW = 32;
            int shieldH = 42;
            int sx = 12;
            int sy = (plateH - shieldH) / 2;

            // Red shield
            Cv2.Rectangle(plate, new Rect(sx, sy, shieldW, shieldH), new Scalar(20, 20, 215), -1); // Swiss red

            // White cross
            int cx = sx + shieldW / 2;
            int cy = sy + shieldH / 2;
            Cv2.Rectangle(plate, new Rect(cx - 3, cy - 12, 6, 24), Scalar.White, -1);
            Cv2.Rectangle(plate, new Rect(cx - 12, cy - 3, 24, 6), Scalar.White, -1);

            textStartX = sx + shieldW + 20;
        }
        else if (!string.IsNullOrEmpty(eurobandCountry))
        {
            int euroW = 52;
            // Blue strip (BGR: Blue=160, Green=40, Red=0)
            Cv2.Rectangle(plate, new Rect(2, 2, euroW, plateH - 4), new Scalar(160, 45, 0), -1);

            // Yellow stars circle simulation (small yellow dots)
            Scalar yellow = new(0, 215, 255);
            Point center = new(euroW / 2 + 1, 28);
            int radius = 14;
            for (int i = 0; i < 12; i++)
            {
                double angle = i * Math.PI / 6.0;
                int sx = (int)(center.X + radius * Math.Cos(angle));
                int sy = (int)(center.Y + radius * Math.Sin(angle));
                Cv2.Circle(plate, new Point(sx, sy), 1, yellow, -1);
            }

            // White country text at bottom of blue strip
            string cCode = eurobandCountry.ToUpperInvariant();
            int fontX = cCode.Length == 1 ? 20 : 12;
            double fontScale = cCode.Length == 1 ? 0.75 : 0.65;
            Cv2.PutText(plate, cCode, new Point(fontX, 74), HersheyFonts.HersheySimplex, fontScale, Scalar.White, 2, LineTypes.AntiAlias);

            textStartX = euroW + 25;
        }

        // 3. Render plate characters with clean spacing
        string[] tokens = plateText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int currentX = textStartX;

        foreach (var token in tokens)
        {
            foreach (char c in token)
            {
                string charStr = c.ToString();
                var tSize = Cv2.GetTextSize(charStr, HersheyFonts.HersheySimplex, 1.6, 4, out _);
                Cv2.PutText(plate, charStr, new Point(currentX, 64), HersheyFonts.HersheySimplex, 1.6, Scalar.Black, 4, LineTypes.AntiAlias);
                currentX += Math.Max(30, tSize.Width + 8);
            }
            currentX += 16; // token gap
        }

        if (!embedInBumper)
        {
            return plate;
        }

        // Embed in a realistic car bumper background
        int canvasW = 800;
        int canvasH = 600;
        Mat canvas = new(new Size(canvasW, canvasH), MatType.CV_8UC3, new Scalar(45, 50, 55)); // dark slate bumper

        Cv2.Line(canvas, new Point(0, 200), new Point(canvasW, 200), new Scalar(30, 35, 40), 4);
        Cv2.Line(canvas, new Point(0, 420), new Point(canvasW, 420), new Scalar(30, 35, 40), 4);

        int posX = (canvasW - plateW) / 2;
        int posY = 260;

        using var roi = new Mat(canvas, new Rect(posX, posY, plateW, plateH));
        plate.CopyTo(roi);
        plate.Dispose();

        return canvas;
    }

    public static byte[] CreatePlateImageBytes(string plateText, bool includeEuroband = true, bool embedInBumper = false)
    {
        using var mat = CreatePlateImage(plateText, includeEuroband, embedInBumper);
        using var ms = mat.ToMemoryStream(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
        return ms.ToArray();
    }
}
