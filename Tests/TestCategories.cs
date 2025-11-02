// TestCategories.cs
using System;

namespace TiXL.Tests.Categories
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class CategoryAttribute : Attribute
    {
        public string Category { get; }
        
        public CategoryAttribute(string category)
        {
            Category = category;
        }
    }
    
    public static class TestCategories
    {
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Performance = "Performance";
        public const string Graphics = "Graphics";
        public const string UI = "UI";
        public const string Security = "Security";
        public const string Audio = "Audio";
        public const string Network = "Network";
        public const string IO = "IO";
        public const string Rendering = "Rendering";
        public const string Operators = "Operators";
        public const string Core = "Core";
        public const string Editor = "Editor";
        public const string Gui = "Gui";
        public const string Smoke = "Smoke";
        
        // Production Readiness Categories
        public const string Production = "Production";
        public const string ProductionReadiness = "ProductionReadiness";
        
        // Test priorities
        public const string P0 = "P0";
        public const string P1 = "P1";
        public const string P2 = "P2";
        public const string P3 = "P3";
        
        // Test speed
        public const string Fast = "Fast";
        public const string Medium = "Medium";
        public const string Slow = "Slow";
    }
}