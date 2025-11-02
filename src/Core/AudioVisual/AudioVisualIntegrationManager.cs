using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.Logging;
using TiXL.Core.Operators;
using TiXL.Core.Performance;

namespace TiXL.Core.AudioVisual
{
    /// <summary>
    /// Integration module for Audio-Visual Queue Scheduling with existing TiXL systems
    /// 
    /// This module provides:
    /// - Integration with TiXL's EvaluationContext for operator evaluation
    /// - Integration with the existing PerformanceMonitor for unified metrics
    /// - Integration with TiXL's logging system for debugging
    /// - Integration with shader parameter updates for real-time visual effects
    /// - Integration with frame scheduling for optimal timing
    /// </summary>
    public class AudioVisualIntegrationManager : IDisposable
    {
        private readonly AudioVisualQueueScheduler _scheduler;
        private readonly EvaluationContext _evaluationContext;
        private readonly TiXLLogging _logging;
        private readonly Dictionary<string, VisualEffectBinding> _effectBindings;
        private readonly Timer _frameTimer;
        
        public event EventHandler<VisualEffectEventArgs> VisualEffectTriggered;
        public event EventHandler<AudioAnalysisEventArgs> AudioAnalysisCompleted;
        
        public int TargetFrameRate { get; private set; }
        public bool IsRunning { get; private set; }
        public AudioVisualIntegrationStats CurrentStats { get; private set; }
        
        public AudioVisualIntegrationManager(
            EvaluationContext evaluationContext,
            TiXLLogging logging,
            int targetFrameRate = 60)
        {
            _evaluationContext = evaluationContext ?? throw new ArgumentNullException(nameof(evaluationContext));
            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _effectBindings = new Dictionary<string, VisualEffectBinding>();
            TargetFrameRate = targetFrameRate;
            
            _scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: targetFrameRate,
                maxQueueDepth: 2000,
                batchSize: 64);
            
            SetupEventHandlers();
            InitializeIntegration();
        }
        
        /// <summary>
        /// Start the audio-visual synchronization system
        /// </summary>
        public void Start()
        {
            if (IsRunning)
                return;
                
            IsRunning = true;
            
            // Start frame timer
            var frameInterval = 1000 / TargetFrameRate;
            _frameTimer = new Timer(ProcessFrame, null, frameInterval, frameInterval);
            
            _logging.LogInfo("AudioVisualIntegrationManager", "Started audio-visual synchronization system");
            
            // Initialize performance monitoring
            _scheduler.PerformanceMetrics += OnPerformanceMetrics;
        }
        
        /// <summary>
        /// Stop the audio-visual synchronization system
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;
                
            IsRunning = false;
            _frameTimer?.Dispose();
            
