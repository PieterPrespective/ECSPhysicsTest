using Unity.Entities;
using UnityEngine;

namespace PPTP
{
    /// <summary>
    /// Configuration component for BallBox performance settings
    /// Allows runtime tuning of frame budget and update limits
    /// </summary>
    [CreateAssetMenu(fileName = "BallBoxPerformanceConfig", menuName = "PPTP/Performance Config")]
    public class BallBoxPerformanceConfig : ScriptableObject
    {
        [Header("Mesh Application Limits")]
        [Tooltip("Maximum mesh updates processed per frame")]
        [Range(1, 200)]
        public int maxMeshUpdatesPerFrame = 50;
        
        [Tooltip("Maximum milliseconds to spend on mesh updates per frame")]
        [Range(0.5f, 20.0f)]
        public float maxFrameTimeMs = 8.0f;

        [Header("Change Detection")]
        [Tooltip("Minimum frames between mesh updates for animating entities")]
        [Range(1, 10)]
        public int minFramesBetweenUpdates = 1; // Changed from 2 to 1 for more responsive animation
        
        [Tooltip("Update threshold for shape state changes")]
        [Range(0.001f, 0.1f)]
        public float shapeStateThreshold = 0.001f;

        [Header("Animation Settings")]
        [Tooltip("Force all entities to update every frame (performance intensive)")]
        public bool forceEveryFrameUpdate = false;
        
        [Tooltip("Enable performance logging")]
        public bool enablePerformanceLogging = true;

        /// <summary>
        /// Singleton instance for easy access
        /// </summary>
        private static BallBoxPerformanceConfig instance;
        
        public static BallBoxPerformanceConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<BallBoxPerformanceConfig>("BallBoxPerformanceConfig");
                    if (instance == null)
                    {
                        // Create default instance if none found
                        instance = CreateInstance<BallBoxPerformanceConfig>();
                        Debug.LogWarning("No BallBoxPerformanceConfig found in Resources folder. Using default settings.");
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// Apply high performance settings for stress testing
        /// </summary>
        [ContextMenu("Apply Stress Test Settings")]
        public void ApplyStressTestSettings()
        {
            maxMeshUpdatesPerFrame = 100;
            maxFrameTimeMs = 16.0f; // Full frame budget
            minFramesBetweenUpdates = 1;
            forceEveryFrameUpdate = false; // Still use smart updating
            enablePerformanceLogging = true;
            
            Debug.Log("Applied stress test performance settings");
        }

        /// <summary>
        /// Apply conservative settings for stable performance
        /// </summary>
        [ContextMenu("Apply Conservative Settings")]
        public void ApplyConservativeSettings()
        {
            maxMeshUpdatesPerFrame = 10;
            maxFrameTimeMs = 2.0f;
            minFramesBetweenUpdates = 2;
            forceEveryFrameUpdate = false;
            enablePerformanceLogging = true;
            
            Debug.Log("Applied conservative performance settings");
        }

        /// <summary>
        /// Apply maximum performance settings (use with caution)
        /// </summary>
        [ContextMenu("Apply Maximum Performance Settings")]
        public void ApplyMaximumSettings()
        {
            maxMeshUpdatesPerFrame = 200;
            maxFrameTimeMs = 20.0f;
            minFramesBetweenUpdates = 1;
            forceEveryFrameUpdate = true; // Update everything every frame
            enablePerformanceLogging = true;
            
            Debug.Log("Applied maximum performance settings - use with caution!");
        }
    }

    /// <summary>
    /// Singleton component to hold performance config reference in ECS
    /// </summary>
    public struct BallBoxPerformanceConfigData : IComponentData
    {
        public int MaxMeshUpdatesPerFrame;
        public float MaxFrameTimeMs;
        public int MinFramesBetweenUpdates;
        public float ShapeStateThreshold;
        public bool ForceEveryFrameUpdate;
        public bool EnablePerformanceLogging;
    }
}