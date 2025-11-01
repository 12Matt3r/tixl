using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// Interface for generating unique signatures for PSO caching
    /// </summary>
    public interface IPSOKey
    {
        /// <summary>
        /// Generate a unique hash for this PSO signature
        /// </summary>
        /// <returns>Hash code for cache lookup</returns>
        int GetHashCode();
        
        /// <summary>
        /// Check equality with another PSO key
        /// </summary>
        bool Equals(IPSOKey other);
        
        /// <summary>
        /// Get the string representation of the key for debugging
        /// </summary>
        string ToDebugString();
        
        /// <summary>
        /// Serialize the key to a byte array for hashing
        /// </summary>
        byte[] Serialize();
    }
}