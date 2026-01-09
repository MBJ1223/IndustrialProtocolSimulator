using S7ProtocolSimulator.Protocol;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace S7ProtocolSimulator.Simulator;

/// <summary>
/// 메모리 변경 이벤트 인자
/// </summary>
public class S7MemoryChangedEventArgs : EventArgs
{
    public S7AreaType AreaType { get; }
    public int DbNumber { get; }
    public int Address { get; }
    public int Count { get; }

    public S7MemoryChangedEventArgs(S7AreaType areaType, int dbNumber, int address, int count)
    {
        AreaType = areaType;
        DbNumber = dbNumber;
        Address = address;
        Count = count;
    }
}

/// <summary>
/// S7 PLC 메모리 시뮬레이터
/// </summary>
public class S7Memory : INotifyPropertyChanged
{
    // 메모리 영역 크기
    public const int InputSize = 1024;      // I0.0 ~ I1023.7
    public const int OutputSize = 1024;     // Q0.0 ~ Q1023.7
    public const int MerkerSize = 4096;     // M0.0 ~ M4095.7
    public const int DefaultDbSize = 65536; // DB 기본 크기

    // 메모리 배열
    private readonly byte[] _inputs;
    private readonly byte[] _outputs;
    private readonly byte[] _merkers;
    private readonly ConcurrentDictionary<int, byte[]> _dataBlocks;

    private readonly object _lock = new();

    public event EventHandler<S7MemoryChangedEventArgs>? MemoryChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public S7Memory()
    {
        _inputs = new byte[InputSize];
        _outputs = new byte[OutputSize];
        _merkers = new byte[MerkerSize];
        _dataBlocks = new ConcurrentDictionary<int, byte[]>();

        // 기본 DB 생성
        CreateDataBlock(1, 1024);
    }

    #region Data Block 관리

    /// <summary>
    /// 데이터 블록 생성
    /// </summary>
    public void CreateDataBlock(int dbNumber, int size = 1024)
    {
        _dataBlocks[dbNumber] = new byte[size];
    }

    /// <summary>
    /// 데이터 블록 존재 여부
    /// </summary>
    public bool HasDataBlock(int dbNumber) => _dataBlocks.ContainsKey(dbNumber);

    /// <summary>
    /// 데이터 블록 목록
    /// </summary>
    public IEnumerable<int> DataBlockNumbers => _dataBlocks.Keys.OrderBy(k => k);

    #endregion

    #region 바이트 읽기/쓰기

    /// <summary>
    /// 영역에서 바이트 읽기
    /// </summary>
    public byte[] ReadBytes(byte area, int dbNumber, int startAddress, int count)
    {
        lock (_lock)
        {
            var memory = GetMemoryArea(area, dbNumber);
            if (memory == null) return Array.Empty<byte>();

            var result = new byte[count];
            int available = Math.Min(count, memory.Length - startAddress);
            if (available > 0 && startAddress >= 0)
            {
                Array.Copy(memory, startAddress, result, 0, available);
            }
            return result;
        }
    }

    /// <summary>
    /// 영역에 바이트 쓰기
    /// </summary>
    public bool WriteBytes(byte area, int dbNumber, int startAddress, byte[] data)
    {
        lock (_lock)
        {
            var memory = GetMemoryArea(area, dbNumber);
            if (memory == null) return false;

            int available = Math.Min(data.Length, memory.Length - startAddress);
            if (available > 0 && startAddress >= 0)
            {
                Array.Copy(data, 0, memory, startAddress, available);
            }
        }

        var areaType = GetAreaType(area);
        OnMemoryChanged(areaType, dbNumber, startAddress, data.Length);
        return true;
    }

    #endregion

    #region 비트 읽기/쓰기

    /// <summary>
    /// 비트 읽기
    /// </summary>
    public bool ReadBit(byte area, int dbNumber, int byteAddress, int bitAddress)
    {
        var bytes = ReadBytes(area, dbNumber, byteAddress, 1);
        if (bytes.Length == 0) return false;
        return (bytes[0] & (1 << bitAddress)) != 0;
    }

