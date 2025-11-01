---
name: Performance Issue
about: Report performance problems, bottlenecks, or regressions in TiXL
title: '[PERF] '
labels: ['performance', 'needs-triage']
assignees: ''
---

<!-- 
Performance issues help us optimize TiXL! This template covers:
- Performance regressions from previous versions
- Memory leaks or excessive memory usage
- CPU-intensive operations
- GPU bottlenecks
- Slow startup or loading times
- Real-time performance issues
-->

## Performance Issue Description

**What performance problem are you experiencing?**

<!--
Clear description of the performance issue:
- Type of performance problem (memory, CPU, GPU, I/O, startup time, etc.)
- When it started occurring
- Impact on user experience
- Specific scenarios where it occurs
-->

## Performance Metrics

### Current Performance

<!-- Provide specific measurements if available -->

- **CPU Usage:** [% or specific values during issue]
- **Memory Usage:** [RAM usage in MB/GB]
- **GPU Usage:** [% GPU utilization, VRAM usage]
- **Frame Rate:** [FPS if rendering-related]
- **Startup Time:** [Time to launch TiXL]
- **Operation Time:** [Time for specific operations]

### Expected Performance

<!-- What performance should be expected -->

- **CPU Usage:** [Normal expected usage]
- **Memory Usage:** [Expected memory usage]
- **Frame Rate:** [Expected FPS]
- **Startup Time:** [Expected launch time]
- **Operation Time:** [Expected operation duration]

## Issue Classification

**What type of performance issue is this?**

- [ ] **Memory Leak** - Gradual memory usage increase over time
- [ ] **Memory Usage** - Excessive memory consumption
- [ ] **CPU Performance** - High CPU usage or slow CPU-bound operations
- [ ] **GPU Performance** - Graphics rendering bottlenecks or slow GPU operations
- [ ] **I/O Performance** - Slow file operations, network operations, or disk usage
- [ ] **Startup Time** - Slow application startup or initialization
- [ ] **Compilation Performance** - Slow shader compilation or operator compilation
- [ ] **Real-time Performance** - Performance issues affecting real-time operations
- [ ] **Performance Regression** - Performance degraded from previous version

### Performance Area

**Which area of TiXL is most affected?**

- [ ] **Core Engine** - Fundamental engine performance
- [ ] **Operator System** - Operator evaluation and execution performance
- [ ] **Graphics Pipeline** - Rendering performance and GPU utilization
- [ ] **User Interface** - UI responsiveness and rendering
- [ ] **File I/O** - Project loading, saving, and resource management
- [ ] **Audio Processing** - Audio-related performance
- [ ] **Animation System** - Animation and keyframe performance
- [ ] **Resource Management** - Memory management and resource cleanup

## Reproduction

### Steps to Reproduce

**What steps lead to this performance issue?**

1. 
2. 
3. 

### Minimal Reproduction

**Can you provide a minimal case that reproduces this issue?**

<!--
- Simple project file that demonstrates the issue
- Specific operator configuration
- Particular workload or scenario
- Minimal set of steps to trigger the problem
-->

### Frequency

**How often does this performance issue occur?**

- [ ] **Always** - Every time under the same conditions
- [ ] **Often** - Frequently, but not always
- [ ] **Sometimes** - Occasional occurrence
- [ ] **Random** - Unpredictable timing

## Environment Information

### Hardware Configuration

- **CPU:** [e.g., Intel i7-12700K, AMD Ryzen 7 5800X]
- **GPU:** [e.g., RTX 3070, GTX 970, Intel UHD 620]
- **RAM:** [e.g., 16GB, 32GB, 64GB]
- **Storage:** [e.g., SSD NVMe, HDD, type of storage drive]

### Software Configuration

- **Operating System:** [e.g., Windows 11, Windows 10]
- **TiXL Version:** [e.g., v4.2.1.0, latest development build]
- **Build Configuration:** [e.g., Release x64, Debug]
- **.NET Version:** [e.g., .NET 9.0.0]
- **Graphics Driver Version:** [e.g., NVIDIA 537.13, AMD 23.5.2]

### Project Details

- **Project Complexity:** [e.g., simple test, complex scene, multiple operators]
- **Operator Count:** [approximate number of operators in node graph]
- **Resource Count:** [textures, models, audio files, etc.]
- **Scene Complexity:** [polygon count, shader complexity, etc.]

## Performance Analysis

### Profiling Data

**Have you profiled the application to identify bottlenecks?**

- [ ] **Yes, using TiXL built-in profiler** - Profiler data attached
- [ ] **Yes, using external profiler** - External profiler data provided
- [ ] **Yes, using Visual Studio profiler** - VS Profiler results attached
- [ ] **Yes, using GPU profiler** - GPU profiler data provided
- [ ] **No profiling done** - Performance observation only

### Performance Tools Used

**What profiling or analysis tools did you use?**

