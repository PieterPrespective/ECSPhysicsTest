using Unity.Entities;
using Unity.Mathematics;

namespace Mandelbrot3D
{
    public struct MandelbrotData : IComponentData
    {
        public float3 Center;
        public float Scale;
        public int MaxIterations;
        public float TimeOffset;
        public int Resolution;
        public float AnimationSpeed;
    }

    public struct MeshUpdateRequest : IComponentData
    {
        public int VertexCount;
        public int TriangleCount;
        public float Time;
        public bool RequiresUpdate;
    }

    public struct MeshPoolIndex : IComponentData
    {
        public int Value;
    }

}