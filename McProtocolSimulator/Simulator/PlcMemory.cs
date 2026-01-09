using McProtocolSimulator.Protocol;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace McProtocolSimulator.Simulator;

/// <summary>
/// 메모리 변경 이벤트 인자
/// </summary>
public class MemoryChangedEventArgs : EventArgs
{
    public DeviceType DeviceType { get; }
    public int Address { get; }
    public int Count { get; }

    public MemoryChangedEventArgs(DeviceType deviceType, int address, int count)
    {
        DeviceType = deviceType;
        Address = address;
        Count = count;
    }
}

/// <summary>
/// PLC 메모리 시뮬레이터
/// 미쯔비시 PLC의 메모리 영역을 시뮬레이션
/// </summary>
public class PlcMemory : INotifyPropertyChanged
{
    // 메모리 영역 크기 설정
    public const int DRegisterSize = 65536;   // D0 ~ D65535
    public const int MRelaySize = 65536;      // M0 ~ M65535
    public const int XInputSize = 4096;       // X0 ~ X4095 (8진수 주소)
    public const int YOutputSize = 4096;      // Y0 ~ Y4095 (8진수 주소)
    public const int WRegisterSize = 65536;   // W0 ~ W65535
    public const int BRelaySize = 65536;      // B0 ~ B65535
    public const int RRegisterSize = 65536;   // R0 ~ R65535

    // 메모리 배열 (워드 단위 저장)
    private readonly ushort[] _dRegisters;
    private readonly ushort[] _mRelays;      // 비트를 워드로 패킹
    private readonly ushort[] _xInputs;      // 비트를 워드로 패킹
    private readonly ushort[] _yOutputs;     // 비트를 워드로 패킹
    private readonly ushort[] _wRegisters;
    private readonly ushort[] _bRelays;      // 비트를 워드로 패킹
    private readonly ushort[] _rRegisters;

    // 동시성 제어를 위한 락 객체
    private readonly object _lock = new();

    public event EventHandler<MemoryChangedEventArgs>? MemoryChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public PlcMemory()
    {
        _dRegisters = new ushort[DRegisterSize];
        _mRelays = new ushort[MRelaySize / 16 + 1];
        _xInputs = new ushort[XInputSize / 16 + 1];
        _yOutputs = new ushort[YOutputSize / 16 + 1];
        _wRegisters = new ushort[WRegisterSize];
        _bRelays = new ushort[BRelaySize / 16 + 1];
        _rRegisters = new ushort[RRegisterSize];
    }

    #region 워드 단위 읽기/쓰기

