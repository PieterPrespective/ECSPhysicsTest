using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Unity.Entities.Graphics;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace PPTP
{
    /// <summary>
    /// Mesh pool system for BallBox entities that handles mesh allocation, registration, and rendering component setup
    /// Based on the pattern from Mandelbrot3D.MeshPoolSystem with proper ECS rendering integration
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(BallBoxAlternateSystem))]
    public partial class BallBoxMeshPoolSystem : SystemBase
    {
        private const int MAX_MESHES = 10000;
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
                meshPool[i].name = $"BallBoxMesh_{i}";
                meshPool[i].MarkDynamic(); // Optimize for frequent updates
                
                // Set initial mesh structure
                InitializeMesh(meshPool[i]);
                
                registeredMeshIDs[i] = BatchMeshID.Null;
                meshInUse[i] = false;
            }
            
        }
        
        /// <summary>
        /// Registers the default material with EntitiesGraphicsSystem
        /// </summary>
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
                }
                catch (System.Exception e)
                {
                }
            }
        }
        
        /// <summary>
        /// Creates a default material with proper shader fallback hierarchy
        /// </summary>
        private Material CreateDefaultMaterial()
        {
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
                }
            }
            
            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "BallBoxDefaultMaterial";
                
                // Set up material properties for better visibility and lighting
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", UnityEngine.Color.green);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", UnityEngine.Color.green);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", Texture2D.whiteTexture);
                    
                // Ensure material receives shadows and lighting
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", 0.0f);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.5f);
                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", 0.5f);
                    
                // Enable keywords for lighting if they exist
                if (shader.name.Contains("Universal Render Pipeline"))
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.EnableKeyword("_EMISSION");
                }
                
                return material;
            }
            
            return null;
        }
        
        /// <summary>
        /// Registers all unregistered meshes with EntitiesGraphicsSystem
        /// </summary>
        private void RegisterMeshes()
        {
            for (int i = 0; i < MAX_MESHES; i++)
            {
                if (registeredMeshIDs[i] == BatchMeshID.Null && meshPool[i] != null)
                {
                    try
                    {
                        registeredMeshIDs[i] = entitiesGraphicsSystem.RegisterMesh(meshPool[i]);
                    }
                    catch (System.Exception e)
                    {
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
            // Ensure EntitiesGraphicsSystem is available
            if (entitiesGraphicsSystem == null)
            {
                entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (entitiesGraphicsSystem == null)
                {
                        return;
                }
            }

            // Debug: Check entity counts
            var totalEntities = 0;
            var entitiesWithMeshIndex = 0;
            
            Entities.WithAll<BallBoxData, BallBoxMeshUpdateRequest>().ForEach((Entity entity) =>
            {
                totalEntities++;
                if (EntityManager.HasComponent<BallBoxMeshPoolIndex>(entity))
                {
                    entitiesWithMeshIndex++;
                }
            }).WithoutBurst().Run();
            
            if (totalEntities > 0)
            {
                }

            // Handle mesh allocation requests for new BallBox entities
            var entityCount = 0;
            Entities
                .WithNone<BallBoxMeshPoolIndex>()
                .WithAll<BallBoxData, BallBoxMeshUpdateRequest>()
                .ForEach((Entity entity, in BallBoxData ballBoxData) =>
                {
                    entityCount++;
                    
                    int availableIndex = GetAvailableMeshIndex();
                    if (availableIndex >= 0)
                    {
                        EntityManager.AddComponentData(entity, new BallBoxMeshPoolIndex { Value = availableIndex });
                        
                        
                        // Force registration right now if needed
                        if (registeredMeshIDs[availableIndex].value == 0 && entitiesGraphicsSystem != null && meshPool[availableIndex] != null)
                        {
                            try
                            {
                                registeredMeshIDs[availableIndex] = entitiesGraphicsSystem.RegisterMesh(meshPool[availableIndex]);
                            }
                            catch (System.Exception e)
                            {
                            }
                        }
                        
                        // Check if entity has a custom material reference
                        Material materialToUse = null;
                        if (EntityManager.HasComponent<BallBoxMaterialReference>(entity))
                        {
                            var materialRef = EntityManager.GetComponentData<BallBoxMaterialReference>(entity);
                            materialToUse = materialRef.Material.Value;
                        }
                        
                        // Register material (either custom or default)
                        if (materialToUse != null)
                        {
                            // Register custom material
                            try
                            {
                                var customMaterialID = entitiesGraphicsSystem.RegisterMaterial(materialToUse);
                                
                                // Store the custom material ID for this specific entity
                                if (registeredMeshIDs[availableIndex].value != 0 && customMaterialID.value != 0)
                                {
                                    SetupRenderingComponents(entity, availableIndex, customMaterialID);
                                }
                            }
                            catch (System.Exception e)
                            {
                                // Fall back to default material
                                if (defaultMaterialID.value == 0) RegisterMaterial();
                                if (registeredMeshIDs[availableIndex].value != 0 && defaultMaterialID.value != 0)
                                {
                                    SetupRenderingComponents(entity, availableIndex, defaultMaterialID);
                                }
                            }
                        }
                        else
                        {
                            // Entity doesn't have custom material, use default material
                            if (defaultMaterialID.value == 0 && entitiesGraphicsSystem != null)
                            {
                                // Register default material if not done yet
                                RegisterMaterial();
                            }
                            
                            // Set up rendering components for this entity using default material
                            if (registeredMeshIDs[availableIndex].value != 0 && defaultMaterialID.value != 0)
                            {
                                SetupRenderingComponents(entity, availableIndex, defaultMaterialID);
                            }
                        }
                        
                        // Note: Rendering components are set up in the material registration section above
                    }
                    else
                    {
                    }
                }).WithStructuralChanges().Run();
                
            if (entityCount > 0)
            {
            }
                
            // Try to register meshes and material if not done yet
            if (entitiesGraphicsSystem != null)
            {
                RegisterMeshes();
                
                if (defaultMaterialID == BatchMaterialID.Null)
                {
                    RegisterMaterial();
                }
            }
        }

        /// <summary>
        /// Sets up all required rendering components for a BallBox entity
        /// Following the complete checklist from ECSRenderResolution.md
        /// </summary>
        private void SetupRenderingComponents(Entity entity, int meshIndex, BatchMaterialID materialID)
        {
            // Validate IDs are positive
            int meshID = (int)registeredMeshIDs[meshIndex].value;
            int materialIdValue = (int)materialID.value;
            
            if (meshID <= 0 || materialIdValue <= 0)
            {
                return;
            }

            // 1. MaterialMeshInfo - Links mesh and material IDs to entity
            if (!EntityManager.HasComponent<MaterialMeshInfo>(entity))
            {
                EntityManager.AddComponentData(entity, new MaterialMeshInfo
                {
                    Material = materialIdValue,
                    Mesh = meshID,
                    SubMesh = 0
                });
            }
            
            // 2. RenderBounds - Defines visibility bounds for culling
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
            }
            
            // 3. LocalToWorld - Transform matrix for positioning
            if (!EntityManager.HasComponent<LocalToWorld>(entity))
            {
                EntityManager.AddComponentData(entity, new LocalToWorld
                {
                    Value = float4x4.identity
                });
            }
            
            // 4. WorldRenderBounds - Used by rendering system for world-space culling
            if (!EntityManager.HasComponent<WorldRenderBounds>(entity))
            {
                EntityManager.AddComponent<WorldRenderBounds>(entity);
            }
            
            // 5. RenderFilterSettings - CRITICAL: Prevents "Key: -1" error (must be shared component)
            if (!EntityManager.HasComponent<RenderFilterSettings>(entity))
            {
                EntityManager.AddSharedComponentManaged(entity, RenderFilterSettings.Default);
            }
            
        }

        /// <summary>
        /// Finds an available mesh index in the pool and marks it as used
        /// </summary>
        private int GetAvailableMeshIndex()
        {
            // First, update meshInUse array based on current entity assignments
            UpdateMeshUsageTracking();
            
            for (int i = 0; i < MAX_MESHES; i++)
            {
                if (!meshInUse[i])
                {
                    // Mark as used immediately when allocated
                    meshInUse[i] = true;
                    return i;
                }
            }
            return -1; // No available mesh
        }
        
        /// <summary>
        /// Updates the meshInUse tracking by checking which entities currently have mesh pool indices
        /// </summary>
        private void UpdateMeshUsageTracking()
        {
            // Reset all meshes to unused first
            for (int i = 0; i < MAX_MESHES; i++)
            {
                meshInUse[i] = false;
            }
            
            // Mark meshes as used based on current entity assignments
            Entities.WithAll<BallBoxMeshPoolIndex>().ForEach((Entity entity, in BallBoxMeshPoolIndex meshPoolIndex) =>
            {
                if (meshPoolIndex.Value >= 0 && meshPoolIndex.Value < MAX_MESHES)
                {
                    meshInUse[meshPoolIndex.Value] = true;
                }
            }).WithoutBurst().Run();
        }

        /// <summary>
        /// Requests a mesh from the pool for a new entity
        /// </summary>
        /// <returns>Mesh pool index, or -1 if no meshes available</returns>
        public int RequestMesh()
        {
            return GetAvailableMeshIndex();
        }

        /// <summary>
        /// Gets a mesh from the pool by index
        /// </summary>
        public Mesh GetMesh(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                return meshPool[index];
            }
            return null;
        }

        /// <summary>
        /// Gets a registered mesh ID by index
        /// </summary>
        public BatchMeshID GetMeshID(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                return registeredMeshIDs[index];
            }
            return BatchMeshID.Null;
        }

        /// <summary>
        /// Releases a mesh back to the pool
        /// </summary>
        public void ReleaseMesh(int index)
        {
            if (index >= 0 && index < MAX_MESHES)
            {
                meshInUse[index] = false;
            }
        }

        /// <summary>
        /// Initializes a mesh with a simple placeholder geometry
        /// This will be updated by the BallBoxAlternateSystem mesh generation jobs
        /// </summary>
        private void InitializeMesh(Mesh mesh)
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