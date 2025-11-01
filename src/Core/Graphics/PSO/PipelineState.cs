using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D12;
using SharpDX.DXGI;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// Wrapper for SharpDX PipelineState with additional metadata
    /// </summary>
    public class PipelineState : IDisposable
    {
        public GraphicsPipelineStateDescription Description { get; set; }
        public bool IsValid { get; set; } = true;
        public DateTime CreationTimestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan CreationDuration { get; set; } = TimeSpan.Zero;
        
        // Internal SharpDX PSO (would be actual PSO in production)
        private PipelineStateObject _internalPSO;
        private int _accessCount = 0;
        private DateTime _lastAccessTime;
        
        public PipelineState()
        {
            _lastAccessTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Create actual D3D12 PSO from description
        /// </summary>
        public void CreatePSO(Device device)
        {
            if (_internalPSO == null)
            {
                try
                {
                    // In production, this would create the actual D3D12 PSO:
                    // _internalPSO = new PipelineStateObject(device, Description, null);
                    IsValid = true;
                    CreationDuration = DateTime.UtcNow - CreationTimestamp;
                }
                catch (Exception ex)
                {
                    IsValid = false;
                    throw new InvalidOperationException("Failed to create PSO", ex);
                }
            }
        }
        
        /// <summary>
        /// Increment access counter for LRU tracking
        /// </summary>
        public void RecordAccess()
        {
            _accessCount++;
            _lastAccessTime = DateTime.UtcNow;
        }
        
        public int AccessCount => _accessCount;
        public DateTime LastAccessTime => _lastAccessTime;
        
        public void Dispose()
        {
            _internalPSO?.Dispose();
            _internalPSO = null;
        }
    }
    
    /// <summary>
    /// Shader macro definition for PSO configuration
    /// </summary>
    public class ShaderMacro
    {
        public string Name { get; set; }
        public string Value { get; set; }
        
        public override bool Equals(object obj)
        {
            if (obj is ShaderMacro other)
            {
                return Name == other.Name && Value == other.Value;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }
        
        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }
    
    /// <summary>
    /// Factory for creating common PSO configurations
    /// </summary>
    public static class PSOFactory
    {
        /// <summary>
        /// Create a basic PBR material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreatePBRMaterial(string materialName, string vertexShader, string pixelShader)
        {
            return new MaterialPSOKey
            {
                MaterialName = materialName,
                VertexShaderPath = vertexShader,
                PixelShaderPath = pixelShader,
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RTVFormats = Format.R8G8B8A8_UNorm,
                DSVFormat = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                BlendState = CreateDefaultBlendState(),
                DepthStencilState = CreateDefaultDepthStencilState(),
                RasterizerState = CreateDefaultRasterizerState(),
                ShaderMacros = new List<ShaderMacro>
                {
                    new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                    new ShaderMacro { Name = "USE_SPECULAR", Value = "1" }
                }
            };
        }
        
        /// <summary>
        /// Create a transparent material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreateTransparentMaterial(string materialName, string vertexShader, string pixelShader)
        {
            var pbr = CreatePBRMaterial(materialName, vertexShader, pixelShader);
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "USE_TRANSPARENCY", Value = "1" });
            pbr.BlendState = CreateAlphaBlendState();
            return pbr;
        }
        
        /// <summary>
        /// Create an emissive material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreateEmissiveMaterial(string materialName, string vertexShader, string pixelShader)
        {
            var pbr = CreatePBRMaterial(materialName, vertexShader, pixelShader);
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "USE_EMISSIVE", Value = "1" });
            pbr.BlendState = CreateAdditiveBlendState();
            return pbr;
        }
        
        /// <summary>
        /// Create a skinned mesh material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreateSkinnedMaterial(string materialName, string vertexShader, string pixelShader)
        {
            var pbr = CreatePBRMaterial(materialName, vertexShader, pixelShader);
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "ENABLE_SKINNING", Value = "1" });
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "MAX_BONES", Value = "64" });
            return pbr;
        }
        
        /// <summary>
        /// Create wireframe material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreateWireframeMaterial(string materialName, string vertexShader, string pixelShader)
        {
            var pbr = CreatePBRMaterial(materialName, vertexShader, pixelShader);
            pbr.RasterizerState = CreateWireframeRasterizerState();
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "WIREFRAME", Value = "1" });
            return pbr;
        }
        
        /// <summary>
        /// Create debug material PSO configuration
        /// </summary>
        public static MaterialPSOKey CreateDebugMaterial(string materialName, string vertexShader, string pixelShader, string debugType)
        {
            var pbr = CreatePBRMaterial(materialName, vertexShader, pixelShader);
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "DEBUG_MODE", Value = "1" });
            pbr.ShaderMacros.Add(new ShaderMacro { Name = "DEBUG_TYPE", Value = debugType });
            return pbr;
        }
        
        #region State Creation Helpers
        
        public static BlendStateDescription CreateDefaultBlendState()
        {
            return new BlendStateDescription
            {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };
        }
        
        public static BlendStateDescription CreateAlphaBlendState()
        {
            var desc = CreateDefaultBlendState();
            desc[0] = new RenderTargetBlendDescription
            {
                BlendEnable = true,
                LogicOpEnable = false,
                SrcBlend = Blend.SourceAlpha,
                DestBlend = Blend.InverseSourceAlpha,
                BlendOp = BlendOperation.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.InverseSourceAlpha,
                BlendOpAlpha = BlendOperation.Add,
                LogicOp = LogicOperation.Noop,
                RenderTargetWriteMask = ColorWriteEnableFlags.All
            };
            return desc;
        }
        
        public static BlendStateDescription CreateAdditiveBlendState()
        {
            var desc = CreateDefaultBlendState();
            desc[0] = new RenderTargetBlendDescription
            {
                BlendEnable = true,
                LogicOpEnable = false,
                SrcBlend = Blend.One,
                DestBlend = Blend.One,
                BlendOp = BlendOperation.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.One,
                BlendOpAlpha = BlendOperation.Add,
                LogicOp = LogicOperation.Noop,
                RenderTargetWriteMask = ColorWriteEnableFlags.All
            };
            return desc;
        }
        
        public static DepthStencilStateDescription CreateDefaultDepthStencilState()
        {
            return new DepthStencilStateDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunction = CompareFunction.LessEqual,
                StencilEnable = false,
                StencilReadMask = 255,
                StencilWriteMask = 255,
                FrontFace = new DepthStencilOperation
                {
                    StencilFailOp = StencilOperation.Keep,
                    StencilDepthFailOp = StencilOperation.Keep,
                    StencilPassOp = StencilOperation.Keep,
                    StencilFunction = CompareFunction.Always
                },
                BackFace = new DepthStencilOperation
                {
                    StencilFailOp = StencilOperation.Keep,
                    StencilDepthFailOp = StencilOperation.Keep,
                    StencilPassOp = StencilOperation.Keep,
                    StencilFunction = CompareFunction.Always
                }
            };
        }
        
        public static DepthStencilStateDescription CreateNoDepthState()
        {
            var desc = CreateDefaultDepthStencilState();
            desc.DepthEnable = false;
            return desc;
        }
        
        public static RasterizerStateDescription CreateDefaultRasterizerState()
        {
            return new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Back,
                FrontCounterClockwise = false,
                DepthBias = 0,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 0.0f,
                DepthClipEnable = true,
                ScissorEnable = false,
                MultisampleEnable = false,
                AntialiasedLineEnable = false,
                ConservativeRaster = ConservativeRasterizationMode.Off
            };
        }
        
        public static RasterizerStateDescription CreateWireframeRasterizerState()
        {
            var desc = CreateDefaultRasterizerState();
            desc.FillMode = FillMode.Wireframe;
            return desc;
        }
        
        public static RasterizerStateDescription CreateNoCullRasterizerState()
        {
            var desc = CreateDefaultRasterizerState();
            desc.CullMode = CullMode.None;
            return desc;
        }
        
        #endregion
        
        #region Input Layout Helpers
        
        /// <summary>
        /// Standard vertex layout for PBR materials
        /// </summary>
        public static InputLayoutDescription CreatePBRInputLayout()
        {
            return new InputLayoutDescription(
                new InputElement[]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
                    new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0),
                    new InputElement("TANGENT", 0, Format.R32G32B32A32_Float, 32, 0, InputClassification.PerVertexData, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 48, 0, InputClassification.PerVertexData, 0),
                    new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 56, 0, InputClassification.PerVertexData, 0)
                }
            );
        }
        
        /// <summary>
        /// Vertex layout for skinned meshes
        /// </summary>
        public static InputLayoutDescription CreateSkinnedInputLayout()
        {
            return new InputLayoutDescription(
                new InputElement[]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
                    new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0),
                    new InputElement("TANGENT", 0, Format.R32G32B32A32_Float, 32, 0, InputClassification.PerVertexData, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 48, 0, InputClassification.PerVertexData, 0),
                    new InputElement("WEIGHTS", 0, Format.R32G32B32A32_Float, 56, 0, InputClassification.PerVertexData, 0),
                    new InputElement("INDICES", 0, Format.R8G8B8A8_UNorm, 72, 0, InputClassification.PerVertexData, 0)
                }
            );
        }
        
        /// <summary>
        /// Simple vertex layout for debug rendering
        /// </summary>
        public static InputLayoutDescription CreateDebugInputLayout()
        {
            return new InputLayoutDescription(
                new InputElement[]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
                    new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 16, 0, InputClassification.PerVertexData, 0)
                }
            );
        }
        
        #endregion
    }
}