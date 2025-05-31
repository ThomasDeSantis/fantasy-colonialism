using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class MultifractalNoise
    {
        FastNoiseLite noise;
        Random random = new Random();
        public MultifractalNoise()
        {
            noise = new FastNoiseLite(random.Next());
            noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(5);
            noise.SetFractalLacunarity(2.0f);
            noise.SetFractalGain(0.5f);
            noise.SetFractalWeightedStrength(0.5f);
            noise.SetFrequency(0.01f);
        }

        public float GetNoise(float x, float y)
        {
            return noise.GetNoise(x, y);
        }
    }
}
