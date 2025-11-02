using System;

namespace TiXL.Tests.Categories
{
    /// <summary>
    /// Test categories for production readiness validation
    /// </summary>
    public static class ProductionCategories
    {
        /// <summary>
        /// Production readiness validation tests
        /// </summary>
        public const string Production = "Production";

        /// <summary>
        /// Error handling and recovery mechanism tests
        /// </summary>
        public const string ErrorHandling = "Production.ErrorHandling";

        /// <summary>
        /// Recovery and resilience tests
        /// </summary>
        public const string Recovery = "Production.Recovery";

        /// <summary>
        /// Resource cleanup and disposal pattern tests
        /// </summary>
        public const string Disposal = "Production.Disposal";

        /// <summary>
        /// Logging and monitoring integration tests
        /// </summary>
        public const string Logging = "Production.Logging";

        /// <summary>
        /// Performance monitoring tests
        /// </summary>
        public const string Performance = "Production.Performance";

        /// <summary>
        /// Sustained load testing
        /// </summary>
        public const string SustainedLoad = "Production.SustainedLoad";

        /// <summary>
        /// Configuration validation and startup tests
        /// </summary>
        public const string Configuration = "Production.Configuration";

        /// <summary>
        /// Graceful shutdown and cleanup tests
        /// </summary>
        public const string Shutdown = "Production.Shutdown";

        /// <summary>
        /// Security validation tests
        /// </summary>
        public const string Security = "Production.Security";

        /// <summary>
        /// Resource management tests
        /// </summary>
        public const string ResourceManagement = "Production.ResourceManagement";

        /// <summary>
        /// Integration testing for production scenarios
        /// </summary>
        public const string Integration = "Production.Integration";
    }

    /// <summary>
    /// Test category attribute for production readiness tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionTestAttribute : Attribute
    {
        public string Category { get; }

        public ProductionTestAttribute(string category)
        {
            Category = category;
        }
    }

    /// <summary>
    /// Test category attribute for production readiness tests (convenience)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionAttribute : Attribute
    {
    }

    /// <summary>
    /// Test category attribute for production error handling tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionErrorHandlingAttribute : Attribute
    {
    }

    /// <summary>
    /// Test category attribute for production resource management tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionResourceManagementAttribute : Attribute
    {
    }

    /// <summary>
    /// Test category attribute for production performance tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionPerformanceAttribute : Attribute
    {
    }

    /// <summary>
    /// Test category attribute for production security tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionSecurityAttribute : Attribute
    {
    }

    /// <summary>
    /// Test category attribute for production configuration tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ProductionConfigurationAttribute : Attribute
    {
    }
}