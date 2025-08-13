using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities.Graphics;
using Unity.Rendering;
using System.Runtime.InteropServices;

namespace Mandelbrot3D
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MeshPoolSystem))]
    public partial struct MandelbrotMeshSystem : ISystem
    {
        private ComponentLookup<MeshPoolIndex> meshPoolLookup;
        private SystemHandle meshPoolSystemHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            meshPoolLookup = state.GetComponentLookup<MeshPoolIndex>(true);
            meshPoolSystemHandle = state.World.GetExistingSystem<MeshPoolSystem>();
            
            // Only run this system when there are entities with MandelbrotData
            state.RequireForUpdate<MandelbrotData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            meshPoolLookup.Update(ref state);
            
            var meshPoolSystem = state.World.GetExistingSystemManaged<MeshPoolSystem>();
            if (meshPoolSystem == null) 
            {
                UnityEngine.Debug.LogWarning("MeshPoolSystem not found");
                return;
            }

            var time = (float)SystemAPI.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;

            var processedCount = 0;
            
            // Update mesh generation requests
            foreach (var (meshRequest, mandelbrotData, meshPoolIndex, entity) in 
                     SystemAPI.Query<RefRW<MeshUpdateRequest>, RefRO<MandelbrotData>, RefRO<MeshPoolIndex>>()
                              .WithEntityAccess())
            {
                processedCount++;
                UnityEngine.Debug.Log($"Processing mesh update for entity {entity.Index}, pool index {meshPoolIndex.ValueRO.Value}");
                if (!meshRequest.ValueRO.RequiresUpdate && 
                    math.abs(time - meshRequest.ValueRO.Time) < 1.0f / 30.0f) // 30 FPS update rate
                    continue;

                var mesh = meshPoolSystem.GetMesh(meshPoolIndex.ValueRO.Value);
                if (mesh == null) 
                {
                    UnityEngine.Debug.LogWarning($"Mesh is null for pool index {meshPoolIndex.ValueRO.Value}");
                    continue;
                }

                UnityEngine.Debug.Log($"Generating mesh for entity {entity.Index}, resolution {mandelbrotData.ValueRO.Resolution}");

                // Schedule mesh generation job
                var meshDataArray = Mesh.AllocateWritableMeshData(1);
                var meshData = meshDataArray[0];
                
                // Set up the mesh structure
                var gridSize = math.min(mandelbrotData.ValueRO.Resolution, 16);
                var quadCount = (gridSize - 1) * (gridSize - 1);
                var vertexCount = quadCount * 4; // 4 vertices per quad
                var indexCount = quadCount * 6; // 6 indices per quad
                
                var attributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
                attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
                attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
                attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
                
                meshData.SetVertexBufferParams(vertexCount, attributes);
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                attributes.Dispose();
                
                var job = new GenerateMandelbrotMeshJob
                {
                    MandelbrotData = mandelbrotData.ValueRO,
                    Time = time,
                    Resolution = mandelbrotData.ValueRO.Resolution,
                    MeshData = meshData
                };

                var jobHandle = job.Schedule();
                state.Dependency = jobHandle;
                jobHandle.Complete();

                // Apply mesh data on main thread
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
                
                UnityEngine.Debug.Log($"Applied mesh data: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles, bounds: {mesh.bounds}");

                // Update request status
                meshRequest.ValueRW.Time = time;
                meshRequest.ValueRW.RequiresUpdate = false;
            }
            
            if (processedCount == 0)
            {
                UnityEngine.Debug.Log("MandelbrotMeshSystem: No entities to process");
            }
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    struct MandelbrotVertex
    {
        public float3 position;
        public float3 normal;
        public float2 uv;
    }

    [BurstCompile]
    public struct GenerateMandelbrotMeshJob : IJob
    {
        public MandelbrotData MandelbrotData;
        public float Time;
        public int Resolution;
        public Mesh.MeshData MeshData;

        public void Execute()
        {
            GenerateMandelbrotMesh();
        }
        
        private float3 GetVertexPosition(int x, int y, int z, int resolution, float3 center, float scale)
        {
            float3 localPos = new float3(
                (x - resolution * 0.5f) / resolution,
                (y - resolution * 0.5f) / resolution,
                (z - resolution * 0.5f) / resolution
            );
            
            return center + localPos * scale;
        }
        
        private float CalculateMandelbrotValue(float3 position, float3 center, float scale, int maxIterations, float time)
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
        
        private float IterateMandelbrot(float2 c, int maxIterations)
        {
            float2 z = float2.zero;
            
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
            }
            
            return 1.0f; // Point is in the set
        }

        private void GenerateMandelbrotMesh()
        {
            // Get vertex and index data
            var vertices = MeshData.GetVertexData<MandelbrotVertex>(0);
            var indices = MeshData.GetIndexData<uint>();
            
            int vertexIndex = 0;
            int triangleIndex = 0;

            // Generate a simple procedural mesh based on Mandelbrot values
            // This is a simplified approach - in production you'd use marching cubes
            int gridSize = math.min(Resolution, 16); // Limit grid size for performance
            
            for (int x = 0; x < gridSize - 1; x++)
            {
                for (int z = 0; z < gridSize - 1; z++)
                {
                    // Calculate positions
                    float3 p00 = GetVertexPosition(x, 0, z, gridSize, MandelbrotData.Center, MandelbrotData.Scale);
                    float3 p10 = GetVertexPosition(x + 1, 0, z, gridSize, MandelbrotData.Center, MandelbrotData.Scale);
                    float3 p01 = GetVertexPosition(x, 0, z + 1, gridSize, MandelbrotData.Center, MandelbrotData.Scale);
                    float3 p11 = GetVertexPosition(x + 1, 0, z + 1, gridSize, MandelbrotData.Center, MandelbrotData.Scale);
                    
                    // Calculate Mandelbrot values for height
                    float h00 = CalculateMandelbrotValue(p00, MandelbrotData.Center, MandelbrotData.Scale, MandelbrotData.MaxIterations, Time);
                    float h10 = CalculateMandelbrotValue(p10, MandelbrotData.Center, MandelbrotData.Scale, MandelbrotData.MaxIterations, Time);
                    float h01 = CalculateMandelbrotValue(p01, MandelbrotData.Center, MandelbrotData.Scale, MandelbrotData.MaxIterations, Time);
                    float h11 = CalculateMandelbrotValue(p11, MandelbrotData.Center, MandelbrotData.Scale, MandelbrotData.MaxIterations, Time);
                    
                    // Adjust Y positions based on Mandelbrot values
                    p00.y = h00 * MandelbrotData.Scale * 0.5f;
                    p10.y = h10 * MandelbrotData.Scale * 0.5f;
                    p01.y = h01 * MandelbrotData.Scale * 0.5f;
                    p11.y = h11 * MandelbrotData.Scale * 0.5f;
                    
                    // Add vertices
                    if (vertexIndex + 4 <= vertices.Length)
                    {
                        // Calculate normal
                        float3 normal = math.normalize(math.cross(p10 - p00, p01 - p00));
                        
                        // Create vertices
                        var v0 = new MandelbrotVertex
                        {
                            position = p00,
                            normal = normal,
                            uv = new float2((float)x / gridSize, (float)z / gridSize)
                        };
                        var v1 = new MandelbrotVertex
                        {
                            position = p10,
                            normal = normal,
                            uv = new float2((float)(x + 1) / gridSize, (float)z / gridSize)
                        };
                        var v2 = new MandelbrotVertex
                        {
                            position = p01,
                            normal = normal,
                            uv = new float2((float)x / gridSize, (float)(z + 1) / gridSize)
                        };
                        var v3 = new MandelbrotVertex
                        {
                            position = p11,
                            normal = normal,
                            uv = new float2((float)(x + 1) / gridSize, (float)(z + 1) / gridSize)
                        };
                        
                        vertices[vertexIndex] = v0;
                        vertices[vertexIndex + 1] = v1;
                        vertices[vertexIndex + 2] = v2;
                        vertices[vertexIndex + 3] = v3;
                        
                        // Add triangles
                        if (triangleIndex + 6 <= indices.Length)
                        {
                            indices[triangleIndex] = (uint)vertexIndex;
                            indices[triangleIndex + 1] = (uint)(vertexIndex + 2);
                            indices[triangleIndex + 2] = (uint)(vertexIndex + 1);
                            
                            indices[triangleIndex + 3] = (uint)(vertexIndex + 1);
                            indices[triangleIndex + 4] = (uint)(vertexIndex + 2);
                            indices[triangleIndex + 5] = (uint)(vertexIndex + 3);
                            
                            triangleIndex += 6;
                        }
                        
                        vertexIndex += 4;
                    }
                }
            }
            
            // Update the actual vertex and index counts
            MeshData.subMeshCount = 1;
            MeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndex, MeshTopology.Triangles));
            
            // Bounds will be set when applying to mesh
        }

    }
}