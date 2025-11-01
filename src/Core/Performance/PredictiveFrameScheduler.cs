using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// Predictive frame scheduling system for maintaining consistent frame times
    /// Uses historical frame data to predict workload and schedule background tasks
    /// </summary>
    public class PredictiveFrameScheduler
    {
        private readonly CircularBuffer<FrameMetrics> _frameHistory;
        private readonly Queue<BackgroundTask> _pendingTasks;
        private readonly HashSet<BackgroundTask> _runningTasks;
        private readonly object _taskLock = new object();
        private readonly int _maxBackgroundTasks;
        private readonly Timer _scheduleTimer;
        
        // Performance targets
        private readonly double _targetFrameTimeMs = 16.67; // 60 FPS
        private readonly double _predictionAlpha = 0.3; // Exponential moving average weight
        private readonly int _historySize = 30;
        
        public event EventHandler<TaskScheduledEventArgs> TaskScheduled;
        public event EventHandler<ScheduleEventArgs> ScheduleUpdated;
        
        public int PendingTaskCount => _pendingTasks.Count;
        public int RunningTaskCount => _runningTasks.Count;
        
        public PredictiveFrameScheduler(int maxBackgroundTasks = 2, int historySize = 30)
        {
            _maxBackgroundTasks = maxBackgroundTasks;
            _historySize = historySize;
            _frameHistory = new CircularBuffer<FrameMetrics>(historySize);
            _pendingTasks = new Queue<BackgroundTask>();
            _runningTasks = new HashSet<BackgroundTask>();
            
            // Start scheduling timer
            _scheduleTimer = new Timer(ProcessScheduling, null, 0, 8); // ~120Hz
        }
        
        /// <summary>
        /// Update frame metrics and adjust scheduling
        /// </summary>
        public void UpdateFrameMetrics(double cpuTimeMs, double gpuTimeMs, double totalTimeMs)
        {
            var metrics = new FrameMetrics
            {
                CpuTime = cpuTimeMs,
                GpuTime = gpuTimeMs,
                TotalTime = totalTimeMs,
                Timestamp = Stopwatch.GetTimestamp()
            };
            
            _frameHistory.Add(metrics);
            
            // Predict next frame workload and adjust scheduling
            var prediction = PredictNextFrameWorkload();
            AdjustScheduling(prediction);
        }
        
        /// <summary>
        /// Queue a background task for execution during idle time
        /// </summary>
        public void QueueBackgroundTask(BackgroundTask task)
        {
            lock (_taskLock)
            {
                task.QueuedAt = DateTime.UtcNow;
                _pendingTasks.Enqueue(task);
            }
            
            TaskScheduled?.Invoke(this, new TaskScheduledEventArgs
            {
                Task = task,
                PendingCount = _pendingTasks.Count
            });
        }
        
        /// <summary>
        /// Get scheduling statistics
        /// </summary>
        public SchedulerStatistics GetStatistics()
        {
            var recentFrames = _frameHistory.GetRecentFrames(10);
            
            return new SchedulerStatistics
            {
                PendingTasks = _pendingTasks.Count,
                RunningTasks = _runningTasks.Count,
                PredictedFrameTime = PredictNextFrameWorkload(),
                RecentFrameTime = recentFrames.Count > 0 ? recentFrames.Average(f => f.TotalTime) : 0,
                FrameTimeVariance = CalculateVariance(recentFrames.Select(f => f.TotalTime)),
                AvailableTimeBudget = CalculateAvailableTimeBudget()
            };
        }
        
        /// <summary>
        /// Clear all pending tasks
        /// </summary>
        public void ClearPendingTasks()
        {
            lock (_taskLock)
            {
                _pendingTasks.Clear();
            }
        }
        
        private WorkloadPrediction PredictNextFrameWorkload()
        {
            var recentFrames = _frameHistory.GetRecentFrames(_historySize);
            if (recentFrames.Count < 5)
            {
                return new WorkloadPrediction
                {
                    PredictedCpuTime = _targetFrameTimeMs * 0.6,
                    PredictedGpuTime = _targetFrameTimeMs * 0.4,
                    Confidence = 0.5
                };
            }
            
            // Use exponential moving average for prediction
            var cpuWeights = GenerateWeights(recentFrames.Count, _predictionAlpha);
            var gpuWeights = GenerateWeights(recentFrames.Count, _predictionAlpha);
            
            var predictedCpuTime = 0.0;
            var predictedGpuTime = 0.0;
            
            for (int i = 0; i < recentFrames.Count; i++)
            {
                var frame = recentFrames[recentFrames.Count - 1 - i]; // Most recent first
                predictedCpuTime += frame.CpuTime * cpuWeights[i];
                predictedGpuTime += frame.GpuTime * gpuWeights[i];
            }
            
            // Add trend analysis
            var trendCpuTime = CalculateTrend(recentFrames.Select(f => f.CpuTime).ToArray());
            var trendGpuTime = CalculateTrend(recentFrames.Select(f => f.GpuTime).ToArray());
            
            // Adjust prediction based on trends
            predictedCpuTime += trendCpuTime * 0.1; // 10% trend influence
            predictedGpuTime += trendGpuTime * 0.1;
            
            var confidence = CalculatePredictionConfidence(recentFrames);
            
            return new WorkloadPrediction
            {
                PredictedCpuTime = Math.Max(0, predictedCpuTime),
                PredictedGpuTime = Math.Max(0, predictedGpuTime),
                Confidence = confidence
            };
        }
        
        private void AdjustScheduling(WorkloadPrediction prediction)
        {
            var availableTime = _targetFrameTimeMs - prediction.PredictedCpuTime - prediction.PredictedGpuTime;
            
            ScheduleUpdated?.Invoke(this, new ScheduleEventArgs
            {
                AvailableTimeBudget = availableTime,
                PredictedWorkload = prediction,
                ShouldScheduleBackgroundWork = availableTime > 2.0 && _pendingTasks.Count > 0
            });
            
            // Schedule background tasks if we have idle time
            if (availableTime > 2.0 && _pendingTasks.Count > 0 && _runningTasks.Count < _maxBackgroundTasks)
            {
                ScheduleBackgroundTasks(availableTime);
            }
        }
        
        private void ScheduleBackgroundTasks(double availableTime)
        {
            lock (_taskLock)
            {
                var scheduledCount = 0;
                
                while (_pendingTasks.Count > 0 && 
                       _runningTasks.Count < _maxBackgroundTasks && 
                       scheduledCount < 3) // Limit tasks per frame
                {
                    var task = _pendingTasks.Dequeue();
                    
                    if (task.EstimatedDuration <= availableTime)
                    {
                        _runningTasks.Add(task);
                        Task.Run(() => ExecuteBackgroundTask(task));
                        
                        scheduledCount++;
                        availableTime -= task.EstimatedDuration;
                    }
                    else
                    {
                        // Task too large for current budget, re-queue
                        _pendingTasks.Enqueue(task);
                        break;
                    }
                }
            }
        }
        
        private async Task ExecuteBackgroundTask(BackgroundTask task)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await task.ExecuteAsync();
            }
            catch (Exception ex)
            {
                // Log exception but don't crash
                System.Diagnostics.Debug.WriteLine($"Background task failed: {task.Name} - {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                
                lock (_taskLock)
                {
                    _runningTasks.Remove(task);
                }
                
                // Update task with actual execution time
                task.ActualDuration = stopwatch.Elapsed.TotalMilliseconds;
                task.CompletedAt = DateTime.UtcNow;
            }
        }
        
        private void ProcessScheduling(object state)
        {
            // Process any housekeeping tasks
            CleanupCompletedTasks();
            
            // Adjust scheduling based on current prediction
            var prediction = PredictNextFrameWorkload();
            AdjustScheduling(prediction);
        }
        
        private void CleanupCompletedTasks()
        {
            lock (_taskLock)
            {
                var completedTasks = _runningTasks.Where(t => t.CompletedAt.HasValue).ToList();
                foreach (var task in completedTasks)
                {
                    _runningTasks.Remove(task);
                }
            }
        }
        
        private double CalculateAvailableTimeBudget()
        {
            var prediction = PredictNextFrameWorkload();
            return _targetFrameTimeMs - prediction.PredictedCpuTime - prediction.PredictedGpuTime;
        }
        
        private static double[] GenerateWeights(int count, double alpha)
        {
            var weights = new double[count];
            var sum = 0.0;
            
            for (int i = 0; i < count; i++)
            {
                weights[i] = Math.Pow(1 - alpha, i);
                sum += weights[i];
            }
            
            // Normalize weights
            for (int i = 0; i < count; i++)
            {
                weights[i] /= sum;
            }
            
            return weights;
        }
        
        private static double CalculateTrend(double[] values)
        {
            if (values.Length < 3) return 0;
            
            // Simple linear trend
            var n = values.Length;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumXX = 0.0;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumXX += i * i;
            }
            
            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            return slope;
        }
        
        private static double CalculatePredictionConfidence(List<FrameMetrics> recentFrames)
        {
            if (recentFrames.Count < 5) return 0.5;
            
            var variance = CalculateVariance(recentFrames.Select(f => f.TotalTime));
            var avgFrameTime = recentFrames.Average(f => f.TotalTime);
            
            // Higher confidence when variance is low relative to mean
            var coefficientOfVariation = Math.Sqrt(variance) / (avgFrameTime + 0.1);
            return Math.Max(0.1, Math.Min(1.0, 1.0 - coefficientOfVariation));
        }
        
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0.0;
            
            var mean = valuesList.Average();
            return valuesList.Sum(x => (x - mean) * (x - mean)) / valuesList.Count;
        }
        
        public void Dispose()
        {
            _scheduleTimer?.Dispose();
            ClearPendingTasks();
        }
    }
    
    /// <summary>
    /// Background task for deferred execution
    /// </summary>
    public class BackgroundTask
    {
        public string Name { get; set; }
        public Func<Task> ExecuteAsync { get; set; }
        public double EstimatedDuration { get; set; } // milliseconds
        public double ActualDuration { get; set; } // milliseconds
        public DateTime QueuedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public bool IsCritical { get; set; }
    }
    
    /// <summary>
    /// Workload prediction for next frame
    /// </summary>
    public class WorkloadPrediction
    {
        public double PredictedCpuTime { get; set; }
        public double PredictedGpuTime { get; set; }
        public double Confidence { get; set; } // 0.0 to 1.0
    }
    
    /// <summary>
    /// Scheduler statistics
    /// </summary>
    public class SchedulerStatistics
    {
        public int PendingTasks { get; set; }
        public int RunningTasks { get; set; }
        public double PredictedFrameTime { get; set; }
        public double RecentFrameTime { get; set; }
        public double FrameTimeVariance { get; set; }
        public double AvailableTimeBudget { get; set; }
    }
    
    /// <summary>
    /// Task priority levels
    /// </summary>
    public enum TaskPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
    
    /// <summary>
    /// Event args for task scheduling
    /// </summary>
    public class TaskScheduledEventArgs : EventArgs
    {
        public BackgroundTask Task { get; set; }
        public int PendingCount { get; set; }
    }
    
    /// <summary>
    /// Event args for schedule updates
    /// </summary>
    public class ScheduleEventArgs : EventArgs
    {
        public double AvailableTimeBudget { get; set; }
        public WorkloadPrediction PredictedWorkload { get; set; }
        public bool ShouldScheduleBackgroundWork { get; set; }
    }
}
