// GraphicsRenderingTests.cs
using Xunit;
using FluentAssertions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Mocks.Graphics;
using SharpDX.Direct3D12;
using SharpDX;

namespace TiXL.Tests.Graphics
{
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.Rendering)]
    [Category(TestCategories.P0)]
    public class HeadlessRenderingTests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Fast)]
        public void MockDevice_CreateDevice_Succeeds()
        {
            // Arrange & Act
            using var device = new MockD3D12Device();
            
            // Assert
            device.DeviceInfo.Should().NotBeNull();
            device.DeviceInfo.FeatureLevel.Should().Be(FeatureLevel.Level_12_0);
            device.DeviceInfo.DedicatedVideoMemory.Should().BeGreaterThan(0);
        }
        
        [Fact]
        [Category(TestCategories.Medium)]
        public void RenderTarget_CreateTexture2D_Succeeds()
        {
            // Arrange
            using var device = new MockD3D12Device();
            var description = new ResourceDescription
            {
                Dimension = ResourceDimension.Texture2D,
                Width = 1920,
                Height = 1080,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0)
            };
            
            // Act
            using var renderTarget = device.CreateCommittedResource(
                HeapType.Default,
                ResourceStates.RenderTarget,
                description);
            
            // Assert
            renderTarget.Description.Dimension.Should().Be(ResourceDimension.Texture2D);
            renderTarget.Description.Width.Should().Be(1920);
            renderTarget.Description.Height.Should().Be(1080);
            renderTarget.Description.Format.Should().Be(Format.R8G8B8A8_UNorm);
        }
        
        [Fact]
        [Category(TestCategories.Fast)]
        public void Buffer_WriteAndReadData_Succeeds()
        {
            // Arrange
            using var device = new MockD3D12Device();
            var description = new ResourceDescription
            {
                Dimension = ResourceDimension.Buffer,
                Width = 1024,
                Height = 1,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = Format.Unknown
            };
            
            using var buffer = device.CreateCommittedResource(
                HeapType.Upload,
                ResourceStates.GenericRead,
                description);
            
            var testData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            var readData = new float[testData.Length];
            
            // Act
            buffer.WriteToBuffer(testData);
            buffer.ReadFromBuffer(readData);
            
            // Assert
            readData.Should().BeEquivalentTo(testData);
        }
        
        [Fact]
        [Category(TestCategories.Medium)]
        public void CommandQueue_ExecuteCommands_Succeeds()
        {
            // Arrange
            using var device = new MockD3D12Device();
            using var commandQueue = device.CreateCommandQueue();
            using var commandList = new MockD3D12CommandList();
            
            commandList.Close();
            
            // Act & Assert
            Action act = () => commandQueue.ExecuteCommandLists(commandList);
            act.Should().NotThrow();
        }
        
        [Theory]
        [InlineData(640, 480)]   // VGA
        [InlineData(1280, 720)]  // HD
        [InlineData(1920, 1080)] // Full HD
        [InlineData(3840, 2160)] // 4K
        public void RenderTarget_VariousResolutions_CreatesSuccessfully(int width, int height)
        {
            // Arrange
            using var device = new MockD3D12Device();
            var description = new ResourceDescription
            {
                Dimension = ResourceDimension.Texture2D,
                Width = width,
                Height = height,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm
            };
            
            // Act
            using var renderTarget = device.CreateCommittedResource(
                HeapType.Default,
                ResourceStates.RenderTarget,
                description);
            
            // Assert
            renderTarget.Description.Width.Should().Be(width);
            renderTarget.Description.Height.Should().Be(height);
        }
        
        [Fact]
        [Category(TestCategories.Slow)]
        public void MemoryUsage_MultipleResources_CreatesAndDisposesCorrectly()
        {
            // Arrange
            const int resourceCount = 100;
            var resources = new List<MockD3D12Resource>();
            
            // Act
            using (var device = new MockD3D12Device())
            {
                // Create multiple resources
                for (int i = 0; i < resourceCount; i++)
                {
                    var description = new ResourceDescription
                    {
                        Dimension = ResourceDimension.Buffer,
                        Width = 1024 * (i + 1),
                        Height = 1,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = Format.Unknown
                    };
                    
                    var resource = device.CreateCommittedResource(
                        HeapType.Default,
                        ResourceStates.GenericRead,
                        description);
                    
                    resources.Add(resource);
                }
                
                resources.Should().HaveCount(resourceCount);
                
                // Verify resource properties
                for (int i = 0; i < resourceCount; i++)
                {
                    var resource = resources[i];
                    resource.Description.Width.Should().Be(1024 * (i + 1));
                }
            } // Dispose device and all resources
            
            // Assert - All resources should be disposed
            resources.ForEach(r => r.State.Should().Be(ResourceStates.Common));
        }
    }
    
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.Performance)]
    [Category(TestCategories.P1)]
    public class GraphicsPerformanceTests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Slow)]
        public void DeviceCreation_PerformanceMeetsBudget()
        {
            // Arrange
            const double maxCreationTimeMs = 10; // 10ms budget
            const int iterations = 100;
            
            // Act & Assert
            for (int i = 0; i < iterations; i++)
            {
                var startTime = DateTime.UtcNow;
                
                using var device = new MockD3D12Device();
                device.DeviceInfo.Should().NotBeNull(); // Ensure device is created
                
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                duration.Should().BeLessThan(maxCreationTimeMs, 
                    $"Device creation should complete within {maxCreationTimeMs}ms");
            }
        }
        
        [Fact]
        [Category(TestCategories.Medium)]
        public void ResourceCreation_MultipleResources_PerformsWithinBudget()
        {
            // Arrange
            const int resourceCount = 50;
            const double maxCreationTimeMs = 100; // 100ms for all resources
            
            // Act
            var startTime = DateTime.UtcNow;
            
            using var device = new MockD3D12Device();
            for (int i = 0; i < resourceCount; i++)
            {
                var description = new ResourceDescription
                {
                    Dimension = ResourceDimension.Buffer,
                    Width = 1024,
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = Format.Unknown
                };
                
                device.CreateCommittedResource(
                    HeapType.Default,
                    ResourceStates.GenericRead,
                    description);
            }
            
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            // Assert
            duration.Should().BeLessThan(maxCreationTimeMs, 
                $"Creating {resourceCount} resources should complete within {maxCreationTimeMs}ms");
        }
    }
}