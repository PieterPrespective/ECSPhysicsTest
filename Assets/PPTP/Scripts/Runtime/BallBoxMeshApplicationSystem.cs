using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace PPTP
{
    /// <summary>
    /// System that applies generated mesh data to Unity meshes on the main thread
    /// Uses deferred application with priority queue and frame budget
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BallBoxMeshGenerationSystem))]
    [UpdateBefore(typeof(BallBoxAlternateSystem))]
    public partial class BallBoxMeshApplicationSystem : SystemBase
    {
        private BallBoxMeshPoolSystem meshPoolSystem;
        private EntityQuery pendingMeshUpdatesQuery;
        
        // Configuration
        private const int MaxMeshUpdatesPerFrame = 625; // Increased for stress testing
        private const float MaxFrameTimeMs = 8.0f; // Increased for stress testing - Max milliseconds to spend on mesh updates per frame

        protected override void OnCreate()
        {
            var queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BallBoxMeshUpdateQueue, BallBoxMeshGenerationData, BallBoxMeshPoolIndex>()
                .WithAll<BallBoxMeshDataBuffer, BallBoxChangeTracker>();
            
            pendingMeshUpdatesQuery = GetEntityQuery(queryBuilder);
            
            RequireForUpdate(pendingMeshUpdatesQuery);
        }

        protected override void OnStartRunning()
        {
            meshPoolSystem = World.GetExistingSystemManaged<BallBoxMeshPoolSystem>();
        }

        protected override void OnUpdate()
        {
            if (meshPoolSystem == null)
                return;

            var frameStartTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            var updatesProcessed = 0;

            // Collect entities with pending updates and sort by priority
            var pendingUpdates = new NativeList<MeshUpdateEntry>(Allocator.Temp);
            
            Entities
                .WithAll<BallBoxMeshPoolIndex>()
                .ForEach((Entity entity, 
                    ref BallBoxMeshUpdateQueue queue,
                    ref BallBoxMeshGenerationData genData,
                    ref BallBoxChangeTracker tracker,
                    in BallBoxMeshPoolIndex poolIndex,
                    in DynamicBuffer<BallBoxMeshDataBuffer> meshDataBuffer) =>
                {
                    if (queue.HasPendingUpdate && genData.IsDataReady)
                    {
                        pendingUpdates.Add(new MeshUpdateEntry
                        {
                            Entity = entity,
                            Priority = queue.UpdatePriority,
                            PoolIndex = poolIndex.Value,
                            GenerationVersion = genData.GenerationVersion
                        });
                    }
                }).Run();

            // Sort by priority (highest first)
            pendingUpdates.Sort(new MeshUpdateComparer());

            // Process updates within frame budget
            for (int i = 0; i < pendingUpdates.Length; i++)
            {
                // Check frame time budget
                var elapsedMs = (UnityEngine.Time.realtimeSinceStartupAsDouble - frameStartTime) * 1000.0;
                if (elapsedMs > MaxFrameTimeMs && updatesProcessed > 0)
                    break;

                // Check max updates per frame
                if (updatesProcessed >= MaxMeshUpdatesPerFrame)
                    break;

                var update = pendingUpdates[i];
                
                // Apply mesh update
                if (ApplyMeshUpdate(update.Entity, update.PoolIndex))
                {
                    updatesProcessed++;
                    
                    // Clear the pending update flag
                    var queue = EntityManager.GetComponentData<BallBoxMeshUpdateQueue>(update.Entity);
                    queue.HasPendingUpdate = false;
                    EntityManager.SetComponentData(update.Entity, queue);
                    
                    // Update change tracker
                    var tracker = EntityManager.GetComponentData<BallBoxChangeTracker>(update.Entity);
                    tracker.LastMeshUpdateTime = (float)SystemAPI.Time.ElapsedTime;
                    tracker.FramesSinceUpdate = 0;
                    tracker.LastBallBoxDataVersion = update.GenerationVersion;
                    EntityManager.SetComponentData(update.Entity, tracker);
                    
                    // Clear the data ready flag
                    var genData = EntityManager.GetComponentData<BallBoxMeshGenerationData>(update.Entity);
                    genData.IsDataReady = false;
                    EntityManager.SetComponentData(update.Entity, genData);
                }
            }

            pendingUpdates.Dispose();

            // Log performance metrics in debug mode
            #if UNITY_EDITOR
            if (updatesProcessed > 0)
            {
                //var totalMs = (UnityEngine.Time.realtimeSinceStartupAsDouble - frameStartTime) * 1000.0;
                //Debug.Log($"BallBoxMeshApplication: Processed {updatesProcessed} mesh updates in {totalMs:F2}ms");
            }
            #endif
        }
        
        /// <summary>
        /// Applies mesh data from buffer to actual Unity mesh
        /// </summary>
        private bool ApplyMeshUpdate(Entity entity, int poolIndex)
        {
            var mesh = meshPoolSystem.GetMesh(poolIndex);
            if (mesh == null)
                return false;

            var genData = EntityManager.GetComponentData<BallBoxMeshGenerationData>(entity);
            var meshDataBuffer = EntityManager.GetBuffer<BallBoxMeshDataBuffer>(entity);

            if (meshDataBuffer.Length < genData.VertexDataSize + genData.IndexDataSize)
                return false;

            // Allocate mesh data for update
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            // Set up vertex attributes
            var attributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
            
            meshData.SetVertexBufferParams(genData.VertexCount, attributes);
            meshData.SetIndexBufferParams(genData.IndexCount, IndexFormat.UInt32);
            attributes.Dispose();

            // Copy data from buffer to mesh
            unsafe
            {
                var bufferPtr = meshDataBuffer.GetUnsafeReadOnlyPtr();
                
                // Copy vertex data
                var vertices = meshData.GetVertexData<BallBoxVertex>(0);
                UnsafeUtility.MemCpy(vertices.GetUnsafePtr(), bufferPtr, genData.VertexDataSize);
                
                // Copy index data
                var indices = meshData.GetIndexData<uint>();
                UnsafeUtility.MemCpy(indices.GetUnsafePtr(), 
                    (byte*)bufferPtr + genData.VertexDataSize, genData.IndexDataSize);
            }

            // Set submesh
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, genData.IndexCount, MeshTopology.Triangles));

            // Apply to actual mesh
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            mesh.RecalculateBounds();

            return true;
        }

        /// <summary>
        /// Entry for mesh updates with priority
        /// </summary>
        private struct MeshUpdateEntry
        {
            public Entity Entity;
            public float Priority;
            public int PoolIndex;
            public uint GenerationVersion;
        }

        /// <summary>
        /// Comparer for sorting mesh updates by priority
        /// </summary>
        private struct MeshUpdateComparer : IComparer<MeshUpdateEntry>
        {
            public int Compare(MeshUpdateEntry x, MeshUpdateEntry y)
            {
                return y.Priority.CompareTo(x.Priority); // Higher priority first
            }
        }
    }
}