using Unity.Mathematics;
using Unity.Burst;

namespace Mandelbrot3D
{
    [BurstCompile]
    public static class MandelbrotMath
    {
        [BurstCompile]
        public static float CalculateMandelbrotValue(float3 position, float3 center, float scale, int maxIterations, float time)
        {
            // Convert 3D position to complex number
            // Use X and Z as real and imaginary parts, Y affects the iteration
            float2 c = (position.xz - center.xz) / scale;
            
            // Add time-based evolution
            c += math.sin(time * 0.5f) * 0.1f;
            
            // Use Y coordinate to create 3D variation
            float yOffset = (position.y - center.y) / scale;
            c += math.sin(yOffset * math.PI + time) * 0.05f;
            
            return IterateMandelbrot(c, maxIterations);
        }

        [BurstCompile]
        private static float IterateMandelbrot(float2 c, int maxIterations)
        {
            float2 z = float2.zero;
            int iteration = 0;
            
            for (int i = 0; i < maxIterations; i++)
            {
                // z = z^2 + c
                float zx2 = z.x * z.x;
                float zy2 = z.y * z.y;
                
                // Early escape condition
                if (zx2 + zy2 > 4.0f)
                {
                    // Smooth coloring using fractional iteration count
                    float smoothValue = i + 1 - math.log2(math.log2(zx2 + zy2));
                    return smoothValue / maxIterations;
                }
                
                z = new float2(zx2 - zy2 + c.x, 2.0f * z.x * z.y + c.y);
                iteration = i;
            }
            
            return 1.0f; // Point is in the set
        }

        [BurstCompile]
        public static float3 CalculateNormal(float3 position, float3 center, float scale, int maxIterations, float time, float epsilon = 0.01f)
        {
            float centerValue = CalculateMandelbrotValue(position, center, scale, maxIterations, time);
            
            float dx = CalculateMandelbrotValue(position + new float3(epsilon, 0, 0), center, scale, maxIterations, time) - centerValue;
            float dy = CalculateMandelbrotValue(position + new float3(0, epsilon, 0), center, scale, maxIterations, time) - centerValue;
            float dz = CalculateMandelbrotValue(position + new float3(0, 0, epsilon), center, scale, maxIterations, time) - centerValue;
            
            float3 normal = new float3(dx, dy, dz);
            return math.normalize(normal);
        }

        [BurstCompile]
        public static float3 GetVertexPosition(int x, int y, int z, int resolution, float3 center, float scale)
        {
            float3 localPos = new float3(
                (x - resolution * 0.5f) / resolution,
                (y - resolution * 0.5f) / resolution,
                (z - resolution * 0.5f) / resolution
            );
            
            return center + localPos * scale;
        }

        [BurstCompile]
        public static bool IsInsideSet(float mandelbrotValue, float threshold = 0.8f)
        {
            return mandelbrotValue > threshold;
        }
    }
}