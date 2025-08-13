using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using UnityEngine;
using System.Runtime.InteropServices;

namespace PPTP
{
    /// <summary>
    /// System that handles parallel mesh generation for ball-box entities
    /// Generates mesh data in jobs without blocking the main thread
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(BallBoxAlternateSystem))]
    public partial struct BallBoxMeshGenerationSystem : ISystem
    {
        private EntityQuery meshGenerationQuery;
        private ComponentTypeHandle<BallBoxData> ballBoxDataHandle;
        private ComponentTypeHandle<BallBoxMeshUpdateQueue> updateQueueHandle;
        private ComponentTypeHandle<BallBoxMeshGenerationData> generationDataHandle;
        private BufferTypeHandle<BallBoxMeshDataBuffer> meshDataBufferHandle;
        private ComponentTypeHandle<BallBoxChangeTracker> changeTrackerHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BallBoxData, BallBoxMeshUpdateQueue, BallBoxMeshGenerationData>()
                .WithAll<BallBoxMeshDataBuffer>();
            
            meshGenerationQuery = state.GetEntityQuery(queryBuilder);
            
            ballBoxDataHandle = state.GetComponentTypeHandle<BallBoxData>(true);
            updateQueueHandle = state.GetComponentTypeHandle<BallBoxMeshUpdateQueue>(false);
            generationDataHandle = state.GetComponentTypeHandle<BallBoxMeshGenerationData>(false);
            meshDataBufferHandle = state.GetBufferTypeHandle<BallBoxMeshDataBuffer>(false);
            changeTrackerHandle = state.GetComponentTypeHandle<BallBoxChangeTracker>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ballBoxDataHandle.Update(ref state);
            updateQueueHandle.Update(ref state);
            generationDataHandle.Update(ref state);
            meshDataBufferHandle.Update(ref state);
            changeTrackerHandle.Update(ref state);

            var currentFrame = state.World.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // First, update animation states for all entities
            var animationJob = new UpdateBallBoxAnimationJob
            {
                DeltaTime = deltaTime,
                Time = (float)currentFrame
            };
            
            var animationHandle = animationJob.ScheduleParallel(state.Dependency);

            // Check for changes and queue mesh updates
            var changeDetectionJob = new DetectMeshChangesJob
            {
                BallBoxDataHandle = ballBoxDataHandle,
                ChangeTrackerHandle = changeTrackerHandle,
                UpdateQueueHandle = updateQueueHandle,
                CurrentFrame = (uint)state.World.Time.ElapsedTime
            };

            var changeHandle = changeDetectionJob.ScheduleParallel(meshGenerationQuery, animationHandle);

            // Generate mesh data for queued entities in parallel
            var meshGenJob = new ParallelMeshGenerationJob
            {
                BallBoxDataHandle = ballBoxDataHandle,
                UpdateQueueHandle = updateQueueHandle,
                GenerationDataHandle = generationDataHandle,
                MeshDataBufferHandle = meshDataBufferHandle,
                Time = (float)currentFrame
            };

            state.Dependency = meshGenJob.ScheduleParallel(meshGenerationQuery, changeHandle);
        }

        /// <summary>
        /// Job to detect changes in ball-box data and queue mesh updates
        /// </summary>
        [BurstCompile]
        private struct DetectMeshChangesJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<BallBoxData> BallBoxDataHandle;
            public ComponentTypeHandle<BallBoxChangeTracker> ChangeTrackerHandle;
            public ComponentTypeHandle<BallBoxMeshUpdateQueue> UpdateQueueHandle;
            public uint CurrentFrame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ballBoxData = chunk.GetNativeArray(ref BallBoxDataHandle);
                var changeTrackers = chunk.GetNativeArray(ref ChangeTrackerHandle);
                var updateQueues = chunk.GetNativeArray(ref UpdateQueueHandle);

                // Check if the chunk data has changed
                bool hasChanged = chunk.DidChange(ref BallBoxDataHandle, changeTrackers[0].LastBallBoxDataVersion);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var tracker = changeTrackers[i];
                    var queue = updateQueues[i];
                    var data = ballBoxData[i];

                    // Check if we need to update based on animation state or changes
                    bool needsUpdate = hasChanged || 
                                     tracker.FramesSinceUpdate > 0 || // Update every frame during animation for stress testing
                                     math.abs(data.CurrentShapeState - 0.0f) > 0.001f && math.abs(data.CurrentShapeState - 1.0f) > 0.001f; // Animating

                    if (needsUpdate && !queue.HasPendingUpdate)
                    {
                        queue.HasPendingUpdate = true;
                        queue.QueuedFrame = CurrentFrame;
                        
                        // Calculate priority based on visibility and animation state
                        float animationPriority = math.abs(data.CurrentShapeState - 0.5f) * 2.0f; // Higher priority during mid-animation
                        queue.UpdatePriority = animationPriority;
                        
                        updateQueues[i] = queue;
                    }

                    tracker.FramesSinceUpdate++;
                    changeTrackers[i] = tracker;
                }
            }
        }

        /// <summary>
        /// Parallel job for generating mesh data without blocking main thread
        /// </summary>
        [BurstCompile]
        private struct ParallelMeshGenerationJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<BallBoxData> BallBoxDataHandle;
            public ComponentTypeHandle<BallBoxMeshUpdateQueue> UpdateQueueHandle;
            public ComponentTypeHandle<BallBoxMeshGenerationData> GenerationDataHandle;
            public BufferTypeHandle<BallBoxMeshDataBuffer> MeshDataBufferHandle;
            public float Time;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ballBoxData = chunk.GetNativeArray(ref BallBoxDataHandle);
                var updateQueues = chunk.GetNativeArray(ref UpdateQueueHandle);
                var generationData = chunk.GetNativeArray(ref GenerationDataHandle);
                var meshDataBuffers = chunk.GetBufferAccessor(ref MeshDataBufferHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var queue = updateQueues[i];
                    if (!queue.HasPendingUpdate)
                        continue;

                    var data = ballBoxData[i];
                    var genData = generationData[i];
                    var buffer = meshDataBuffers[i];

                    // Calculate required sizes
                    var vertexCount = BallBoxMeshUtility.CalculateCubeVertexCount(data.CubeTessellation);
                    var indexCount = BallBoxMeshUtility.CalculateCubeTriangleCount(data.CubeTessellation) * 3;
                    
                    var vertexSize = Marshal.SizeOf<BallBoxVertex>();
                    var totalVertexDataSize = vertexCount * vertexSize;
                    var totalIndexDataSize = indexCount * sizeof(uint);
                    var totalSize = totalVertexDataSize + totalIndexDataSize;

                    // Resize buffer if needed
                    buffer.Clear();
                    buffer.ResizeUninitialized(totalSize);

                    // Generate mesh data directly into buffer
                    unsafe
                    {
                        var bufferPtr = buffer.GetUnsafePtr();
                        GenerateMeshDataIntoBuffer(data, bufferPtr, vertexCount, indexCount, totalVertexDataSize);
                    }

                    // Update generation metadata
                    genData.VertexCount = vertexCount;
                    genData.IndexCount = indexCount;
                    genData.VertexDataSize = totalVertexDataSize;
                    genData.IndexDataSize = totalIndexDataSize;
                    genData.GenerationVersion++;
                    genData.IsDataReady = true;
                    
                    generationData[i] = genData;

                    // Mark as processed but not yet applied
                    queue.LastProcessedVersion = genData.GenerationVersion;
                    updateQueues[i] = queue;
                }
            }

            [BurstCompile]
            private unsafe void GenerateMeshDataIntoBuffer(in BallBoxData data, void* bufferPtr, 
                int vertexCount, int indexCount, int vertexDataSize)
            {
                // Cast buffer to appropriate types
                var vertices = (BallBoxVertex*)bufferPtr;
                var indices = (uint*)((byte*)bufferPtr + vertexDataSize);

                // Generate cube vertices
                var cubePositions = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var cubeNormals = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var cubeUVs = new NativeArray<float2>(vertexCount, Allocator.Temp);
                
                BallBoxMeshUtility.GenerateCubeVertices(data.CubeSize, data.CubeTessellation, 
                    float3.zero, cubePositions, cubeNormals, cubeUVs);

                // Generate sphere positions
                var spherePositions = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var sphereNormals = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var sphereUVs = new NativeArray<float2>(vertexCount, Allocator.Temp);
                
                GenerateSphereFromCubeLayout(data.CubeTessellation, data.BallSize, 
                    cubePositions, spherePositions, sphereNormals, sphereUVs);

                // Interpolate and write vertices
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new BallBoxVertex
                    {
                        position = math.lerp(cubePositions[i], spherePositions[i], data.CurrentShapeState),
                        normal = math.normalize(math.lerp(cubeNormals[i], sphereNormals[i], data.CurrentShapeState)),
                        uv = math.lerp(cubeUVs[i], sphereUVs[i], data.CurrentShapeState)
                    };
                }

                // Generate indices
                GenerateIndices(data.CubeTessellation, indices, indexCount);

                // Cleanup
                cubePositions.Dispose();
                cubeNormals.Dispose();
                cubeUVs.Dispose();
                spherePositions.Dispose();
                sphereNormals.Dispose();
                sphereUVs.Dispose();
            }

            [BurstCompile]
            private void GenerateSphereFromCubeLayout(int tessellation, float radius,
                NativeArray<float3> cubePositions, NativeArray<float3> spherePositions,
                NativeArray<float3> sphereNormals, NativeArray<float2> sphereUVs)
            {
                for (int i = 0; i < cubePositions.Length; i++)
                {
                    float3 direction = math.normalize(cubePositions[i]);
                    spherePositions[i] = direction * radius;
                    sphereNormals[i] = direction;
                    sphereUVs[i] = new float2(
                        (direction.x + 1.0f) * 0.5f,
                        (direction.y + 1.0f) * 0.5f
                    );
                }
            }

            [BurstCompile]
            private unsafe void GenerateIndices(int tessellation, uint* indices, int maxIndices)
            {
                int indexCount = 0;
                int verticesPerFace = (tessellation + 1) * (tessellation + 1);
                
                for (int face = 0; face < 6 && indexCount < maxIndices - 6; face++)
                {
                    int faceVertexOffset = face * verticesPerFace;
                    
                    for (int y = 0; y < tessellation; y++)
                    {
                        for (int x = 0; x < tessellation; x++)
                        {
                            if (indexCount + 6 > maxIndices)
                                return;
                            
                            uint v0 = (uint)(faceVertexOffset + y * (tessellation + 1) + x);
                            uint v1 = (uint)(faceVertexOffset + y * (tessellation + 1) + x + 1);
                            uint v2 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x);
                            uint v3 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x + 1);
                            
                            indices[indexCount++] = v0;
                            indices[indexCount++] = v1;
                            indices[indexCount++] = v2;
                            indices[indexCount++] = v1;
                            indices[indexCount++] = v3;
                            indices[indexCount++] = v2;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Vertex structure for ball-box meshes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BallBoxVertex
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
}