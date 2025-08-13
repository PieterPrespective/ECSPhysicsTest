using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Unity.Entities.Graphics;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace Mandelbrot3D
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MeshPoolSystem : SystemBase
    {
        private const int MAX_MESHES = 10;
        private Mesh[] meshPool;
        private BatchMeshID[] registeredMeshIDs;
        private NativeArray<bool> meshInUse;
        private EntitiesGraphicsSystem entitiesGraphicsSystem;
        private BatchMaterialID defaultMaterialID;
        
        protected override void OnCreate()
        {
            meshPool = new Mesh[MAX_MESHES];
            registeredMeshIDs = new BatchMeshID[MAX_MESHES];
            meshInUse = new NativeArray<bool>(MAX_MESHES, Allocator.Persistent);
            
            entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            defaultMaterialID = BatchMaterialID.Null;
            
            // Initialize all meshes as unregistered first
            for (int i = 0; i < MAX_MESHES; i++)
            {
                meshPool[i] = new Mesh();
                meshPool[i].name = $"MandelbrotMesh_{i}";
                meshPool[i].MarkDynamic(); // Optimize for frequent updates
                
                // Set initial mesh structure
                InitializeMesh(meshPool[i], 16); // Start with 16x16 resolution for performance
                
                registeredMeshIDs[i] = BatchMeshID.Null;
                meshInUse[i] = false;
            }
            
            // Don't register meshes immediately - wait until they're needed
            UnityEngine.Debug.Log("MeshPoolSystem created - meshes will be registered when needed");
            
            // Only run this system when there are entities with MandelbrotData
            RequireForUpdate<MandelbrotData>();
        }
        
        private void RegisterMaterial()
        {
            if (defaultMaterialID != BatchMaterialID.Null || entitiesGraphicsSystem == null)
                return;
                
            Material defaultMaterial = CreateDefaultMaterial();
            if (defaultMaterial != null)
            {
                try
                {
                    defaultMaterialID = entitiesGraphicsSystem.RegisterMaterial(defaultMaterial);
                    UnityEngine.Debug.Log($"Successfully registered material with ID: {defaultMaterialID.value}");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to register material: {e.Message}");
                }
            }
        }
        
        private Material CreateDefaultMaterial()
        {
            // Try to find available shaders in order of preference
            Shader shader = null;
            
            // Try URP shaders first
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            
            // Fallback to built-in shaders
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            
            // Last resort - try to get any shader
            if (shader == null)
            {
                var allShaders = Resources.FindObjectsOfTypeAll<Shader>();
                if (allShaders.Length > 0)
                {
                    shader = allShaders[0];
                    UnityEngine.Debug.LogWarning($"Using fallback shader: {shader.name}");
                }
            }
            
            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "MandelbrotDefaultMaterial";
                material.color = UnityEngine.Color.cyan; // Make it visible
                return material;
            }
            
            UnityEngine.Debug.LogError("No shader found for material creation");
            return null;
        }
        
        private void RegisterMeshes()
        {
            for (int i = 0; i < MAX_MESHES; i++)
            {
                if (registeredMeshIDs[i] == BatchMeshID.Null && meshPool[i] != null)
                {
                    try
                    {
                        registeredMeshIDs[i] = entitiesGraphicsSystem.RegisterMesh(meshPool[i]);
                        UnityEngine.Debug.Log($"Successfully registered mesh {i}");
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError($"Failed to register mesh {i}: {e.Message}");
                        registeredMeshIDs[i] = BatchMeshID.Null;
                    }
                }
            }
        }

        protected override void OnDestroy()
        {
            if (meshInUse.IsCreated)
            {
                meshInUse.Dispose();
            }
            
            if (meshPool != null)
            {
                for (int i = 0; i < meshPool.Length; i++)
                {
                    if (meshPool[i] != null)
                    {
                        Object.DestroyImmediate(meshPool[i]);
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            // Debug: Check if EntitiesGraphicsSystem is available
            if (entitiesGraphicsSystem == null)
            {
                entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (entitiesGraphicsSystem == null)
                {
                    UnityEngine.Debug.LogWarning("EntitiesGraphicsSystem still not found");
                    return;
                }
            }

            // Debug: Check total entity count with required components
            var totalEntities = 0;
            var entitiesWithMeshIndex = 0;
            
            Entities.WithAll<MandelbrotData, MeshUpdateRequest>().ForEach((Entity entity) =>
            {
                totalEntities++;
                if (EntityManager.HasComponent<MeshPoolIndex>(entity))
                {
                    entitiesWithMeshIndex++;
                }
            }).WithoutBurst().Run();
            
            UnityEngine.Debug.Log($"Total Mandelbrot entities: {totalEntities}, with MeshPoolIndex: {entitiesWithMeshIndex}");

            // Handle mesh allocation requests
            var entityCount = 0;
            Entities
                .WithNone<MeshPoolIndex>()
                .WithAll<MandelbrotData, MeshUpdateRequest>()
                .ForEach((Entity entity, in MandelbrotData mandelbrotData) =>
                {
                    entityCount++;
                    UnityEngine.Debug.Log($"Found Mandelbrot entity {entity.Index}, processing mesh allocation");
                    
                    int availableIndex = GetAvailableMeshIndex();
                    if (availableIndex >= 0)
                    {
                        EntityManager.AddComponentData(entity, new MeshPoolIndex { Value = availableIndex });
                        meshInUse[availableIndex] = true;
                        
                        UnityEngine.Debug.Log($"Assigned mesh pool index {availableIndex} to entity {entity.Index}");
                        
                        // Force registration right now if needed
                        if (registeredMeshIDs[availableIndex].value == 0 && entitiesGraphicsSystem != null && meshPool[availableIndex] != null)
                        {
                            UnityEngine.Debug.Log($"Force registering mesh {availableIndex}");
                            try
                            {
                                registeredMeshIDs[availableIndex] = entitiesGraphicsSystem.RegisterMesh(meshPool[availableIndex]);
                                UnityEngine.Debug.Log($"Force registered mesh {availableIndex} with ID: {registeredMeshIDs[availableIndex].value}");
                            }
                            catch (System.Exception e)
                            {
                                UnityEngine.Debug.LogError($"Force mesh registration failed: {e.Message}");
                            }
                        }
                        
                        if (defaultMaterialID.value == 0 && entitiesGraphicsSystem != null)
                        {
                            UnityEngine.Debug.Log("Force registering material");
                            RegisterMaterial();
                        }
                        
                        // Set up runtime rendering components
                        UnityEngine.Debug.Log($"After forced registration - MeshID value={registeredMeshIDs[availableIndex].value}, MaterialID value={defaultMaterialID.value}");
                        
                        if (registeredMeshIDs[availableIndex].value != 0 && defaultMaterialID.value != 0)
                        {
                            // Validate IDs are positive
                            int meshID = (int)registeredMeshIDs[availableIndex].value;
                            int materialID = (int)defaultMaterialID.value;
                            
                            if (meshID > 0 && materialID > 0)
                            {
                                // Use manual component setup - more reliable than RenderMeshUtility for procedural meshes
                                if (!EntityManager.HasComponent<MaterialMeshInfo>(entity))
                                {
                                    EntityManager.AddComponentData(entity, new MaterialMeshInfo
                                    {
                                        Material = materialID,
                                        Mesh = meshID,
                                        SubMesh = 0
                                    });
                                    UnityEngine.Debug.Log($"Added MaterialMeshInfo to entity {entity.Index}: Material={materialID}, Mesh={meshID}");
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.LogError($"Invalid IDs: MeshID={meshID}, MaterialID={materialID}");
                            }
                            
                            // Ensure entity has all required rendering components
                            if (!EntityManager.HasComponent<RenderBounds>(entity))
                            {
                                EntityManager.AddComponentData(entity, new RenderBounds
                                {
                                    Value = new Unity.Mathematics.AABB
                                    {
                                        Center = new float3(0, 0, 0),
                                        Extents = new float3(10, 10, 10) // Large bounds to ensure visibility
                                    }
                                });
                                UnityEngine.Debug.Log($"Added RenderBounds to entity {entity.Index}");
                            }
                            
                            // Ensure entity has LocalToWorld for positioning
                            if (!EntityManager.HasComponent<LocalToWorld>(entity))
                            {
                                EntityManager.AddComponentData(entity, new LocalToWorld
                                {
                                    Value = float4x4.identity
                                });
                                UnityEngine.Debug.Log($"Added LocalToWorld to entity {entity.Index}");
                            }
                            
                            // Add all essential rendering components for Entities Graphics
                            if (!EntityManager.HasComponent<WorldRenderBounds>(entity))
                            {
                                EntityManager.AddComponent<WorldRenderBounds>(entity);
                                UnityEngine.Debug.Log($"Added WorldRenderBounds to entity {entity.Index}");
                            }
                            
                            // Add RenderFilterSettings component (required for rendering)
                            if (!EntityManager.HasComponent<RenderFilterSettings>(entity))
                            {
                                EntityManager.AddSharedComponentManaged(entity, RenderFilterSettings.Default);
                                UnityEngine.Debug.Log($"Added RenderFilterSettings to entity {entity.Index}");
                            }
                            
                            // Note: RenderMeshArray not needed for manual MaterialMeshInfo approach
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"Cannot set up rendering: MeshID.value={registeredMeshIDs[availableIndex].value}, MaterialID.value={defaultMaterialID.value}");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("No available mesh pool index");
                    }
                }).WithStructuralChanges().Run();
                
            if (entityCount == 0)
            {
                UnityEngine.Debug.Log("No Mandelbrot entities without MeshPoolIndex found");
            }
            else
            {
                UnityEngine.Debug.Log($"Processed {entityCount} entities for mesh allocation");
            }
                
            // Try to register meshes if not done yet
            if (entitiesGraphicsSystem != null)
            {
                RegisterMeshes();
                
                // Also ensure material is registered
                if (defaultMaterialID == BatchMaterialID.Null)
                {
                    UnityEngine.Debug.Log("Attempting to register default material...");
                    RegisterMaterial();
                }
            }
            else
            {
                entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                UnityEngine.Debug.Log($"EntitiesGraphicsSystem found: {entitiesGraphicsSystem != null}");
            }
                
            // Register default material if not already done
            if (defaultMaterialID == BatchMaterialID.Null && entitiesGraphicsSystem != null)
            {
                // Create a simple material directly
                Material defaultMaterial = CreateDefaultMaterial();
                
                if (defaultMaterial != null)
                {
                    try
                    {
                        defaultMaterialID = entitiesGraphicsSystem.RegisterMaterial(defaultMaterial);
                        UnityEngine.Debug.Log("Successfully registered default material");
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError($"Failed to register material: {e.Message}");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("Failed to create default material");
                }
            }
        }

        private int GetAvailableMeshIndex()
        {
            for (int i = 0; i < MAX_MESHES; i++)
            {
                if (!meshInUse[i])
                {
                    return i;
                }
            }
            return -1; // No available mesh
        }

        public Mesh GetMesh(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                return meshPool[index];
            }
            return null;
        }

        public BatchMeshID GetMeshID(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                return registeredMeshIDs[index];
            }
            return BatchMeshID.Null;
        }

        public void ReleaseMesh(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                meshInUse[index] = false;
            }
        }

        private void InitializeMesh(Mesh mesh, int resolution)
        {
            // Create a simple valid mesh with at least one triangle
            // This is just a placeholder that will be updated by the mesh generation job
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0.5f, 0, 1),
                new Vector3(0.5f, 1, 0.5f)
            };
            
            Vector3[] normals = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0.5f, 1),
                new Vector2(0.5f, 0.5f)
            };
            
            int[] triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 1,
                1, 3, 2
            };
            
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}