    /// <summary>
    /// 비트 쓰기
    /// </summary>
    public bool WriteBit(byte area, int dbNumber, int byteAddress, int bitAddress, bool value)
    {
        lock (_lock)
        {
            var memory = GetMemoryArea(area, dbNumber);
            if (memory == null || byteAddress >= memory.Length) return false;

            if (value)
                memory[byteAddress] |= (byte)(1 << bitAddress);
            else
                memory[byteAddress] &= (byte)~(1 << bitAddress);
        }

        var areaType = GetAreaType(area);
        OnMemoryChanged(areaType, dbNumber, byteAddress, 1);
        return true;
    }

    #endregion

    #region 워드/더블워드 읽기/쓰기

    /// <summary>
    /// 워드 읽기 (Big Endian)
    /// </summary>
    public ushort ReadWord(byte area, int dbNumber, int address)
    {
        var bytes = ReadBytes(area, dbNumber, address, 2);
        if (bytes.Length < 2) return 0;
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    /// <summary>
    /// 워드 쓰기 (Big Endian)
    /// </summary>
    public bool WriteWord(byte area, int dbNumber, int address, ushort value)
    {
        return WriteBytes(area, dbNumber, address, new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) });
    }

    /// <summary>
    /// 더블워드 읽기 (Big Endian)
    /// </summary>
    public uint ReadDWord(byte area, int dbNumber, int address)
    {
        var bytes = ReadBytes(area, dbNumber, address, 4);
        if (bytes.Length < 4) return 0;
        return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
    }

    /// <summary>
    /// 더블워드 쓰기 (Big Endian)
    /// </summary>
    public bool WriteDWord(byte area, int dbNumber, int address, uint value)
    {
        return WriteBytes(area, dbNumber, address, new byte[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)(value & 0xFF)
        });
    }

    /// <summary>
    /// Real 읽기 (Big Endian IEEE 754)
    /// </summary>
    public float ReadReal(byte area, int dbNumber, int address)
    {
        var bytes = ReadBytes(area, dbNumber, address, 4);
        if (bytes.Length < 4) return 0;
        // Big Endian to Little Endian
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Real 쓰기 (Big Endian IEEE 754)
    /// </summary>
    public bool WriteReal(byte area, int dbNumber, int address, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes); // Little Endian to Big Endian
        return WriteBytes(area, dbNumber, address, bytes);
    }

    #endregion

    #region 메모리 초기화

    public void ClearAll()
    {
        lock (_lock)
        {
            Array.Clear(_inputs);
            Array.Clear(_outputs);
            Array.Clear(_merkers);
            foreach (var db in _dataBlocks.Values)
            {
                Array.Clear(db);
            }
        }
        OnPropertyChanged(nameof(Inputs));
        OnPropertyChanged(nameof(Outputs));
        OnPropertyChanged(nameof(Merkers));
    }

    public void ClearArea(byte area, int dbNumber = 0)
    {
        lock (_lock)
        {
            var memory = GetMemoryArea(area, dbNumber);
            if (memory != null) Array.Clear(memory);
        }
    }

    #endregion

    #region 속성

    public byte[] Inputs => _inputs;
    public byte[] Outputs => _outputs;
    public byte[] Merkers => _merkers;

    public byte[]? GetDataBlock(int dbNumber)
    {
        return _dataBlocks.TryGetValue(dbNumber, out var db) ? db : null;
    }

    #endregion

    #region Private Helpers

    private byte[]? GetMemoryArea(byte area, int dbNumber) => area switch
    {
        S7Constants.AreaInput => _inputs,
        S7Constants.AreaOutput => _outputs,
        S7Constants.AreaFlags => _merkers,
        S7Constants.AreaDB => _dataBlocks.TryGetValue(dbNumber, out var db) ? db : null,
        _ => null
    };

    private static S7AreaType GetAreaType(byte area) => area switch
    {
        S7Constants.AreaInput => S7AreaType.Input,
        S7Constants.AreaOutput => S7AreaType.Output,
        S7Constants.AreaFlags => S7AreaType.Merker,
        S7Constants.AreaDB => S7AreaType.DataBlock,
        _ => S7AreaType.Merker
    };

    private void OnMemoryChanged(S7AreaType areaType, int dbNumber, int address, int count)
    {
        MemoryChanged?.Invoke(this, new S7MemoryChangedEventArgs(areaType, dbNumber, address, count));
        OnPropertyChanged(areaType.ToString());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
