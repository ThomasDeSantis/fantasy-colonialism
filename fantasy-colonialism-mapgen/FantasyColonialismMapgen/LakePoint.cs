using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class LakePoint
    {
        int id;
        int lakeId;//The ID of the lake this point belongs to.
        int pointId;//The ID of the point this lake point belongs to.
        int x;//X coordinate of the point in the world.
        int y;//Y coordinate of the point in the world.
        int depth;//Average depth of the lake at this point in meters.
        double pointAreaPercent;//Percentage of the point that is covered by the lake.

        public int Id { get => id; }
        public int LakeId { get => lakeId; }
        public int PointId { get => pointId; }
        public int X { get => x; }
        public int Y { get => y; }
        public int Depth { get => depth; set => depth = value; }
        public double PointAreaPercent { get => pointAreaPercent; set => pointAreaPercent = value; }

        public LakePoint(int id, int lakeId, int pointId, int x, int y)
        {
            this.id = id;
            this.lakeId = lakeId;
            this.pointId = pointId;
            this.x = x;
            this.y = y;
            this.depth = depth;
            this.pointAreaPercent = pointAreaPercent;
        }
    }
}
