using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Unit tests for CacheManager
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class CacheManagerTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<CacheManagerTests> _logger;

        public CacheManagerTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<CacheManagerTests>>();
        }

        [Fact]
        public void CacheManager_CreateInstance_WithDefaultConfig_InitializesCorrectly()
        {
            // Arrange & Act
            var cache = new CacheManager();

            // Assert
            cache.Should().NotBeNull();
            var stats = cache.GetStatistics();
            stats.CacheSize.Should().Be(0);
            stats.HitRate.Should().Be(0.0f);
        }

        [Theory]
        [InlineData(1024 * 1024)]      // 1MB
        [InlineData(10 * 1024 * 1024)] // 10MB
        [InlineData(100 * 1024 * 1024)] // 100MB
        public void CacheManager_CreateInstance_WithCustomMemoryLimit_InitializesCorrectly(long memoryLimit)
        {
            // Arrange & Act
            var cache = new CacheManager(memoryLimit);

            // Assert
            cache.Should().NotBeNull();
            cache.MemoryLimit.Should().Be(memoryLimit);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CacheManager_CreateInstance_WithInvalidMemoryLimit_ThrowsArgumentException(long memoryLimit)
        {
            // Arrange & Act & Assert
            Action act = () => new CacheManager(memoryLimit);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_Store_WithValidData_StoresSuccessfully()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";
            var data = new { value = 42, timestamp = DateTime.UtcNow };

            // Act
            cache.Store(nodeId, key, data);

            // Assert
            var retrieved = cache.Retrieve(nodeId, key);
            retrieved.Should().NotBeNull();
            retrieved.Should().BeEquivalentTo(data);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Store_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var cache = new CacheManager();
            var key = "test-key";
            var data = new { value = 42 };

            // Act & Assert
            Action act = () => cache.Store(nodeId, key, data);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Store_WithInvalidKey_ThrowsArgumentException(string key)
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var data = new { value = 42 };

            // Act & Assert
            Action act = () => cache.Store(nodeId, key, data);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_Store_WithNullData_ThrowsArgumentException()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";

            // Act & Assert
            Action act = () => cache.Store(nodeId, key, null!);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_Retrieve_WithExistingData_ReturnsCorrectData()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";
            var data = new { value = 42, name = "test" };
            
            cache.Store(nodeId, key, data);

            // Act
            var retrieved = cache.Retrieve(nodeId, key);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void CacheManager_Retrieve_WithNonExistentNode_ReturnsNull()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "non-existent-node";
            var key = "test-key";

            // Act
            var retrieved = cache.Retrieve(nodeId, key);

            // Assert
            retrieved.Should().BeNull();
        }

        [Fact]
        public void CacheManager_Retrieve_WithNonExistentKey_ReturnsNull()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "non-existent-key";
            
            cache.Store(nodeId, "other-key", new { value = 42 });

            // Act
            var retrieved = cache.Retrieve(nodeId, key);

            // Assert
            retrieved.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Retrieve_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var cache = new CacheManager();
            var key = "test-key";

            // Act & Assert
            Action act = () => cache.Retrieve(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Retrieve_WithInvalidKey_ThrowsArgumentException(string key)
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";

            // Act & Assert
            Action act = () => cache.Retrieve(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_Has_WithExistingData_ReturnsTrue()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";
            var data = new { value = 42 };
            
            cache.Store(nodeId, key, data);

            // Act
            var hasData = cache.Has(nodeId, key);

            // Assert
            hasData.Should().BeTrue();
        }

        [Fact]
        public void CacheManager_Has_WithNonExistentData_ReturnsFalse()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";

            // Act
            var hasData = cache.Has(nodeId, key);

            // Assert
            hasData.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Has_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var cache = new CacheManager();
            var key = "test-key";

            // Act & Assert
            Action act = () => cache.Has(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_Has_WithInvalidKey_ThrowsArgumentException(string key)
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";

            // Act & Assert
            Action act = () => cache.Has(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_InvalidateNode_WithExistingNode_RemovesAllData()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key1 = "key1";
            var key2 = "key2";
            
            cache.Store(nodeId, key1, new { value = 1 });
            cache.Store(nodeId, key2, new { value = 2 });
            cache.Store("other-node", key1, new { value = 3 });

            // Act
            cache.InvalidateNode(nodeId);

            // Assert
            cache.Has(nodeId, key1).Should().BeFalse();
            cache.Has(nodeId, key2).Should().BeFalse();
            cache.Has("other-node", key1).Should().BeTrue(); // Other node should be unaffected
        }

        [Fact]
        public void CacheManager_InvalidateNode_WithNonExistentNode_HandlesGracefully()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => cache.InvalidateNode(nodeId);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_InvalidateNode_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var cache = new CacheManager();

            // Act & Assert
            Action act = () => cache.InvalidateNode(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_InvalidateKey_WithExistingKey_RemovesCorrectData()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "test-key";
            
            cache.Store(nodeId, key, new { value = 42 });

            // Act
            cache.InvalidateKey(nodeId, key);

            // Assert
            cache.Has(nodeId, key).Should().BeFalse();
        }

        [Fact]
        public void CacheManager_InvalidateKey_WithNonExistentKey_HandlesGracefully()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "non-existent-key";

            // Act & Assert
            Action act = () => cache.InvalidateKey(nodeId, key);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_InvalidateKey_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var cache = new CacheManager();
            var key = "test-key";

            // Act & Assert
            Action act = () => cache.InvalidateKey(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CacheManager_InvalidateKey_WithInvalidKey_ThrowsArgumentException(string key)
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";

            // Act & Assert
            Action act = () => cache.InvalidateKey(nodeId, key);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CacheManager_Clear_WithData_RemovesAllData()
        {
            // Arrange
            var cache = new CacheManager();
            
            cache.Store("node1", "key1", new { value = 1 });
            cache.Store("node1", "key2", new { value = 2 });
            cache.Store("node2", "key1", new { value = 3 });

            // Act
            cache.Clear();

            // Assert
            cache.Has("node1", "key1").Should().BeFalse();
            cache.Has("node1", "key2").Should().BeFalse();
            cache.Has("node2", "key1").Should().BeFalse();
        }

        [Fact]
        public void CacheManager_LRUEviction_WithSmallMemoryLimit_EvictsCorrectly()
        {
            // Arrange
            // Set memory limit to something small to force eviction
            var cache = new CacheManager(1024); // 1KB limit
            
            // Store data that should exceed the limit
            var largeData = new { value = new byte[1024] }; // ~1KB
            var smallData = new { value = 100 }; // Small data

            cache.Store("node1", "key1", largeData);

            // Act - Store another item to trigger eviction
            cache.Store("node2", "key1", smallData);

            // Assert - Large data should be evicted, small data should remain
            cache.Has("node1", "key1").Should().BeFalse(); // Should be evicted
            cache.Has("node2", "key1").Should().BeTrue();  // Should remain
        }

        [Fact]
        public void CacheManager_LRUEviction_LeastRecentlyUsed_EvictedFirst()
        {
            // Arrange
            var cache = new CacheManager(2048); // 2KB limit
            
            cache.Store("node1", "key1", new { value = new byte[1024] }); // 1KB
            cache.Store("node2", "key1", new { value = new byte[1024] }); // 1KB
            
            // Access node1 to make it more recently used
            cache.Retrieve("node1", "key1");

            // Act - Store third item to trigger eviction
            cache.Store("node3", "key1", new { value = new byte[1024] }); // 1KB

            // Assert - node2 (least recently used) should be evicted
            cache.Has("node1", "key1").Should().BeTrue();  // Recently accessed
            cache.Has("node2", "key1").Should().BeFalse(); // Least recently used - evicted
            cache.Has("node3", "key1").Should().BeTrue();  // Newly added
        }

        [Fact]
        public void CacheManager_GetStatistics_WithData_ReturnsCorrectStats()
        {
            // Arrange
            var cache = new CacheManager();
            
            // Store some data
            cache.Store("node1", "key1", new { value = 1 });
            cache.Store("node1", "key2", new { value = 2 });
            cache.Store("node2", "key1", new { value = 3 });

            // Retrieve some data (hits)
            cache.Retrieve("node1", "key1"); // Hit
            cache.Retrieve("node1", "key2"); // Hit
            cache.Has("node2", "key1");      // Hit (Has also counts as access)

            // Miss
            cache.Retrieve("node3", "nonexistent"); // Miss

            // Act
            var stats = cache.GetStatistics();

            // Assert
            stats.CacheSize.Should().BeGreaterThan(0);
            stats.HitRate.Should().BeGreaterThan(0.0f);
            stats.TotalAccesses.Should().BeGreaterThan(0);
            stats.CacheHits.Should().BeGreaterThan(0);
            stats.CacheMisses.Should().Be(1);
        }

        [Fact]
        public void CacheManager_GetStatistics_WithoutData_ReturnsDefaultStats()
        {
            // Arrange
            var cache = new CacheManager();

            // Act
            var stats = cache.GetStatistics();

            // Assert
            stats.CacheSize.Should().Be(0);
            stats.HitRate.Should().Be(0.0f);
            stats.TotalAccesses.Should().Be(0);
            stats.CacheHits.Should().Be(0);
            stats.CacheMisses.Should().Be(0);
        }

        [Fact]
        public void CacheManager_GetMemoryUsage_WithData_ReturnsCorrectUsage()
        {
            // Arrange
            var cache = new CacheManager();
            var data = new { value = new byte[1024] }; // ~1KB of data
            
            cache.Store("node1", "key1", data);

            // Act
            var usage = cache.GetMemoryUsage();

            // Assert
            usage.Should().BeGreaterThan(0);
            usage.Should().BeGreaterThanOrEqualTo(1024); // At least the data size
        }

        [Fact]
        public void CacheManager_ComplexDataTypes_WithVariousObjects_HandlesCorrectly()
        {
            // Arrange
            var cache = new CacheManager();
            
            var testCases = new[]
            {
                new { node = "node1", key = "int", data = 42 },
                new { node = "node1", key = "string", data = "test string" },
                new { node = "node1", key = "datetime", data = DateTime.UtcNow },
                new { node = "node2", key = "list", data = new List<int> { 1, 2, 3 } },
                new { node = "node2", key = "dict", data = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } } }
            };

            // Act - Store all data
            foreach (var testCase in testCases)
            {
                cache.Store(testCase.node, testCase.key, testCase.data);
            }

            // Assert - All data should be retrievable
            cache.Has("node1", "int").Should().BeTrue();
            cache.Has("node1", "string").Should().BeTrue();
            cache.Has("node1", "datetime").Should().BeTrue();
            cache.Has("node2", "list").Should().BeTrue();
            cache.Has("node2", "dict").Should().BeTrue();

            var retrievedInt = cache.Retrieve("node1", "int");
            retrievedInt.Should().Be(42);
        }

        [Fact]
        public async Task CacheManager_ConcurrentAccess_MultipleThreads_Succeeds()
        {
            // Arrange
            var cache = new CacheManager();
            var tasks = new List<Task>();
            var nodeCount = 10;
            var operationsPerNode = 10;

            // Act - Perform concurrent operations
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                var nodeId = $"node-{nodeIndex}";
                
                tasks.Add(Task.Run(() =>
                {
                    for (int opIndex = 0; opIndex < operationsPerNode; opIndex++)
                    {
                        var key = $"key-{opIndex}";
                        var data = new { value = nodeIndex * operationsPerNode + opIndex };
                        
                        // Store data
                        cache.Store(nodeId, key, data);
                        
                        // Retrieve data
                        var retrieved = cache.Retrieve(nodeId, key);
                        retrieved.Should().NotBeNull();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var stats = cache.GetStatistics();
            stats.CacheHits.Should().Be(nodeCount * operationsPerNode);
            cache.Has("node-0", "key-0").Should().BeTrue();
            cache.Has($"node-{nodeCount - 1}", $"key-{operationsPerNode - 1}").Should().BeTrue();
        }

        [Fact]
        public void CacheManager_MemoryPressure_WithLargeObjects_HandlesCorrectly()
        {
            // Arrange
            var cache = new CacheManager(10 * 1024 * 1024); // 10MB limit
            
            // Create large objects that approach the memory limit
            var largeObjectSize = 1024 * 1024; // 1MB
            var numberOfLargeObjects = 5;

            // Act - Store large objects
            for (int i = 0; i < numberOfLargeObjects; i++)
            {
                var largeData = new { value = new byte[largeObjectSize] };
                cache.Store($"large-node-{i}", "data", largeData);
            }

            // Store one more to trigger eviction
            cache.Store("overflow-node", "data", new { value = new byte[1024] });

            // Assert - Some objects should be evicted due to memory pressure
            var stats = cache.GetStatistics();
            stats.CacheSize.Should().BeGreaterThan(0);
            
            // Memory usage should be within reasonable bounds
            cache.GetMemoryUsage().Should().BeLessThanOrEqualTo(10 * 1024 * 1024);
        }

        [Fact]
        public void CacheManager_NullValues_HandlesNullDataCorrectly()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "null-key";

            // Act & Assert - Null data should be rejected
            Action act = () => cache.Store(nodeId, key, null!);
            act.Should().Throw<ArgumentException>();

            // But we can store non-null data that contains null properties
            var dataWithNull = new { value = (object?)null, name = "test" };
            cache.Store(nodeId, key, dataWithNull);

            var retrieved = cache.Retrieve(nodeId, key);
            retrieved.Should().NotBeNull();
            retrieved.Should().BeEquivalentTo(dataWithNull);
        }

        [Fact]
        public void CacheManager_SerializationRoundtrip_DataIntegrityMaintained()
        {
            // Arrange
            var cache = new CacheManager();
            var nodeId = "test-node";
            var key = "roundtrip-key";
            
            // Create complex object with various data types
            var originalData = new
            {
                Integer = 42,
                String = "test string",
                Boolean = true,
                DateTime = DateTime.UtcNow,
                Array = new[] { 1, 2, 3, 4, 5 },
                Dictionary = new Dictionary<string, object>
                {
                    { "nested1", "value1" },
                    { "nested2", 42 },
                    { "nested3", true }
                },
                CustomObject = new { InnerValue = "inner", Number = 123 }
            };

            // Act
            cache.Store(nodeId, key, originalData);
            var retrieved = cache.Retrieve(nodeId, key);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Should().BeEquivalentTo(originalData, options => options.Using<DateTime>(ctx => 
                ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1000))));
        }
    }
}