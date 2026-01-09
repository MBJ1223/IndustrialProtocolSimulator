using ModbusProtocolSimulator.Protocol;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModbusProtocolSimulator.Simulator;

/// <summary>
/// 메모리 변경 이벤트 인자
/// </summary>
public class ModbusMemoryChangedEventArgs : EventArgs
{
    public ModbusAreaType AreaType { get; }
    public int Address { get; }
    public int Count { get; }

    public ModbusMemoryChangedEventArgs(ModbusAreaType areaType, int address, int count)
    {
        AreaType = areaType;
        Address = address;
        Count = count;
    }
}

/// <summary>
/// Modbus 메모리 시뮬레이터
///
/// Modbus 메모리 영역:
/// - Coils (0xxxxx): 1비트, 읽기/쓰기 (DO - Digital Output)
/// - Discrete Inputs (1xxxxx): 1비트, 읽기 전용 (DI - Digital Input)
/// - Input Registers (3xxxxx): 16비트, 읽기 전용 (AI - Analog Input)
/// - Holding Registers (4xxxxx): 16비트, 읽기/쓰기 (AO - Analog Output)
/// </summary>
public class ModbusMemory : INotifyPropertyChanged
{
    // 메모리 크기 (시뮬레이터용으로 제한)
    public const int CoilsSize = 10000;           // 0 ~ 9999
    public const int DiscreteInputsSize = 10000;  // 0 ~ 9999
    public const int InputRegistersSize = 10000;  // 0 ~ 9999
    public const int HoldingRegistersSize = 10000; // 0 ~ 9999

    // 메모리 배열
    private readonly bool[] _coils;
    private readonly bool[] _discreteInputs;
    private readonly ushort[] _inputRegisters;
    private readonly ushort[] _holdingRegisters;

    private readonly object _lock = new();

    public event EventHandler<ModbusMemoryChangedEventArgs>? MemoryChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ModbusMemory()
    {
        _coils = new bool[CoilsSize];
        _discreteInputs = new bool[DiscreteInputsSize];
        _inputRegisters = new ushort[InputRegistersSize];
        _holdingRegisters = new ushort[HoldingRegistersSize];
    }

    #region Coils (DO - 0xxxxx)

    /// <summary>Coils 읽기</summary>
    public bool[] ReadCoils(int address, int count)
    {
        lock (_lock)
        {
            var result = new bool[count];
            for (int i = 0; i < count && address + i < CoilsSize; i++)
            {
                result[i] = _coils[address + i];
            }
            return result;
        }
    }

    /// <summary>단일 Coil 쓰기</summary>
    public bool WriteSingleCoil(int address, bool value)
    {
        if (address >= CoilsSize) return false;

        lock (_lock)
        {
            _coils[address] = value;
        }

        OnMemoryChanged(ModbusAreaType.Coils, address, 1);
        return true;
    }

    /// <summary>다중 Coils 쓰기</summary>
    public bool WriteMultipleCoils(int address, bool[] values)
    {
        if (address + values.Length > CoilsSize) return false;

        lock (_lock)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _coils[address + i] = values[i];
            }
        }

        OnMemoryChanged(ModbusAreaType.Coils, address, values.Length);
        return true;
    }

    #endregion

    #region Discrete Inputs (DI - 1xxxxx)

    /// <summary>Discrete Inputs 읽기</summary>
    public bool[] ReadDiscreteInputs(int address, int count)
    {
        lock (_lock)
        {
            var result = new bool[count];
            for (int i = 0; i < count && address + i < DiscreteInputsSize; i++)
            {
                result[i] = _discreteInputs[address + i];
            }
            return result;
        }
    }

    /// <summary>Discrete Input 설정 (시뮬레이터용)</summary>
    public bool SetDiscreteInput(int address, bool value)
    {
        if (address >= DiscreteInputsSize) return false;

        lock (_lock)
        {
            _discreteInputs[address] = value;
        }

        OnMemoryChanged(ModbusAreaType.DiscreteInputs, address, 1);
        return true;
    }

    #endregion

    #region Input Registers (AI - 3xxxxx)

    /// <summary>Input Registers 읽기</summary>
    public ushort[] ReadInputRegisters(int address, int count)
    {
        lock (_lock)
        {
            var result = new ushort[count];
            for (int i = 0; i < count && address + i < InputRegistersSize; i++)
            {
                result[i] = _inputRegisters[address + i];
            }
            return result;
        }
    }

    /// <summary>Input Register 설정 (시뮬레이터용)</summary>
    public bool SetInputRegister(int address, ushort value)
    {
        if (address >= InputRegistersSize) return false;

        lock (_lock)
        {
            _inputRegisters[address] = value;
        }

        OnMemoryChanged(ModbusAreaType.InputRegisters, address, 1);
        return true;
    }

    #endregion

    #region Holding Registers (AO - 4xxxxx)

    /// <summary>Holding Registers 읽기</summary>
    public ushort[] ReadHoldingRegisters(int address, int count)
    {
        lock (_lock)
        {
            var result = new ushort[count];
            for (int i = 0; i < count && address + i < HoldingRegistersSize; i++)
            {
                result[i] = _holdingRegisters[address + i];
            }
            return result;
        }
    }

    /// <summary>단일 Holding Register 쓰기</summary>
    public bool WriteSingleRegister(int address, ushort value)
    {
        if (address >= HoldingRegistersSize) return false;

        lock (_lock)
        {
            _holdingRegisters[address] = value;
        }

        OnMemoryChanged(ModbusAreaType.HoldingRegisters, address, 1);
        return true;
    }

    /// <summary>다중 Holding Registers 쓰기</summary>
    public bool WriteMultipleRegisters(int address, ushort[] values)
    {
        if (address + values.Length > HoldingRegistersSize) return false;

        lock (_lock)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _holdingRegisters[address + i] = values[i];
            }
        }

        OnMemoryChanged(ModbusAreaType.HoldingRegisters, address, values.Length);
        return true;
    }

    #endregion

    #region 메모리 초기화

    public void ClearAll()
    {
        lock (_lock)
        {
            Array.Clear(_coils);
            Array.Clear(_discreteInputs);
            Array.Clear(_inputRegisters);
            Array.Clear(_holdingRegisters);
        }

        OnPropertyChanged(nameof(Coils));
        OnPropertyChanged(nameof(DiscreteInputs));
        OnPropertyChanged(nameof(InputRegisters));
        OnPropertyChanged(nameof(HoldingRegisters));
    }

    public void ClearArea(ModbusAreaType areaType)
    {
        lock (_lock)
        {
            switch (areaType)
            {
                case ModbusAreaType.Coils:
                    Array.Clear(_coils);
                    break;
                case ModbusAreaType.DiscreteInputs:
                    Array.Clear(_discreteInputs);
                    break;
                case ModbusAreaType.InputRegisters:
                    Array.Clear(_inputRegisters);
                    break;
                case ModbusAreaType.HoldingRegisters:
                    Array.Clear(_holdingRegisters);
                    break;
            }
        }
    }

    #endregion

    #region 속성

    public bool[] Coils => _coils;
    public bool[] DiscreteInputs => _discreteInputs;
    public ushort[] InputRegisters => _inputRegisters;
    public ushort[] HoldingRegisters => _holdingRegisters;

    #endregion

    #region Private Helpers

    private void OnMemoryChanged(ModbusAreaType areaType, int address, int count)
    {
        MemoryChanged?.Invoke(this, new ModbusMemoryChangedEventArgs(areaType, address, count));
        OnPropertyChanged(areaType.ToString());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