    /// <summary>
    /// 워드 단위 읽기
    /// </summary>
    public ushort[] ReadWords(DeviceType deviceType, int startAddress, int count)
    {
        lock (_lock)
        {
            var result = new ushort[count];
            var memory = GetWordMemory(deviceType);

            if (memory == null) return result;

            for (int i = 0; i < count; i++)
            {
                int addr = startAddress + i;
                if (addr >= 0 && addr < memory.Length)
                {
                    result[i] = memory[addr];
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 워드 단위 쓰기
    /// </summary>
    public void WriteWords(DeviceType deviceType, int startAddress, ushort[] values)
    {
        lock (_lock)
        {
            var memory = GetWordMemory(deviceType);
            if (memory == null) return;

            for (int i = 0; i < values.Length; i++)
            {
                int addr = startAddress + i;
                if (addr >= 0 && addr < memory.Length)
                {
                    memory[addr] = values[i];
                }
            }
        }

        OnMemoryChanged(deviceType, startAddress, values.Length);
    }

    /// <summary>
    /// 단일 워드 읽기
    /// </summary>
    public ushort ReadWord(DeviceType deviceType, int address)
    {
        return ReadWords(deviceType, address, 1)[0];
    }

    /// <summary>
    /// 단일 워드 쓰기
    /// </summary>
    public void WriteWord(DeviceType deviceType, int address, ushort value)
    {
        WriteWords(deviceType, address, new[] { value });
    }

    #endregion

    #region 비트 단위 읽기/쓰기

    /// <summary>
    /// 비트 단위 읽기
    /// </summary>
    public bool[] ReadBits(DeviceType deviceType, int startAddress, int count)
    {
        lock (_lock)
        {
            var result = new bool[count];
            var memory = GetBitMemory(deviceType);

            if (memory == null) return result;

            for (int i = 0; i < count; i++)
            {
                int addr = startAddress + i;
                int wordIndex = addr / 16;
                int bitIndex = addr % 16;

                if (wordIndex >= 0 && wordIndex < memory.Length)
                {
                    result[i] = (memory[wordIndex] & (1 << bitIndex)) != 0;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 비트 단위 쓰기
    /// </summary>
    public void WriteBits(DeviceType deviceType, int startAddress, bool[] values)
    {
        lock (_lock)
        {
            var memory = GetBitMemory(deviceType);
            if (memory == null) return;

            for (int i = 0; i < values.Length; i++)
            {
                int addr = startAddress + i;
                int wordIndex = addr / 16;
                int bitIndex = addr % 16;

                if (wordIndex >= 0 && wordIndex < memory.Length)
                {
                    if (values[i])
                    {
                        memory[wordIndex] |= (ushort)(1 << bitIndex);
                    }
                    else
                    {
                        memory[wordIndex] &= (ushort)~(1 << bitIndex);
                    }
                }
            }
        }

        OnMemoryChanged(deviceType, startAddress, values.Length);
    }

    /// <summary>
    /// 단일 비트 읽기
    /// </summary>
    public bool ReadBit(DeviceType deviceType, int address)
    {
        return ReadBits(deviceType, address, 1)[0];
    }

    /// <summary>
    /// 단일 비트 쓰기
    /// </summary>
    public void WriteBit(DeviceType deviceType, int address, bool value)
    {
        WriteBits(deviceType, address, new[] { value });
    }

    #endregion

    #region 메모리 초기화

    /// <summary>
    /// 전체 메모리 초기화
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            Array.Clear(_dRegisters);
            Array.Clear(_mRelays);
            Array.Clear(_xInputs);
            Array.Clear(_yOutputs);
            Array.Clear(_wRegisters);
            Array.Clear(_bRelays);
            Array.Clear(_rRegisters);
        }

        OnPropertyChanged(nameof(DRegisters));
        OnPropertyChanged(nameof(MRelays));
        OnPropertyChanged(nameof(XInputs));
        OnPropertyChanged(nameof(YOutputs));
    }

    /// <summary>
    /// 특정 디바이스 영역 초기화
    /// </summary>
    public void Clear(DeviceType deviceType)
    {
        lock (_lock)
        {
            switch (deviceType)
            {
                case DeviceType.D:
                    Array.Clear(_dRegisters);
                    OnPropertyChanged(nameof(DRegisters));
                    break;
                case DeviceType.M:
                    Array.Clear(_mRelays);
                    OnPropertyChanged(nameof(MRelays));
                    break;
                case DeviceType.X:
                    Array.Clear(_xInputs);
                    OnPropertyChanged(nameof(XInputs));
                    break;
                case DeviceType.Y:
                    Array.Clear(_yOutputs);
                    OnPropertyChanged(nameof(YOutputs));
                    break;
                case DeviceType.W:
                    Array.Clear(_wRegisters);
                    break;
                case DeviceType.B:
                    Array.Clear(_bRelays);
                    break;
                case DeviceType.R:
                    Array.Clear(_rRegisters);
                    break;
            }
        }
    }

    #endregion

    #region 속성 (UI 바인딩용)

    public ushort[] DRegisters => _dRegisters;
    public ushort[] MRelays => _mRelays;
    public ushort[] XInputs => _xInputs;
    public ushort[] YOutputs => _yOutputs;
    public ushort[] WRegisters => _wRegisters;

    #endregion

    #region Private Helpers

    private ushort[]? GetWordMemory(DeviceType deviceType) => deviceType switch
    {
        DeviceType.D => _dRegisters,
        DeviceType.W => _wRegisters,
        DeviceType.R => _rRegisters,
        _ => null
    };

    private ushort[]? GetBitMemory(DeviceType deviceType) => deviceType switch
    {
        DeviceType.M => _mRelays,
        DeviceType.X => _xInputs,
        DeviceType.Y => _yOutputs,
        DeviceType.B => _bRelays,
        DeviceType.L => null, // 필요시 추가
        _ => null
    };

    private void OnMemoryChanged(DeviceType deviceType, int address, int count)
    {
        MemoryChanged?.Invoke(this, new MemoryChangedEventArgs(deviceType, address, count));

        // UI 바인딩 알림
        switch (deviceType)
        {
            case DeviceType.D:
                OnPropertyChanged(nameof(DRegisters));
                break;
            case DeviceType.M:
                OnPropertyChanged(nameof(MRelays));
                break;
            case DeviceType.X:
                OnPropertyChanged(nameof(XInputs));
                break;
            case DeviceType.Y:
                OnPropertyChanged(nameof(YOutputs));
                break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
