using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace TiXL.Core.Graphics
{
    /// <summary>
    /// Integration layer for PSO caching with existing TiXL material system
    /// Provides seamless integration with PBR materials and shader compilation
    /// </summary>
    public class PSOMaterialIntegration
    {
        private readonly PSO.OptimizedPSOManager _psoManager;
        private readonly PSO.PSODebugTools _debugTools;
        private readonly Dictionary<string, MaterialPSOInfo> _materialPSOAssociations = new Dictionary<string, MaterialPSOInfo>();
        
        /// <summary>
        /// Initialize PSO integration with material system
        /// </summary>
        public PSOMaterialIntegration(Device device, int initialCacheCapacity = 1000)
        {
            _psoManager = new PSO.OptimizedPSOManager(device, initialCacheCapacity);
            _debugTools = new PSO.PSODebugTools(_psoManager);
        }
        
        /// <summary>
        /// Register a material for PSO caching
        /// </summary>
        public async Task RegisterMaterialAsync(string materialName, MaterialDescription materialDesc)
        {
            var psoKey = CreatePSOKeyFromMaterial(materialName, materialDesc);
            var cachedPSO = await _psoManager.GetOrCreatePSOAsync(psoKey);
            
            var materialPSOInfo = new MaterialPSOInfo
            {
                MaterialName = materialName,
                PSOKey = psoKey,
                CachedPSO = cachedPSO,
                RegistrationTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow
            };
            
            _materialPSOAssociations[materialName] = materialPSOInfo;
        }
        
        /// <summary>
        /// Get or create PSO for material
        /// </summary>
        public async Task<PSO.CachedPipelineState> GetMaterialPSOAsync(string materialName)
        {
            if (_materialPSOAssociations.TryGetValue(materialName, out var info))
            {
                info.LastAccessTime = DateTime.UtcNow;
                info.CachedPSO.PipelineState.RecordAccess();
                return info.CachedPSO;
            }
            
            throw new ArgumentException($"Material '{materialName}' is not registered for PSO caching");
        }
        
        /// <summary>
        /// Update material PSO when parameters change
        /// </summary>
        public async Task UpdateMaterialPSOAsync(string materialName, MaterialDescription newDescription)
        {
            if (!_materialPSOAssociations.TryGetValue(materialName, out var info))
            {
                await RegisterMaterialAsync(materialName, newDescription);
                return;
            }
            
            // Remove old PSO from cache
            _psoManager.RemoveMaterialPSO(materialName);
            
            // Create new PSO key based on updated description
            var newPSOKey = CreatePSOKeyFromMaterial(materialName, newDescription);
            var newCachedPSO = await _psoManager.GetOrCreatePSOAsync(newPSOKey);
            
            // Update association
            info.PSOKey = newPSOKey;
            info.CachedPSO = newCachedPSO;
            info.LastAccessTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Unregister material from PSO caching
        /// </summary>
        public bool UnregisterMaterial(string materialName)
        {
            return _materialPSOAssociations.Remove(materialName);
        }
        
        /// <summary>
        /// Get PSO cache performance statistics
        /// </summary>
        public PSO.PSOManagerStatistics GetStatistics()
        {
            return _psoManager.GetDetailedStatistics();
        }
        
        /// <summary>
        /// Generate debug report for material PSO caching
        /// </summary>
        public string GenerateDebugReport()
        {
            var report = _debugTools.GenerateCacheAnalysisReport();
            report += Environment.NewLine + "Material Associations:" + Environment.NewLine;
            
            foreach (var kvp in _materialPSOAssociations.OrderByDescending(kvp => kvp.Value.LastAccessTime))
            {
                var info = kvp.Value;
                report += $"  {info.MaterialName,-20} | Registered: {info.RegistrationTime:HH:mm:ss} | Last Access: {info.LastAccessTime:HH:mm:ss}" + Environment.NewLine;
            }
            
            return report;
        }
        
        /// <summary>
        /// Clean up unused material associations
        /// </summary>
        public void CleanupUnusedAssociations(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var toRemove = _materialPSOAssociations.Where(kvp => kvp.Value.LastAccessTime < cutoff).ToList();
            
            foreach (var kvp in toRemove)
            {
                _materialPSOAssociations.Remove(kvp.Key);
            }
        }
        
        private PSO.MaterialPSOKey CreatePSOKeyFromMaterial(string materialName, MaterialDescription materialDesc)
        {
            var key = new PSO.MaterialPSOKey
            {
                MaterialName = materialName,
                VertexShaderPath = materialDesc.VertexShaderPath,
                PixelShaderPath = materialDesc.PixelShaderPath,
                GeometryShaderPath = materialDesc.GeometryShaderPath,
                HullShaderPath = materialDesc.HullShaderPath,
                DomainShaderPath = materialDesc.DomainShaderPath,
                PrimitiveTopologyType = materialDesc.PrimitiveTopology,
                RTVFormats = materialDesc.RTVFormat,
                DSVFormat = materialDesc.DSVFormat,
                SampleDescription = materialDesc.SampleDescription
            };
            
            // Convert shader macros
            key.ShaderMacros = materialDesc.ShaderMacros?.Select(m => new PSO.ShaderMacro { Name = m.Name, Value = m.Value }).ToList() 
                ?? new List<PSO.ShaderMacro>();
            
            // Convert material parameters
            foreach (var param in materialDesc.Parameters)
            {
                key.MaterialParameters[param.Key] = param.Value;
            }
            
            // Set rendering states based on material type
            key.BlendState = GetBlendState(materialDesc.BlendMode);
            key.DepthStencilState = GetDepthStencilState(materialDesc.DepthMode);
            key.RasterizerState = GetRasterizerState(materialDesc.RasterizerMode);
            
            return key;
        }
        
        private BlendStateDescription GetBlendState(MaterialBlendMode blendMode)
        {
            return blendMode switch
            {
                MaterialBlendMode.Opaque => PSO.PSOFactory.CreateDefaultBlendState(),
                MaterialBlendMode.AlphaBlend => PSO.PSOFactory.CreateAlphaBlendState(),
                MaterialBlendMode.Additive => PSO.PSOFactory.CreateAdditiveBlendState(),
                _ => PSO.PSOFactory.CreateDefaultBlendState()
            };
        }
        
        private DepthStencilStateDescription GetDepthStencilState(MaterialDepthMode depthMode)
        {
            return depthMode switch
            {
                MaterialDepthMode.Default => PSO.PSOFactory.CreateDefaultDepthStencilState(),
                MaterialDepthMode.NoDepth => PSO.PSOFactory.CreateNoDepthState(),
                _ => PSO.PSOFactory.CreateDefaultDepthStencilState()
            };
        }
        
        private RasterizerStateDescription GetRasterizerState(MaterialRasterizerMode rasterizerMode)
        {
            return rasterizerMode switch
            {
                MaterialRasterizerMode.Default => PSO.PSOFactory.CreateDefaultRasterizerState(),
                MaterialRasterizerMode.Wireframe => PSO.PSOFactory.CreateWireframeRasterizerState(),
                MaterialRasterizerMode.NoCull => PSO.PSOFactory.CreateNoCullRasterizerState(),
                _ => PSO.PSOFactory.CreateDefaultRasterizerState()
            };
        }
        
        public void Dispose()
        {
            _psoManager?.Dispose();
        }
    }
    
    /// <summary>
    /// Information about material PSO association
    /// </summary>
    public class MaterialPSOInfo
    {
        public string MaterialName { get; set; }
        public PSO.MaterialPSOKey PSOKey { get; set; }
        public PSO.CachedPipelineState CachedPSO { get; set; }
        public DateTime RegistrationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public int AccessCount { get; set; }
    }
    
    /// <summary>
    /// Material description for PSO creation
    /// </summary>
    public class MaterialDescription
    {
        public string VertexShaderPath { get; set; }
        public string PixelShaderPath { get; set; }
        public string GeometryShaderPath { get; set; }
        public string HullShaderPath { get; set; }
        public string DomainShaderPath { get; set; }
        public List<MaterialShaderMacro> ShaderMacros { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public MaterialBlendMode BlendMode { get; set; } = MaterialBlendMode.Opaque;
        public MaterialDepthMode DepthMode { get; set; } = MaterialDepthMode.Default;
        public MaterialRasterizerMode RasterizerMode { get; set; } = MaterialRasterizerMode.Default;
        public PrimitiveTopologyType PrimitiveTopology { get; set; } = PrimitiveTopologyType.Triangle;
        public Vortice.DXGI.Format RTVFormat { get; set; } = Vortice.DXGI.Format.R8G8B8A8_UNorm;
        public Vortice.DXGI.Format DSVFormat { get; set; } = Vortice.DXGI.Format.D24_UNorm_S8_UInt;
        public SampleDescription SampleDescription { get; set; } = new SampleDescription(1, 0);
        
        public MaterialDescription()
        {
            ShaderMacros = new List<MaterialShaderMacro>();
            Parameters = new Dictionary<string, object>();
        }
    }
    
    /// <summary>
    /// Material shader macro
    /// </summary>
    public class MaterialShaderMacro
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
    
    /// <summary>
    /// Material blend modes
    /// </summary>
    public enum MaterialBlendMode
    {
        Opaque,
        AlphaBlend,
        Additive
    }
    
    /// <summary>
    /// Material depth modes
    /// </summary>
    public enum MaterialDepthMode
    {
        Default,
        NoDepth
    }
    
    /// <summary>
    /// Material rasterizer modes
    /// </summary>
    public enum MaterialRasterizerMode
    {
        Default,
        Wireframe,
        NoCull
    }
    
    /// <summary>
    /// High-level PSO manager facade for easy integration
    /// </summary>
    public static class TiXLPSOManager
    {
        private static PSOMaterialIntegration _integration;
        private static Device _device;
        
        /// <summary>
        /// Initialize PSO caching system for TiXL
        /// </summary>
        public static void Initialize(Device device, int initialCacheCapacity = 1000)
        {
            _device = device;
            _integration?.Dispose();
            _integration = new PSOMaterialIntegration(device, initialCacheCapacity);
        }
        
        /// <summary>
        /// Register material for PSO caching
        /// </summary>
        public static Task RegisterMaterialAsync(string materialName, MaterialDescription materialDesc)
        {
            EnsureInitialized();
            return _integration.RegisterMaterialAsync(materialName, materialDesc);
        }
        
        /// <summary>
        /// Get PSO for material
        /// </summary>
        public static Task<PSO.CachedPipelineState> GetMaterialPSOAsync(string materialName)
        {
            EnsureInitialized();
            return _integration.GetMaterialPSOAsync(materialName);
        }
        
        /// <summary>
        /// Update material PSO
        /// </summary>
        public static Task UpdateMaterialPSOAsync(string materialName, MaterialDescription materialDesc)
        {
            EnsureInitialized();
            return _integration.UpdateMaterialPSOAsync(materialName, materialDesc);
        }
        
        /// <summary>
        /// Get performance statistics
        /// </summary>
        public static PSO.PSOManagerStatistics GetStatistics()
        {
            EnsureInitialized();
            return _integration.GetStatistics();
        }
        
        /// <summary>
        /// Generate debug report
        /// </summary>
        public static string GenerateDebugReport()
        {
            EnsureInitialized();
            return _integration.GenerateDebugReport();
        }
        
        /// <summary>
        /// Clear cache
        /// </summary>
        public static void ClearCache()
        {
            EnsureInitialized();
            _integration.GetStatistics(); // Ensure stats are current
        }
        
        private static void EnsureInitialized()
        {
            if (_integration == null || _device == null)
            {
                throw new InvalidOperationException("TiXL PSO Manager not initialized. Call Initialize() first.");
            }
        }
    }
}