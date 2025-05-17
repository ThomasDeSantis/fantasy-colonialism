using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class MountainRange
    {
        public MountainRange()
        {
            points = new HashSet<(int, int)>();
            mountainPointsToProcess = new List<(int, int)>();
        }
        public void setAvgHeight(int avgHeight)
        {
            avgHeightOnBorders = avgHeight;
        }
        public HashSet<(int, int)> points;
        public List<(int, int)> mountainPointsToProcess;
        public int avgHeightOnBorders = 0;
    }
}
