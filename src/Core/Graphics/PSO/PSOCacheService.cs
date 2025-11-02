using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D12;
using Vortice.DXGI;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Performance;
using TiXL.Core.Logging;

namespace TiXL.Core.Graphics.DirectX12.PSO
{
    /// <summary>
    /// PSO Cache integration service for DirectX 12 rendering engine
    /// Provides seamless integration between the real PSO cache and rendering engine
    /// </summary>
    public class PSOCacheService : IDisposable
    {
        private readonly ID3D12Device5 _device;
        private readonly ID3D12CommandQueue _commandQueue;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ILogger<PSOCacheService> _logger;
        
        // Core PSO cache instance
        private readonly RealPSOCache _psoCache;
        
        // Engine integration
        private readonly DirectX12RenderingEngine _renderingEngine;
        private readonly PSOCacheServiceConfig _config;
        
        // Material management
        private readonly ConcurrentDictionary<string, MaterialPSOKey> _registeredMaterials = new();
        private readonly List<MaterialPSOKey> _precompilationQueue = new();
        private readonly SemaphoreSlim _precompilationSemaphore;
        
        // Performance tracking
        private readonly Timer _statsTimer;
        private RealPSOCacheStatistics _lastStats;
        private DateTime _lastStatsTime;
        
        // Events
        public event EventHandler<PSOCacheServiceEventArgs> MaterialRegistered;
        public event EventHandler<PSOCacheServiceEventArgs> MaterialPrecompiled;
        public event EventHandler<PSOCacheServiceEventArgs> PerformanceWarning;
        
