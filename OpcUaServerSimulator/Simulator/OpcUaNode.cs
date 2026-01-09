using OpcUaServerSimulator.Protocol;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpcUaServerSimulator.Simulator;

/// <summary>
/// OPC UA 노드
/// </summary>
public class OpcUaNode : INotifyPropertyChanged
{
    private object? _value;
    private DateTime _timestamp;

    public string NodeId { get; set; } = "";
    public string BrowseName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public OpcUaNodeClass NodeClass { get; set; }
    public OpcUaDataType DataType { get; set; }
    public bool IsWritable { get; set; } = true;
    public string? ParentNodeId { get; set; }

    public object? Value
    {
        get => _value;
        set
        {
            _value = value;
            _timestamp = DateTime.UtcNow;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Timestamp));
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(); }
    }

    public string DisplayValue => Value?.ToString() ?? "(null)";

    public uint StatusCode { get; set; } = OpcUaConstants.StatusCodeGood;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// OPC UA 노드 저장소
/// </summary>
public class OpcUaNodeStore
{
    private readonly Dictionary<string, OpcUaNode> _nodes = new();

    public event EventHandler<OpcUaNode>? NodeValueChanged;

    public OpcUaNodeStore()
    {
        // 기본 폴더 구조 생성
        CreateDefaultStructure();
    }

    private void CreateDefaultStructure()
    {
        // Root Objects
        AddNode(new OpcUaNode
        {
            NodeId = "ns=0;i=85",
            BrowseName = "Objects",
            DisplayName = "Objects",
            NodeClass = OpcUaNodeClass.Object
        });

        // Simulation 폴더
        AddNode(new OpcUaNode
        {
            NodeId = "ns=2;s=Simulation",
            BrowseName = "Simulation",
            DisplayName = "Simulation",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = "ns=0;i=85"
        });

        // 기본 변수들
        AddVariable("ns=2;s=Simulation.Counter", "Counter", OpcUaDataType.Int32, 0);
        AddVariable("ns=2;s=Simulation.Random", "Random", OpcUaDataType.Double, 0.0);
        AddVariable("ns=2;s=Simulation.SineWave", "SineWave", OpcUaDataType.Double, 0.0);
        AddVariable("ns=2;s=Simulation.Boolean", "Boolean", OpcUaDataType.Boolean, false);
        AddVariable("ns=2;s=Simulation.String", "String", OpcUaDataType.String, "Hello OPC UA");

        // 태그 폴더
        AddNode(new OpcUaNode
        {
            NodeId = "ns=2;s=Tags",
            BrowseName = "Tags",
            DisplayName = "Tags",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = "ns=0;i=85"
        });

        // 태그 변수들
        for (int i = 1; i <= 10; i++)
        {
            AddVariable($"ns=2;s=Tags.Tag{i}", $"Tag{i}", OpcUaDataType.Double, 0.0, "ns=2;s=Tags");
        }

        // 디바이스 폴더
        AddNode(new OpcUaNode
        {
            NodeId = "ns=2;s=Device",
            BrowseName = "Device",
            DisplayName = "Device",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = "ns=0;i=85"
        });

        AddVariable("ns=2;s=Device.Temperature", "Temperature", OpcUaDataType.Double, 25.0, "ns=2;s=Device");
        AddVariable("ns=2;s=Device.Pressure", "Pressure", OpcUaDataType.Double, 1013.25, "ns=2;s=Device");
        AddVariable("ns=2;s=Device.Status", "Status", OpcUaDataType.Int32, 1, "ns=2;s=Device");
        AddVariable("ns=2;s=Device.Running", "Running", OpcUaDataType.Boolean, true, "ns=2;s=Device");
    }

    private void AddVariable(string nodeId, string name, OpcUaDataType dataType, object value, string? parentId = "ns=2;s=Simulation")
    {
        AddNode(new OpcUaNode
        {
            NodeId = nodeId,
            BrowseName = name,
            DisplayName = name,
            NodeClass = OpcUaNodeClass.Variable,
            DataType = dataType,
            Value = value,
            ParentNodeId = parentId,
            IsWritable = true
        });
    }

    public void AddNode(OpcUaNode node)
    {
        _nodes[node.NodeId] = node;
        node.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(OpcUaNode.Value))
                NodeValueChanged?.Invoke(this, node);
        };
    }

    public OpcUaNode? GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public IEnumerable<OpcUaNode> GetAllNodes() => _nodes.Values;

    public IEnumerable<OpcUaNode> GetVariables() =>
        _nodes.Values.Where(n => n.NodeClass == OpcUaNodeClass.Variable);

    public IEnumerable<OpcUaNode> GetChildNodes(string parentNodeId) =>
        _nodes.Values.Where(n => n.ParentNodeId == parentNodeId);

    public bool WriteValue(string nodeId, object value)
    {
        var node = GetNode(nodeId);
        if (node == null || !node.IsWritable) return false;

        try
        {
            node.Value = ConvertValue(value, node.DataType);
            return true;
        }
        catch { return false; }
    }

    private static object ConvertValue(object value, OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => Convert.ToBoolean(value),
            OpcUaDataType.SByte => Convert.ToSByte(value),
            OpcUaDataType.Byte => Convert.ToByte(value),
            OpcUaDataType.Int16 => Convert.ToInt16(value),
            OpcUaDataType.UInt16 => Convert.ToUInt16(value),
            OpcUaDataType.Int32 => Convert.ToInt32(value),
            OpcUaDataType.UInt32 => Convert.ToUInt32(value),
            OpcUaDataType.Int64 => Convert.ToInt64(value),
            OpcUaDataType.UInt64 => Convert.ToUInt64(value),
            OpcUaDataType.Float => Convert.ToSingle(value),
            OpcUaDataType.Double => Convert.ToDouble(value),
            OpcUaDataType.String => value.ToString() ?? "",
            _ => value
        };
    }
}
