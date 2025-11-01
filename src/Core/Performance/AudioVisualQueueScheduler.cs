using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// High-performance audio-visual queue scheduling system for TiXL
    /// 
    /// Features:
    /// - Audio Event Queue for low-latency audio-driven updates
    /// - Visual Parameter Batching for efficient updates
    /// - Frame-Coherent Updates ensuring synchronization
    /// - Priority Handling for high-frequency audio events
    /// - Performance Monitoring and optimization
    /// - Seamless integration with existing rendering systems
    /// </summary>
    public class AudioVisualQueueScheduler : IDisposable
    {
        private readonly AudioEventQueue _audioEventQueue;
        private readonly VisualParameterBatch _visualParameterBatch;
        private readonly FrameCoherentUpdater _frameUpdater;
        private readonly LatencyOptimizer _latencyOptimizer;
        private readonly PerformanceMonitor _performanceMonitor;
        
        private readonly int _targetFrameRate;
        private readonly double _targetFrameTimeMs;
        private readonly Timer _schedulingTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Queue management
        private readonly int _maxQueueDepth;
        private readonly int _batchSize;
        
        public event EventHandler<AudioVisualSyncEventArgs> SyncEvent;
        public event EventHandler<PerformanceMetricsEventArgs> PerformanceMetrics;
        public event EventHandler<QueueStatusEventArgs> QueueStatusChanged;
        
        public int PendingAudioEvents => _audioEventQueue.PendingCount;
        public int PendingVisualUpdates => _visualParameterBatch.PendingCount;
        public double AverageLatencyMs => _performanceMonitor.GetAverageLatency();
        public double FrameTimeConsistency => _performanceMonitor.GetFrameTimeConsistency();
        
        public AudioVisualQueueScheduler(
            int targetFrameRate = 60,
            int maxQueueDepth = 1000,
            int batchSize = 32)
        {
            _targetFrameRate = targetFrameRate;
            _targetFrameTimeMs = 1000.0 / targetFrameRate;
            _maxQueueDepth = maxQueueDepth;
            _batchSize = batchSize;
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize core components
            _audioEventQueue = new AudioEventQueue(maxQueueDepth);
            _visualParameterBatch = new VisualParameterBatch(batchSize);
            _frameUpdater = new FrameCoherentUpdater(targetFrameRate);
            _latencyOptimizer = new LatencyOptimizer();
            _performanceMonitor = new PerformanceMonitor();
            
            // Start scheduling timer (higher frequency than target frame rate)
            var timerInterval = Math.Max(1, (int)(1000.0 / (targetFrameRate * 4))); // 4x target frequency
            _schedulingTimer = new Timer(ProcessScheduling, null, timerInterval, timerInterval);
            
            SetupEventHandlers();
        }
        
        /// <summary>
        /// Queue an audio event for processing
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueAudioEvent(AudioEvent audioEvent)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;
                
            _audioEventQueue.TryEnqueue(audioEvent);
            
            // Track performance metrics
            _performanceMonitor.TrackAudioEventLatency(audioEvent.Timestamp);
        }
        
        /// <summary>
        /// Queue a visual parameter update
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueVisualUpdate(VisualParameterUpdate update)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;
                
            _visualParameterBatch.TryEnqueue(update);
        }
        
        /// <summary>
        /// Process a frame (call this from your main render loop)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessFrame()
        {
            var frameStartTime = Stopwatch.GetTimestamp();
            
            try
            {
                // Update frame timing
                _frameUpdater.BeginFrame();
                
                // Get audio events for this frame
                var audioEvents = _audioEventQueue.DequeueForFrame();
                
                // Process audio events and batch visual updates
                ProcessAudioEvents(audioEvents);
                
                // Apply batched visual updates at frame boundary
                ApplyFrameVisualUpdates();
                
                // End frame processing
                _frameUpdater.EndFrame();
                
                // Track performance
                var frameEndTime = Stopwatch.GetTimestamp();
                var frameTimeMs = (frameEndTime - frameStartTime) / (double)Stopwatch.Frequency * 1000;
                
                _performanceMonitor.TrackFrameTime(frameTimeMs);
                _performanceMonitor.TrackAudioVisualSync(_audioEventQueue.PendingCount, _visualParameterBatch.PendingCount);
                
                // Fire performance metrics event
                FirePerformanceMetricsEvent();
            }
            catch (Exception ex)
            {
                // Log error but don't crash the frame
                System.Diagnostics.Debug.WriteLine($"Audio-visual scheduler error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get current performance statistics
        /// </summary>
        public AudioVisualSchedulerStats GetStatistics()
        {
            return new AudioVisualSchedulerStats
            {
                PendingAudioEvents = _audioEventQueue.PendingCount,
                PendingVisualUpdates = _visualParameterBatch.PendingCount,
                AverageLatencyMs = _performanceMonitor.GetAverageLatency(),
                FrameTimeConsistency = _performanceMonitor.GetFrameTimeConsistency(),
                AudioEventRate = _performanceMonitor.GetAudioEventRate(),
                VisualUpdateRate = _performanceMonitor.GetVisualUpdateRate(),
                QueueDepth = _audioEventQueue.PendingCount + _visualParameterBatch.PendingCount,
                MaxQueueDepth = _maxQueueDepth,
                TargetFrameRate = _targetFrameRate,
                CurrentFrameRate = _performanceMonitor.GetCurrentFrameRate()
            };
        }
        
        /// <summary>
        /// Configure latency optimization parameters
        /// </summary>
        public void ConfigureLatencyOptimization(LatencyOptimizationSettings settings)
        {
            _latencyOptimizer.Configure(settings);
        }
        
        /// <summary>
        /// Force flush all pending updates (use carefully)
        /// </summary>
        public void FlushPendingUpdates()
        {
            _audioEventQueue.Flush();
            _visualParameterBatch.Flush();
            ApplyFrameVisualUpdates();
        }
        
        private void ProcessScheduling(object state)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;
                
            try
            {
                // Process background optimization tasks
                _latencyOptimizer.Optimize(this);
                
                // Fire queue status event
                FireQueueStatusEvent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scheduling error: {ex.Message}");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessAudioEvents(List<AudioEvent> audioEvents)
        {
            if (audioEvents.Count == 0)
                return;
                
            foreach (var audioEvent in audioEvents)
            {
                var visualUpdates = _latencyOptimizer.OptimizeAudioEvent(audioEvent);
                
                foreach (var update in visualUpdates)
                {
                    _visualParameterBatch.TryEnqueue(update);
                }
                
                // Fire sync event
                SyncEvent?.Invoke(this, new AudioVisualSyncEventArgs
                {
                    AudioEvent = audioEvent,
                    VisualUpdatesGenerated = visualUpdates.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyFrameVisualUpdates()
        {
            var updates = _visualParameterBatch.DequeueForFrame();
            
            if (updates.Count > 0)
            {
                foreach (var update in updates)
                {
                    // Apply visual parameter update
                    // In real implementation, this would interface with the rendering system
                    ApplyVisualUpdateInternal(update);
                }
            }
        }
        
        private void ApplyVisualUpdateInternal(VisualParameterUpdate update)
        {
            // Placeholder for actual visual update application
            // This would interface with the shader system, material system, etc.
            _performanceMonitor.TrackVisualUpdateApplication(update);
        }
        
        private void SetupEventHandlers()
        {
            _audioEventQueue.Overflow += OnAudioEventQueueOverflow;
            _visualParameterBatch.Overflow += OnVisualParameterBatchOverflow;
        }
        
        private void OnAudioEventQueueOverflow(object sender, EventArgs e)
        {
            _performanceMonitor.RecordQueueOverflow("AudioEvent");
            QueueStatusChanged?.Invoke(this, new QueueStatusEventArgs
            {
                QueueType = QueueType.AudioEvent,
                IsOverflowing = true,
                PendingCount = _audioEventQueue.PendingCount
            });
        }
        
        private void OnVisualParameterBatchOverflow(object sender, EventArgs e)
        {
            _performanceMonitor.RecordQueueOverflow("VisualParameter");
            QueueStatusChanged?.Invoke(this, new QueueStatusEventArgs
            {
                QueueType = QueueType.VisualParameter,
                IsOverflowing = true,
                PendingCount = _visualParameterBatch.PendingCount
            });
        }
        
        private void FirePerformanceMetricsEvent()
        {
            var stats = GetStatistics();
            PerformanceMetrics?.Invoke(this, new PerformanceMetricsEventArgs
            {
                Stats = stats,
                Timestamp = DateTime.UtcNow
            });
        }
        
        private void FireQueueStatusEvent()
        {
            QueueStatusChanged?.Invoke(this, new QueueStatusEventArgs
            {
                QueueType = QueueType.Both,
                IsOverflowing = _audioEventQueue.IsOverflowing || _visualParameterBatch.IsOverflowing,
                PendingAudioEvents = _audioEventQueue.PendingCount,
                PendingVisualUpdates = _visualParameterBatch.PendingCount
            });
        }
        
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _schedulingTimer?.Dispose();
            _audioEventQueue?.Dispose();
            _visualParameterBatch?.Dispose();
            _frameUpdater?.Dispose();
            _latencyOptimizer?.Dispose();
            _performanceMonitor?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
    
    /// <summary>
    /// High-performance lock-free audio event queue
    /// </summary>
    public class AudioEventQueue : IDisposable
    {
        private readonly ConcurrentQueue<AudioEvent> _queue;
        private readonly int _maxDepth;
        private readonly object _overflowLock = new object();
        
        public int PendingCount => _queue.Count;
        public bool IsOverflowing { get; private set; }
        public event EventHandler Overflow;
        
        public AudioEventQueue(int maxDepth)
        {
            _maxDepth = maxDepth;
            _queue = new ConcurrentQueue<AudioEvent>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(AudioEvent audioEvent)
        {
            if (_queue.Count >= _maxDepth)
            {
                OnOverflow();
                return false;
            }
            
            _queue.Enqueue(audioEvent);
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<AudioEvent> DequeueForFrame()
        {
            var events = new List<AudioEvent>();
            AudioEvent audioEvent;
            
            // Dequeue all events for this frame (up to reasonable limit)
            while (_queue.TryDequeue(out audioEvent) && events.Count < 50) // Limit per frame
            {
                events.Add(audioEvent);
            }
            
            return events;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            while (_queue.TryDequeue(out _))
            {
                // Clear all events
            }
        }
        
        private void OnOverflow()
        {
            IsOverflowing = true;
            Overflow?.Invoke(this, EventArgs.Empty);
            
            // Reset overflow flag after a short delay
            Task.Delay(100).ContinueWith(_ => IsOverflowing = false);
        }
        
        public void Dispose()
        {
            // No special cleanup needed for ConcurrentQueue
        }
    }
    
    /// <summary>
    /// Batching system for visual parameter updates
    /// </summary>
    public class VisualParameterBatch : IDisposable
    {
        private readonly ConcurrentQueue<VisualParameterUpdate> _batchQueue;
        private readonly int _batchSize;
        private readonly object _overflowLock = new object();
        
        public int PendingCount => _batchQueue.Count;
        public bool IsOverflowing { get; private set; }
        public event EventHandler Overflow;
        
        public VisualParameterBatch(int batchSize)
        {
            _batchSize = batchSize;
            _batchQueue = new ConcurrentQueue<VisualParameterUpdate>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(VisualParameterUpdate update)
        {
            if (_batchQueue.Count >= _batchSize)
            {
                OnOverflow();
                return false;
            }
            
            _batchQueue.Enqueue(update);
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<VisualParameterUpdate> DequeueForFrame()
        {
            var updates = new List<VisualParameterUpdate>();
            VisualParameterUpdate update;
            
            // Dequeue all updates for this frame
            while (_batchQueue.TryDequeue(out update))
            {
                updates.Add(update);
            }
            
            return updates;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            while (_batchQueue.TryDequeue(out _))
            {
                // Clear all updates
            }
        }
        
        private void OnOverflow()
        {
            IsOverflowing = true;
            Overflow?.Invoke(this, EventArgs.Empty);
            
            // Reset overflow flag after a short delay
            Task.Delay(100).ContinueWith(_ => IsOverflowing = false);
        }
        
        public void Dispose()
        {
            // No special cleanup needed for ConcurrentQueue
        }
    }
    
    /// <summary>
    /// Frame-coherent update system ensuring synchronization
    /// </summary>
    public class FrameCoherentUpdater : IDisposable
    {
        private readonly int _targetFrameRate;
        private readonly Stopwatch _frameTimer;
        private long _frameCount;
        private double _currentFrameTimeMs;
        
        public long CurrentFrameNumber => _frameCount;
        public double CurrentFrameTimeMs => _currentFrameTimeMs;
        public double TargetFrameTimeMs => 1000.0 / _targetFrameRate;
        
        public FrameCoherentUpdater(int targetFrameRate)
        {
            _targetFrameRate = targetFrameRate;
            _frameTimer = Stopwatch.StartNew();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFrame()
        {
            _currentFrameTimeMs = _frameTimer.Elapsed.TotalMilliseconds;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndFrame()
        {
            _frameCount++;
            
            // Reset for next frame
            _frameTimer.Restart();
        }
        
        public void Dispose()
        {
            _frameTimer?.Stop();
        }
    }
    
    /// <summary>
    /// Latency optimization system for audio-visual synchronization
    /// </summary>
    public class LatencyOptimizer
    {
        private LatencyOptimizationSettings _settings;
        private readonly CircularBuffer<double> _latencyHistory;
        private readonly AudioEventFilter _eventFilter;
        
        public LatencyOptimizer()
        {
            _settings = new LatencyOptimizationSettings();
            _latencyHistory = new CircularBuffer<double>(100);
            _eventFilter = new AudioEventFilter();
        }
        
        public void Configure(LatencyOptimizationSettings settings)
        {
            _settings = settings;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<VisualParameterUpdate> OptimizeAudioEvent(AudioEvent audioEvent)
        {
            var updates = new List<VisualParameterUpdate>();
            
            // Filter audio events based on settings
            if (_eventFilter.ShouldProcessEvent(audioEvent, _settings))
            {
                // Generate visual updates based on audio characteristics
                updates.AddRange(GenerateVisualUpdates(audioEvent));
            }
            
            // Track latency
            var latency = (DateTime.UtcNow - audioEvent.Timestamp).TotalMilliseconds;
            _latencyHistory.Add(latency);
            
            return updates;
        }
        
        public void Optimize(AudioVisualQueueScheduler scheduler)
        {
            // Perform background optimization tasks
            if (_latencyHistory.Count > 0)
            {
                var avgLatency = _latencyHistory.GetAverage();
                
                // Adjust queue sizes based on latency
                if (avgLatency > _settings.TargetLatencyMs * 1.5)
                {
                    // High latency - reduce queue sizes
                    // This would typically involve adjusting internal parameters
                }
                else if (avgLatency < _settings.TargetLatencyMs * 0.7)
                {
                    // Low latency - can potentially increase performance
                    // This would involve aggressive optimization strategies
                }
            }
        }
        
        private List<VisualParameterUpdate> GenerateVisualUpdates(AudioEvent audioEvent)
        {
            var updates = new List<VisualParameterUpdate>();
            
            // Simplified visual update generation based on audio characteristics
            // In real implementation, this would be much more sophisticated
            
            var intensityUpdate = new VisualParameterUpdate
            {
                ParameterName = "GlobalIntensity",
                Value = audioEvent.Intensity,
                Timestamp = audioEvent.Timestamp,
                Priority = audioEvent.Priority
            };
            updates.Add(intensityUpdate);
            
            if (audioEvent.Frequency > 0)
            {
                var frequencyUpdate = new VisualParameterUpdate
                {
                    ParameterName = "ColorFrequency",
                    Value = audioEvent.Frequency,
                    Timestamp = audioEvent.Timestamp,
                    Priority = audioEvent.Priority
                };
                updates.Add(frequencyUpdate);
            }
            
            return updates;
        }
        
        public void Dispose()
        {
            // Cleanup if needed
        }
    }
    
    /// <summary>
    /// Performance monitoring for audio-visual scheduling
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly CircularBuffer<double> _frameTimeHistory;
        private readonly CircularBuffer<double> _latencyHistory;
        private readonly CircularBuffer<double> _audioEventRateHistory;
        private readonly CircularBuffer<double> _visualUpdateRateHistory;
        
        private DateTime _lastAudioEventTime = DateTime.MinValue;
        private DateTime _lastVisualUpdateTime = DateTime.MinValue;
        private int _totalAudioEvents;
        private int _totalVisualUpdates;
        
        public PerformanceMonitor()
        {
            _frameTimeHistory = new CircularBuffer<double>(60); // 1 second at 60fps
            _latencyHistory = new CircularBuffer<double>(100);
            _audioEventRateHistory = new CircularBuffer<double>(60);
            _visualUpdateRateHistory = new CircularBuffer<double>(60);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackFrameTime(double frameTimeMs)
        {
            _frameTimeHistory.Add(frameTimeMs);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackAudioEventLatency(DateTime eventTime)
        {
            var latency = (DateTime.UtcNow - eventTime).TotalMilliseconds;
            _latencyHistory.Add(latency);
            
            // Track event rate
            if (_lastAudioEventTime != DateTime.MinValue)
            {
                var timeSinceLastEvent = (DateTime.UtcNow - _lastAudioEventTime).TotalSeconds;
                if (timeSinceLastEvent > 0)
                {
                    var eventRate = 1.0 / timeSinceLastEvent;
                    _audioEventRateHistory.Add(eventRate);
                }
            }
            
            _lastAudioEventTime = DateTime.UtcNow;
            _totalAudioEvents++;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackVisualUpdateApplication(VisualParameterUpdate update)
        {
            // Track visual update rate
            if (_lastVisualUpdateTime != DateTime.MinValue)
            {
                var timeSinceLastUpdate = (DateTime.UtcNow - _lastVisualUpdateTime).TotalSeconds;
                if (timeSinceLastUpdate > 0)
                {
                    var updateRate = 1.0 / timeSinceLastUpdate;
                    _visualUpdateRateHistory.Add(updateRate);
                }
            }
            
            _lastVisualUpdateTime = DateTime.UtcNow;
            _totalVisualUpdates++;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackAudioVisualSync(int pendingAudioEvents, int pendingVisualUpdates)
        {
            // Track sync metrics
            // This could include queue sizes, sync lag, etc.
        }
        
        public void RecordQueueOverflow(string queueType)
        {
            // Record queue overflow events
            System.Diagnostics.Debug.WriteLine($"Queue overflow detected: {queueType}");
        }
        
        public double GetAverageLatency()
        {
            return _latencyHistory.Count > 0 ? _latencyHistory.GetAverage() : 0;
        }
        
        public double GetFrameTimeConsistency()
        {
            if (_frameTimeHistory.Count < 2) return 1.0;
            
            var frameTimes = _frameTimeHistory.GetRecentFrames(_frameTimeHistory.Count);
            var mean = frameTimes.Average();
            var variance = frameTimes.Select(t => Math.Pow(t - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            // Return consistency score (higher is better)
            return mean / (stdDev > 0 ? stdDev : 1);
        }
        
        public double GetAudioEventRate()
        {
            return _audioEventRateHistory.Count > 0 ? _audioEventRateHistory.GetAverage() : 0;
        }
        
        public double GetVisualUpdateRate()
        {
            return _visualUpdateRateHistory.Count > 0 ? _visualUpdateRateHistory.GetAverage() : 0;
        }
        
        public double GetCurrentFrameRate()
        {
            if (_frameTimeHistory.Count == 0) return 0;
            
            var avgFrameTime = _frameTimeHistory.GetAverage();
            return avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;
        }
    }
    
    /// <summary>
    /// Audio event filter for priority handling
    /// </summary>
    public class AudioEventFilter
    {
        public bool ShouldProcessEvent(AudioEvent audioEvent, LatencyOptimizationSettings settings)
        {
            // Filter based on frequency, intensity, and priority
            if (audioEvent.Frequency < settings.MinFrequencyHz) return false;
            if (audioEvent.Intensity < settings.MinIntensity) return false;
            
            // Priority-based filtering
            switch (audioEvent.Priority)
            {
                case AudioEventPriority.Critical:
                    return true;
                case AudioEventPriority.High:
                    return audioEvent.Intensity >= 0.5 || _isPeakMoment(audioEvent);
                case AudioEventPriority.Normal:
                    return audioEvent.Intensity >= 0.3;
                case AudioEventPriority.Low:
                    return audioEvent.Intensity >= 0.7; // Only process very loud low-priority events
                default:
                    return false;
            }
        }
        
        private bool _isPeakMoment(AudioEvent audioEvent)
        {
            // Simple peak detection - could be more sophisticated
            return audioEvent.Intensity > 0.8;
        }
    }
    
    // Data structures and event args
    
    public class AudioEvent
    {
        public DateTime Timestamp { get; set; }
        public float Intensity { get; set; }
        public float Frequency { get; set; }
        public AudioEventPriority Priority { get; set; } = AudioEventPriority.Normal;
        public AudioEventType Type { get; set; } = AudioEventType.Beat;
        public object Data { get; set; }
    }
    
    public class VisualParameterUpdate
    {
        public string ParameterName { get; set; }
        public float Value { get; set; }
        public DateTime Timestamp { get; set; }
        public AudioEventPriority Priority { get; set; }
        public object Data { get; set; }
    }
    
    public enum AudioEventPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
    
    public enum AudioEventType
    {
        Beat,
        Frequency,
        Volume,
        Custom
    }
    
    public enum QueueType
    {
        AudioEvent,
        VisualParameter,
        Both
    }
    
    public class AudioVisualSchedulerStats
    {
        public int PendingAudioEvents { get; set; }
        public int PendingVisualUpdates { get; set; }
        public double AverageLatencyMs { get; set; }
        public double FrameTimeConsistency { get; set; }
        public double AudioEventRate { get; set; }
        public double VisualUpdateRate { get; set; }
        public int QueueDepth { get; set; }
        public int MaxQueueDepth { get; set; }
        public int TargetFrameRate { get; set; }
        public double CurrentFrameRate { get; set; }
    }
    
    public class LatencyOptimizationSettings
    {
        public double TargetLatencyMs { get; set; } = 16.67; // 60 FPS
        public double MaxLatencyMs { get; set; } = 50.0;
        public float MinIntensity { get; set; } = 0.1f;
        public float MinFrequencyHz { get; set; } = 20.0f;
        public bool EnablePredictiveBatching { get; set; } = true;
        public bool EnablePriorityBoosting { get; set; } = true;
    }
    
    public class AudioVisualSyncEventArgs : EventArgs
    {
        public AudioEvent AudioEvent { get; set; }
        public int VisualUpdatesGenerated { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class PerformanceMetricsEventArgs : EventArgs
    {
        public AudioVisualSchedulerStats Stats { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class QueueStatusEventArgs : EventArgs
    {
        public QueueType QueueType { get; set; }
        public bool IsOverflowing { get; set; }
        public int PendingCount { get; set; }
        public int PendingAudioEvents { get; set; }
        public int PendingVisualUpdates { get; set; }
    }
}