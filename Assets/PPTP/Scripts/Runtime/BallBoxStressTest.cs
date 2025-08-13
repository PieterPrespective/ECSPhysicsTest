using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Entities.Graphics;
using UnityEngine;
using System.Collections.Generic;

namespace PPTP
{
    /// <summary>
    /// MonoBehaviour script for stress testing the BallBox hybrid mesh generation system
    /// Spawns a configurable grid of BallBox entities with random colors and animation offsets
    /// </summary>
    public class BallBoxStressTest : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Number of entities along X axis")]
        [Range(1, 100)]
        public int gridSizeX = 10;
        
        [Tooltip("Number of entities along Z axis")]
        [Range(1, 100)]
        public int gridSizeZ = 10;
        
        [Tooltip("Spacing between entities")]
        [Range(1.0f, 10.0f)]
        public float spacing = 3.0f;

        [Header("BallBox Settings - Shared")]
        [Tooltip("Duration in seconds between shape changes")]
        [Range(0.5f, 10.0f)]
        public float shapeChangeDuration = 2.0f;

        [Tooltip("Size/radius of the ball shape")]
        [Range(0.1f, 5.0f)]
        public float ballSize = 1.0f;
        
        [Tooltip("Number of tessellation divisions for the ball")]
        [Range(4, 32)]
        public int ballTessellation = 16;

        [Tooltip("Size of the cube shape (edge length)")]
        [Range(0.1f, 5.0f)]
        public float cubeSize = 1.0f;
        
        [Tooltip("Number of tessellation divisions per edge for the cube")]
        [Range(1, 16)]
        public int cubeTessellation = 4;

        [Header("Animation Variation")]
        [Tooltip("Maximum random offset for animation timing (in seconds)")]
        [Range(0.0f, 10.0f)]
        public float maxAnimationOffset = 2.0f;

        [Header("Visual Settings")]
        [Tooltip("Base material to use (will be cloned with random colors)")]
        public Material baseMaterial;
        
        [Tooltip("Use random colors for each entity")]
        public bool useRandomColors = true;
        
        [Tooltip("Color saturation for random colors")]
        [Range(0.0f, 1.0f)]
        public float colorSaturation = 0.8f;
        
        [Tooltip("Color brightness for random colors")]
        [Range(0.0f, 1.0f)]
        public float colorBrightness = 1.0f;

        [Header("Performance Monitoring")]
        [Tooltip("Log performance metrics")]
        public bool logPerformanceMetrics = true;
        
        [Tooltip("Update interval for performance logging (seconds)")]
        [Range(1.0f, 10.0f)]
        public float performanceLogInterval = 3.0f;

        [Header("Controls")]
        [Space(10)]
        [Tooltip("Click to spawn the grid")]
        public bool spawnGrid = false;
        
        [Tooltip("Click to clear all spawned entities")]
        public bool clearGrid = false;

        // Runtime data
        private EntityManager entityManager;
        private BallBoxMeshPoolSystem meshPoolSystem;
        private List<Entity> spawnedEntities = new List<Entity>();
        private List<Material> createdMaterials = new List<Material>();
        private float lastPerformanceLogTime;
        private int totalEntitiesSpawned;

        void Start()
        {
            // Get EntityManager
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Get mesh pool system
            meshPoolSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<BallBoxMeshPoolSystem>();
            
            if (meshPoolSystem == null)
            {
                Debug.LogError("BallBoxMeshPoolSystem not found! Make sure the PPTP systems are properly set up.");
            }

            lastPerformanceLogTime = Time.time;
        }

        void Update()
        {
            // Handle spawn button
            if (spawnGrid)
            {
                spawnGrid = false;
                SpawnTestGrid();
            }

            // Handle clear button
            if (clearGrid)
            {
                clearGrid = false;
                ClearTestGrid();
            }

            // Performance logging
            if (logPerformanceMetrics && Time.time - lastPerformanceLogTime > performanceLogInterval)
            {
                LogPerformanceMetrics();
                lastPerformanceLogTime = Time.time;
            }
        }