- [ ] **TiXL Performance Monitor** - Built-in performance monitoring
- [ ] **Task Manager/Resource Monitor** - Windows system monitoring
- [ ] **Visual Studio Profiler** - VS built-in profiling tools
- [ ] **dotMemory** - JetBrains memory profiling
- [ ] **RenderDoc** - Graphics debugging and profiling
- [ ] **GPU-Z** - GPU monitoring and statistics
- [ ] **Process Monitor** - File and registry monitoring
- [ ] **Custom Performance Tests** - Created specific test cases

### Bottleneck Identification

**Where do you suspect the performance bottleneck is located?**

- [ ] **Application Startup** - Initialization and loading phase
- [ ] **Operator Evaluation** - Node graph processing
- [ ] **Rendering Pipeline** - Graphics rendering operations
- [ ] **Memory Management** - Allocation and garbage collection
- [ ] **File Operations** - Project loading/saving, resource management
- [ ] **UI Rendering** - User interface drawing and interactions
- [ ] **Resource Loading** - Textures, shaders, models loading
- [ ] **Shader Compilation** - Runtime shader compilation

## Performance Data

### Memory Analysis

<!-- If memory-related issues -->

**Memory Usage Pattern:**
- **Initial Memory:** [MB at startup]
- **Peak Memory:** [MB at maximum usage]
- **Memory Growth Rate:** [MB per minute/hour of operation]
- **Memory Leak Evidence:** [Description of leak pattern]

### CPU Analysis

<!-- If CPU-related issues -->

**CPU Usage Pattern:**
- **Idle CPU Usage:** [CPU % when not active]
- **Peak CPU Usage:** [CPU % during heavy operations]
- **CPU Hotspots:** [Specific operations causing high CPU usage]
- **Thread Usage:** [Number of active threads, thread blocking]

### GPU Analysis

<!-- If GPU-related issues -->

**GPU Performance:**
- **GPU Utilization:** [% GPU usage during rendering]
- **VRAM Usage:** [Graphics memory usage]
- **Frame Time:** [Average and frame time spikes]
- **Shader Performance:** [Compilation time, runtime performance]

### I/O Analysis

<!-- If I/O-related issues -->

**I/O Performance:**
- **Disk Usage:** [Read/write operations per second]
- **Network Usage:** [If network operations involved]
- **File Access Patterns:** [Specific files causing I/O bottlenecks]

## Comparison Data

### Version Comparison

**Does this performance issue exist in previous versions?**

- [ ] **Regression from v** - This is worse than version X.X.X
- [ ] **New in current version** - Issue appeared in current version
- [ ] **Always existed** - Issue has been present for multiple versions
- [ ] **Unsure** - Don't know performance in previous versions

### Baseline Performance

**What was the performance like before the issue?**

<!--
- Previous version performance
- Earlier in same version before recent changes
- Expected performance based on hardware capabilities
- Performance in different configurations
-->

## Impact Assessment

### User Impact

**How does this performance issue affect your workflow?**

- [ ] **Critical** - Makes TiXL unusable or causes crashes
- [ ] **High** - Significantly impacts productivity or user experience
- [ ] **Medium** - Noticeable but manageable impact
- [ ] **Low** - Minor impact, can be worked around

### Workaround

**Are there any workarounds you've found?**

<!--
- Alternative workflows that avoid the issue
- Configuration changes that improve performance
- Hardware upgrades that help
- Usage pattern changes that reduce impact
-->

## Investigation and Fix Attempts

### What You've Tried

**What have you already tried to improve performance?**

- [ ] **Updated graphics drivers**
- [ ] **Cleared TiXL cache and temporary files**
- [ ] **Tried different project configurations**
- [ ] **Tested with minimal project setup**
- [ ] **Updated TiXL to latest version**
- [ ] **Disabled unnecessary operators/features**
- [ ] **Adjusted TiXL performance settings**
- [ ] **Monitored system resources during operation**

### Configuration Changes

**What TiXL or system configuration changes have you made?**

<!--
- Performance settings adjustments
- Graphics settings changes
- Memory management configuration
- Graphics driver settings
- Windows power management settings
-->

## Related Issues

**Are there any related issues, discussions, or known problems?**

<!--
- Similar performance issues reported by others
- Issues with specific operator combinations
- Hardware compatibility issues
- Graphics driver-related performance problems
-->

---

## For Developers

**If you're a TiXL developer investigating this performance issue, please include:**

### Technical Analysis

**Preliminary technical investigation:**

- [ ] Code profiling completed
- [ ] Performance regression analysis
- [ ] Memory leak investigation
- [ ] GPU bottleneck analysis
- [ ] I/O pattern analysis

### Implementation Details

**Relevant code areas or changes that might be affecting performance:**

<!--
- Recent commits that might have introduced regression
- Known performance-critical code areas
- Architecture changes that could impact performance
- Third-party dependency updates
-->

### Testing Environment

**Testing configuration and results:**

- [ ] Performance test suite results
- [ ] Benchmark comparison data
- [ ] Regression test outcomes
- [ ] Memory profiling results

---

**Thank you for helping optimize TiXL!** 🚀

Performance improvements based on real-world usage patterns make TiXL faster and more responsive for everyone.