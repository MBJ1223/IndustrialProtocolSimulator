using McProtocolSimulator.Protocol;
using McProtocolSimulator.Simulator;
using System.Windows;

namespace McProtocolSimulator.Views;

/// <summary>
/// MemoryEditWindow.xaml에 대한 상호 작용 논리
/// </summary>
public partial class MemoryEditWindow : Window
{
    private readonly PlcMemory _memory;

    public MemoryEditWindow(PlcMemory memory)
    {
        InitializeComponent();
        _memory = memory;
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var deviceType = DeviceCombo.SelectedIndex switch
            {
                0 => DeviceType.D,
                1 => DeviceType.M,
                2 => DeviceType.X,
                3 => DeviceType.Y,
                _ => DeviceType.D
            };

            int address = int.Parse(AddressTextBox.Text);
            int value = int.Parse(ValueTextBox.Text);

            bool isBitDevice = deviceType is DeviceType.M or DeviceType.X or DeviceType.Y;

            if (isBitDevice)
            {
                _memory.WriteBit(deviceType, address, value != 0);
            }
            else
            {
                _memory.WriteWord(deviceType, address, (ushort)value);
            }

            MessageBox.Show($"{deviceType}{address} = {value} 쓰기 완료", "성공",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"쓰기 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
