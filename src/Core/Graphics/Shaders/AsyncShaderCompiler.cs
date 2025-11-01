using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.Graphics.Shaders
{
    /// <summary>
    /// Asynchronous shader compilation system with intelligent caching and background processing
    /// Eliminates runtime compilation stalls by pre-compiling and caching shader variants
    /// </summary>
    public class AsyncShaderCompiler
    {
        private readonly ConcurrentDictionary<ShaderKey, ShaderProgram> _shaderCache;
        private readonly TaskFactory _compilationTaskFactory;
        private readonly Queue<CompilationRequest> _pendingCompilations;
        private readonly SemaphoreSlim _compilationSemaphore;
        private readonly Dictionary<string, ShaderSource> _shaderSources;
        private readonly Dictionary<string, List<ShaderMacro>> _commonMacros;
        
        private readonly int _maxConcurrentCompilations;
        private readonly Timer _compilationTimer;
        
        public int CacheHitRate { get; private set; }
        public int TotalCompilations { get; private set; }
        public int CacheHits { get; private set; }
        
        public AsyncShaderCompiler(int maxConcurrentCompilations = 2)
        {
            _maxConcurrentCompilations = maxConcurrentCompilations;
            _shaderCache = new ConcurrentDictionary<ShaderKey, ShaderProgram>();
            _compilationTaskFactory = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(maxConcurrentCompilations));
            _pendingCompilations = new Queue<CompilationRequest>();
            _compilationSemaphore = new SemaphoreSlim(maxConcurrentCompilations, maxConcurrentCompilations);
            _shaderSources = new Dictionary<string, ShaderSource>();
            _commonMacros = new Dictionary<string, List<ShaderMacro>>();
            
            SetupCommonMacros();
            
            // Process compilation queue periodically
            _compilationTimer = new Timer(ProcessCompilationQueue, null, 100, 16); // ~60Hz
        }
        
        /// <summary>
        /// Register shader source for compilation
        /// </summary>
        public void RegisterShaderSource(string shaderName, ShaderSource source)
        {
            _shaderSources[shaderName] = source;
        }
        
        /// <summary>
        /// Compile shader asynchronously (non-blocking)
        /// </summary>
        public Task<ShaderProgram> CompileShaderAsync(string shaderName, ShaderMacro[] defines = null)
        {
            var key = new ShaderKey(shaderName, defines ?? Array.Empty<ShaderMacro>());
            
            // Check cache first
            if (_shaderCache.TryGetValue(key, out var cachedShader))
            {
                CacheHits++;
                TotalCompilations++;
                UpdateCacheHitRate();
                return Task.FromResult(cachedShader);
            }
            
            // Queue for compilation
            var compilationRequest = new CompilationRequest
            {
                Key = key,
                ShaderName = shaderName,
                Defines = defines ?? Array.Empty<ShaderMacro>(),
                CompletionSource = new TaskCompletionSource<ShaderProgram>()
            };
            
            lock (_pendingCompilations)
            {
                _pendingCompilations.Enqueue(compilationRequest);
            }
            
            // Process queue if compilation slots available
            ProcessCompilationQueue();
            
            TotalCompilations++;
            UpdateCacheHitRate();
            
            return compilationRequest.CompletionSource.Task;
        }
        
        /// <summary>
        /// Pre-compile common shader variants based on usage patterns
        /// </summary>
        public async Task PrecompileCommonVariants(string shaderName)
        {
            if (!_shaderSources.TryGetValue(shaderName, out var source))
                return;
            
            var commonVariants = GetCommonVariants(shaderName);
            
            var precompileTasks = new List<Task>();
            foreach (var variant in commonVariants)
            {
                precompileTasks.Add(CompileShaderAsync(shaderName, variant));
            }
            
            await Task.WhenAll(precompileTasks);
        }
        
        /// <summary>
        /// Force compile specific shader variant (blocking)
        /// </summary>
        public ShaderProgram CompileShaderBlocking(string shaderName, ShaderMacro[] defines = null)
        {
            var task = CompileShaderAsync(shaderName, defines);
            return task.GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Get shader compilation statistics
        /// </summary>
        public ShaderCompilationStatistics GetStatistics()
        {
            return new ShaderCompilationStatistics
            {
                CacheHitRate = CacheHitRate,
                TotalCompilations = TotalCompilations,
                CacheHits = CacheHits,
                PendingCompilations = _pendingCompilations.Count,
                ActiveCompilations = _maxConcurrentCompilations - _compilationSemaphore.CurrentCount,
                CachedShaders = _shaderCache.Count
            };
        }
        
        /// <summary>
        /// Clear shader cache
        /// </summary>
        public void ClearCache()
        {
            _shaderCache.Clear();
            CacheHits = 0;
            TotalCompilations = 0;
            CacheHitRate = 0;
        }
        
        /// <summary>
        /// Warm up cache with commonly used shaders
        /// </summary>
        public async Task WarmUpCache()
        {
            var warmupTasks = new List<Task>();
            
            // Pre-compile common shader variants
            foreach (var shaderName in _shaderSources.Keys)
            {
                warmupTasks.Add(PrecompileCommonVariants(shaderName));
            }
            
            await Task.WhenAll(warmupTasks);
        }
        
        private void ProcessCompilationQueue(object state = null)
        {
            CompilationRequest request;
            
            lock (_pendingCompilations)
            {
                if (_pendingCompilations.Count == 0) return;
                request = _pendingCompilations.Dequeue();
            }
            
            _compilationSemaphore.WaitAsync().ContinueWith(t =>
            {
                try
                {
                    CompileShaderInternal(request);
                }
                finally
                {
                    _compilationSemaphore.Release();
                }
            });
        }
        
        private async void CompileShaderInternal(CompilationRequest request)
        {
            try
            {
                var shaderProgram = await CompileShaderProgram(request.ShaderName, request.Defines);
                
                // Cache the compiled shader
                _shaderCache.TryAdd(request.Key, shaderProgram);
                
                request.CompletionSource.SetResult(shaderProgram);
            }
            catch (Exception ex)
            {
                request.CompletionSource.SetException(ex);
            }
        }
        
        private async Task<ShaderProgram> CompileShaderProgram(string shaderName, ShaderMacro[] defines)
        {
            if (!_shaderSources.TryGetValue(shaderName, out var source))
            {
                throw new ArgumentException($"Shader source not found: {shaderName}");
            }
            
            // Preprocess shader on background thread
            var preprocessedSource = await Task.Run(() => PreprocessShader(source, defines));
            
            // Create pipeline state object (would use actual DirectX compilation)
            var pipelineState = await Task.Run(() => CreatePipelineState(preprocessedSource));
            
            return new ShaderProgram
            {
                PipelineState = pipelineState,
                Key = new ShaderKey(shaderName, defines),
                CompilationTime = DateTime.UtcNow
            };
        }
        
        private ShaderSource PreprocessShader(ShaderSource source, ShaderMacro[] defines)
        {
            // Simulate shader preprocessing with macro expansion
            var preprocessed = new ShaderSource
            {
                VertexShaderCode = source.VertexShaderCode,
                PixelShaderCode = source.PixelShaderCode,
                ShaderModel = source.ShaderModel
            };
            
            // Apply macros (simplified - actual implementation would process HLSL)
            foreach (var define in defines)
            {
                preprocessed.PixelShaderCode = preprocessed.PixelShaderCode.Replace($"//{define.Name}", $"#define {define.Name} {define.Value}");
            }
            
            return preprocessed;
        }
        
        private PipelineState CreatePipelineState(ShaderSource source)
        {
            // Simulate DirectX pipeline state creation
            // In actual implementation, this would use D3DCompile API
            
            return new PipelineState
            {
                VertexShader = new CompiledShader { ShaderCode = source.VertexShaderCode },
                PixelShader = new CompiledShader { ShaderCode = source.PixelShaderCode },
                CreationTime = DateTime.UtcNow
            };
        }
        
        private void SetupCommonMacros()
        {
            _commonMacros["DefaultPBR"] = new List<ShaderMacro>
            {
                new ShaderMacro { Name = "ENABLE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "ENABLE_SPECULAR", Value = "1" }
            };
            
            _commonMacros["AnimatedPBR"] = new List<ShaderMacro>
            {
                new ShaderMacro { Name = "ENABLE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "ENABLE_SKINNING", Value = "1" },
                new ShaderMacro { Name = "ENABLE_ANIMATION", Value = "1" }
            };
            
            _commonMacros["TransparentPBR"] = new List<ShaderMacro>
            {
                new ShaderMacro { Name = "ENABLE_NORMAL_MAPPING", Value = "1" },
                new ShaderMacro { Name = "ENABLE_TRANSPARENCY", Value = "1" },
                new ShaderMacro { Name = "ENABLE_ALPHA_BLEND", Value = "1" }
            };
        }
        
        private List<ShaderMacro[]> GetCommonVariants(string shaderName)
        {
            var variants = new List<ShaderMacro[]>();
            
            // Always include base variant (no macros)
            variants.Add(Array.Empty<ShaderMacro>());
            
            // Add common variants
            foreach (var variant in _commonMacros.Values)
            {
                variants.Add(variant.ToArray());
            }
            
            return variants;
        }
        
        private void UpdateCacheHitRate()
        {
            if (TotalCompilations > 0)
            {
                CacheHitRate = (int)((double)CacheHits / TotalCompilations * 100);
            }
        }
        
        public void Dispose()
        {
            _compilationTimer?.Dispose();
            _compilationSemaphore?.Dispose();
        }
    }
    
    /// <summary>
    /// Limited concurrency level task scheduler for shader compilation
    /// </summary>
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        private readonly SemaphoreSlim _semaphore;
        
        public LimitedConcurrencyLevelTaskScheduler(int maxConcurrencyLevel)
        {
            _semaphore = new SemaphoreSlim(maxConcurrencyLevel, maxConcurrencyLevel);
        }
        
        protected override void QueueTask(Task task)
        {
            _semaphore.WaitAsync().ContinueWith(t =>
            {
                TryExecuteTask(task).ContinueWith(t2 => _semaphore.Release());
            });
        }
        
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false; // Never execute inline
        }
        
        public override int MaximumConcurrencyLevel => _semaphore.CurrentCount;
    }
    
    // Supporting classes and data structures
    
    public class ShaderKey : IEquatable<ShaderKey>
    {
        public string ShaderName { get; }
        public ShaderMacro[] Defines { get; }
        
        public ShaderKey(string shaderName, ShaderMacro[] defines)
        {
            ShaderName = shaderName;
            Defines = defines ?? Array.Empty<ShaderMacro>();
        }
        
        public bool Equals(ShaderKey other)
        {
            if (ShaderName != other.ShaderName || Defines.Length != other.Defines.Length)
                return false;
            
            for (int i = 0; i < Defines.Length; i++)
            {
                if (!Defines[i].Equals(other.Defines[i]))
                    return false;
            }
            
            return true;
        }
        
        public override bool Equals(object obj) => Equals(obj as ShaderKey);
        public override int GetHashCode() => HashCode.Combine(ShaderName, Defines.Length);
    }
    
    public class ShaderMacro
    {
        public string Name { get; set; }
        public string Value { get; set; }
        
        public bool Equals(ShaderMacro other)
        {
            return other != null && Name == other.Name && Value == other.Value;
        }
        
        public override bool Equals(object obj) => Equals(obj as ShaderMacro);
        public override int GetHashCode() => HashCode.Combine(Name, Value);
    }
    
    public class ShaderSource
    {
        public string VertexShaderCode { get; set; }
        public string PixelShaderCode { get; set; }
        public string ShaderModel { get; set; }
    }
    
    public class CompilationRequest
    {
        public ShaderKey Key { get; set; }
        public string ShaderName { get; set; }
        public ShaderMacro[] Defines { get; set; }
        public TaskCompletionSource<ShaderProgram> CompletionSource { get; set; }
    }
    
    public class ShaderProgram
    {
        public PipelineState PipelineState { get; set; }
        public ShaderKey Key { get; set; }
        public DateTime CompilationTime { get; set; }
    }
    
    public class PipelineState
    {
        public CompiledShader VertexShader { get; set; }
        public CompiledShader PixelShader { get; set; }
        public DateTime CreationTime { get; set; }
    }
    
    public class CompiledShader
    {
        public string ShaderCode { get; set; }
        public byte[] CompiledBytecode { get; set; }
    }
    
    public class ShaderCompilationStatistics
    {
        public int CacheHitRate { get; set; }
        public int TotalCompilations { get; set; }
        public int CacheHits { get; set; }
        public int PendingCompilations { get; set; }
        public int ActiveCompilations { get; set; }
        public int CachedShaders { get; set; }
    }
}
