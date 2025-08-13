using UnityEngine;

namespace PPTP
{
    /// <summary>
    /// Simple test harness that can be added to a GameObject to easily test BallBox performance
    /// Includes preset configurations for different stress test scenarios
    /// </summary>
    public class BallBoxTestHarness : MonoBehaviour
    {
        [Header("Test Presets")]
        [Tooltip("Apply preset test configurations")]
        public TestPreset testPreset = TestPreset.Custom;
        
        [Header("Custom Configuration")]
        [Tooltip("The stress test component (will be auto-found if not set)")]
        public BallBoxStressTest stressTest;

        public enum TestPreset
        {
            Custom,
            Small_5x5,
            Medium_10x10,
            Large_20x20,
            Extreme_50x50,
            UltraExtreme_100x100
        }

        void Start()
        {
            // Auto-find stress test component if not set
            if (stressTest == null)
            {
                stressTest = GetComponent<BallBoxStressTest>();
                if (stressTest == null)
                {
                    stressTest = FindFirstObjectByType<BallBoxStressTest>();
                }
            }

            if (stressTest == null)
            {
                Debug.LogError("No BallBoxStressTest component found! Add one to this GameObject or the scene.");
                return;
            }

            // Apply preset if not custom
            if (testPreset != TestPreset.Custom)
            {
                ApplyTestPreset(testPreset);
            }
        }

        /// <summary>
        /// Apply a predefined test configuration
        /// </summary>
        public void ApplyTestPreset(TestPreset preset)
        {
            if (stressTest == null) return;

            switch (preset)
            {
                case TestPreset.Small_5x5:
                    stressTest.gridSizeX = 5;
                    stressTest.gridSizeZ = 5;
                    stressTest.spacing = 3.0f;
                    stressTest.cubeTessellation = 4;
                    stressTest.ballTessellation = 12;
                    Debug.Log("Applied Small 5x5 preset (25 entities)");
                    break;

                case TestPreset.Medium_10x10:
                    stressTest.gridSizeX = 10;
                    stressTest.gridSizeZ = 10;
                    stressTest.spacing = 3.0f;
                    stressTest.cubeTessellation = 4;
                    stressTest.ballTessellation = 16;
                    Debug.Log("Applied Medium 10x10 preset (100 entities)");
                    break;

                case TestPreset.Large_20x20:
                    stressTest.gridSizeX = 20;
                    stressTest.gridSizeZ = 20;
                    stressTest.spacing = 2.5f;
                    stressTest.cubeTessellation = 3;
                    stressTest.ballTessellation = 12;
                    Debug.Log("Applied Large 20x20 preset (400 entities)");
                    break;

                case TestPreset.Extreme_50x50:
                    stressTest.gridSizeX = 50;
                    stressTest.gridSizeZ = 50;
                    stressTest.spacing = 2.0f;
                    stressTest.cubeTessellation = 2;
                    stressTest.ballTessellation = 8;
                    Debug.Log("Applied Extreme 50x50 preset (2500 entities)");
                    break;

                case TestPreset.UltraExtreme_100x100:
                    stressTest.gridSizeX = 100;
                    stressTest.gridSizeZ = 100;
                    stressTest.spacing = 1.5f;
                    stressTest.cubeTessellation = 1;
                    stressTest.ballTessellation = 6;
                    Debug.Log("Applied Ultra Extreme 100x100 preset (10000 entities) - USE WITH CAUTION!");
                    break;
            }
        }

        /// <summary>
        /// Spawn the current test configuration
        /// </summary>
        [ContextMenu("Spawn Test Grid")]
        public void SpawnTestGrid()
        {
            if (stressTest != null)
            {
                stressTest.spawnGrid = true;
            }
        }

        /// <summary>
        /// Clear all spawned entities
        /// </summary>
        [ContextMenu("Clear Test Grid")]
        public void ClearTestGrid()
        {
            if (stressTest != null)
            {
                stressTest.clearGrid = true;
            }
        }

        void OnValidate()
        {
            // Auto-apply preset when changed in inspector during play mode
            if (Application.isPlaying && testPreset != TestPreset.Custom)
            {
                ApplyTestPreset(testPreset);
            }
        }

        // Simple GUI for quick testing
        void OnGUI()
        {
            if (!Application.isPlaying || stressTest == null)
                return;

            var rect = new Rect(Screen.width - 320, 10, 300, 200);
            GUI.Box(rect, "Test Harness");

            var contentRect = new Rect(rect.x + 10, rect.y + 25, rect.width - 20, rect.height - 35);
            
            GUILayout.BeginArea(contentRect);
            
            GUILayout.Label($"Current Preset: {testPreset}");
            GUILayout.Label($"Grid: {stressTest.gridSizeX}x{stressTest.gridSizeZ}");
            GUILayout.Label($"Total: {stressTest.gridSizeX * stressTest.gridSizeZ} entities");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Small (5x5)"))
            {
                testPreset = TestPreset.Small_5x5;
                ApplyTestPreset(testPreset);
            }
            
            if (GUILayout.Button("Medium (10x10)"))
            {
                testPreset = TestPreset.Medium_10x10;
                ApplyTestPreset(testPreset);
            }
            
            if (GUILayout.Button("Large (20x20)"))
            {
                testPreset = TestPreset.Large_20x20;
                ApplyTestPreset(testPreset);
            }
            
            if (GUILayout.Button("Extreme (50x50)"))
            {
                testPreset = TestPreset.Extreme_50x50;
                ApplyTestPreset(testPreset);
            }
            
            GUILayout.EndArea();
        }
    }
}