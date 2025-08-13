using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace PPTP
{
    /// <summary>
    /// Authoring component for the ball-box alternating system
    /// Provides inspector interface for configuring shape animation parameters
    /// </summary>
    public class BallBoxAuthoring : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Duration in seconds between shape changes")]
        [Range(0.5f, 10.0f)]
        public float shapeChangeDuration = 2.0f;

        [Header("Ball Settings")]
        [Tooltip("Size/radius of the ball shape")]
        [Range(0.1f, 5.0f)]
        public float ballSize = 1.0f;
        
        [Tooltip("Number of tessellation divisions for the ball (higher = smoother sphere)")]
        [Range(4, 32)]
        public int ballTessellation = 16;

        [Header("Cube Settings")]
        [Tooltip("Size of the cube shape (edge length)")]
        [Range(0.1f, 5.0f)]
        public float cubeSize = 1.0f;
        
        [Tooltip("Number of tessellation divisions per edge for the cube")]
        [Range(1, 16)]
        public int cubeTessellation = 4;

        [Header("Rendering")]
        [Tooltip("Material to use for rendering the shape")]
        public Material material;
        
        [Tooltip("Fallback mesh (not used in runtime, for reference only)")]
        public Mesh fallbackMesh;
    }

    /// <summary>
    /// Baker that converts BallBoxAuthoring to ECS components during baking process
    /// </summary>
    public class BallBoxBaker : Baker<BallBoxAuthoring>
    {
        /// <summary>
        /// Bakes the authoring component into ECS components
        /// </summary>
        /// <param name="authoring">The authoring component to bake</param>
        public override void Bake(BallBoxAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add ball-box animation components
            AddComponent(entity, new BallBoxData
            {
                ShapeChangeDuration = authoring.shapeChangeDuration,
                BallSize = authoring.ballSize,
                BallTessellation = authoring.ballTessellation,
                CubeSize = authoring.cubeSize,
                CubeTessellation = authoring.cubeTessellation,
                InterpolationTime = 0.0f,
                CurrentShapeState = 0.0f, // Start as cube
                AnimationDirection = 1 // Start animating towards ball
            });

            // Calculate initial mesh requirements based on cube (starting shape)
            var cubeVertexCount = CalculateCubeVertexCount(authoring.cubeTessellation);
            var cubeTriangleCount = CalculateCubeTriangleCount(authoring.cubeTessellation);

            AddComponent(entity, new BallBoxMeshUpdateRequest
            {
                VertexCount = cubeVertexCount,
                TriangleCount = cubeTriangleCount,
                Time = 0,
                RequiresUpdate = true
            });

            // Handle material setup - store material reference for runtime use
            if (authoring.material != null)
            {
                // Store material reference as a component for the mesh pool system to use
                AddComponent(entity, new BallBoxMaterialReference
                {
                    Material = authoring.material
                });
            }
            
            // Add bounds component for culling (will be updated at runtime)
            AddComponent<RenderBounds>(entity);
        }


        /// <summary>
        /// Calculates the vertex count needed for a cube with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <returns>Total vertex count</returns>
        private int CalculateCubeVertexCount(int tessellation)
        {
            // Each face has (tessellation+1)^2 vertices, but vertices are shared
            // For a cube with tessellation, we need to account for shared vertices
            // Simplified calculation: 6 faces * tessellation^2 * 4 vertices per quad
            return 6 * tessellation * tessellation * 4;
        }

        /// <summary>
        /// Calculates the triangle count needed for a cube with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <returns>Total triangle count</returns>
        private int CalculateCubeTriangleCount(int tessellation)
        {
            // Each face has tessellation^2 quads, each quad = 2 triangles
            return 6 * tessellation * tessellation * 2;
        }
    }
}