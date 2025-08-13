using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PPTP
{
    /// <summary>
    /// Component that tracks mesh update queue status for deferred mesh application
    /// </summary>
    public struct BallBoxMeshUpdateQueue : IComponentData
    {
        /// <summary>
        /// Indicates if this entity has a pending mesh update
        /// </summary>
        public bool HasPendingUpdate;
        
        /// <summary>
        /// Priority for mesh update (higher values update first)
        /// </summary>
        public float UpdatePriority;
        
        /// <summary>
        /// Frame number when update was queued
        /// </summary>
        public uint QueuedFrame;
        
        /// <summary>
        /// Version number of the last processed update
        /// </summary>
        public uint LastProcessedVersion;
    }

    /// <summary>
    /// Buffer element that stores generated mesh data for deferred application
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BallBoxMeshDataBuffer : IBufferElementData
    {
        /// <summary>
        /// Raw byte data for mesh vertices/indices
        /// </summary>
        public byte Value;
    }

    /// <summary>
    /// Component that tracks mesh generation metadata
    /// </summary>
    public struct BallBoxMeshGenerationData : IComponentData
    {
        /// <summary>
        /// Number of vertices in the generated mesh
        /// </summary>
        public int VertexCount;
        
        /// <summary>
        /// Number of indices in the generated mesh
        /// </summary>
        public int IndexCount;
        
        /// <summary>
        /// Size of vertex data in bytes
        /// </summary>
        public int VertexDataSize;
        
        /// <summary>
        /// Size of index data in bytes
        /// </summary>
        public int IndexDataSize;
        
        /// <summary>
        /// Version number of this mesh generation
        /// </summary>
        public uint GenerationVersion;
        
        /// <summary>
        /// Indicates if mesh data is ready for application
        /// </summary>
        public bool IsDataReady;
    }

    /// <summary>
    /// Component for tracking component version changes
    /// </summary>
    public struct BallBoxChangeTracker : IComponentData
    {
        /// <summary>
        /// Last known version of BallBoxData component
        /// </summary>
        public uint LastBallBoxDataVersion;
        
        /// <summary>
        /// Last time the mesh was actually updated
        /// </summary>
        public float LastMeshUpdateTime;
        
        /// <summary>
        /// Number of frames since last update
        /// </summary>
        public uint FramesSinceUpdate;
    }

    /// <summary>
    /// Tag component to mark entities that need immediate mesh updates
    /// </summary>
    public struct BallBoxImmediateMeshUpdate : IComponentData
    {
    }

    /// <summary>
    /// System state component for managing mesh update batches
    /// </summary>
    public struct BallBoxMeshBatchState : ICleanupComponentData
    {
        /// <summary>
        /// Current batch index for round-robin processing
        /// </summary>
        public int CurrentBatchIndex;
        
        /// <summary>
        /// Total number of batches
        /// </summary>
        public int TotalBatches;
        
        /// <summary>
        /// Maximum updates per frame
        /// </summary>
        public int MaxUpdatesPerFrame;
    }
}