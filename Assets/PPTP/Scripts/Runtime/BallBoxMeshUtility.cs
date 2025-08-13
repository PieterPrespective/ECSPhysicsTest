using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

namespace PPTP
{
    /// <summary>
    /// Utility class containing burst-compiled functions for generating ball and cube meshes
    /// Follows functional programming principles by separating data processing logic from data structures
    /// </summary>
    [BurstCompile]
    public static class BallBoxMeshUtility
    {
        /// <summary>
        /// Generates vertex positions for a sphere with given parameters
        /// </summary>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="tessellation">Number of divisions (longitude and latitude)</param>
        /// <param name="center">Center position of the sphere</param>
        /// <param name="vertices">Output array for vertex positions</param>
        /// <param name="normals">Output array for vertex normals</param>
        /// <param name="uvs">Output array for texture coordinates</param>
        [BurstCompile]
        public static void GenerateSphereVertices(float radius, int tessellation, float3 center,
            NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<float2> uvs)
        {
            int vertexIndex = 0;
            
            // Generate vertices using spherical coordinates
            for (int lat = 0; lat <= tessellation; lat++)
            {
                for (int lon = 0; lon <= tessellation; lon++)
                {
                    if (vertexIndex >= vertices.Length) return;
                    
                    // Convert to spherical coordinates
                    float theta = lat * math.PI / tessellation; // Latitude angle (0 to PI)
                    float phi = lon * 2.0f * math.PI / tessellation; // Longitude angle (0 to 2*PI)
                    
                    // Calculate position on unit sphere
                    float3 unitPosition = new float3(
                        math.sin(theta) * math.cos(phi),
                        math.cos(theta),
                        math.sin(theta) * math.sin(phi)
                    );
                    
                    // Scale and translate
                    vertices[vertexIndex] = center + unitPosition * radius;
                    normals[vertexIndex] = unitPosition; // Normal is same as unit position for sphere
                    uvs[vertexIndex] = new float2((float)lon / tessellation, (float)lat / tessellation);
                    
                    vertexIndex++;
                }
            }
        }

        /// <summary>
        /// Generates triangle indices for a sphere with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions</param>
        /// <param name="indices">Output array for triangle indices</param>
        [BurstCompile]
        public static void GenerateSphereIndices(int tessellation, NativeArray<uint> indices)
        {
            int indexCount = 0;
            
            for (int lat = 0; lat < tessellation; lat++)
            {
                for (int lon = 0; lon < tessellation; lon++)
                {
                    if (indexCount + 6 > indices.Length) return;
                    
                    // Calculate vertex indices for current quad
                    uint v0 = (uint)(lat * (tessellation + 1) + lon);
                    uint v1 = (uint)(lat * (tessellation + 1) + lon + 1);
                    uint v2 = (uint)((lat + 1) * (tessellation + 1) + lon);
                    uint v3 = (uint)((lat + 1) * (tessellation + 1) + lon + 1);
                    
                    // First triangle
                    indices[indexCount++] = v0;
                    indices[indexCount++] = v2;
                    indices[indexCount++] = v1;
                    
                    // Second triangle
                    indices[indexCount++] = v1;
                    indices[indexCount++] = v2;
                    indices[indexCount++] = v3;
                }
            }
        }

        /// <summary>
        /// Generates vertex positions for a cube with given parameters
        /// </summary>
        /// <param name="size">Size of the cube (edge length)</param>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <param name="center">Center position of the cube</param>
        /// <param name="vertices">Output array for vertex positions</param>
        /// <param name="normals">Output array for vertex normals</param>
        /// <param name="uvs">Output array for texture coordinates</param>
        [BurstCompile]
        public static void GenerateCubeVertices(float size, int tessellation, float3 center,
            NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<float2> uvs)
        {
            int vertexIndex = 0;
            float halfSize = size * 0.5f;
            
            // Define cube face directions and their corresponding normals
            var faceDirections = new NativeArray<float3>(6, Allocator.Temp);
            var faceNormals = new NativeArray<float3>(6, Allocator.Temp);
            
            // +X, -X, +Y, -Y, +Z, -Z faces
            faceDirections[0] = new float3(1, 0, 0);  faceNormals[0] = new float3(1, 0, 0);
            faceDirections[1] = new float3(-1, 0, 0); faceNormals[1] = new float3(-1, 0, 0);
            faceDirections[2] = new float3(0, 1, 0);  faceNormals[2] = new float3(0, 1, 0);
            faceDirections[3] = new float3(0, -1, 0); faceNormals[3] = new float3(0, -1, 0);
            faceDirections[4] = new float3(0, 0, 1);  faceNormals[4] = new float3(0, 0, 1);
            faceDirections[5] = new float3(0, 0, -1); faceNormals[5] = new float3(0, 0, -1);
            
            // Generate vertices for each face
            for (int face = 0; face < 6; face++)
            {
                var faceDir = faceDirections[face];
                var normal = faceNormals[face];
                
                // Get tangent and bitangent for this face
                var tangent = math.abs(faceDir.y) > 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                tangent = math.normalize(math.cross(normal, tangent));
                var bitangent = math.cross(normal, tangent);
                
                // Generate grid of vertices for this face
                for (int y = 0; y <= tessellation; y++)
                {
                    for (int x = 0; x <= tessellation; x++)
                    {
                        if (vertexIndex >= vertices.Length) 
                        {
                            faceDirections.Dispose();
                            faceNormals.Dispose();
                            return;
                        }
                        
                        // Calculate position on face
                        float u = (float)x / tessellation - 0.5f; // -0.5 to 0.5
                        float v = (float)y / tessellation - 0.5f; // -0.5 to 0.5
                        
                        float3 localPos = faceDir * halfSize + tangent * u * size + bitangent * v * size;
                        
                        vertices[vertexIndex] = center + localPos;
                        normals[vertexIndex] = normal;
                        uvs[vertexIndex] = new float2((float)x / tessellation, (float)y / tessellation);
                        
                        vertexIndex++;
                    }
                }
            }
            
            faceDirections.Dispose();
            faceNormals.Dispose();
        }

