using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

namespace Mandelbrot3D
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MandelbrotEvolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Only run this system when there are entities with MandelbrotData
            state.RequireForUpdate<MandelbrotData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = (float)SystemAPI.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Update Mandelbrot parameters over time
            foreach (var (mandelbrotData, meshRequest) in 
                     SystemAPI.Query<RefRW<MandelbrotData>, RefRW<MeshUpdateRequest>>())
            {
                var data = mandelbrotData.ValueRO;
                bool needsUpdate = false;

                // Evolve center position
                float3 newCenter = data.Center;
                newCenter.x += math.sin(time * data.AnimationSpeed + data.TimeOffset) * 0.01f;
                newCenter.z += math.cos(time * data.AnimationSpeed * 0.7f + data.TimeOffset) * 0.01f;
                newCenter.y += math.sin(time * data.AnimationSpeed * 0.5f + data.TimeOffset) * 0.005f;
                
                if (math.distance(newCenter, data.Center) > 0.001f)
                {
                    data.Center = newCenter;
                    needsUpdate = true;
                }

                // Evolve scale
                float newScale = data.Scale * (1.0f + math.sin(time * data.AnimationSpeed * 0.3f + data.TimeOffset) * 0.02f);
                if (math.abs(newScale - data.Scale) > 0.001f)
                {
                    data.Scale = newScale;
                    needsUpdate = true;
                }

                // Evolve max iterations
                int newMaxIterations = (int)(data.MaxIterations + math.sin(time * data.AnimationSpeed * 0.2f + data.TimeOffset) * 10);
                newMaxIterations = math.clamp(newMaxIterations, 10, 100);
                if (newMaxIterations != data.MaxIterations)
                {
                    data.MaxIterations = newMaxIterations;
                    needsUpdate = true;
                }

                // Update component data
                mandelbrotData.ValueRW = data;

                // Request mesh update if needed
                if (needsUpdate)
                {
                    var request = meshRequest.ValueRW;
                    request.RequiresUpdate = true;
                    request.Time = time;
                    meshRequest.ValueRW = request;
                }
            }
        }
    }
}