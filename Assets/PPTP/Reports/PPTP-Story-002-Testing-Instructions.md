# BallBox Hybrid Mesh System - Testing Instructions

## Overview
Two new testing scripts have been created to stress test the hybrid mesh generation system:

1. **BallBoxStressTest.cs** - Main stress testing component
2. **BallBoxTestHarness.cs** - Easy preset configurations

## How to Set Up Testing

### Step 1: Create Test GameObject
1. In Unity, create an empty GameObject in your scene
2. Name it "BallBox Stress Test"
3. Add both components:
   - `BallBoxStressTest`
   - `BallBoxTestHarness`

### Step 2: Configure Materials
1. Create a material or use an existing one
2. Assign it to the `Base Material` field in `BallBoxStressTest`
3. Enable `Use Random Colors` for better visual distinction

### Step 3: Choose Test Configuration

#### Option A: Use Presets (Recommended)
In the `BallBoxTestHarness` component:
- **Small (5x5)**: 25 entities - Good for initial testing
- **Medium (10x10)**: 100 entities - Standard performance test
- **Large (20x20)**: 400 entities - High load test
- **Extreme (50x50)**: 2,500 entities - Stress test
- **Ultra Extreme (100x100)**: 10,000 entities - ⚠️ Use with caution!

#### Option B: Custom Configuration
In the `BallBoxStressTest` component:
- `Grid Size X/Z`: Number of entities per axis
- `Spacing`: Distance between entities
- `Shape Change Duration`: Animation speed
- `Ball/Cube Size`: Entity dimensions  
- `Tessellation`: Mesh detail level
- `Max Animation Offset`: Randomizes animation timing

## Testing Workflow

### 1. Performance Baseline
- Start with **Small (5x5)** preset
- Click "Spawn Grid" button
- Note FPS and frame time
- Observe smooth animations

### 2. Scale Testing
- Progress through presets: Small → Medium → Large
- Monitor performance at each level
- Note when frame rate begins to drop

### 3. Key Metrics to Watch
- **FPS**: Target >30fps for good performance
- **Frame Time**: Should stay <33ms for 30fps
- **Animation Smoothness**: No stuttering during morph
- **Memory Usage**: Monitor in Unity Profiler

### 4. Stress Limits
- **Expected Performance**:
  - Small (25): 60+ FPS
  - Medium (100): 45+ FPS  
  - Large (400): 30+ FPS
  - Extreme (2500): 15+ FPS

## Features to Test

### Visual Verification
✅ **Random Colors**: Each entity has unique color  
✅ **Animation Offsets**: Entities animate at different phases  
✅ **Smooth Morphing**: Clean transition between cube/sphere  
✅ **Grid Layout**: Evenly spaced in world coordinates

### Performance Features
✅ **Non-blocking**: UI remains responsive during generation  
✅ **Frame Budget**: No frame drops during mesh updates  
✅ **Scalability**: Performance degrades gracefully  
✅ **Memory Efficiency**: No excessive memory allocation

## Controls

### Inspector Controls
- **Spawn Grid**: Creates the entity grid
- **Clear Grid**: Removes all entities
- **Performance Logging**: Enables console metrics

### Runtime GUI
- Two GUI panels show current stats
- Quick preset buttons for easy testing
- Real-time FPS monitoring

### Keyboard Shortcuts (if needed)
You can add custom controls in the `Update()` method:
```csharp
if (Input.GetKeyDown(KeyCode.Space))
    SpawnTestGrid();
if (Input.GetKeyDown(KeyCode.C))
    ClearTestGrid();
```

## Performance Analysis

### Expected Behavior
1. **Spawn Time**: Should be <100ms for 1000 entities
2. **Mesh Generation**: Happens in background jobs
3. **Frame Rate**: Maintains target FPS during animation
4. **Memory**: Minimal garbage collection spikes

### Troubleshooting
- **Low FPS**: Reduce tessellation or grid size
- **Stuttering**: Check Unity Profiler for main thread spikes  
- **No Animation**: Verify BallBoxAlternateSystem is running
- **No Colors**: Ensure base material supports color property

## Comparison Testing

### Before/After Hybrid System
If you have the old system available:
1. Test same grid size with old system
2. Compare FPS and frame time
3. Note main thread blocking in Profiler
4. Document performance improvements

### Expected Improvements
- **30-50% better main thread performance**
- **2-3x better scalability** with entity count
- **Smoother frame rates** during mesh updates
- **No blocking** during mesh generation

## Advanced Testing

### Custom Scenarios
- Mixed tessellation levels per entity
- Different animation speeds
- Varying entity sizes
- Camera culling tests

### Profiler Analysis
1. Open Unity Profiler
2. Enable CPU and Memory profiling
3. Run stress tests
4. Analyze:
   - Main thread time
   - Job system usage
   - Memory allocation patterns
   - Render thread performance

Enjoy testing the new hybrid mesh system!