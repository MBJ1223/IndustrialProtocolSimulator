using ModbusProtocolSimulator.ViewModels;
using ModbusProtocolSimulator.Views;
using System.Windows;

namespace ModbusProtocolSimulator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void EditMemory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var editWindow = new ModbusMemoryEditWindow(vm)
            {
                Owner = this
            };
            editWindow.ShowDialog();
        }
    }
}
