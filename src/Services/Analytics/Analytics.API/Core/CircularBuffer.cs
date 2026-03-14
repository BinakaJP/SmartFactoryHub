namespace Analytics.API.Core;

/// <summary>
/// Fixed-capacity circular buffer. New values overwrite the oldest when full.
/// O(1) push, O(n) enumeration in chronological order.
/// Used to maintain rolling metric windows for statistical analysis.
/// </summary>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;   // index of next write position
    private int _count;  // number of values currently stored

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
    }

    public int Capacity => _buffer.Length;
    public int Count => _count;
    public bool IsFull => _count == _buffer.Length;

    /// <summary>Push a value. Overwrites oldest entry when full.</summary>
    public void Push(T value)
    {
        _buffer[_head % _buffer.Length] = value;
        _head++;
        if (_count < _buffer.Length) _count++;
    }

    /// <summary>Enumerate values oldest-first.</summary>
    public IEnumerable<T> Values
    {
        get
        {
            int start = _head - _count;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i + _buffer.Length * 2) % _buffer.Length;
                yield return _buffer[idx];
            }
        }
    }
}
