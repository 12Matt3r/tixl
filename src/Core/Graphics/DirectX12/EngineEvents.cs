using System;
using TiXL.Core.Performance;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Event args for render frame completion
    /// </summary>
    public class RenderFrameEventArgs : EventArgs
    {
        public ulong FrameId { get; set; }
        public double FrameTime { get; set; }
        public DirectX12RenderingEngineStats Statistics { get; set; }
        public DateTime Timestamp { get; set; }
        
        public double Fps => FrameTime > 0 ? 1000.0 / FrameTime : 0;
        public bool IsHealthy => Statistics?.FramePacing.IsPerformanceHealthy ?? false;
    }
    
    /// <summary>
    /// Event args for engine alerts
    /// </summary>
    public class EngineAlertEventArgs : EventArgs
    {
        public EngineAlertType AlertType { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
        
        public bool IsError => Severity == AlertSeverity.Error || Severity == AlertSeverity.Critical;
        public bool IsWarning => Severity == AlertSeverity.Warning;
    }
    
    /// <summary>
    /// Comprehensive statistics for DirectX 12 rendering engine
    /// </summary>
    public class DirectX12RenderingEngineStats
    {
        public FramePacingStatistics FramePacing { get; set; }
        public ResourceOperationStatistics ResourceManagement { get; set; }
        public GpuProfilerStatistics GpuProfiling { get; set; }
        public FrameAnalysis Performance { get; set; }
        public ulong FrameId { get; set; }
        public bool IsRunning { get; set; }
        public DateTime Timestamp { get; set; }
        
        public bool IsHealthy => 
            Performance?.MeetsPerformanceTarget() == true && 
            FramePacing?.IsPerformanceHealthy == true;
        
        public double OverallFps => Performance?.AverageFps ?? 0;
        public string PerformanceGrade => Performance?.PerformanceGrade ?? "Unknown";
        
        public SystemHealthStatus GetSystemHealth()
        {
            if (!IsRunning) return SystemHealthStatus.Stopped;
            
            if (Performance?.MeetsPerformanceTarget() == true && 
                FramePacing.FrameBudgetComplianceRate > 0.95)
                return SystemHealthStatus.Optimal;
                
            if (Performance?.MeetsPerformanceTarget() == true)
                return SystemHealthStatus.Good;
                
            if (Performance?.AverageFrameTime < 33.33) // 30 FPS
                return SystemHealthStatus.Poor;
                
            return SystemHealthStatus.Critical;
        }
    }
    
    /// <summary>
    /// Types of engine alerts
    /// </summary>
    public enum EngineAlertType
    {
        Initialization,
        InitializationError,
        FrameBudgetExceeded,
        BudgetExceeded,
        GpuSubmissionError,
        FrameEndError,
        RenderLoopError,
        RenderLoopFatalError,
        OptimizationError,
        PerformanceOptimization,
        ResourceOptimization,
        GpuProfilingOptimization,
        FramePacingAlert,
        SyncBreakdown
    }
    
    /// <summary>
    /// System health status enumeration
    /// </summary>
    public enum SystemHealthStatus
    {
        Stopped,
        Optimal,
        Good,
        Poor,
        Critical
    }
    
    /// <summary>
    /// Performance analysis recommendations
    /// </summary>
    public class PerformanceRecommendation
    {
        public string Category { get; set; }
        public string Issue { get; set; }
        public string Recommendation { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Implementation { get; set; }
        
        public bool IsHighPriority => Severity == AlertSeverity.Error || Severity == AlertSeverity.Critical;
    }
    
    /// <summary>
    /// Rendering pipeline analysis
    /// </summary>
    public class RenderingPipelineAnalysis
    {
        public string PipelineStage { get; set; }
        public double AverageDuration { get; set; }
        public double Variance { get; set; }
        public double MinDuration { get; set; }
        public double MaxDuration { get; set; }
        public int CallCount { get; set; }
        public bool IsBottleneck => AverageDuration > 5.0 || Variance > 25.0;
        
        public string PerformanceGrade => CalculateGrade(AverageDuration, Variance);
        
        private string CalculateGrade(double avgDuration, double variance)
        {
            var stdDev = Math.Sqrt(variance);
            
            if (avgDuration < 2.0 && stdDev < 1.0) return "A+";
            if (avgDuration < 3.5 && stdDev < 2.0) return "A";
            if (avgDuration < 5.0 && stdDev < 4.0) return "B";
            if (avgDuration < 7.5 && stdDev < 6.0) return "C";
            return "D";
        }
    }
}