            _logging.LogInfo("AudioVisualIntegrationManager", "Stopped audio-visual synchronization system");
        }
        
        /// <summary>
        /// Queue audio analysis results for visual processing
        /// </summary>
        public void QueueAudioAnalysis(AudioAnalysisResult analysis)
        {
            if (!IsRunning || analysis == null)
                return;
                
            // Convert audio analysis to audio events
            var audioEvents = ConvertAudioAnalysisToEvents(analysis);
            
            foreach (var audioEvent in audioEvents)
            {
                _scheduler.QueueAudioEvent(audioEvent);
            }
        }
        
        /// <summary>
        /// Register a visual effect binding for audio-reactive visuals
        /// </summary>
        public void RegisterVisualEffectBinding(string effectName, VisualEffectBinding binding)
        {
            if (string.IsNullOrEmpty(effectName) || binding == null)
                throw new ArgumentException("Effect name and binding cannot be null or empty");
                
            _effectBindings[effectName] = binding;
            
            _logging.LogDebug("AudioVisualIntegrationManager", 
                $"Registered visual effect binding: {effectName}");
        }
        
        /// <summary>
        /// Unregister a visual effect binding
        /// </summary>
        public void UnregisterVisualEffectBinding(string effectName)
        {
            if (_effectBindings.ContainsKey(effectName))
            {
                _effectBindings.Remove(effectName);
                _logging.LogDebug("AudioVisualIntegrationManager", 
                    $"Unregistered visual effect binding: {effectName}");
            }
        }
        
        /// <summary>
        /// Update visual parameters directly (bypass audio analysis)
        /// </summary>
        public void QueueVisualParameterUpdate(string parameterName, float value, AudioEventPriority priority = AudioEventPriority.Normal)
        {
            if (!IsRunning || string.IsNullOrEmpty(parameterName))
                return;
                
            var update = new VisualParameterUpdate
            {
                ParameterName = parameterName,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Priority = priority
            };
            
            _scheduler.QueueVisualUpdate(update);
        }
        
        /// <summary>
        /// Configure latency optimization for specific use cases
        /// </summary>
        public void ConfigureForRealtimeAudio(string audioType, double targetLatencyMs = 16.67)
        {
            var settings = new LatencyOptimizationSettings
            {
                TargetLatencyMs = targetLatencyMs,
                MaxLatencyMs = targetLatencyMs * 2,
                MinIntensity = audioType.ToLower() switch
                {
                    "music" => 0.1f,
                    "speech" => 0.05f,
                    "beats" => 0.3f,
                    "frequencies" => 0.2f,
                    _ => 0.1f
                },
                MinFrequencyHz = audioType.ToLower() switch
                {
                    "music" => 20.0f,
                    "speech" => 85.0f,
                    "beats" => 60.0f,
                    "frequencies" => 100.0f,
                    _ => 20.0f
                },
                EnablePredictiveBatching = true,
                EnablePriorityBoosting = true
            };
            
            _scheduler.ConfigureLatencyOptimization(settings);
            
            _logging.LogInfo("AudioVisualIntegrationManager", 
                $"Configured for {audioType} audio with target latency {targetLatencyMs}ms");
        }
        
        /// <summary>
        /// Get current performance statistics
        /// </summary>
        public AudioVisualIntegrationStats GetIntegrationStats()
        {
            var schedulerStats = _scheduler.GetStatistics();
            
            return new AudioVisualIntegrationStats
            {
                // Audio-Visual Scheduler stats
                PendingAudioEvents = schedulerStats.PendingAudioEvents,
                PendingVisualUpdates = schedulerStats.PendingVisualUpdates,
                AverageLatencyMs = schedulerStats.AverageLatencyMs,
                FrameTimeConsistency = schedulerStats.FrameTimeConsistency,
                AudioEventRate = schedulerStats.AudioEventRate,
                VisualUpdateRate = schedulerStats.VisualUpdateRate,
                CurrentFrameRate = schedulerStats.CurrentFrameRate,
                
                // Integration-specific stats
                RegisteredEffects = _effectBindings.Count,
                IsRunning = IsRunning,
                TargetFrameRate = TargetFrameRate,
                ActiveEffectBindings = _effectBindings.Count(b => b.Value.IsActive),
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Force flush all pending updates (emergency use)
        /// </summary>
        public void EmergencyFlush()
        {
            _scheduler.FlushPendingUpdates();
            _logging.LogWarning("AudioVisualIntegrationManager", "Emergency flush executed");
        }
        
        private void ProcessFrame(object state)
        {
            if (!IsRunning)
                return;
                
            try
            {
                // Process frame through scheduler
                _scheduler.ProcessFrame();
                
                // Update statistics
                CurrentStats = GetIntegrationStats();
                
                // Log performance metrics periodically
                if (DateTime.UtcNow.Second % 10 == 0)
                {
                    _logging.LogDebug("AudioVisualIntegrationManager", 
                        $"Performance: {CurrentStats.CurrentFrameRate:F1} FPS, " +
                        $"Latency: {CurrentStats.AverageLatencyMs:F2}ms, " +
                        $"Audio Events: {CurrentStats.PendingAudioEvents}, " +
                        $"Visual Updates: {CurrentStats.PendingVisualUpdates}");
                }
            }
            catch (Exception ex)
            {
                _logging.LogError("AudioVisualIntegrationManager", 
                    $"Frame processing error: {ex.Message}", ex);
            }
        }
        
        private void SetupEventHandlers()
        {
            _scheduler.SyncEvent += OnAudioVisualSync;
            _scheduler.QueueStatusChanged += OnQueueStatusChanged;
            _scheduler.PerformanceMetrics += OnPerformanceMetrics;
        }
        
        private void InitializeIntegration()
        {
            // Register default effect bindings
            RegisterDefaultEffectBindings();
            
            // Setup EvaluationContext integration
            SetupEvaluationContextIntegration();
        }
        
        private void RegisterDefaultEffectBindings()
        {
            // Default visual effect bindings for common audio-reactive patterns
            RegisterVisualEffectBinding("GlobalIntensity", new VisualEffectBinding
            {
                ParameterName = "GlobalIntensity",
                EffectType = EffectType.Multiplier,
                MinValue = 0.0f,
                MaxValue = 2.0f,
                SmoothingFactor = 0.8f,
                IsActive = true
            });
            
            RegisterVisualEffectBinding("ColorShift", new VisualEffectBinding
            {
                ParameterName = "HueShift",
                EffectType = EffectType.Additive,
                MinValue = -180.0f,
                MaxValue = 180.0f,
                SmoothingFactor = 0.7f,
                IsActive = true
            });
            
            RegisterVisualEffectBinding("Distortion", new VisualEffectBinding
            {
                ParameterName = "DistortionAmount",
                EffectType = EffectType.Clamped,
                MinValue = 0.0f,
                MaxValue = 1.0f,
                SmoothingFactor = 0.9f,
                IsActive = false
            });
            
            RegisterVisualEffectBinding("Glow", new VisualEffectBinding
            {
                ParameterName = "GlowIntensity",
                EffectType = EffectType.Additive,
                MinValue = 0.0f,
                MaxValue = 5.0f,
                SmoothingFactor = 0.6f,
                IsActive = true
            });
        }
        
        private void SetupEvaluationContextIntegration()
        {
            // Integration with TiXL's EvaluationContext for operator-based visual effects
            // This would be expanded based on the actual EvaluationContext implementation
            // For now, we provide the framework for future integration
        }
        
        private List<AudioEvent> ConvertAudioAnalysisToEvents(AudioAnalysisResult analysis)
        {
            var events = new List<AudioEvent>();
            
            // Convert beat detection
            if (analysis.BeatConfidence > 0.5)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = analysis.BeatConfidence,
                    Frequency = 0, // Beats don't have frequency
                    Priority = analysis.BeatConfidence > 0.8 ? AudioEventPriority.High : AudioEventPriority.Normal,
                    Type = AudioEventType.Beat,
                    Data = new { Confidence = analysis.BeatConfidence }
                });
            }
            
            // Convert frequency analysis
            foreach (var freqAnalysis in analysis.FrequencyBands)
            {
                if (freqAnalysis.Magnitude > 0.1) // Only significant frequencies
                {
                    events.Add(new AudioEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Intensity = freqAnalysis.Magnitude,
                        Frequency = freqAnalysis.CenterFrequency,
                        Priority = freqAnalysis.Magnitude > 0.7 ? AudioEventPriority.High : AudioEventPriority.Normal,
                        Type = AudioEventType.Frequency,
                        Data = new { BandIndex = freqAnalysis.BandIndex, Magnitude = freqAnalysis.Magnitude }
                    });
                }
            }
            
            // Convert volume analysis
            if (analysis.Volume > 0.1)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = analysis.Volume,
                    Frequency = 0,
                    Priority = analysis.Volume > 0.8 ? AudioEventPriority.High : AudioEventPriority.Low,
                    Type = AudioEventType.Volume,
                    Data = new { Volume = analysis.Volume }
                });
            }
            
            return events;
        }
        
        private void OnAudioVisualSync(object sender, AudioVisualSyncEventArgs e)
        {
            // Process visual effect triggers based on audio events
            foreach (var effectBinding in _effectBindings.Values.Where(b => b.IsActive))
            {
                var visualUpdate = GenerateVisualUpdateFromAudioEvent(e.AudioEvent, effectBinding);
                if (visualUpdate != null)
                {
                    _scheduler.QueueVisualUpdate(visualUpdate);
                    
                    VisualEffectTriggered?.Invoke(this, new VisualEffectEventArgs
                    {
                        EffectName = effectBinding.ParameterName,
                        AudioEvent = e.AudioEvent,
                        VisualUpdate = visualUpdate,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            
            AudioAnalysisCompleted?.Invoke(this, new AudioAnalysisEventArgs
            {
                AudioEvent = e.AudioEvent,
                VisualUpdatesGenerated = e.VisualUpdatesGenerated,
                Timestamp = DateTime.UtcNow
            });
        }
        
        private VisualParameterUpdate GenerateVisualUpdateFromAudioEvent(AudioEvent audioEvent, VisualEffectBinding binding)
        {
            // Apply effect binding logic
            var rawValue = binding.EffectType switch
            {
                EffectType.Multiplier => audioEvent.Intensity * binding.MaxValue,
                EffectType.Additive => binding.MinValue + (audioEvent.Intensity * (binding.MaxValue - binding.MinValue)),
                EffectType.Clamped => Math.Clamp(binding.MinValue + (audioEvent.Intensity * (binding.MaxValue - binding.MinValue)), 
                                                  binding.MinValue, binding.MaxValue),
                _ => audioEvent.Intensity
            };
            
            // Apply smoothing
            var smoothedValue = ApplySmoothing(rawValue, binding);
            
            return new VisualParameterUpdate
            {
                ParameterName = binding.ParameterName,
                Value = smoothedValue,
                Timestamp = DateTime.UtcNow,
                Priority = audioEvent.Priority,
                Data = new { RawValue = rawValue, Binding = binding }
            };
        }
        
        private float ApplySmoothing(float rawValue, VisualEffectBinding binding)
        {
            // Simple exponential smoothing
            if (!binding._lastValue.HasValue)
            {
                binding._lastValue = rawValue;
                return rawValue;
            }
            
            var smoothed = binding._lastValue.Value * binding.SmoothingFactor + rawValue * (1 - binding.SmoothingFactor);
            binding._lastValue = smoothed;
            
            return smoothed;
        }
        
        private void OnQueueStatusChanged(object sender, QueueStatusEventArgs e)
        {
            if (e.IsOverflowing)
            {
                _logging.LogWarning("AudioVisualIntegrationManager", 
                    $"Queue overflow detected: {e.QueueType}, Pending: {e.PendingCount}");
            }
        }
        
        private void OnPerformanceMetrics(object sender, PerformanceMetricsEventArgs e)
        {
            // Update integration statistics
            CurrentStats = GetIntegrationStats();
            
            // Check for performance degradation
            if (e.Stats.AverageLatencyMs > 50.0)
            {
                _logging.LogWarning("AudioVisualIntegrationManager", 
                    $"High latency detected: {e.Stats.AverageLatencyMs:F2}ms");
            }
            
            if (e.Stats.CurrentFrameRate < TargetFrameRate * 0.8)
            {
                _logging.LogWarning("AudioVisualIntegrationManager", 
                    $"Frame rate degradation: {e.Stats.CurrentFrameRate:F1} FPS (target: {TargetFrameRate})");
            }
        }
        
        public void Dispose()
        {
            Stop();
            _scheduler?.Dispose();
            _frameTimer?.Dispose();
            _effectBindings?.Clear();
        }
    }
    
    // Supporting data structures for integration
    
    public class AudioAnalysisResult
    {
        public float BeatConfidence { get; set; }
        public float Volume { get; set; }
        public List<FrequencyBandAnalysis> FrequencyBands { get; set; } = new List<FrequencyBandAnalysis>();
        public DateTime Timestamp { get; set; }
    }
    
    public class FrequencyBandAnalysis
    {
        public int BandIndex { get; set; }
        public float CenterFrequency { get; set; }
        public float Magnitude { get; set; }
        public float Phase { get; set; }
    }
    
    public class VisualEffectBinding
    {
        public string ParameterName { get; set; }
        public EffectType EffectType { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float SmoothingFactor { get; set; } = 0.8f;
        public bool IsActive { get; set; } = true;
        
        // Internal for smoothing
        internal float? _lastValue;
    }
    
    public enum EffectType
    {
        Multiplier,
        Additive,
        Clamped
    }
    
    public class AudioVisualIntegrationStats
    {
        // From scheduler
        public int PendingAudioEvents { get; set; }
        public int PendingVisualUpdates { get; set; }
        public double AverageLatencyMs { get; set; }
        public double FrameTimeConsistency { get; set; }
        public double AudioEventRate { get; set; }
        public double VisualUpdateRate { get; set; }
        public double CurrentFrameRate { get; set; }
        
        // Integration-specific
        public int RegisteredEffects { get; set; }
        public bool IsRunning { get; set; }
        public int TargetFrameRate { get; set; }
        public int ActiveEffectBindings { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class VisualEffectEventArgs : EventArgs
    {
        public string EffectName { get; set; }
        public AudioEvent AudioEvent { get; set; }
        public VisualParameterUpdate VisualUpdate { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class AudioAnalysisEventArgs : EventArgs
    {
        public AudioEvent AudioEvent { get; set; }
        public int VisualUpdatesGenerated { get; set; }
        public DateTime Timestamp { get; set; }
    }
}