        /// <summary>
        /// Generates triangle indices for a cube with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <param name="indices">Output array for triangle indices</param>
        [BurstCompile]
        public static void GenerateCubeIndices(int tessellation, NativeArray<uint> indices)
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
                        if (indexCount + 6 > indices.Length) return;
                        
                        // Calculate vertex indices for current quad on this face
                        uint v0 = (uint)(faceVertexOffset + y * (tessellation + 1) + x);
                        uint v1 = (uint)(faceVertexOffset + y * (tessellation + 1) + x + 1);
                        uint v2 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x);
                        uint v3 = (uint)(faceVertexOffset + (y + 1) * (tessellation + 1) + x + 1);
                        
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
        }

        /// <summary>
        /// Interpolates between cube and sphere vertex positions
        /// </summary>
        /// <param name="cubeVertices">Array of cube vertex positions</param>
        /// <param name="sphereVertices">Array of sphere vertex positions</param>
        /// <param name="outputVertices">Output array for interpolated positions</param>
        /// <param name="t">Interpolation factor (0 = cube, 1 = sphere)</param>
        [BurstCompile]
        public static void InterpolateVertices(NativeArray<float3> cubeVertices, NativeArray<float3> sphereVertices,
            NativeArray<float3> outputVertices, float t)
        {
            int count = math.min(math.min(cubeVertices.Length, sphereVertices.Length), outputVertices.Length);
            
            for (int i = 0; i < count; i++)
            {
                outputVertices[i] = math.lerp(cubeVertices[i], sphereVertices[i], t);
            }
        }

        /// <summary>
        /// Interpolates between cube and sphere vertex normals and normalizes the result
        /// </summary>
        /// <param name="cubeNormals">Array of cube vertex normals</param>
        /// <param name="sphereNormals">Array of sphere vertex normals</param>
        /// <param name="outputNormals">Output array for interpolated normals</param>
        /// <param name="t">Interpolation factor (0 = cube, 1 = sphere)</param>
        [BurstCompile]
        public static void InterpolateNormals(NativeArray<float3> cubeNormals, NativeArray<float3> sphereNormals,
            NativeArray<float3> outputNormals, float t)
        {
            int count = math.min(math.min(cubeNormals.Length, sphereNormals.Length), outputNormals.Length);
            
            for (int i = 0; i < count; i++)
            {
                // Interpolate and normalize to ensure proper lighting
                float3 interpolatedNormal = math.lerp(cubeNormals[i], sphereNormals[i], t);
                outputNormals[i] = math.normalize(interpolatedNormal);
            }
        }

        /// <summary>
        /// Calculates the total vertex count needed for a sphere with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions</param>
        /// <returns>Total vertex count</returns>
        [BurstCompile]
        public static int CalculateSphereVertexCount(int tessellation)
        {
            return (tessellation + 1) * (tessellation + 1);
        }

        /// <summary>
        /// Calculates the total triangle count needed for a sphere with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions</param>
        /// <returns>Total triangle count</returns>
        [BurstCompile]
        public static int CalculateSphereTriangleCount(int tessellation)
        {
            return tessellation * tessellation * 2;
        }

        /// <summary>
        /// Calculates the total vertex count needed for a cube with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <returns>Total vertex count</returns>
        [BurstCompile]
        public static int CalculateCubeVertexCount(int tessellation)
        {
            return 6 * (tessellation + 1) * (tessellation + 1);
        }

        /// <summary>
        /// Calculates the total triangle count needed for a cube with given tessellation
        /// </summary>
        /// <param name="tessellation">Number of divisions per edge</param>
        /// <returns>Total triangle count</returns>
        [BurstCompile]
        public static int CalculateCubeTriangleCount(int tessellation)
        {
            return 6 * tessellation * tessellation * 2;
        }
    }
}