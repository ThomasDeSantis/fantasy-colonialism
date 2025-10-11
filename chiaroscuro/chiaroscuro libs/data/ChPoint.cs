using System;
using System.Text.Json;

namespace MapData
{
    public class ChPoint : Point
    {
        protected int hexId;

        // Getter Properties
        public int HexId { get => hexId; set => hexId = value; }

        public ChPoint(int x, int y, int hexId, int id) : base(x, y, id)
        {
            this.hexId = hexId; // Default value indicating no hex assigned
        }

        public ChPoint(int x, int y) : base(x, y)
        {
        }
    }

}