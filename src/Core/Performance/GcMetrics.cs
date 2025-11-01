using System;
using System.Collections.Generic;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// Garbage collection metrics tracking for performance monitoring
    /// </summary>
    public class GcMetrics
    {
        private int _lastGen0Collections;
        private int _lastGen1Collections;
        private int _lastGen2Collections;
        private long _lastMemoryUsage;
        private readonly List<CollectionSnapshot> _collectionHistory = new List<CollectionSnapshot>();
        
        public int LastGen0Collections { get; private set; }
        public int LastGen1Collections { get; private set; }
        public int LastGen2Collections { get; private set; }
        public long LastMemoryUsage { get; private set; }
        public double CollectionFrequency { get; private set; } // Collections per second
        
        /// <summary>
        /// Update GC metrics from system
        /// </summary>
        public void Update()
        {
            var currentGen0 = GC.CollectionCount(0);
            var currentGen1 = GC.CollectionCount(1);
            var currentGen2 = GC.CollectionCount(2);
            var currentMemory = GC.GetTotalMemory(false);
            
            LastGen0Collections = currentGen0 - _lastGen0Collections;
            LastGen1Collections = currentGen1 - _lastGen1Collections;
            LastGen2Collections = currentGen2 - _lastGen2Collections;
            LastMemoryUsage = currentMemory - _lastMemoryUsage;
            
            _lastGen0Collections = currentGen0;
            _lastGen1Collections = currentGen1;
            _lastGen2Collections = currentGen2;
            _lastMemoryUsage = currentMemory;
            
            // Add to history
            _collectionHistory.Add(new CollectionSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Gen0Collections = LastGen0Collections,
                Gen1Collections = LastGen1Collections,
                Gen2Collections = LastGen2Collections,
                MemoryUsage = LastMemoryUsage
            });
            
            // Keep only recent history (last 10 seconds)
            var cutoff = DateTime.UtcNow.AddSeconds(-10);
            _collectionHistory.RemoveAll(s => s.Timestamp < cutoff);
            
            // Calculate collection frequency
            CalculateCollectionFrequency();
        }
        
        /// <summary>
        /// Get total collections in the last frame
        /// </summary>
        public int GetCollectionsLastFrame()
        {
            return LastGen0Collections + LastGen1Collections + LastGen2Collections;
        }
        
        /// <summary>
        /// Get current memory usage in bytes
        /// </summary>
        public long GetMemoryUsage()
        {
            return GC.GetTotalMemory(false);
        }
        
        /// <summary>
        /// Get memory pressure level
        /// </summary>
        public MemoryPressureLevel GetMemoryPressureLevel()
        {
            var memoryMB = GetMemoryUsage() / (1024 * 1024);
            
            if (memoryMB > 1024) return MemoryPressureLevel.High;
            if (memoryMB > 512) return MemoryPressureLevel.Medium;
            if (memoryMB > 256) return MemoryPressureLevel.Low;
            return MemoryPressureLevel.Normal;
        }
        
        /// <summary>
        /// Check if GC pressure is high
        /// </summary>
        public bool IsHighGcPressure()
        {
            // High pressure: more than 5 collections per second
            return CollectionFrequency > 5.0;
        }
        
        /// <summary>
        /// Get recommended GC mode based on current pressure
        /// </summary>
        public GcMode GetRecommendedGcMode()
        {
            if (IsHighGcPressure())
                return GcMode.Batch; // Batch mode for high pressure
                
            var pressure = GetMemoryPressureLevel();
            return pressure switch
            {
                MemoryPressureLevel.High => GcMode.Batch,
                MemoryPressureLevel.Medium => GcMode.Interactive,
                MemoryPressureLevel.Low => GcMode.LowLatency,
                _ => GcMode.LowLatency
            };
        }
        
        /// <summary>
        /// Get GC statistics summary
        /// </summary>
        public GcStatistics GetStatistics()
        {
            return new GcStatistics
            {
                TotalCollectionsLastFrame = GetCollectionsLastFrame(),
                MemoryUsageMB = GetMemoryUsage() / (1024 * 1024),
                MemoryPressure = GetMemoryPressureLevel(),
                CollectionFrequency = CollectionFrequency,
                IsHighPressure = IsHighGcPressure(),
                RecommendedMode = GetRecommendedGcMode()
            };
        }
        
        private void CalculateCollectionFrequency()
        {
            if (_collectionHistory.Count < 2)
            {
                CollectionFrequency = 0;
                return;
            }
            
            var first = _collectionHistory[0];
            var last = _collectionHistory[_collectionHistory.Count - 1];
            
            var timeSpan = (last.Timestamp - first.Timestamp).TotalSeconds;
            if (timeSpan <= 0)
            {
                CollectionFrequency = 0;
                return;
            }
            
            var totalCollections = last.Gen0Collections - first.Gen0Collections +
                                  last.Gen1Collections - first.Gen1Collections +
                                  last.Gen2Collections - first.Gen2Collections;
            
            CollectionFrequency = totalCollections / timeSpan;
        }
    }
    
    /// <summary>
    /// Snapshot of GC state at a point in time
    /// </summary>
    public class CollectionSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long MemoryUsage { get; set; }
    }
    
    /// <summary>
    /// Memory pressure level
    /// </summary>
    public enum MemoryPressureLevel
    {
        Normal,
        Low,
        Medium,
        High
    }
    
    /// <summary>
    /// GC mode recommendations
    /// </summary>
    public enum GcMode
    {
        Batch,       // For high pressure, throughput optimized
        Interactive, // Balanced mode
        LowLatency   // For low pressure, latency optimized
    }
    
    /// <summary>
    /// GC statistics summary
    /// </summary>
    public class GcStatistics
    {
        public int TotalCollectionsLastFrame { get; set; }
        public long MemoryUsageMB { get; set; }
        public MemoryPressureLevel MemoryPressure { get; set; }
        public double CollectionFrequency { get; set; }
        public bool IsHighPressure { get; set; }
        public GcMode RecommendedMode { get; set; }
    }
}
