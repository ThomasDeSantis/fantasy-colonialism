using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class FractalCoastlineSegment
    {
        (int x, int y) p1;
        (int x, int y) p2;
        int roughness;
        public FractalCoastlineSegment((int xI1, int yI1) pI1, (int xI2, int yI2) pI2, int roughnessI)
        {
            p1 = pI1;
            p2 = pI2;
            roughness = roughnessI;
        } 

        public (int,int) getP1()
        {
            return p1;
        }

        public (int, int) getP2()
        {
            return p2;
        }

        public FractalCoastlineSegment subdivide(Random r,int width, int height)
        {
            // Calculate Manhattan distance between p1 and p2
            int dx = Math.Abs(p2.x - p1.x);
            int dy = Math.Abs(p2.y - p1.y);
            int manhattanDist = dx + dy;

            // Base case: stop if the segment is already at unit length
            if (manhattanDist <= 8)
            {
                return null; // no new segment created (segment is minimal)
            }

            // Find midpoint (integer midpoint on grid)
            int midX = (p1.x + p2.x) / 2;
            int midY = (p1.y + p2.y) / 2;

            // Randomly displace the midpoint using roughness as scale
            // e.g. random offset in range [-roughness, +roughness]
            //int maxOffset = roughness;
            int maxOffset = manhattanDist/4;
            if (maxOffset < 4) maxOffset = 4;
            // Generate random offsets for x and y
            int offsetX = r.Next(-maxOffset, maxOffset + 1);
            int offsetY = r.Next(-maxOffset, maxOffset + 1);
            // Apply displacement
            midX += offsetX;
            midY += offsetY;

            // Ensure the midpoint is not identical to endpoints (avoid zero-length segment)
            if ((midX == p1.x && midY == p1.y) || (midX == p2.x && midY ==p2.y))
            {
                // Adjust midpoint by at least 1 in one direction towards the other end
                if (dx >= dy)
                {
                    // move one step in x-direction (toward p2.x)
                    midX += (p2.x > p1.x) ? 1 : -1;
                }
                else
                {
                    // move one step in y-direction (toward p2.y)
                    midY += (p2.y > p1.y) ? 1 : -1;
                }
            }

            // Define the displaced midpoint
            (int x, int y) midpoint = (midX, midY);


            if(midpoint.x <= 0 || midpoint.x >= width-1 || midpoint.y <= 0 || midpoint.y >= height - 1)
            {

                Console.WriteLine("Invalid: " + midpoint);
                //return subdivide(r,width,height);//If it is out of range reattempt the subdivision
                return null;
            }
            else
            {
                Console.WriteLine("Pre change: " + p1 + " to " + p2);
                // Create a new segment from midpoint to the original p2
                FractalCoastlineSegment newSegment = new FractalCoastlineSegment(midpoint, p2, roughness);
                // Update original segment's end to the midpoint
                p2 = midpoint;
                Console.WriteLine("Old Segment: " + p1 + " to " + p2);
                Console.WriteLine("New Segment: " + newSegment.p1 + " to " + newSegment.p2 + " with roughness " + newSegment.roughness);

                return newSegment;
            }
        }
    }
}
