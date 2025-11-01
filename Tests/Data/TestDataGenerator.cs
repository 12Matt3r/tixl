using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiXL.Tests.Categories;

namespace TiXL.Tests.Data
{
    /// <summary>
    /// Provides standardized test data generators for TiXL testing
    /// </summary>
    public static class TestDataGenerator
    {
        #region Vector Data Generation

        public static Vector2D[] GenerateVector2DArray(int count, float minValue = -100f, float maxValue = 100f)
        {
            var random = new Random(42); // Seed for reproducibility
            var vectors = new Vector2D[count];
            
            for (int i = 0; i < count; i++)
            {
                vectors[i] = new Vector2D(
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue)
                );
            }
            
            return vectors;
        }

        public static Vector3D[] GenerateVector3DArray(int count, float minValue = -100f, float maxValue = 100f)
        {
            var random = new Random(42); // Seed for reproducibility
            var vectors = new Vector3D[count];
            
            for (int i = 0; i < count; i++)
            {
                vectors[i] = new Vector3D(
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue)
                );
            }
            
            return vectors;
        }

        public static Vector4D[] GenerateVector4DArray(int count, float minValue = -100f, float maxValue = 100f)
        {
            var random = new Random(42); // Seed for reproducibility
            var vectors = new Vector4D[count];
            
            for (int i = 0; i < count; i++)
            {
                vectors[i] = new Vector4D(
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue),
                    (float)(random.NextDouble() * (maxValue - minValue) + minValue)
                );
            }
            
            return vectors;
        }

        #endregion

        #region Matrix Data Generation

        public static Matrix3x3[] GenerateMatrix3x3Array(int count)
        {
            var random = new Random(42); // Seed for reproducibility
            var matrices = new Matrix3x3[count];
            
            for (int i = 0; i < count; i++)
            {
                var data = new float[9];
                for (int j = 0; j < 9; j++)
                {
                    data[j] = (float)(random.NextDouble() * 2 - 1); // -1 to 1
                }
                matrices[i] = new Matrix3x3(data);
            }
            
            return matrices;
        }

        public static Matrix4x4[] GenerateMatrix4x4Array(int count)
        {
            var random = new Random(42); // Seed for reproducibility
            var matrices = new Matrix4x4[count];
            
            for (int i = 0; i < count; i++)
            {
                var data = new float[16];
                for (int j = 0; j < 16; j++)
                {
                    data[j] = (float)(random.NextDouble() * 2 - 1); // -1 to 1
                }
                matrices[i] = new Matrix4x4(data);
            }
            
            return matrices;
        }

        #endregion

        #region Color Data Generation

        public static ColorRgba[] GenerateColorRgbaArray(int count)
        {
            var random = new Random(42); // Seed for reproducibility
            var colors = new ColorRgba[count];
            
            for (int i = 0; i < count; i++)
            {
                colors[i] = new ColorRgba(
                    (byte)random.Next(256),
                    (byte)random.Next(256),
                    (byte)random.Next(256),
                    (byte)random.Next(256)
                );
            }
            
            return colors;
        }

        public static ColorRgb[] GenerateColorRgbArray(int count)
        {
            var random = new Random(42); // Seed for reproducibility
            var colors = new ColorRgb[count];
            
            for (int i = 0; i < count; i++)
            {
                colors[i] = new ColorRgb(
                    (byte)random.Next(256),
                    (byte)random.Next(256),
                    (byte)random.Next(256)
                );
            }
            
            return colors;
        }

        #endregion

        #region Audio Data Generation

        public static float[] GenerateAudioSamples(int sampleCount, int sampleRate = 44100, float frequency = 440f)
        {
            var samples = new float[sampleCount];
            var random = new Random(42); // Seed for reproducibility
            
            // Generate sine wave with some noise
            for (int i = 0; i < sampleCount; i++)
            {
                var t = (float)i / sampleRate;
                var sineWave = (float)Math.Sin(2 * Math.PI * frequency * t);
                var noise = (float)(random.NextDouble() * 2 - 1) * 0.1f; // 10% noise
                samples[i] = sineWave + noise;
            }
            
            return samples;
        }

        public static AudioBuffer GenerateAudioBuffer(int channels = 2, int sampleRate = 44100, float duration = 1.0f)
        {
            var sampleCount = (int)(sampleRate * duration);
            var channelData = new float[channels][];
            
            for (int ch = 0; ch < channels; ch++)
            {
                channelData[ch] = GenerateAudioSamples(sampleCount, sampleRate, 440f + (ch * 55f));
            }
            
            return new AudioBuffer(channelData, sampleRate);
        }

        #endregion

        #region File Path Data Generation

        public static string[] GenerateValidFilePaths(int count)
        {
            var random = new Random(42);
            var paths = new string[count];
            var basePaths = new[]
            {
                @"C:\Projects\TiXL",
                @"C:\Users\Developer\Documents",
                @"D:\Workspace\Testing",
                @"/home/user/projects/tixl",
                @"/var/log/tixl",
                @"C:\ProgramData\TiXL",
                @"C:\Temp\TiXLTemp"
            };
            
            var fileNames = new[]
            {
                "project.json",
                "config.xml",
                "data.txt",
                "output.png",
                "shader.fx",
                "audio.wav",
                "scene.t3",
                "operator.ops"
            };
            
            for (int i = 0; i < count; i++)
            {
                var basePath = basePaths[random.Next(basePaths.Length)];
                var fileName = fileNames[random.Next(fileNames.Length)];
                var subfolder = random.Next(0, 3) > 0 ? $"subfolder{random.Next(1, 4)}\\" : "";
                
                paths[i] = Path.Combine(basePath, subfolder, $"{fileNames[random.Next(fileNames.Length)]}_{i:D3}.{fileName.Split('.').Last()}");
            }
            
            return paths;
        }

        public static string[] GenerateInvalidFilePaths(int count)
        {
            var random = new Random(42);
            var paths = new string[count];
            var invalidPatterns = new[]
            {
                "../../../etc/passwd",
                "..\\..\\Windows\\System32",
                "CON.txt",
                "PRN.txt",
                "",
                null!,
                "   ",
                new string('a', 300) + ".txt",
                "file%2e%2e%2f%2e%2e%2f",
                "\\/??\\C:\\Windows\\System32",
                "test.txt\x00null",
                "..\x00..\x00test.txt"
            };
            
            for (int i = 0; i < count; i++)
            {
                paths[i] = invalidPatterns[random.Next(invalidPatterns.Length)];
            }
            
            return paths;
        }

        #endregion

        #region JSON Data Generation

        public static TestProjectData GenerateTestProjectData()
        {
            return new TestProjectData
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test TiXL Project",
                Version = "1.0.0",
                Description = "A test project for automated testing",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Author = "TiXL Test Suite",
                Tags = new List<string> { "test", "automated", "suite" },
                Settings = new Dictionary<string, object>
                {
                    { "Theme", "Dark" },
                    { "AutoSave", true },
                    { "MaxUndoLevels", 50 },
                    { "DefaultSampleRate", 44100 }
                },
                Operators = GenerateTestOperatorData(10),
                Scenes = GenerateTestSceneData(5)
            };
        }

        public static List<TestOperatorData> GenerateTestOperatorData(int count)
        {
            var random = new Random(42);
            var operators = new List<TestOperatorData>();
            
            var operatorTypes = new[] { "Input", "Transform", "Output", "Generator", "Filter", "Math" };
            
            for (int i = 0; i < count; i++)
            {
                operators.Add(new TestOperatorData
                {
                    Id = $"op_{i:D3}",
                    Name = $"TestOperator_{i}",
                    Type = operatorTypes[random.Next(operatorTypes.Length)],
                    Position = new Vector2D(random.Next(-100, 100), random.Next(-100, 100)),
                    Parameters = new Dictionary<string, object>
                    {
                        { "Value", random.NextDouble() * 100 },
                        { "Enabled", random.Next(2) == 1 },
                        { "Label", $"Parameter_{i}" }
                    },
                    Connections = GenerateTestConnections(random.Next(0, 3))
                });
            }
            
            return operators;
        }

        public static List<TestConnectionData> GenerateTestConnections(int count)
        {
            var random = new Random(42);
            var connections = new List<TestConnectionData>();
            
            for (int i = 0; i < count; i++)
            {
                connections.Add(new TestConnectionData
                {
                    SourceOperatorId = $"op_{random.Next(0, 10):D3}",
                    SourceOutputIndex = random.Next(0, 3),
                    TargetOperatorId = $"op_{random.Next(0, 10):D3}",
                    TargetInputIndex = random.Next(0, 3)
                });
            }
            
            return connections;
        }

        public static List<TestSceneData> GenerateTestSceneData(int count)
        {
            var random = new Random(42);
            var scenes = new List<TestSceneData>();
            
            for (int i = 0; i < count; i++)
            {
                scenes.Add(new TestSceneData
                {
                    Id = $"scene_{i:D3}",
                    Name = $"Test Scene {i}",
                    Description = $"Test scene number {i}",
                    Resolution = new Vector2D(1920, 1080),
                    Duration = random.NextDouble() * 60.0, // 0-60 seconds
                    CameraSettings = new Dictionary<string, object>
                    {
                        { "Position", new Vector3D(random.Next(-10, 10), random.Next(-10, 10), random.Next(-10, 10)) },
                        { "Rotation", new Vector3D(random.Next(-180, 180), random.Next(-180, 180), random.Next(-180, 180)) },
                        { "FOV", random.NextDouble() * 120.0 }
                    }
                });
            }
            
            return scenes;
        }

        #endregion

        #region Performance Test Data

        public static PerformanceTestData GeneratePerformanceTestData()
        {
            return new PerformanceTestData
            {
                TestName = "Automated Performance Test",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(1),
                Metrics = new Dictionary<string, double>
                {
                    { "FPS", 60.0 },
                    { "CPU_Usage", 25.5 },
                    { "Memory_MB", 512.0 },
                    { "GPU_Usage", 45.2 },
                    { "Audio_Latency_MS", 10.5 }
                }
            };
        }

        public static LargeDataset GenerateLargeDataset(int size)
        {
            var random = new Random(42);
            var data = new List<object>(size);
            
            for (int i = 0; i < size; i++)
            {
                data.Add(new
                {
                    Id = i,
                    Value = random.NextDouble(),
                    Text = $"Item_{i:D6}",
                    Vector = new Vector3D(
                        (float)(random.NextDouble() * 100),
                        (float)(random.NextDouble() * 100),
                        (float)(random.NextDouble() * 100)
                    ),
                    Timestamp = DateTime.UtcNow.AddMilliseconds(i)
                });
            }
            
            return new LargeDataset { Items = data };
        }

        #endregion

        #region Stress Test Data

        public static StressTestScenario[] GenerateStressTestScenarios()
        {
            return new[]
            {
                new StressTestScenario
                {
                    Name = "High Memory Usage",
                    Description = "Test with high memory allocation",
                    MaxMemoryBytes = 1024 * 1024 * 1024, // 1GB
                    Duration = TimeSpan.FromMinutes(5)
                },
                new StressTestScenario
                {
                    Name = "CPU Intensive Operations",
                    Description = "Test with CPU intensive calculations",
                    MaxCpuUsage = 90.0,
                    Duration = TimeSpan.FromMinutes(2)
                },
                new StressTestScenario
                {
                    Name = "Concurrent Operations",
                    Description = "Test with high concurrency",
                    MaxConcurrentOperations = 100,
                    Duration = TimeSpan.FromMinutes(1)
                },
                new StressTestScenario
                {
                    Name = "Long Running Operations",
                    Description = "Test with long running operations",
                    MaxOperationDuration = TimeSpan.FromSeconds(30),
                    Duration = TimeSpan.FromMinutes(10)
                }
            };
        }

        #endregion

        #region Test Data Serialization

        public static string SerializeToJson<T>(T data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Serialize(data, options);
        }

        public static T? DeserializeFromJson<T>(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Deserialize<T>(json, options);
        }

        #endregion
    }

    #region Test Data Models

    public struct Vector2D(float x, float y)
    {
        public float X = x;
        public float Y = y;
    }

    public struct Vector3D(float x, float y, float z)
    {
        public float X = x;
        public float Y = y;
        public float Z = z;
    }

    public struct Vector4D(float x, float y, float z, float w)
    {
        public float X = x;
        public float Y = y;
        public float Z = z;
        public float W = w;
    }

    public class Matrix3x3
    {
        private readonly float[] _data = new float[9];

        public Matrix3x3(float[] data)
        {
            if (data.Length != 9)
                throw new ArgumentException("Matrix3x3 requires 9 float values");
            
            Array.Copy(data, _data, 9);
        }

        public float this[int row, int col] => _data[row * 3 + col];
    }

    public class Matrix4x4
    {
        private readonly float[] _data = new float[16];

        public Matrix4x4(float[] data)
        {
            if (data.Length != 16)
                throw new ArgumentException("Matrix4x4 requires 16 float values");
            
            Array.Copy(data, _data, 16);
        }

        public float this[int row, int col] => _data[row * 4 + col];
    }

    public struct ColorRgba(byte r, byte g, byte b, byte a)
    {
        public byte R = r;
        public byte G = g;
        public byte B = b;
        public byte A = a;
    }

    public struct ColorRgb(byte r, byte g, byte b)
    {
        public byte R = r;
        public byte G = g;
        public byte B = b;
    }

    public class AudioBuffer
    {
        public float[][] ChannelData { get; }
        public int SampleRate { get; }
        public int ChannelCount => ChannelData.Length;
        public int SampleCount => ChannelData[0].Length;

        public AudioBuffer(float[][] channelData, int sampleRate)
        {
            ChannelData = channelData;
            SampleRate = sampleRate;
        }
    }

    public class TestProjectData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public string Author { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public List<TestOperatorData> Operators { get; set; } = new();
        public List<TestSceneData> Scenes { get; set; } = new();
    }

    public class TestOperatorData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Vector2D Position { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<TestConnectionData> Connections { get; set; } = new();
    }

    public class TestConnectionData
    {
        public string SourceOperatorId { get; set; } = string.Empty;
        public int SourceOutputIndex { get; set; }
        public string TargetOperatorId { get; set; } = string.Empty;
        public int TargetInputIndex { get; set; }
    }

    public class TestSceneData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Vector2D Resolution { get; set; }
        public double Duration { get; set; }
        public Dictionary<string, object> CameraSettings { get; set; } = new();
    }

    public class PerformanceTestData
    {
        public string TestName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
    }

    public class LargeDataset
    {
        public List<object> Items { get; set; } = new();
    }

    public class StressTestScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long MaxMemoryBytes { get; set; }
        public double MaxCpuUsage { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public TimeSpan MaxOperationDuration { get; set; }
        public TimeSpan Duration { get; set; }
    }

    #endregion
}