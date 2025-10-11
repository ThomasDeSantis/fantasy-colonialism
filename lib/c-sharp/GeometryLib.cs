using System;
using System.Collections.Generic;

// Provides helpers for constructing geometric primitives used in the project.
public static class Geometry
{
    public static List<(int x, int y)> generateHexagonPoints((int x, int y) origin, double hexagonWidth, double hexagonHeight)
    {
        double halfWidth = hexagonWidth / 2.0;
        double halfHeight = hexagonHeight / 2.0;
        double sqrtThree = Math.Sqrt(3);
        double sqrtThreeHalf = sqrtThree / 2.0;
        double scaleX = halfWidth;
        double scaleY = halfHeight;

        int minX = (int)Math.Floor(origin.x - halfWidth);
        int maxX = (int)Math.Ceiling(origin.x + halfWidth);
        int minY = (int)Math.Floor(origin.y - halfHeight);
        int maxY = (int)Math.Ceiling(origin.y + halfHeight);

        Console.WriteLine($"Hexagon Generation Parameters: Origin({origin.x}, {origin.y}) Width: {hexagonWidth} Height: {hexagonHeight}");
        Console.WriteLine($"Hexagon Generation Bounds: MinX: {minX} MaxX: {maxX} MinY: {minY} MaxY: {maxY}");

        var points = new List<(int x, int y)>();

        for (int y = minY; y <= maxY; y++)
        {
            double normalizedY = (y - origin.y) / scaleY;
            double absNormalizedY = Math.Abs(normalizedY);

            if (absNormalizedY > sqrtThree)
            {
                continue;
            }

            for (int x = minX; x <= maxX; x++)
            {
                double normalizedX = (x - origin.x) / scaleX;
                double absNormalizedX = Math.Abs(normalizedX);

                if (sqrtThree * absNormalizedX + absNormalizedY <= sqrtThree)
                {
                    points.Add((x, y));
                }
            }
        }

        return points;
    }
}