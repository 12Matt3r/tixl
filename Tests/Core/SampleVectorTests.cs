// SampleVectorTests.cs
using Xunit;
using FluentAssertions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;

namespace TiXL.Tests.Core.DataTypes
{
    [Collection("Core Tests")]
    [Category(TestCategories.Unit)]
    [Category(TestCategories.Core)]
    [Category(TestCategories.P0)]
    public class Vector2Tests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Fast)]
        public void Vector2_CreateWithComponents_ReturnsCorrectValues()
        {
            // Arrange & Act
            var vector = new Vector2(3.0f, 4.0f);
            
            // Assert
            vector.X.Should().Be(3.0f);
            vector.Y.Should().Be(4.0f);
            vector.Length.Should().BeApproximately(5.0f, 0.001f);
        }
        
        [Fact]
        public void Vector2_Addition_ReturnsCorrectSum()
        {
            // Arrange
            var vector1 = new Vector2(1, 2);
            var vector2 = new Vector2(3, 4);
            var expected = new Vector2(4, 6);
            
            // Act
            var result = vector1 + vector2;
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 0, 0)]           // Zero vector
        [InlineData(1, 0, 1)]           // Unit X
        [InlineData(0, 1, 1)]           // Unit Y
        [InlineData(3, 4, 5)]           // 3-4-5 triangle
        public void Vector2_Length_CalculatesCorrectMagnitude(float x, float y, float expectedLength)
        {
            // Arrange
            var vector = new Vector2(x, y);
            
            // Act
            var length = vector.Length;
            
            // Assert
            length.Should().BeApproximately(expectedLength, 0.001f);
        }
        
        [Fact]
        public void Vector2_Normalize_ZeroVector_ThrowsException()
        {
            // Arrange
            var zeroVector = new Vector2(0, 0);
            
            // Act & Assert
            Action act = () => zeroVector.Normalized();
            act.Should().Throw<ArgumentException>()
                .WithMessage("*zero*");
        }
        
        [Fact]
        public void Vector2_Normalize_ValidVector_ReturnsUnitVector()
        {
            // Arrange
            var vector = new Vector2(3, 4);
            var expected = new Vector2(0.6f, 0.8f);
            
            // Act
            var normalized = vector.Normalized();
            
            // Assert
            normalized.X.Should().BeApproximately(expected.X, 0.001f);
            normalized.Y.Should().BeApproximately(expected.Y, 0.001f);
            normalized.Length.Should().BeApproximately(1.0f, 0.001f);
        }
    }
    
    // Simple Vector2 struct for demonstration (replace with actual TiXL Vector2)
    public struct Vector2
    {
        public float X { get; set; }
        public float Y { get; set; }
        
        public float Length => MathF.Sqrt(X * X + Y * Y);
        
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }
        
        public Vector2 Normalized()
        {
            var length = Length;
            if (length < float.Epsilon)
            {
                throw new ArgumentException("Cannot normalize zero vector");
            }
            
            return new Vector2(X / length, Y / length);
        }
        
        public static Vector2 operator +(Vector2 a, Vector2 b)
        {
            return new Vector2(a.X + b.X, a.Y + b.Y);
        }
        
        public static bool operator ==(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Y - b.Y) < 0.001f;
        }
        
        public static bool operator !=(Vector2 a, Vector2 b)
        {
            return !(a == b);
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Vector2 other && this == other;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
        
        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }
    }
}