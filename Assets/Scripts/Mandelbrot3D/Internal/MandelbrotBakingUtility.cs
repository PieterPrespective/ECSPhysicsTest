using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Mandelbrot3D.Internal
{
    /// <summary>
    /// Provides access to internal Unity.Entities.Graphics utilities for baking
    /// </summary>
    public static class MandelbrotBakingUtility
    {
        /// <summary>
        /// Converts a renderer with a single material using the internal MeshRendererBakingUtility
        /// </summary>
        public static void ConvertRenderer<T>(Baker<T> baker, Renderer renderer, Mesh mesh) where T : Component
        {
            // Access the internal MeshRendererBakingUtility
            Unity.Collections.NativeArray<Entity> additionalEntities;
            MeshRendererBakingUtility.ConvertOnPrimaryEntityForSingleMaterial(baker, renderer, mesh, out additionalEntities);
            
            if (additionalEntities.IsCreated)
                additionalEntities.Dispose();
        }
    }
}