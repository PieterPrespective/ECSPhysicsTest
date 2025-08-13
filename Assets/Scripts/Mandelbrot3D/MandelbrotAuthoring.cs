using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Mandelbrot3D
{
    public class MandelbrotAuthoring : MonoBehaviour
    {
        [Header("Mandelbrot Settings")] 
        public float3 center = float3.zero;
        public float scale = 2.0f;
        public int maxIterations = 50;
        public int resolution = 32;
        public float animationSpeed = 1.0f;
        public float timeOffset = 0.0f;

        [Header("Rendering")]
        public Material material;
        public Mesh fallbackMesh;
    }

    public class MandelbrotBaker : Baker<MandelbrotAuthoring>
    {
        public override void Bake(MandelbrotAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add Mandelbrot components
            AddComponent(entity, new MandelbrotData
            {
                Center = authoring.center,
                Scale = authoring.scale,
                MaxIterations = authoring.maxIterations,
                Resolution = authoring.resolution,
                AnimationSpeed = authoring.animationSpeed,
                TimeOffset = authoring.timeOffset
            });

            AddComponent(entity, new MeshUpdateRequest
            {
                VertexCount = authoring.resolution * authoring.resolution * authoring.resolution,
                TriangleCount = authoring.resolution * authoring.resolution * authoring.resolution * 6,
                Time = 0,
                RequiresUpdate = true
            });

            // Add rendering components 
            // For procedural meshes, we'll need to handle rendering at runtime
            // since the mesh will be generated dynamically
            if (authoring.material != null)
            {
                // Add basic rendering components that will be populated at runtime
                AddComponent<RenderBounds>(entity);
                
                // Note: The actual mesh rendering will be handled by the MeshPoolSystem
                // which will update MaterialMeshInfo at runtime when meshes are generated
            }
        }
    }
}