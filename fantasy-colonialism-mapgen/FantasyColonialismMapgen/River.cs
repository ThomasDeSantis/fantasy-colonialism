using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class River
    {
        private int id;//The ID of the river.
        //All points within the lake including
        List<Point> riverPoints;
        private Point headwater;
        private Point outflowPoint;//The point from which the lake outflows.

        public int tributaryId;//The id of the tributary which this flows into.
        public WaterType tributaryType;//The type of tributary this flows into.


        public int Id { get => id; set => id = value;  }
        public List<Point> RiverPoints { get => riverPoints; }
        public Point Headwater { get => headwater; set => headwater = value; }
        public Point OutflowPoint { get => outflowPoint; set => outflowPoint = value; }
        public int TributaryId { get => tributaryId; set => tributaryId = value; }
        public WaterType TributaryType { get => tributaryType; set => tributaryType = value; }


        public River(int id, Point headwater)
        {
            riverPoints = new List<Point>();
            this.id = id;
            this.headwater = headwater;
        }

        public void addRiverPoint(Point p)
        {
            riverPoints.Add(p);
        }

    }

    enum WaterType
    {
        river,
        lake,
        ocean
    }
}
