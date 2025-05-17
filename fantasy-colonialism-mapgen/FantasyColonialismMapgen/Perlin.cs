using LibNoise;
using LibNoise.Primitive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class Perlin
    {
        SimplexPerlin perlin;
        public Perlin()
        {
            perlin = new SimplexPerlin();
        }

        public float getPerlin(float x, float y)
        {
            var value = perlin.GetValue(x, y);
            return value;
        }
    }
}
