using Unity.Entities;
using Unity.Mathematics;

namespace PPTP
{
    /// <summary>
    /// Component data for the ball-box alternating system that controls shape animation settings
    /// </summary>
    public struct BallBoxData : IComponentData
    {
        /// <summary>
        /// Duration in seconds between shape changes
        /// </summary>
        public float ShapeChangeDuration;
        
        /// <summary>
        /// Size/radius of the ball shape
        /// </summary>
        public float BallSize;
        
        /// <summary>
        /// Number of vertices/tessellation divisions for the ball (higher = smoother sphere)
        /// </summary>
        public int BallTessellation;
        
        /// <summary>
        /// Size of the cube shape (edge length)
        /// </summary>
        public float CubeSize;
        
        /// <summary>
        /// Number of vertices/tessellation divisions per edge for the cube
        /// </summary>
        public int CubeTessellation;
        
        /// <summary>
        /// Current interpolation time (0-1) between shapes
        /// </summary>
        public float InterpolationTime;
        
        /// <summary>
        /// Current shape state: 0 = cube, 1 = ball, values in between are interpolated
        /// </summary>
        public float CurrentShapeState;
        
        /// <summary>
        /// Direction of animation: 1 = towards ball, -1 = towards cube
        /// </summary>
        public int AnimationDirection;
    }

    /// <summary>
    /// Component to track mesh update requests for ball-box entities
    /// </summary>
    public struct BallBoxMeshUpdateRequest : IComponentData
    {
        /// <summary>
        /// Total vertex count needed for the current shape
        /// </summary>
        public int VertexCount;
        
        /// <summary>
        /// Total triangle count for the current shape
        /// </summary>
        public int TriangleCount;
        
        /// <summary>
        /// Last update time
        /// </summary>
        public float Time;
        
        /// <summary>
        /// Whether the mesh requires an update
        /// </summary>
        public bool RequiresUpdate;
    }

    /// <summary>
    /// Component to reference mesh pool index for ball-box entities
    /// </summary>
    public struct BallBoxMeshPoolIndex : IComponentData
    {
        /// <summary>
        /// Index in the mesh pool system
        /// </summary>
        public int Value;
    }

    /// <summary>
    /// Component to store material reference from authoring for runtime use
    /// </summary>
    public struct BallBoxMaterialReference : IComponentData
    {
        /// <summary>
        /// Reference to the material from authoring component
        /// </summary>
        public UnityObjectRef<UnityEngine.Material> Material;
    }
}