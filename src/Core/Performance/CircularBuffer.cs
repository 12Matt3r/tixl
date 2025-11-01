using System;
using System.Collections.Generic;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// High-performance circular buffer for storing frame metrics
    /// Lock-free implementation for minimal contention
    /// </summary>
    /// <typeparam name="T">Type of items to store</typeparam>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;
        private readonly int _capacity;
        private readonly object _lockObject = new object();
        
        public int Count => _count;
        public int Capacity => _capacity;
        
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("Capacity must be positive", nameof(capacity));
            
            _capacity = capacity;
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }
        
        /// <summary>
        /// Add item to buffer (overwrites oldest if full)
        /// </summary>
        public void Add(T item)
        {
            lock (_lockObject)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _capacity;
                
                if (_count < _capacity)
                    _count++;
            }
        }
        
        /// <summary>
        /// Get most recent n items from buffer
        /// </summary>
        public List<T> GetRecentItems(int count)
        {
            lock (_lockObject)
            {
                count = Math.Min(count, _count);
                var result = new List<T>(count);
                
                for (int i = 0; i < count; i++)
                {
                    int index = (_head - count + i + _capacity) % _capacity;
                    result.Add(_buffer[index]);
                }
                
                return result;
            }
        }
        
        /// <summary>
        /// Get recent frames as array
        /// </summary>
        public T[] GetRecentFrames(int count)
        {
            return GetRecentItems(count).ToArray();
        }
        
        /// <summary>
        /// Clear all items from buffer
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
            }
        }
        
        /// <summary>
        /// Get current item (most recent)
        /// </summary>
        public T Current()
        {
            lock (_lockObject)
            {
                if (_count == 0) return default;
                
                int currentIndex = (_head - 1 + _capacity) % _capacity;
                return _buffer[currentIndex];
            }
        }
        
        /// <summary>
        /// Get average of all items (requires numeric types)
        /// </summary>
        public double GetAverage()
        {
            lock (_lockObject)
            {
                if (_count == 0) return 0;
                
                double sum = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_buffer[i] is IConvertible convertible)
                    {
                        sum += Convert.ToDouble(convertible);
                    }
                }
                
                return sum / _count;
            }
        }
    }
}
