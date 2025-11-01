using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using SharpDX;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// High-performance PSO caching system with lazy creation and intelligent management
    /// Provides significant performance improvements for material-based rendering
    /// </summary>
    public class OptimizedPSOManager : IDisposable
    {
        private readonly Device _device;
        private readonly PSOCache<MaterialPSOKey, CachedPipelineState> _cache;
        private readonly ShaderMacro[] _commonMacros;
        private readonly Timer _performanceTimer;
        private readonly ConcurrentDictionary<string, MaterialPSOKey> _materialMapping;
        
        // Performance tracking
        private long _totalCreationTime;
        private long _totalCacheLookups;
        private long _successfulCacheLookups;
        private readonly Dictionary<string, Stopwatch> _activeCreations = new Dictionary<string, Stopwatch>();
        private readonly object _creationLock = new object();
        
        // Configuration
        private readonly int _initialCapacity;
        private readonly TimeSpan _cacheExpiration;
        private readonly bool _enablePrecompilation;
        private readonly int _maxConcurrentCreations;
        
        // Statistics
        public PSOManagerStatistics Statistics { get; private set; }
        
        /// <summary>
        /// Initialize PSO manager with optimized defaults
        /// </summary>
        /// <param name="device">DirectX 12 device</param>
        /// <param name="initialCapacity">Initial cache capacity</param>
        /// <param name="cacheExpiration">Cache entry expiration time</param>
        /// <param name="enablePrecompilation">Enable pre-compilation of common variants</param>
        /// <param name="maxConcurrentCreations">Max parallel PSO creations</param>
        public OptimizedPSOManager(
            Device device,
            int initialCapacity = 1000,
            TimeSpan? cacheExpiration = null,
            bool enablePrecompilation = true,
            int maxConcurrentCreations = 4)
        {
            _device = device;
            _initialCapacity = initialCapacity;
            _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(1);
            _enablePrecompilation = enablePrecompilation;
            _maxConcurrentCreations = maxConcurrentCreations;
            
            _cache = new PSOCache<MaterialPSOKey, CachedPipelineState>(initialCapacity, _cacheExpiration);
            _materialMapping = new ConcurrentDictionary<string, MaterialPSOKey>();
            
            // Setup common shader macros for pre-compilation
            _commonMacros = new[]
            {
                new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "USE_SPECULAR", Value = "1" },
                new ShaderMacro { Name = "USE_EMISSIVE", Value = "1" },
                new ShaderMacro { Name = "USE_TRANSPARENCY", Value = "0" },
                new ShaderMacro { Name = "ENABLE_SKINNING", Value = "0" }
            };
            
            // Setup performance monitoring timer (every 5 seconds)
            _performanceTimer = new Timer(UpdateStatistics, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            // Pre-warm cache with common variants if enabled
            if (_enablePrecompilation)
            {
                Task.Run(async () => await PrewarmCacheAsync());
            }
        }
        
        /// <summary>
        /// Get or create a PSO for the given material
        /// This is the main entry point for PSO caching
        /// </summary>
        /// <param name="materialKey">Material configuration key</param>
        /// <param name="createPipelineState">Factory function to create actual PSO if not cached</param>
        /// <returns>Created or cached pipeline state object</returns>
        public async Task<CachedPipelineState> GetOrCreatePSOAsync(
            MaterialPSOKey materialKey,
            Func<Task<PipelineState>> createPipelineState = null)
        {
            createPipelineState ??= () => CreateDefaultPipelineState(materialKey);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Check cache first
                var (cachedPSO, wasCached) = _cache.GetOrCreate(materialKey, () => null);
                
                if (cachedPSO != null)
                {
                    stopwatch.Stop();
                    Interlocked.Increment(ref _successfulCacheLookups);
                    return cachedPSO;
                }
                
                // Cache miss - create new PSO
                return await CreateAndCachePSOAsync(materialKey, createPipelineState, stopwatch);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                TiXL.Core.Logging.Logger.Error(ex, $"Failed to create PSO for material: {materialKey.MaterialName}");
                throw;
            }
        }
        
        /// <summary>
        /// Get PSO for material by name (convenience method)
        /// </summary>
        /// <param name="materialName">Material name</param>
        /// <param name="shaderPaths">Shader file paths</param>
        /// <param name="macros">Shader macros</param>
        /// <returns>Pipeline state object</returns>
        public async Task<CachedPipelineState> GetMaterialPSOAsync(
            string materialName,
            (string vs, string ps, string gs, string hs, string ds) shaderPaths,
            ShaderMacro[] macros = null)
        {
            var key = new MaterialPSOKey
            {
                MaterialName = materialName,
                VertexShaderPath = shaderPaths.vs,
                PixelShaderPath = shaderPaths.ps,
                GeometryShaderPath = shaderPaths.gs,
                HullShaderPath = shaderPaths.hs,
                DomainShaderPath = shaderPaths.ds,
                ShaderMacros = new List<ShaderMacro>(macros ?? Array.Empty<ShaderMacro>())
            };
            
            return await GetOrCreatePSOAsync(key);
        }
        
        /// <summary>
        /// Remove PSO from cache by material name
        /// </summary>
        public bool RemoveMaterialPSO(string materialName)
        {
            if (_materialMapping.TryGetValue(materialName, out var key))
            {
                return _cache.Remove(key);
            }
            return false;
        }
        
        /// <summary>
        /// Clear all cached PSOs
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _materialMapping.Clear();
        }
        
        /// <summary>
        /// Get detailed performance statistics
        /// </summary>
        public PSOManagerStatistics GetDetailedStatistics()
        {
            var cacheStats = _cache.GetStatistics();
            return new PSOManagerStatistics
            {
                CacheStatistics = cacheStats,
                TotalPSOCreations = _totalCacheLookups - _successfulCacheLookups,
                TotalCacheLookups = _totalCacheLookups,
                SuccessfulCacheLookups = _successfulCacheLookups,
                AverageCreationTimeMs = (_totalCacheLookups - _successfulCacheLookups) > 0 
                    ? _totalCreationTime / 1000000.0 / (_totalCacheLookups - _successfulCacheLookups) 
                    : 0,
                ActiveCreations = _activeCreations.Count,
                CacheHitRate = cacheStats.CacheHitRate
            };
        }
        
        /// <summary>
        /// Resize cache capacity
        /// </summary>
        public void ResizeCache(int newCapacity)
        {
            _cache.Resize(newCapacity);
        }
        
        /// <summary>
        /// Force cleanup of expired cache entries
        /// </summary>
        public void ForceCacheCleanup()
        {
            _cache.ForceCleanup();
        }
        
        private async Task<CachedPipelineState> CreateAndCachePSOAsync(
            MaterialPSOKey materialKey,
            Func<Task<PipelineState>> createPipelineState,
            Stopwatch stopwatch)
        {
            string creationId = $"{materialKey.MaterialName}_{Guid.NewGuid().ToString("N")[..8]}";
            
            lock (_creationLock)
            {
                if (_activeCreations.ContainsKey(creationId))
                {
                    // This shouldn't happen, but handle duplicate creation attempts
                    creationId += "_" + DateTime.UtcNow.Ticks;
                }
                _activeCreations[creationId] = Stopwatch.StartNew();
            }
            
            try
            {
                // Create the actual PSO
                var pipelineState = await createPipelineState();
                
                // Wrap in cached wrapper
                var cachedPSO = new CachedPipelineState
                {
                    PipelineState = pipelineState,
                    CreationTime = DateTime.UtcNow,
                    MaterialKey = materialKey.Clone(),
                    CreationStopwatch = stopwatch
                };
                
                // Cache the result
                _cache.GetOrCreate(materialKey, () => cachedPSO);
                
                // Register in material mapping for easy lookup
                _materialMapping[materialKey.MaterialName] = materialKey;
                
                stopwatch.Stop();
                Interlocked.Add(ref _totalCreationTime, stopwatch.ElapsedTicks);
                
                return cachedPSO;
            }
            finally
            {
                lock (_creationLock)
                {
                    _activeCreations.Remove(creationId);
                }
            }
        }
        
        private async Task<CachedPipelineState> CreateDefaultPipelineState(MaterialPSOKey key)
        {
            // This creates a basic PSO with the provided configuration
            // In a real implementation, this would compile shaders and create the actual D3D12 PSO
            
            await Task.Delay(1); // Simulate async PSO creation
            
            // Create placeholder PSO description
            var desc = new GraphicsPipelineStateDescription
            {
                RootSignature = null, // Would be set from material
                VertexShader = new ShaderBytecode(System.Text.Encoding.UTF8.GetBytes(key.VertexShaderPath ?? "DefaultVS")),
                PixelShader = new ShaderBytecode(System.Text.Encoding.UTF8.GetBytes(key.PixelShaderPath ?? "DefaultPS")),
                GeometryShader = key.GeometryShaderPath != null ? 
                    new ShaderBytecode(System.Text.Encoding.UTF8.GetBytes(key.GeometryShaderPath)) : null,
                HullShader = key.HullShaderPath != null ? 
                    new ShaderBytecode(System.Text.Encoding.UTF8.GetBytes(key.HullShaderPath)) : null,
                DomainShader = key.DomainShaderPath != null ? 
                    new ShaderBytecode(System.Text.Encoding.UTF8.GetBytes(key.DomainShaderPath)) : null,
                BlendState = key.BlendState,
                SampleMask = int.MaxValue,
                RasterizerState = key.RasterizerState,
                DepthStencilState = key.DepthStencilState,
                InputLayout = key.InputLayout,
                PrimitiveTopologyType = key.PrimitiveTopologyType,
                RenderTargetFormats = new[] { key.RTVFormats },
                DepthStencilFormat = key.DSVFormat,
                SampleDescription = key.SampleDescription
            };
            
            // In real implementation, this would create the actual PSO:
            // var pipelineState = new PipelineState(_device, desc, null);
            
            var pipelineState = new PipelineState
            {
                Description = desc,
                IsValid = true,
                CreationTimestamp = DateTime.UtcNow
            };
            
            return pipelineState;
        }
        
        private async Task PrewarmCacheAsync()
        {
            TiXL.Core.Logging.Logger.Info("Starting PSO cache pre-warming...");
            
            try
            {
                // Pre-warm common material variants
                var commonVariants = GetCommonMaterialVariants();
                
                foreach (var variant in commonVariants)
                {
                    await GetOrCreatePSOAsync(variant);
                }
                
                TiXL.Core.Logging.Logger.Info($"PSO cache pre-warming completed with {commonVariants.Count} variants");
            }
            catch (Exception ex)
            {
                TiXL.Core.Logging.Logger.Error(ex, "Failed to pre-warm PSO cache");
            }
        }
        
        private List<MaterialPSOKey> GetCommonMaterialVariants()
        {
            var variants = new List<MaterialPSOKey>();
            
            // Base PBR variant
            variants.Add(CreateMaterialVariant("DefaultPBR", new[]
            {
                new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "USE_SPECULAR", Value = "1" }
            }));
            
            // Transparent variant
            variants.Add(CreateMaterialVariant("TransparentPBR", new[]
            {
                new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "USE_TRANSPARENCY", Value = "1" },
                new ShaderMacro { Name = "ENABLE_ALPHA_BLEND", Value = "1" }
            }));
            
            // Emissive variant
            variants.Add(CreateMaterialVariant("EmissivePBR", new[]
            {
                new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "USE_EMISSIVE", Value = "1" }
            }));
            
            // Skinned variant
            variants.Add(CreateMaterialVariant("SkinnedPBR", new[]
            {
                new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "ENABLE_SKINNING", Value = "1" }
            }));
            
            return variants;
        }
        
        private MaterialPSOKey CreateMaterialVariant(string name, ShaderMacro[] macros)
        {
            return new MaterialPSOKey
            {
                MaterialName = name,
                VertexShaderPath = $"Shaders/{name}VS.hlsl",
                PixelShaderPath = $"Shaders/{name}PS.hlsl",
                ShaderMacros = new List<ShaderMacro>(macros),
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RTVFormats = Format.R8G8B8A8_UNorm,
                DSVFormat = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0)
            };
        }
        
        private void UpdateStatistics(object state)
        {
            Statistics = GetDetailedStatistics();
        }
        
        public void Dispose()
        {
            _performanceTimer?.Dispose();
            _cache.Clear();
            _materialMapping.Clear();
        }
    }
    
    /// <summary>
    /// Cached PSO wrapper with additional metadata
    /// </summary>
    public class CachedPipelineState
    {
        public PipelineState PipelineState { get; set; }
        public MaterialPSOKey MaterialKey { get; set; }
        public DateTime CreationTime { get; set; }
        public Stopwatch CreationStopwatch { get; set; }
        public bool IsValid { get; set; } = true;
        public int AccessCount { get; set; } = 0;
        
        public TimeSpan CreationTimeElapsed => CreationStopwatch?.Elapsed ?? TimeSpan.Zero;
    }
    
    /// <summary>
    /// PSO Manager performance statistics
    /// </summary>
    public struct PSOManagerStatistics
    {
        public CacheStatistics CacheStatistics { get; set; }
        public long TotalPSOCreations { get; set; }
        public long TotalCacheLookups { get; set; }
        public long SuccessfulCacheLookups { get; set; }
        public double AverageCreationTimeMs { get; set; }
        public int ActiveCreations { get; set; }
        public double CacheHitRate { get; set; }
        
        public string GetFormattedReport()
        {
            return $@"PSO Manager Performance Report:
{CacheStatistics.GetFormattedString()}
    Total Creations: {TotalPSOCreations}
    Avg Creation Time: {AverageCreationTimeMs:F2}ms
    Active Creations: {ActiveCreations}
    Performance: {GetPerformanceRating()}";
        }
        
        private string GetPerformanceRating()
        {
            if (CacheHitRate > 0.8) return "Excellent";
            if (CacheHitRate > 0.6) return "Good";
            if (CacheHitRate > 0.4) return "Fair";
            return "Poor";
        }
    }
}