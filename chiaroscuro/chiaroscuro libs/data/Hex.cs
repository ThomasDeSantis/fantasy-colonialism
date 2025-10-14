using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MapData
{
    public class Hex
    {
        private int id = -1;
        private (int, int) origin;
        private HashSet<ChPoint> points;

        private (int,int) topLeftBounds;
        private (int, int) bottomRightBounds;
        private (int, int) normalizedBottomRightBounds;


        // Getter Properties
        public int Id { get => id; set => id = value; }
        public (int, int) Origin { get => origin; set => origin = value; }
        public HashSet<ChPoint> Points { get => points; set => points = value; }

        public (int,int) TopLeftBounds { get => topLeftBounds; }
        public (int, int) BottomRightBounds { get => bottomRightBounds; }
        public (int,int) NormalizedBottomRightBounds { get => normalizedBottomRightBounds; }//Top left will always be 0,0


        public Hex(HashSet<ChPoint> points, (int, int) origin)
        {
            this.points = points;
            this.origin = (origin.Item1, origin.Item2);

            //Used to mark as unset
            calculateHexBounds();
            calculateNormalizedBounds();
        }

        //To do: Some better logging for points
        public override string ToString()
        {
            StringBuilder hexString = new StringBuilder();
            hexString.Append($"Hex ID: {id}, Origin: ({origin.Item1}, {origin.Item2}) #Points: {points.Count.ToString()}");
            return hexString.ToString();
        }

        //To do: Work on version that uses contains to utilize hashing
        public bool containsPoint((int, int) point)
        {
            // Fix: Check if a ChPoint exists in the HashSet with matching coordinates
            return points.Any(p => p.X == point.Item1 && p.Y == point.Item2);
        }

        //De-normalizes a point and checks if it is in the hex
        public bool containsPointNormalized((int, int) point)
        {
            (int, int) denormalizedPoint = denormalizePoint(point);
            return containsPoint(denormalizedPoint);
        }

        //Denormalizes a point based on the hex's top left bounds
        public (int,int) denormalizePoint((int, int) point)
        {
            return (point.Item1 + topLeftBounds.Item1, point.Item2 + topLeftBounds.Item2);
        }

        //Returns a nested tuple representing the top left corner and bottom right corner of the hex's bounds
        //Returned points will not actually be in the hex, but will represent the bounding box of the hex
        public ((int, int), (int, int)) getHexBounds()
        {
            return (topLeftBounds,bottomRightBounds);
        }

        private void calculateHexBounds()
        {
            int minX = getMinX();
            int maxX = getMaxX();
            int minY = getMinY();
            int maxY = getMaxY();

            topLeftBounds = (minX, minY);
            bottomRightBounds = (maxX, maxY);
        }

        //Returns bounds with top left corner normalized to (0,0)
        //Returns a tuple representing the bottom right corner
        public (int, int) getNormalizedBounds()
        {
            return normalizedBottomRightBounds;
        }

        private int getMinX()
        {
            return points.Min(p => p.X);
        }
        private int getMaxX()
        {
            return points.Max(p => p.X);
        }
        private int getMinY()
        {
            return points.Min(p => p.Y);
        }
        private int getMaxY()
        {
            return points.Max(p => p.Y);
        }

        private void calculateNormalizedBounds()
        {
            ((int, int), (int, int)) initialBounds = getHexBounds();
            normalizedBottomRightBounds = (
                x: initialBounds.Item2.Item1 - initialBounds.Item1.Item1,
                y: initialBounds.Item2.Item2 - initialBounds.Item1.Item2);
        }

        


        

    }

}