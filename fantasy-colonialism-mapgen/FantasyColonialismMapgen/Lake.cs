using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class Lake
    {
        //All points within the lake including
<<<<<<< Updated upstream
        List<LakePoint> lakePoints;
=======
        List<Point> lakePoints;
        private float waterSalinity = .07f;//Stub
        private int lakeRimHeight;
        private int lakeBedHeight;
        private double outflowVolumePerSecond;
        private double volume;
        private Point outflowPoint;//The point from which the lake outflows.


        public int Id { get => id; set => id = value;  }
        public List<Point> LakePoints { get => lakePoints; }
        public float WaterSalinity { get => waterSalinity; set => waterSalinity = value; }
        public int LakeRimHeight { get => lakeRimHeight; }
        public int LakeBedHeight { get => lakeBedHeight; }
        public double OutflowVolumePerSecond { get => outflowVolumePerSecond;}
        public double Volume { get => volume; set => volume = value; }
        public Point OutflowPoint { get => outflowPoint; set => outflowPoint = value; }



        public Lake(int id, List<Point> lakePoints, int lakeRimHeight, int lakeBedHeight, double outflowVolumePerSecond, double volume)
        {
            this.id = id;
            this.lakePoints = lakePoints;
            this.lakeRimHeight = lakeRimHeight;
            this.lakeBedHeight = lakeBedHeight;
            this.outflowVolumePerSecond = outflowVolumePerSecond;
            this.volume = volume;
        }

        public int getDepth()
        {
            return lakeRimHeight - lakeBedHeight;
        }

>>>>>>> Stashed changes
    }
}
