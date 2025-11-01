using System;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Event args for frame budget exceeded situations
    /// </summary>
    public class FrameBudgetExceededEventArgs : EventArgs
    {
        public int InFlightCount { get; set; }
        public int MaxAllowed { get; set; }
        public double FrameTime { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message => $"Frame budget exceeded: {InFlightCount}/{MaxAllowed} frames in flight, {FrameTime:F2}ms frame time";
    }
    
    /// <summary>
    /// Event args for fence signal notifications
    /// </summary>
    public class FenceSignalEventArgs : EventArgs
    {
        public ulong FenceValue { get; set; }
        public DateTime SignalTime { get; set; }
        public double SignalLatencyMs { get; set; }
    }
    
    /// <summary>
    /// Frame pacing alert for real-time issues
    /// </summary>
    public class FramePacingAlert : EventArgs
    {
        public FramePacingAlertType Type { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Types of frame pacing alerts
    /// </summary>
    public enum FramePacingAlertType
    {
        PerformanceAlert,
        FrameEndError,
        GpuSubmitError,
        BudgetSkippedWork,
        BudgetWorkError,
        ResourceOperationError,
        FenceTimeout,
        SyncBreakdown
    }
    
    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
    
    /// <summary>
    /// Statistics for frame pacing performance
    /// </summary>
    public class FramePacingStatistics
    {
        public int InFlightFrameCount { get; set; }
        public int PendingFenceCount { get; set; }
        public double CurrentFrameTime { get; set; }
        public double AverageGpuTime { get; set; }
        public double GpuTimeVariance { get; set; }
        public int GpuTimelineEntryCount { get; set; }
        public int TotalResourceOperationsProcessed { get; set; }
        public double FrameBudgetComplianceRate { get; set; }
        public double AverageFrameTime { get; set; }
        public DateTime Timestamp { get; set; }
        
        public double Fps => AverageFrameTime > 0 ? 1000.0 / AverageFrameTime : 0;
        public bool IsPerformanceHealthy => CurrentFrameTime <= 16.67 && FrameBudgetComplianceRate >= 0.9;
    }
    
    /// <summary>
    /// GPU timeline analysis for performance optimization
    /// </summary>
    public class GpuTimelineAnalysis
    {
        public double AverageGpuTime { get; set; }
        public double GpuTimeVariance { get; set; }
        public double TotalGpuTime { get; set; }
        public int FrameCount { get; set; }
        public Dictionary<string, double> OperationBreakdown { get; set; }
        public DateTime Timestamp { get; set; }
        
        public double Fps => AverageGpuTime > 0 ? 1000.0 / AverageGpuTime : 0;
        public double GpuUtilization => (AverageGpuTime / 16.67) * 100.0; // Assuming 16.67ms target
        public string PerformanceGrade => CalculatePerformanceGrade(AverageGpuTime, GpuTimeVariance);
        
        private string CalculatePerformanceGrade(double avgGpuTime, double variance)
        {
            var stdDev = Math.Sqrt(variance);
            
            if (avgGpuTime < 10.0 && stdDev < 1.0) return "A+";
            if (avgGpuTime < 13.0 && stdDev < 2.0) return "A";
            if (avgGpuTime < 15.0 && stdDev < 3.0) return "B";
            if (avgGpuTime < 16.67 && stdDev < 4.0) return "C";
            return "D";
        }
    }
}