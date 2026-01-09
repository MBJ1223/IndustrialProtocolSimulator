using S7ProtocolSimulator.Protocol;
using S7ProtocolSimulator.Simulator;
using System.Windows;

namespace S7ProtocolSimulator.Views;

public partial class S7MemoryEditWindow : Window
{
    private readonly S7Memory _memory;

    public S7MemoryEditWindow(S7Memory memory, int selectedArea, int selectedDb)
    {
        InitializeComponent();
        _memory = memory;
        AreaCombo.SelectedIndex = selectedArea;
        DbNumberTextBox.Text = selectedDb.ToString();
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            byte area = AreaCombo.SelectedIndex switch
            {
                0 => S7Constants.AreaInput,
                1 => S7Constants.AreaOutput,
                2 => S7Constants.AreaFlags,
                3 => S7Constants.AreaDB,
                _ => S7Constants.AreaFlags
            };

            int dbNumber = int.Parse(DbNumberTextBox.Text);
            int address = int.Parse(AddressTextBox.Text);
            byte value = byte.Parse(ValueTextBox.Text);

            _memory.WriteBytes(area, dbNumber, address, new[] { value });

            string areaName = AreaCombo.SelectedIndex switch
            {
                0 => $"IB{address}",
                1 => $"QB{address}",
                2 => $"MB{address}",
                3 => $"DB{dbNumber}.DBB{address}",
                _ => $"MB{address}"
            };

            MessageBox.Show($"{areaName} = {value} 쓰기 완료", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"쓰기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
