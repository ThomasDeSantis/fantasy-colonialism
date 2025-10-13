using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

// Provides helpers for constructing geometric primitives used in the project.
public static class Geometry
{
    public static List<(int x, int y)> generateHexagonPoints((int x, int y) origin, int hexagonWidth, int hexagonHeight)
    {
        double halfWidth = hexagonWidth / 2.0;
        double halfHeight = hexagonHeight / 2.0;


        int minX = (int)Math.Floor(origin.x - halfWidth);
        int maxX = (int)Math.Ceiling(origin.x + halfWidth);
        int minY = (int)Math.Floor(origin.y - halfHeight);
        int maxY = (int)Math.Ceiling(origin.y + halfHeight);

        Console.WriteLine($"Hexagon Generation Parameters: Origin({origin.x}, {origin.y}) Width: {hexagonWidth} Height: {hexagonHeight}");
        Console.WriteLine($"Hexagon Generation Half Dimensions: HalfWidth: {halfWidth} HalfHeight: {halfHeight}");
        Console.WriteLine($"Hexagon Generation Bounds: MinX: {minX} MaxX: {maxX} MinY: {minY} MaxY: {maxY}");

        var points = new List<(int x, int y)>();

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (pixelAnyPointInsideHex(hexagonWidth, hexagonHeight, (x, y), origin))
                {
                    points.Add((x, y));
                }
            }
        }
        return points;
    }

    //Returns true if half or more of the pixel whose center is at (px, py)
    //lies inside a hexagon centered at (cx,cy) with the given diameter (width) and height.
    //Preload this data to improve performance
    public static bool pixelAnyPointInsideHex(int diameter, int height, (int x, int y) pixelOrigin, (int cx, int cy) hexOrigin)
    {
        var relativeOrigin = (x: pixelOrigin.x - hexOrigin.cx, y: pixelOrigin.y - hexOrigin.cy);
        // Precompute shape constants
        double w = diameter;
        double h = height;
        double halfH = h / 2.0;
        double s = (2.0 * h) / w; // slope factor from the exact inequalities

        // Local function: exact point-in-hex test for a flat-top hex centered at origin
        bool Inside(double x, double y)
        {
            double X = Math.Abs(x);
            double Y = Math.Abs(y);

            if (Y > halfH) return false;
            // small epsilon to count boundary points as inside
            const double eps = 1e-9;
            return (Y + s * X) <= (h + eps);
        }

        // Test the 4 pixel corners around the pixel center
        double px = relativeOrigin.x;
        double py = relativeOrigin.y;

        int insideCount = 0;
        if (Inside(px - 0.5, py - 0.5)) insideCount++;
        if (Inside(px + 0.5, py - 0.5)) insideCount++;
        if (Inside(px + 0.5, py + 0.5)) insideCount++;
        if (Inside(px - 0.5, py + 0.5)) insideCount++;
        if (Inside(px, py - 0.5)) insideCount++;
        if (Inside(px, py + 0.5)) insideCount++;
        if (Inside(px - 0.5, py)) insideCount++;
        if (Inside(px + 0.5, py)) insideCount++;

        return insideCount >= 1;
    }
}