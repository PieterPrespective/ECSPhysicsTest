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
    /// Refactored system that coordinates ball-box animation with the hybrid mesh generation approach
    /// Now works with BallBoxMeshGenerationSystem and BallBoxMeshApplicationSystem for improved performance
    /// No longer blocks main thread for mesh updates
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BallBoxMeshApplicationSystem))]
    public partial struct BallBoxAlternateSystem : ISystem
    {
        /// <summary>
        /// Query for entities needing component initialization
        /// </summary>
        private EntityQuery uninitializedEntitiesQuery;
        
        /// <summary>
        /// Query for entities with immediate update tags
        /// </summary>
        private EntityQuery immediateUpdateQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Query for entities that need hybrid components added
            var uninitializedBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BallBoxData, BallBoxMeshPoolIndex>()
                .WithNone<BallBoxMeshUpdateQueue, BallBoxMeshGenerationData>();
            uninitializedEntitiesQuery = state.GetEntityQuery(uninitializedBuilder);
            
            // Query for immediate updates
            var immediateBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BallBoxImmediateMeshUpdate, BallBoxMeshUpdateQueue>();
            immediateUpdateQuery = state.GetEntityQuery(immediateBuilder);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            // Initialize new entities with hybrid components
            foreach (var (ballBoxData, poolIndex, entity) in 
                     SystemAPI.Query<RefRO<BallBoxData>, RefRO<BallBoxMeshPoolIndex>>()
                              .WithNone<BallBoxMeshUpdateQueue>()
                              .WithEntityAccess())
            {
                // Add hybrid mesh generation components
                ecb.AddComponent(entity, new BallBoxMeshUpdateQueue
                {
                    HasPendingUpdate = true,
                    UpdatePriority = 1.0f,
                    QueuedFrame = 0,
                    LastProcessedVersion = 0
                });
                
                ecb.AddComponent(entity, new BallBoxMeshGenerationData
                {
                    VertexCount = 0,
                    IndexCount = 0,
                    GenerationVersion = 0,
                    IsDataReady = false
                });
                
                ecb.AddComponent(entity, new BallBoxChangeTracker
                {
                    LastBallBoxDataVersion = 0,
                    LastMeshUpdateTime = 0,
                    FramesSinceUpdate = 0
                });
                
                ecb.AddBuffer<BallBoxMeshDataBuffer>(entity);
            }
            
            // Process immediate update requests
            foreach (var (queue, entity) in 
                     SystemAPI.Query<RefRW<BallBoxMeshUpdateQueue>>()
                              .WithAll<BallBoxImmediateMeshUpdate>()
                              .WithEntityAccess())
            {
                queue.ValueRW.HasPendingUpdate = true;
                queue.ValueRW.UpdatePriority = 10.0f; // High priority for immediate updates
                ecb.RemoveComponent<BallBoxImmediateMeshUpdate>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            
            var time = (float)SystemAPI.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Update animation states only - mesh generation is handled by other systems
            var animationJob = new UpdateBallBoxAnimationJob
            {
                DeltaTime = deltaTime,
                Time = time
            };

            // Schedule animation updates without blocking
            state.Dependency = animationJob.ScheduleParallel(state.Dependency);
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

}