        /// <summary>
        /// Initialize PSO Cache Service
        /// </summary>
        /// <param name="device">DirectX 12 device</param>
        /// <param name="commandQueue">Command queue</param>
        /// <param name="renderingEngine">DirectX 12 rendering engine</param>
        /// <param name="performanceMonitor">Performance monitor</param>
        /// <param name="config">Service configuration</param>
        public PSOCacheService(
            ID3D12Device5 device,
            ID3D12CommandQueue commandQueue,
            DirectX12RenderingEngine renderingEngine,
            PerformanceMonitor performanceMonitor = null,
            PSOCacheServiceConfig config = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            _renderingEngine = renderingEngine ?? throw new ArgumentNullException(nameof(renderingEngine));
            _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
            _config = config ?? PSOCacheServiceConfig.Default;
            _logger = Logger.CreateLogger<PSOCacheService>();
            
            // Initialize PSO cache
            _psoCache = new RealPSOCache(device, commandQueue, performanceMonitor, _config.CacheConfig);
            _precompilationSemaphore = new SemaphoreSlim(_config.MaxConcurrentPrecompilation, _config.MaxConcurrentPrecompilation);
            
            // Setup event handlers
            SubscribeToPSOCacheEvents();
            
            // Initialize statistics tracking
            _statsTimer = new Timer(UpdateStatistics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            _lastStatsTime = DateTime.UtcNow;
            
            // Setup engine integration callbacks
            SetupEngineIntegration();
            
            _logger.LogInformation("PSO Cache Service initialized");
        }
        
        /// <summary>
        /// Register a material for PSO caching
        /// </summary>
        /// <param name="materialKey">Material configuration key</param>
        /// <returns>Registration result</returns>
        public async Task<MaterialRegistrationResult> RegisterMaterialAsync(MaterialPSOKey materialKey)
        {
            if (materialKey == null)
                throw new ArgumentNullException(nameof(materialKey));
                
            if (string.IsNullOrEmpty(materialKey.MaterialName))
                throw new ArgumentException("Material name cannot be null or empty", nameof(materialKey));
                
            var materialId = materialKey.MaterialName.ToLowerInvariant();
            
            try
            {
                // Check if material is already registered
                if (_registeredMaterials.TryGetValue(materialId, out var existingKey))
                {
                    // Update existing registration
                    _registeredMaterials[materialId] = materialKey;
                    _logger.LogDebug("Material {MaterialName} registration updated", materialKey.MaterialName);
                    
                    return new MaterialRegistrationResult
                    {
                        IsNew = false,
                        WasUpdated = true,
                        MaterialKey = materialKey
                    };
                }
                
                // Register new material
                _registeredMaterials[materialId] = materialKey;
                
                // Queue for precompilation if enabled
                if (_config.EnableBackgroundPrecompilation)
                {
                    QueueForPrecompilation(materialKey);
                }
                
                MaterialRegistered?.Invoke(this, new PSOCacheServiceEventArgs
                {
                    MaterialKey = materialKey,
                    EventType = MaterialRegistrationEventType.Registered
                });
                
                _logger.LogDebug("Material {MaterialName} registered for PSO caching", materialKey.MaterialName);
                
                return new MaterialRegistrationResult
                {
                    IsNew = true,
                    WasUpdated = false,
                    MaterialKey = materialKey
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register material {MaterialName}", materialKey.MaterialName);
                throw;
            }
        }
        
        /// <summary>
        /// Get or create PSO for registered material
        /// </summary>
        /// <param name="materialName">Material name</param>
        /// <returns>Pipeline state result</returns>
        public async Task<RealPipelineStateResult> GetMaterialPSOAsync(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                throw new ArgumentException("Material name cannot be null or empty", nameof(materialName));
                
            var materialId = materialName.ToLowerInvariant();
            
            try
            {
                // Get from registered materials first
                if (_registeredMaterials.TryGetValue(materialId, out var materialKey))
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    try
                    {
                        return await _psoCache.GetOrCreatePSOAsync(materialKey);
                    }
                    finally
                    {
                        stopwatch.Stop();
                        
                        // Track performance for optimization
                        if (stopwatch.ElapsedMilliseconds > _config.SlowOperationThresholdMs)
                        {
                            PerformanceWarning?.Invoke(this, new PSOCacheServiceEventArgs
                            {
                                MaterialKey = materialKey,
                                EventType = MaterialRegistrationEventType.SlowOperation,
                                Message = $"PSO creation took {stopwatch.ElapsedMilliseconds}ms"
                            });
                        }
                    }
                }
                
                // Create default material if not registered
                var defaultKey = CreateDefaultMaterialKey(materialName);
                await RegisterMaterialAsync(defaultKey);
                
                return await _psoCache.GetOrCreatePSOAsync(defaultKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get PSO for material {MaterialName}", materialName);
                throw;
            }
        }
        
        /// <summary>
        /// Create PSO from shader files with material configuration
        /// </summary>
        public async Task<RealPipelineStateResult> CreatePSOFromShadersAsync(
            string materialName,
            string vertexShaderPath,
            string pixelShaderPath,
            MaterialPSOKey materialKey = null)
        {
            materialKey = materialKey ?? CreateShaderBasedMaterialKey(materialName, vertexShaderPath, pixelShaderPath);
            materialKey.VertexShaderPath = vertexShaderPath;
            materialKey.PixelShaderPath = pixelShaderPath;
            
            await RegisterMaterialAsync(materialKey);
            
            return await _psoCache.CreatePSOFromShadersAsync(vertexShaderPath, pixelShaderPath, materialKey);
        }
        
        /// <summary>
        /// Unregister a material from PSO caching
        /// </summary>
        /// <param name="materialName">Material name</param>
        /// <returns>True if unregistered successfully</returns>
        public bool UnregisterMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;
                
            var materialId = materialName.ToLowerInvariant();
            
            try
            {
                if (_registeredMaterials.TryRemove(materialId, out var materialKey))
                {
                    // Remove from precompilation queue
                    lock (_precompilationQueue)
                    {
                        _precompilationQueue.RemoveAll(k => k.MaterialName == materialName);
                    }
                    
                    // Remove from cache
                    var removed = _psoCache.RemoveMaterialPSO(materialName);
                    
                    _logger.LogDebug("Material {MaterialName} unregistered (PSO removed: {Removed})", 
                        materialName, removed);
                        
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister material {MaterialName}", materialName);
            }
            
            return false;
        }
        
        /// <summary>
        /// Precompile all registered materials
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task PrecompileAllMaterialsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting precompilation of {Count} materials", _registeredMaterials.Count);
            
            var tasks = _registeredMaterials.Values.Select(async materialKey =>
            {
                await _precompilationSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await _psoCache.GetOrCreatePSOAsync(materialKey);
                    
                    MaterialPrecompiled?.Invoke(this, new PSOCacheServiceEventArgs
                    {
                        MaterialKey = materialKey,
                        EventType = MaterialRegistrationEventType.Precompiled
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to precompile material {MaterialName}", materialKey.MaterialName);
                }
                finally
                {
                    _precompilationSemaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);
            
            _logger.LogInformation("Precompilation completed");
        }
        
        /// <summary>
        /// Get comprehensive service statistics
        /// </summary>
        /// <returns>Service statistics</returns>
        public PSOCacheServiceStatistics GetStatistics()
        {
            var cacheStats = _psoCache.GetStatistics();
            
            return new PSOCacheServiceStatistics
            {
                CacheStatistics = cacheStats,
                RegisteredMaterials = _registeredMaterials.Count,
                PrecompilationQueueSize = _precompilationQueue.Count,
                LastStatsUpdate = _lastStatsTime,
                TimeSinceLastUpdate = DateTime.UtcNow - _lastStatsTime,
                EngineIntegrationEnabled = _config.IntegrateWithRenderingEngine,
                BackgroundPrecompilationEnabled = _config.EnableBackgroundPrecompilation
            };
        }
        
        /// <summary>
        /// Clear all materials and cached PSOs
        /// </summary>
        public void ClearAll()
        {
            try
            {
                _registeredMaterials.Clear();
                lock (_precompilationQueue)
                {
                    _precompilationQueue.Clear();
                }
                
                _psoCache.ClearCache();
                
                _logger.LogInformation("PSO Cache Service cleared all data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing PSO Cache Service");
                throw;
            }
        }
        
        /// <summary>
        /// Optimize cache based on usage patterns
        /// </summary>
        public void OptimizeCache()
        {
            try
            {
                var stats = _psoCache.GetStatistics();
                
                // Resize cache if needed
                if (stats.CacheEntries > stats.Capacity * 0.9)
                {
                    var newCapacity = (int)(stats.Capacity * 1.5);
                    _psoCache.ResizeCache(newCapacity);
                    
                    _logger.LogInformation("Cache capacity increased to {NewCapacity}", newCapacity);
                }
                
                // Force cleanup
                _psoCache.ForceCleanup();
                
                // Check for performance issues
                if (stats.CacheHitRate < 0.6)
                {
                    PerformanceWarning?.Invoke(this, new PSOCacheServiceEventArgs
                    {
                        EventType = MaterialRegistrationEventType.PerformanceWarning,
                        Message = $"Low cache hit rate: {stats.CacheHitRate:P1}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing PSO cache");
            }
        }
        
        /// <summary>
        /// Save cache to persistent storage
        /// </summary>
        /// <param name="filePath">File path to save cache</param>
        public async Task SaveCacheAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                await using var stream = File.OpenWrite(filePath);
                await _psoCache.SerializeCacheAsync(stream);
                
                _logger.LogInformation("PSO cache saved to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save PSO cache to {FilePath}", filePath);
                throw;
            }
        }
        
        private void SubscribeToPSOCacheEvents()
        {
            _psoCache.PSOCreated += (sender, args) =>
            {
                _logger.LogDebug("PSO created for material {MaterialName}", args.MaterialKey.MaterialName);
            };
            
            _psoCache.PSOCacheHit += (sender, args) =>
            {
                _logger.LogTrace("PSO cache hit for material {MaterialName}", args.MaterialKey.MaterialName);
            };
            
            _psoCache.PSOCacheMiss += (sender, args) =>
            {
                _logger.LogDebug("PSO cache miss for material {MaterialName} (took {Time}ms)", 
                    args.MaterialKey.MaterialName, args.LookupTime.TotalMilliseconds);
            };
            
            _psoCache.PSOCacheEvicted += (sender, args) =>
            {
                _logger.LogDebug("PSO evicted from cache for material {MaterialName}", args.MaterialKey.MaterialName);
            };
        }
        
        private void SetupEngineIntegration()
        {
            if (!_config.IntegrateWithRenderingEngine) return;
            
            // Subscribe to engine frame events for cache maintenance
            _renderingEngine.FrameRendered += (sender, args) =>
            {
                // Perform lightweight maintenance tasks
                if (args.FrameId % 120 == 0) // Every 120 frames
                {
                    _ = Task.Run(() => OptimizeCache());
                }
            };
            
            // Subscribe to engine alerts for performance monitoring
            _renderingEngine.EngineAlert += (sender, args) =>
            {
                if (args.AlertType == EngineAlertType.FrameBudgetExceeded)
                {
                    // Reduce cache size during budget pressure
                    var stats = _psoCache.GetStatistics();
                    if (stats.CacheEntries > 100)
                    {
                        _psoCache.ResizeCache(stats.CacheEntries - 50);
                    }
                }
            };
        }
        
        private void QueueForPrecompilation(MaterialPSOKey materialKey)
        {
            lock (_precompilationQueue)
            {
                if (!_precompilationQueue.Any(k => k.MaterialName == materialKey.MaterialName))
                {
                    _precompilationQueue.Add(materialKey);
                }
            }
            
            // Start background precompilation if queue is large enough
            if (_precompilationQueue.Count >= _config.PrecompilationBatchSize)
            {
                _ = Task.Run(async () =>
                {
                    MaterialPSOKey[] batch;
                    lock (_precompilationQueue)
                    {
                        batch = _precompilationQueue.Take(_config.PrecompilationBatchSize).ToArray();
                        foreach (var item in batch)
                        {
                            _precompilationQueue.Remove(item);
                        }
                    }
                    
                    await PrecompileBatchAsync(batch);
                });
            }
        }
        
        private async Task PrecompileBatchAsync(MaterialPSOKey[] materials)
        {
            var tasks = materials.Select(async material =>
            {
                await _precompilationSemaphore.WaitAsync();
                try
                {
                    await _psoCache.GetOrCreatePSOAsync(material);
                    
                    MaterialPrecompiled?.Invoke(this, new PSOCacheServiceEventArgs
                    {
                        MaterialKey = material,
                        EventType = MaterialRegistrationEventType.Precompiled
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to precompile material {MaterialName}", material.MaterialName);
                }
                finally
                {
                    _precompilationSemaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);
        }
        
        private void UpdateStatistics(object state)
        {
            try
            {
                _lastStats = _psoCache.GetStatistics();
                _lastStatsTime = DateTime.UtcNow;
                
                // Log slow operations
                if (_lastStats.AverageCreationTimeMs > _config.SlowOperationThresholdMs)
                {
                    _logger.LogWarning("High PSO creation time detected: {Time}ms average", 
                        _lastStats.AverageCreationTimeMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PSO cache statistics");
            }
        }
        
        private MaterialPSOKey CreateDefaultMaterialKey(string materialName)
        {
            return new MaterialPSOKey
            {
                MaterialName = materialName,
                VertexShaderPath = "DefaultVS",
                PixelShaderPath = "DefaultPS",
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RTVFormats = Format.R8G8B8A8_UNorm,
                DSVFormat = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0)
            };
        }
        
        private MaterialPSOKey CreateShaderBasedMaterialKey(string materialName, string vertexShaderPath, string pixelShaderPath)
        {
            return new MaterialPSOKey
            {
                MaterialName = materialName,
                VertexShaderPath = vertexShaderPath,
                PixelShaderPath = pixelShaderPath,
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RTVFormats = Format.R8G8B8A8_UNorm,
                DSVFormat = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0)
            };
        }
        
        public void Dispose()
        {
            _statsTimer?.Dispose();
            _precompilationSemaphore?.Dispose();
            _psoCache?.Dispose();
            
            ClearAll();
            
            _logger.LogInformation("PSO Cache Service disposed");
        }
    }
    
    /// <summary>
    /// PSO Cache Service configuration
    /// </summary>
    public class PSOCacheServiceConfig
    {
        public PSOCacheConfig CacheConfig { get; set; } = new();
        public bool IntegrateWithRenderingEngine { get; set; } = true;
        public bool EnableBackgroundPrecompilation { get; set; } = true;
        public int MaxConcurrentPrecompilation { get; set; } = 4;
        public int PrecompilationBatchSize { get; set; } = 10;
        public int SlowOperationThresholdMs { get; set; } = 100;
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(5);
        
        public static PSOCacheServiceConfig Default => new();
    }
    
    /// <summary>
    /// Material registration result
    /// </summary>
    public class MaterialRegistrationResult
    {
        public bool IsNew { get; set; }
        public bool WasUpdated { get; set; }
        public MaterialPSOKey MaterialKey { get; set; }
    }
    
    /// <summary>
    /// PSO Cache Service event arguments
    /// </summary>
    public class PSOCacheServiceEventArgs : EventArgs
    {
        public MaterialPSOKey MaterialKey { get; set; }
        public MaterialRegistrationEventType EventType { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Material registration event types
    /// </summary>
    public enum MaterialRegistrationEventType
    {
        Registered,
        Precompiled,
        SlowOperation,
        PerformanceWarning
    }
    
    /// <summary>
    /// PSO Cache Service statistics
    /// </summary>
    public struct PSOCacheServiceStatistics
    {
        public RealPSOCacheStatistics CacheStatistics { get; set; }
        public int RegisteredMaterials { get; set; }
        public int PrecompilationQueueSize { get; set; }
        public DateTime LastStatsUpdate { get; set; }
        public TimeSpan TimeSinceLastUpdate { get; set; }
        public bool EngineIntegrationEnabled { get; set; }
        public bool BackgroundPrecompilationEnabled { get; set; }
        
        public string GetFormattedReport()
        {
            return $@"PSO Cache Service Statistics:
{CacheStatistics.GetFormattedReport()}
    Registered Materials: {RegisteredMaterials}
    Precompilation Queue: {PrecompilationQueueSize}
    Last Update: {LastStatsUpdate:HH:mm:ss} ({TimeSinceLastUpdate.TotalSeconds:F1}s ago)
    Engine Integration: {EngineIntegrationEnabled}
    Background Precompilation: {BackgroundPrecompilationEnabled}";
        }
    }
}