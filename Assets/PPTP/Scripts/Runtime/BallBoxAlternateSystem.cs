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

namespace PPTP
{
    /// <summary>
    /// Main system that handles ball-box shape alternation animation and mesh generation
    /// Uses burst compilation and parallel jobs for optimal performance
    /// Works with BallBoxMeshPoolSystem for proper ECS rendering integration
    /// 
    /// Upgrade to offthread?: https://github.com/Dreaming381/Latios-Framework-Add-Ons/blob/main/AddOns/Cyline/LineRenderer3DAuthoring.cs
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BallBoxMeshPoolSystem))]
    public partial struct BallBoxAlternateSystem : ISystem
    {
        /// <summary>
        /// Lookup for mesh pool indices
        /// </summary>
        private ComponentLookup<BallBoxMeshPoolIndex> meshPoolLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            meshPoolLookup = state.GetComponentLookup<BallBoxMeshPoolIndex>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            meshPoolLookup.Update(ref state);
            
            var time = (float)SystemAPI.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // First, update animation states
            var animationJob = new UpdateBallBoxAnimationJob
            {
                DeltaTime = deltaTime,
                Time = time
            };

            // Schedule the animation job and wait for it to complete before accessing components
            var jobHandle = animationJob.ScheduleParallel(state.Dependency);
            state.Dependency = jobHandle;
            
            // Complete the job before accessing the components in the foreach loop
            jobHandle.Complete();

            // Then, handle mesh generation for entities that need updates
            foreach (var (meshRequest, ballBoxData, meshPoolIndex, entity) in 
                     SystemAPI.Query<RefRW<BallBoxMeshUpdateRequest>, RefRO<BallBoxData>, RefRO<BallBoxMeshPoolIndex>>()
                              .WithEntityAccess())
            {
                // Check if mesh needs updating (either forced update or time-based update)
                if (!meshRequest.ValueRO.RequiresUpdate && 
                    math.abs(time - meshRequest.ValueRO.Time) < 1.0f / 30.0f) // 30 FPS update rate
                    continue;

                // Generate mesh using the mesh pool system
                GenerateMeshForEntity(ref state, entity, ballBoxData.ValueRO, ref meshRequest.ValueRW, meshPoolIndex.ValueRO, time);
            }
        }

        /// <summary>
        /// Generates mesh for a specific entity based on its current ball-box state
        /// </summary>
        private void GenerateMeshForEntity(ref SystemState state, Entity entity, BallBoxData ballBoxData, 
            ref BallBoxMeshUpdateRequest meshRequest, BallBoxMeshPoolIndex meshPoolIndex, float time)
        {
            // Get the mesh pool system to access the actual mesh
            var meshPoolSystem = state.World.GetExistingSystemManaged<BallBoxMeshPoolSystem>();
            if (meshPoolSystem == null) 
            {
                    return;
            }

            var mesh = meshPoolSystem.GetMesh(meshPoolIndex.Value);
            if (mesh == null) 
            {
                    return;
            }


            // Use cube tessellation for consistent layout throughout the system
            var vertexCount = BallBoxMeshUtility.CalculateCubeVertexCount(ballBoxData.CubeTessellation);
            var triangleCount = BallBoxMeshUtility.CalculateCubeTriangleCount(ballBoxData.CubeTessellation);
            

            // Create mesh data
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];
            
            // Set up vertex attributes
            var attributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
            
            // Allocate mesh data with exact cube layout requirements
            meshData.SetVertexBufferParams(vertexCount, attributes);
            meshData.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);
            attributes.Dispose();
            
            // Schedule mesh generation job
            var job = new GenerateBallBoxMeshJob
            {
                BallBoxData = ballBoxData,
                Time = time,
                MeshData = meshData
            };

            var jobHandle = job.Schedule();
            state.Dependency = jobHandle;
            jobHandle.Complete();

            // Apply mesh data to the actual mesh from the pool
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            

            // Update request status
            meshRequest.Time = time;
            meshRequest.RequiresUpdate = false;

        }
    }

    /// <summary>
    /// Job for updating ball-box animation states in parallel
    /// </summary>
    [BurstCompile]
    public partial struct UpdateBallBoxAnimationJob : IJobEntity
    {
        /// <summary>
        /// Delta time for this frame
        /// </summary>
        public float DeltaTime;
        
        /// <summary>
        /// Current elapsed time
        /// </summary>
        public float Time;

        /// <summary>
        /// Executes the animation update for a single entity
        /// </summary>
        /// <param name="ballBoxData">Ball-box data to update</param>
        /// <param name="meshRequest">Mesh request to potentially trigger</param>
        public void Execute(ref BallBoxData ballBoxData, ref BallBoxMeshUpdateRequest meshRequest)
        {
            // Update interpolation time
            ballBoxData.InterpolationTime += DeltaTime / ballBoxData.ShapeChangeDuration;
            
            // Check if we need to reverse direction
            if (ballBoxData.InterpolationTime >= 1.0f)
            {
                ballBoxData.InterpolationTime = 1.0f;
                ballBoxData.AnimationDirection *= -1; // Reverse direction
                ballBoxData.InterpolationTime = 0.0f; // Reset interpolation
            }
            
            // Calculate current shape state using smooth step for better animation
            float t = ballBoxData.InterpolationTime;
            t = t * t * (3.0f - 2.0f * t); // Smoothstep
            
            // Apply direction to interpolation
            if (ballBoxData.AnimationDirection > 0)
            {
                ballBoxData.CurrentShapeState = t; // Animating towards ball (1.0)
            }
            else
            {
                ballBoxData.CurrentShapeState = 1.0f - t; // Animating towards cube (0.0)
            }
            
            // Mark mesh for update when animation progresses
            meshRequest.RequiresUpdate = true;
        }
    }

    /// <summary>
    /// Vertex structure for ball-box meshes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct BallBoxVertex
    {
        /// <summary>
        /// 3D position of the vertex
        /// </summary>
        public float3 position;
        
        /// <summary>
        /// Normal vector for lighting
        /// </summary>
        public float3 normal;
        
        /// <summary>
        /// UV texture coordinates
        /// </summary>
        public float2 uv;
    }

    /// <summary>
    /// Burst-compiled job for generating ball-box mesh data
    /// </summary>
    [BurstCompile]
    public struct GenerateBallBoxMeshJob : IJob
    {
        /// <summary>
        /// Ball-box configuration data
        /// </summary>
        public BallBoxData BallBoxData;
        
        /// <summary>
        /// Current time for time-based effects
        /// </summary>
        public float Time;
        
        /// <summary>
        /// Mesh data to write to
        /// </summary>
        public Mesh.MeshData MeshData;

        public void Execute()
        {
            GenerateBallBoxMesh();
        }
        
        /// <summary>
        /// Main mesh generation function that interpolates between cube and sphere
        /// Both shapes use the same vertex layout for proper interpolation
        /// </summary>
        private void GenerateBallBoxMesh()
        {
            // Use cube tessellation for consistent vertex/index layout
            var tessellation = BallBoxData.CubeTessellation;
            var vertexCount = BallBoxMeshUtility.CalculateCubeVertexCount(tessellation);
            var triangleCount = BallBoxMeshUtility.CalculateCubeTriangleCount(tessellation);
            
            
            // Get mesh data arrays
            var vertices = MeshData.GetVertexData<BallBoxVertex>(0);
            var indices = MeshData.GetIndexData<uint>();
            
            
            // Ensure we have enough space
            if (vertices.Length < vertexCount || indices.Length < triangleCount * 3)
            {
                return;
            }
            
            // Create temporary arrays using cube layout for both shapes
            var cubePositions = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var cubeNormals = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var cubeUVs = new NativeArray<float2>(vertexCount, Allocator.Temp);
            
            var spherePositions = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var sphereNormals = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var sphereUVs = new NativeArray<float2>(vertexCount, Allocator.Temp);
            
            // Generate cube mesh data (base layout)
            BallBoxMeshUtility.GenerateCubeVertices(BallBoxData.CubeSize, tessellation, float3.zero,
                cubePositions, cubeNormals, cubeUVs);
            
            // Generate sphere positions using the same cube layout structure
            GenerateSphereFromCubeLayout(tessellation, BallBoxData.BallSize, cubePositions, 
                spherePositions, sphereNormals, sphereUVs);
            
            // Interpolate between cube and sphere based on current shape state
            var interpolatedPositions = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var interpolatedNormals = new NativeArray<float3>(vertexCount, Allocator.Temp);
            
            BallBoxMeshUtility.InterpolateVertices(cubePositions, spherePositions, interpolatedPositions, 
                BallBoxData.CurrentShapeState);
            BallBoxMeshUtility.InterpolateNormals(cubeNormals, sphereNormals, interpolatedNormals, 
                BallBoxData.CurrentShapeState);
            
            // Fill vertex buffer - only fill actual vertex count
            int actualVerticesWritten = 0;
            for (int i = 0; i < vertexCount && i < vertices.Length; i++)
            {
                vertices[i] = new BallBoxVertex
                {
                    position = interpolatedPositions[i],
                    normal = interpolatedNormals[i],
                    uv = math.lerp(cubeUVs[i], sphereUVs[i], BallBoxData.CurrentShapeState)
                };
                actualVerticesWritten++;
            }
            
            
            // Clear indices array first to avoid garbage data
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = 0;
            }
            
            // Generate indices using cube layout - with bounds checking
            var actualIndicesWritten = GenerateSafeIndices(tessellation, indices, actualVerticesWritten);
            
            
            // Set up submesh with actual triangle count written
            var actualTriangleCount = actualIndicesWritten / 3;
            MeshData.subMeshCount = 1;
            MeshData.SetSubMesh(0, new SubMeshDescriptor(0, actualIndicesWritten, MeshTopology.Triangles));
            
            
            // Clean up temporary arrays
            cubePositions.Dispose();
            cubeNormals.Dispose();
            cubeUVs.Dispose();
            spherePositions.Dispose();
            sphereNormals.Dispose();
            sphereUVs.Dispose();
            interpolatedPositions.Dispose();
            interpolatedNormals.Dispose();
        }
        
        /// <summary>
        /// Generates indices with bounds checking to prevent buffer overflow
        /// </summary>
        private int GenerateSafeIndices(int tessellation, NativeArray<uint> indices, int maxVertexIndex)
        {
            int indexCount = 0;
            int verticesPerFace = (tessellation + 1) * (tessellation + 1);
            
            // Generate indices for each face
            for (int face = 0; face < 6; face++)
            {
                int faceVertexOffset = face * verticesPerFace;
                
                for (int y = 0; y < tessellation; y++)
                {
                    for (int x = 0; x < tessellation; x++)
                    {
                        if (indexCount + 6 > indices.Length) 
                        {
                            return indexCount;
                        }
                        
                        // Calculate vertex indices for current quad on this face
                        uint v0 = (uint)(faceVertexOffset + y * (tessellation + 1) + x);
                        uint v1 = (uint)(faceVertexOffset + y * (tessellation + 1) + x + 1);
                        uint v2 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x);
                        uint v3 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x + 1);
                        
                        // Bounds check all vertex indices
                        if (v0 >= maxVertexIndex || v1 >= maxVertexIndex || v2 >= maxVertexIndex || v3 >= maxVertexIndex)
                        {
                            return indexCount;
                        }
                        
                        // First triangle (clockwise winding for outward-facing normals)
                        indices[indexCount++] = v0;
                        indices[indexCount++] = v1;
                        indices[indexCount++] = v2;
                        
                        // Second triangle (clockwise winding for outward-facing normals)
                        indices[indexCount++] = v1;
                        indices[indexCount++] = v3;
                        indices[indexCount++] = v2;
                    }
                }
            }
            
            return indexCount;
        }

        /// <summary>
        /// Generates sphere vertex positions using the same layout as cube vertices
        /// This ensures vertex/index compatibility for interpolation
        /// </summary>
        private void GenerateSphereFromCubeLayout(int tessellation, float radius, 
            NativeArray<float3> cubePositions, NativeArray<float3> spherePositions, 
            NativeArray<float3> sphereNormals, NativeArray<float2> sphereUVs)
        {
            // For each cube vertex, project it onto the sphere surface
            for (int i = 0; i < cubePositions.Length; i++)
            {
                // Get the cube position
                float3 cubePos = cubePositions[i];
                
                // Normalize and scale to sphere radius
                float3 direction = math.normalize(cubePos);
                spherePositions[i] = direction * radius;
                sphereNormals[i] = direction; // Normal is same as direction for sphere
                
                // Keep same UV coordinates as cube for consistency
                sphereUVs[i] = new float2(
                    (direction.x + 1.0f) * 0.5f,
                    (direction.y + 1.0f) * 0.5f
                );
            }
        }
    }
}