using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vortice.Direct3D12;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// PSO cache key based on material properties and shader configuration
    /// </summary>
    public class MaterialPSOKey : IPSOKey
    {
        public string MaterialName { get; set; }
        public string VertexShaderPath { get; set; }
        public string PixelShaderPath { get; set; }
        public string GeometryShaderPath { get; set; }
        public string HullShaderPath { get; set; }
        public string DomainShaderPath { get; set; }
        
        public List<ShaderMacro> ShaderMacros { get; set; }
        public Dictionary<string, object> MaterialParameters { get; set; }
        public BlendStateDescription BlendState { get; set; }
        public DepthStencilStateDescription DepthStencilState { get; set; }
        public RasterizerStateDescription RasterizerState { get; set; }
        public InputLayoutDescription InputLayout { get; set; }
        public PrimitiveTopologyType PrimitiveTopologyType { get; set; }
        public SampleDescription SampleDescription { get; set; }
        public Format RTVFormats { get; set; }
        public Format DSVFormat { get; set; }
        
        public int StreamOutputStride { get; set; }
        public bool EnableDepthBounds { get; set; }
        public DepthBoundsDescription DepthBoundsDescription { get; set; }
        public NodeMask NodeMask { get; set; }
        
        public MaterialPSOKey()
        {
            ShaderMacros = new List<ShaderMacro>();
            MaterialParameters = new Dictionary<string, object>();
            NodeMask = new NodeMask(1);
        }
        
        public MaterialPSOKey Clone()
        {
            return new MaterialPSOKey
            {
                MaterialName = MaterialName,
                VertexShaderPath = VertexShaderPath,
                PixelShaderPath = PixelShaderPath,
                GeometryShaderPath = GeometryShaderPath,
                HullShaderPath = HullShaderPath,
                DomainShaderPath = DomainShaderPath,
                ShaderMacros = new List<ShaderMacro>(ShaderMacros),
                MaterialParameters = new Dictionary<string, object>(MaterialParameters),
                BlendState = BlendState,
                DepthStencilState = DepthStencilState,
                RasterizerState = RasterizerState,
                InputLayout = InputLayout,
                PrimitiveTopologyType = PrimitiveTopologyType,
                SampleDescription = SampleDescription,
                RTVFormats = RTVFormats,
                DSVFormat = DSVFormat,
                StreamOutputStride = StreamOutputStride,
                EnableDepthBounds = EnableDepthBounds,
                DepthBoundsDescription = DepthBoundsDescription,
                NodeMask = NodeMask
            };
        }
        
        public override bool Equals(object obj)
        {
            if (obj is MaterialPSOKey other)
            {
                return Equals(other);
            }
            return false;
        }
        
        public bool Equals(IPSOKey other)
        {
            if (other is MaterialPSOKey materialKey)
            {
                return MaterialName == materialKey.MaterialName &&
                       VertexShaderPath == materialKey.VertexShaderPath &&
                       PixelShaderPath == materialKey.PixelShaderPath &&
                       GeometryShaderPath == materialKey.GeometryShaderPath &&
                       HullShaderPath == materialKey.HullShaderPath &&
                       DomainShaderPath == materialKey.DomainShaderPath &&
                       ShaderMacros.SequenceEqual(materialKey.ShaderMacros) &&
                       MaterialParameters.Count == materialKey.MaterialParameters.Count &&
                       MaterialParameters.All(kvp => materialKey.MaterialParameters.TryGetValue(kvp.Key, out var value) && 
                                                   kvp.Value?.Equals(value) == true) &&
                       CompareBlendState(BlendState, materialKey.BlendState) &&
                       CompareDepthStencilState(DepthStencilState, materialKey.DepthStencilState) &&
                       CompareRasterizerState(RasterizerState, materialKey.RasterizerState) &&
                       PrimitiveTopologyType == materialKey.PrimitiveTopologyType &&
                       SampleDescription.Equals(materialKey.SampleDescription) &&
                       RTVFormats == materialKey.RTVFormats &&
                       DSVFormat == materialKey.DSVFormat &&
                       StreamOutputStride == materialKey.StreamOutputStride &&
                       EnableDepthBounds == materialKey.EnableDepthBounds &&
                       NodeMask.Value == materialKey.NodeMask.Value;
            }
            return false;
        }
        
        private bool CompareBlendState(BlendStateDescription a, BlendStateDescription b)
        {
            if (a.AlphaToCoverageEnable != b.AlphaToCoverageEnable) return false;
            if (a.IndependentBlendEnable != b.IndependentBlendEnable) return false;
            
            for (int i = 0; i < 8; i++)
            {
                if (!CompareRenderTargetBlend(a[i], b[i])) return false;
            }
            
            return true;
        }
        
        private bool CompareRenderTargetBlend(RenderTargetBlendDescription a, RenderTargetBlendDescription b)
        {
            return a.BlendEnable == b.BlendEnable &&
                   a.LogicOpEnable == b.LogicOpEnable &&
                   a.SrcBlend == b.SrcBlend &&
                   a.DestBlend == b.DestBlend &&
                   a.BlendOp == b.BlendOp &&
                   a.SrcBlendAlpha == b.SrcBlendAlpha &&
                   a.DestBlendAlpha == b.DestBlendAlpha &&
                   a.BlendOpAlpha == b.BlendOpAlpha &&
                   a.LogicOp == b.LogicOp &&
                   a.RenderTargetWriteMask == b.RenderTargetWriteMask;
        }
        
        private bool CompareDepthStencilState(DepthStencilStateDescription a, DepthStencilStateDescription b)
        {
            return a.DepthEnable == b.DepthEnable &&
                   a.DepthWriteMask == b.DepthWriteMask &&
                   a.DepthFunction == b.DepthFunction &&
                   a.StencilEnable == b.StencilEnable &&
                   a.StencilReadMask == b.StencilReadMask &&
                   a.StencilWriteMask == b.StencilWriteMask &&
                   CompareDepthStencilOperation(a.FrontFace, b.FrontFace) &&
                   CompareDepthStencilOperation(a.BackFace, b.BackFace);
        }
        
        private bool CompareDepthStencilOperation(DepthStencilOperation a, DepthStencilOperation b)
        {
            return a.StencilFailOp == b.StencilFailOp &&
                   a.StencilDepthFailOp == b.StencilDepthFailOp &&
                   a.StencilPassOp == b.StencilPassOp &&
                   a.StencilFunc == b.StencilFunc;
        }
        
        private bool CompareRasterizerState(RasterizerStateDescription a, RasterizerStateDescription b)
        {
            return a.FillMode == b.FillMode &&
                   a.CullMode == b.CullMode &&
                   a.FrontCounterClockwise == b.FrontCounterClockwise &&
                   a.DepthBias == b.DepthBias &&
                   a.DepthBiasClamp == b.DepthBiasClamp &&
                   a.SlopeScaledDepthBias == b.SlopeScaledDepthBias &&
                   a.DepthClipEnable == b.DepthClipEnable &&
                   a.ScissorEnable == b.ScissorEnable &&
                   a.MultisampleEnable == b.MultisampleEnable &&
                   a.AntialiasedLineEnable == b.AntialiasedLineEnable &&
                   a.ConservativeRaster == b.ConservativeRaster;
        }
        
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(MaterialName);
            hash.Add(VertexShaderPath);
            hash.Add(PixelShaderPath);
            hash.Add(GeometryShaderPath);
            hash.Add(HullShaderPath);
            hash.Add(DomainShaderPath);
            
            foreach (var macro in ShaderMacros)
            {
                hash.Add(macro.GetHashCode());
            }
            
            foreach (var param in MaterialParameters)
            {
                hash.Add(param.Key);
                hash.Add(param.Value?.GetHashCode() ?? 0);
            }
            
            hash.Add((int)BlendState.AlphaToCoverageEnable);
            hash.Add((int)BlendState.IndependentBlendEnable);
            hash.Add(PrimitiveTopologyType);
            hash.Add(SampleDescription.GetHashCode());
            hash.Add((int)RTVFormats);
            hash.Add((int)DSVFormat);
            hash.Add(StreamOutputStride);
            hash.Add(EnableDepthBounds);
            hash.Add(NodeMask.Value);
            
            return hash.ToHashCode();
        }
        
        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            
            // Write strings
            writer.Write(MaterialName ?? "");
            writer.Write(VertexShaderPath ?? "");
            writer.Write(PixelShaderPath ?? "");
            writer.Write(GeometryShaderPath ?? "");
            writer.Write(HullShaderPath ?? "");
            writer.Write(DomainShaderPath ?? "");
            
            // Write shader macros
            writer.Write(ShaderMacros.Count);
            foreach (var macro in ShaderMacros)
            {
                writer.Write(macro.Name ?? "");
                writer.Write(macro.Value ?? "");
            }
            
            // Write material parameters
            writer.Write(MaterialParameters.Count);
            foreach (var param in MaterialParameters)
            {
                writer.Write(param.Key ?? "");
                writer.Write(param.Value?.ToString() ?? "");
            }
            
            // Write states (simplified - would need more detailed serialization in production)
            writer.Write((int)PrimitiveTopologyType);
            writer.Write(SampleDescription.GetHashCode());
            writer.Write((int)RTVFormats);
            writer.Write((int)DSVFormat);
            writer.Write(StreamOutputStride);
            writer.Write(EnableDepthBounds);
            writer.Write(NodeMask.Value);
            
            return ms.ToArray();
        }
        
        public string ToDebugString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Material: {MaterialName}");
            sb.AppendLine($"VS: {VertexShaderPath}");
            sb.AppendLine($"PS: {PixelShaderPath}");
            
            if (GeometryShaderPath != null)
                sb.AppendLine($"GS: {GeometryShaderPath}");
            if (HullShaderPath != null)
                sb.AppendLine($"HS: {HullShaderPath}");
            if (DomainShaderPath != null)
                sb.AppendLine($"DS: {DomainShaderPath}");
            
            if (ShaderMacros.Count > 0)
            {
                sb.AppendLine("Macros:");
                foreach (var macro in ShaderMacros)
                {
                    sb.AppendLine($"  {macro.Name}={macro.Value}");
                }
            }
            
            sb.AppendLine($"Topology: {PrimitiveTopologyType}");
            sb.AppendLine($"RTV: {RTVFormats}, DSV: {DSVFormat}");
            
            return sb.ToString();
        }
    }
}