        /// <summary>
        /// Spawns a grid of BallBox entities for stress testing
        /// </summary>
        void SpawnTestGrid()
        {
            if (entityManager == null || baseMaterial == null)
            {
                Debug.LogError("EntityManager or base material is null. Cannot spawn test grid.");
                return;
            }

            // Clear existing grid first
            ClearTestGrid();

            Debug.Log($"Spawning {gridSizeX}x{gridSizeZ} = {gridSizeX * gridSizeZ} BallBox entities...");
            var spawnStartTime = Time.realtimeSinceStartup;

            // Calculate grid center offset
            float3 gridCenter = new float3(
                (gridSizeX - 1) * spacing * 0.5f,
                0,
                (gridSizeZ - 1) * spacing * 0.5f
            );
            
            Debug.Log($"Grid center: {gridCenter}, Spacing: {spacing}");

            // Spawn entities
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int z = 0; z < gridSizeZ; z++)
                {
                    var entity = CreateBallBoxEntity(x, z, gridCenter);
                    spawnedEntities.Add(entity);
                }
            }

            totalEntitiesSpawned = spawnedEntities.Count;
            var spawnTime = (Time.realtimeSinceStartup - spawnStartTime) * 1000.0;
            
            Debug.Log($"Spawned {totalEntitiesSpawned} entities in {spawnTime:F2}ms ({spawnTime/totalEntitiesSpawned:F2}ms per entity)");
        }

        /// <summary>
        /// Creates a single BallBox entity at the specified grid position
        /// </summary>
        Entity CreateBallBoxEntity(int gridX, int gridZ, float3 gridCenter)
        {
            // Calculate world position - fix the position calculation
            float3 worldPosition = new float3(
                gridX * spacing,
                UnityEngine.Random.Range(-0.1f, 0.1f), // Small Y variation to prevent z-fighting
                gridZ * spacing
            ) - gridCenter;
            
            // Debug log positions for first few entities
            if (gridX < 3 && gridZ < 3)
            {
                Debug.Log($"Entity [{gridX},{gridZ}] position: {worldPosition}");
            }

            // Create entity
            var entity = entityManager.CreateEntity();

            // Add transform components (required for ECS rendering)
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = worldPosition,
                Rotation = quaternion.identity,
                Scale = 1.0f
            });

            // Don't manually add LocalToWorld - let Unity's transform system handle it
            // entityManager.AddComponentData(entity, new LocalToWorld
            // {
            //     Value = float4x4.TRS(worldPosition, quaternion.identity, 1.0f)
            // });

            // Calculate random animation offset
            float animationOffset = UnityEngine.Random.Range(0.0f, maxAnimationOffset);
            float initialInterpolationTime = (animationOffset / shapeChangeDuration) % 1.0f;
            
            // Determine initial animation direction and shape state
            int animationDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
            float currentShapeState = animationDirection > 0 ? initialInterpolationTime : 1.0f - initialInterpolationTime;

            // Add BallBox data with variation
            entityManager.AddComponentData(entity, new BallBoxData
            {
                ShapeChangeDuration = shapeChangeDuration + UnityEngine.Random.Range(-0.2f, 0.2f), // Small variation
                BallSize = ballSize,
                BallTessellation = ballTessellation,
                CubeSize = cubeSize,
                CubeTessellation = cubeTessellation,
                InterpolationTime = initialInterpolationTime,
                CurrentShapeState = currentShapeState,
                AnimationDirection = animationDirection
            });

            // Create material with random color
            Material entityMaterial;
            if (useRandomColors)
            {
                entityMaterial = new Material(baseMaterial);
                var randomColor = Color.HSVToRGB(
                    UnityEngine.Random.Range(0.0f, 1.0f), // Random hue
                    colorSaturation,
                    colorBrightness
                );
                entityMaterial.color = randomColor;
                createdMaterials.Add(entityMaterial);
            }
            else
            {
                entityMaterial = baseMaterial;
            }

            // Request mesh from pool and get mesh pool index
            int meshPoolIndex = -1;
            if (meshPoolSystem != null)
            {
                meshPoolIndex = meshPoolSystem.RequestMesh();
            }

            if (meshPoolIndex >= 0)
            {
                // Add mesh pool index component
                entityManager.AddComponentData(entity, new BallBoxMeshPoolIndex
                {
                    Value = meshPoolIndex
                });

                // Get the mesh from pool for rendering setup
                var mesh = meshPoolSystem.GetMesh(meshPoolIndex);
                
                if (mesh != null)
                {
                    // Add ECS rendering components
                    var renderMeshArray = new RenderMeshArray(new Material[] { entityMaterial }, new Mesh[] { mesh });
                    RenderMeshUtility.AddComponents(
                        entity,
                        entityManager,
                        new RenderMeshDescription
                        {
                            FilterSettings = RenderFilterSettings.Default,
                            LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes,
                        },
                        renderMeshArray,
                        MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                    );
                }
            }

            // Add material reference for the systems
            entityManager.AddComponentData(entity, new BallBoxMaterialReference
            {
                Material = entityMaterial
            });

            // Add mesh update request
            var cubeVertexCount = BallBoxMeshUtility.CalculateCubeVertexCount(cubeTessellation);
            var cubeTriangleCount = BallBoxMeshUtility.CalculateCubeTriangleCount(cubeTessellation);
            
            entityManager.AddComponentData(entity, new BallBoxMeshUpdateRequest
            {
                VertexCount = cubeVertexCount,
                TriangleCount = cubeTriangleCount,
                Time = Time.time + animationOffset,
                RequiresUpdate = true
            });

            // Add render bounds (local bounds, not world bounds)
            var maxSize = math.max(ballSize, cubeSize);
            entityManager.AddComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = float3.zero, // Local center
                    Extents = new float3(maxSize, maxSize, maxSize) // Make it a proper cube
                }
            });

            return entity;
        }

        /// <summary>
        /// Clears all spawned test entities
        /// </summary>
        void ClearTestGrid()
        {
            if (entityManager == null)
                return;

            var clearStartTime = Time.realtimeSinceStartup;

            // Destroy all spawned entities and release mesh pool resources
            foreach (var entity in spawnedEntities)
            {
                if (entityManager.Exists(entity))
                {
                    // Release mesh pool resource if it exists
                    if (meshPoolSystem != null && entityManager.HasComponent<BallBoxMeshPoolIndex>(entity))
                    {
                        var poolIndex = entityManager.GetComponentData<BallBoxMeshPoolIndex>(entity);
                        meshPoolSystem.ReleaseMesh(poolIndex.Value);
                    }
                    
                    entityManager.DestroyEntity(entity);
                }
            }
            spawnedEntities.Clear();

            // Clean up created materials
            foreach (var material in createdMaterials)
            {
                if (material != null)
                {
                    DestroyImmediate(material);
                }
            }
            createdMaterials.Clear();

            var clearTime = (Time.realtimeSinceStartup - clearStartTime) * 1000.0;
            
            if (totalEntitiesSpawned > 0)
            {
                Debug.Log($"Cleared {totalEntitiesSpawned} entities in {clearTime:F2}ms");
                totalEntitiesSpawned = 0;
            }
        }

        /// <summary>
        /// Logs performance metrics for the stress test
        /// </summary>
        void LogPerformanceMetrics()
        {
            if (spawnedEntities.Count == 0)
                return;

            var activeEntities = 0;
            foreach (var entity in spawnedEntities)
            {
                if (entityManager.Exists(entity))
                    activeEntities++;
            }

            var frameTime = Time.deltaTime * 1000.0f;
            var fps = 1.0f / Time.deltaTime;

            Debug.Log($"[BallBoxStressTest] Active Entities: {activeEntities}, Frame Time: {frameTime:F2}ms, FPS: {fps:F1}");

            // Additional mesh pool metrics if available
            if (meshPoolSystem != null)
            {
                // You could add mesh pool statistics here if the system exposes them
            }
        }

        void OnDestroy()
        {
            // Clean up on destroy
            ClearTestGrid();
        }

        void OnValidate()
        {
            // Ensure reasonable limits
            gridSizeX = Mathf.Clamp(gridSizeX, 1, 100);
            gridSizeZ = Mathf.Clamp(gridSizeZ, 1, 100);
            
            // Calculate total entities and warn if excessive
            var totalEntities = gridSizeX * gridSizeZ;
            if (totalEntities > 1000)
            {
                Debug.LogWarning($"Planning to spawn {totalEntities} entities. This may impact performance significantly.");
            }
        }

        // GUI for runtime testing
        void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            var rect = new Rect(10, 10, 300, 140);
            GUI.Box(rect, "BallBox Stress Test");

            var contentRect = new Rect(rect.x + 10, rect.y + 25, rect.width - 20, rect.height - 35);
            
            GUILayout.BeginArea(contentRect);
            GUILayout.Label($"Grid Size: {gridSizeX} x {gridSizeZ} = {gridSizeX * gridSizeZ} entities");
            GUILayout.Label($"Active Entities: {spawnedEntities.Count}");
            GUILayout.Label($"FPS: {(1.0f / Time.deltaTime):F1}");
            
            if (GUILayout.Button("Spawn Grid"))
            {
                SpawnTestGrid();
            }
            
            if (GUILayout.Button("Clear Grid"))
            {
                ClearTestGrid();
            }
            
            GUILayout.EndArea();
        }
    }
}