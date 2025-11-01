using System;
using System.Collections.Generic;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Comprehensive I/O event model for different types of I/O operations
    /// Supports priority handling, metadata tracking, and lifecycle management
    /// </summary>
    public class IOEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public IOEventType EventType { get; set; }
        public IOEventPriority Priority { get; set; }
        public byte[] Data { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public string Source { get; set; }
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool IsCancelled { get; set; } = false;
        
        /// <summary>
        /// Create audio I/O event
        /// </summary>
        public static IOEvent CreateAudioEvent(IOEventType audioType, byte[] audioData, IOEventPriority priority = IOEventPriority.High)
        {
            return new IOEvent
            {
                EventType = audioType,
                Priority = priority,
                Data = audioData,
                Metadata = new Dictionary<string, string>
                {
                    ["AudioType"] = audioType.ToString(),
                    ["DataFormat"] = "PCM",
                    ["SampleRate"] = "44100",
                    ["Channels"] = "2"
                }
            };
        }
        
        /// <summary>
        /// Create MIDI I/O event
        /// </summary>
        public static IOEvent CreateMidiEvent(IOEventType midiType, byte[] midiData, IOEventPriority priority = IOEventPriority.High)
        {
            return new IOEvent
            {
                EventType = midiType,
                Priority = priority,
                Data = midiData,
                Metadata = new Dictionary<string, string>
                {
                    ["MidiType"] = midiType.ToString(),
                    ["DataFormat"] = "MIDI",
                    ["Channel"] = "1",
                    ["Velocity"] = "127"
                }
            };
        }
        
        /// <summary>
        /// Create file I/O event
        /// </summary>
        public static IOEvent CreateFileEvent(IOEventType fileType, string filePath, byte[] fileData = null, IOEventPriority priority = IOEventPriority.Medium)
        {
            return new IOEvent
            {
                EventType = fileType,
                Priority = priority,
                Data = fileData,
                Metadata = new Dictionary<string, string>
                {
                    ["FilePath"] = filePath,
                    ["FileType"] = System.IO.Path.GetExtension(filePath),
                    ["FileSize"] = fileData?.Length.ToString() ?? "0"
                }
            };
        }
        
        /// <summary>
        /// Create network I/O event
        /// </summary>
        public static IOEvent CreateNetworkEvent(string endpoint, byte[] data, IOEventPriority priority = IOEventPriority.Medium)
        {
            return new IOEvent
            {
                EventType = IOEventType.NetworkIO,
                Priority = priority,
                Data = data,
                Metadata = new Dictionary<string, string>
                {
                    ["Endpoint"] = endpoint,
                    ["Protocol"] = "TCP",
                    ["DataFormat"] = "Binary"
                }
            };
        }
        
        /// <summary>
        /// Create Spout data event
        /// </summary>
        public static IOEvent CreateSpoutEvent(string spoutName, byte[] textureData, IOEventPriority priority = IOEventPriority.High)
        {
            return new IOEvent
            {
                EventType = IOEventType.SpoutData,
                Priority = priority,
                Data = textureData,
                Metadata = new Dictionary<string, string>
                {
                    ["SpoutName"] = spoutName,
                    ["TextureFormat"] = "RGBA",
                    ["Width"] = "1920",
                    ["Height"] = "1080"
                }
            };
        }
        
        /// <summary>
        /// Create user input event
        /// </summary>
        public static IOEvent CreateUserInputEvent(string inputType, object inputData, IOEventPriority priority = IOEventPriority.Critical)
        {
            return new IOEvent
            {
                EventType = IOEventType.UserInput,
                Priority = priority,
                Metadata = new Dictionary<string, string>
                {
                    ["InputType"] = inputType,
                    ["InputData"] = inputData?.ToString(),
                    ["Timestamp"] = DateTime.UtcNow.Ticks.ToString()
                }
            };
        }
        
        /// <summary>
        /// Clone event for retry or modification
        /// </summary>
        public IOEvent Clone()
        {
            return new IOEvent
            {
                Id = Guid.NewGuid().ToString(),
                EventType = EventType,
                Priority = Priority,
                Data = Data,
                Metadata = new Dictionary<string, string>(Metadata),
                Source = Source,
                RetryCount = RetryCount,
                MaxRetries = MaxRetries,
                Timeout = Timeout,
                IsCancelled = false // Reset cancelled status for cloned event
            };
        }
        
        /// <summary>
        /// Mark event as cancelled
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
            Metadata["CancelledAt"] = DateTime.UtcNow.Ticks.ToString();
        }
        
        /// <summary>
        /// Check if event has timed out
        /// </summary>
        public bool IsTimeout()
        {
            if (!Timeout.HasValue) return false;
            return DateTime.UtcNow - CreationTime > Timeout.Value;
        }
        
        /// <summary>
        /// Get event age
        /// </summary>
        public TimeSpan GetAge()
        {
            return DateTime.UtcNow - CreationTime;
        }
        
        /// <summary>
        /// Can event be retried
        /// </summary>
        public bool CanRetry()
        {
            return RetryCount < MaxRetries && !IsCancelled && !IsTimeout();
        }
    }
    
    /// <summary>
    /// Types of I/O events
    /// </summary>
    public enum IOEventType
    {
        // Audio I/O (High Priority)
        AudioInput,
        AudioOutput,
        
        // MIDI I/O (High Priority)
        MidiInput,
        MidiOutput,
        
        // User Input (Critical Priority)
        UserInput,
        
        // File I/O (Medium Priority)
        FileRead,
        FileWrite,
        
        // Network I/O (Medium Priority)
        NetworkIO,
        
        // Spout/Texture Sharing (Medium Priority)
        SpoutData,
        
        // Background Tasks (Low Priority)
        CacheUpdate,
        MetadataUpdate
    }
    
    /// <summary>
    /// I/O event priorities
    /// </summary>
    public enum IOEventPriority
    {
        Critical = 0,  // User input, real-time audio
        High = 1,      // Audio, MIDI, Spout
        Medium = 2,    // File I/O, network
        Low = 3        // Background tasks, caching
    }
    
    /// <summary>
    /// Result of I/O event processing
    /// </summary>
    public class IOEventResult
    {
        public bool IsSuccess { get; set; }
        public string EventId { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> ResultMetadata { get; set; } = new();
        
        public static IOEventResult Success(string eventId, string status = "Success")
        {
            return new IOEventResult
            {
                IsSuccess = true,
                EventId = eventId,
                Status = status
            };
        }
        
        public static IOEventResult Failed(string eventId, string errorMessage)
        {
            return new IOEventResult
            {
                IsSuccess = false,
                EventId = eventId,
                ErrorMessage = errorMessage
            };
        }
        
        public static IOEventResult QueuedForNextFrame(string eventId)
        {
            return new IOEventResult
            {
                IsSuccess = true,
                EventId = eventId,
                Status = "QueuedForNextFrame"
            };
        }
    }
    
    /// <summary>
    /// Result of batch I/O event processing
    /// </summary>
    public class IOBatchResult
    {
        public bool IsSuccess { get; set; }
        public List<IOEventResult> Results { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public int TotalEvents { get; set; }
        public int SuccessfulEvents { get; set; }
        public int FailedEvents { get; set; }
        
        public static IOBatchResult Success(List<IOEventResult> results)
        {
            var successful = results.Count(r => r.IsSuccess);
            var failed = results.Count - successful;
            
            return new IOBatchResult
            {
                IsSuccess = failed == 0,
                Results = results,
                TotalEvents = results.Count,
                SuccessfulEvents = successful,
                FailedEvents = failed
            };
        }
    }
    
    /// <summary>
    /// Performance metrics for I/O operations
    /// </summary>
    public class IOPerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public long TotalEventsProcessed { get; set; }
        public long TotalEventsBatched { get; set; }
        public long TotalFrameSavingsMs { get; set; }
        public int HighPriorityQueueDepth { get; set; }
        public int MediumPriorityQueueDepth { get; set; }
        public int LowPriorityQueueDepth { get; set; }
        public int ActiveWorkerCount { get; set; }
        public double AverageProcessingTime { get; set; }
        public Dictionary<string, double> WorkerMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// Performance statistics for I/O isolation system
    /// </summary>
    public class IOPerformanceStatistics
    {
        public long TotalEventsProcessed { get; set; }
        public long TotalEventsBatched { get; set; }
        public long TotalFrameSavingsMs { get; set; }
        public IOEventQueueStatistics HighPriorityQueueStats { get; set; } = new();
        public IOEventQueueStatistics MediumPriorityQueueStats { get; set; } = new();
        public IOEventQueueStatistics LowPriorityQueueStats { get; set; } = new();
        public IOResourcePoolStatistics ResourcePoolStats { get; set; } = new();
        public Dictionary<string, IOWorkerStatistics> WorkerStats { get; set; } = new();
        public int ActiveResourceCount { get; set; }
        
        public double AverageFrameSavingsMs => TotalEventsProcessed > 0 ? 
            (double)TotalFrameSavingsMs / TotalEventsProcessed : 0;
        
        public double BatchingEfficiency => TotalEventsProcessed > 0 ? 
            (double)TotalEventsBatched / TotalEventsProcessed * 100 : 0;
        
        public int TotalQueueDepth => HighPriorityQueueStats.CurrentSize + 
                                    MediumPriorityQueueStats.CurrentSize + 
                                    LowPriorityQueueStats.CurrentSize;
    }
    
    /// <summary>
    /// Alert types for I/O isolation system
    /// </summary>
    public enum AlertType
    {
        // Queue alerts
        QueueFull,
        AddFailed,
        TakeFailed,
        BatchTakeFailed,
        PriorityTakeFailed,
        
        // Worker alerts
        WorkerStarted,
        WorkerError,
        BatchProcessingFailed,
        EventProcessingFailed,
        WorkerStopTimeout,
        WorkerDisposeError,
        
        // Performance alerts
        HighPriorityQueueBacklog,
        ProcessingDelay,
        WorkerFailure,
        
        // Recovery alerts
        MetricsCollectionFailed,
        BackgroundCleanupFailed,
        
        // System alerts
        ResourceExhaustion,
        MemoryPressure,
        ThreadPoolExhaustion
    }
    
    /// <summary>
    /// Alert for I/O isolation system
    /// </summary>
    public class IOIsolationAlert : EventArgs
    {
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Context { get; set; } = new();
    }
}