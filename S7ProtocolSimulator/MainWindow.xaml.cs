using S7ProtocolSimulator.Views;
using System.Collections.Specialized;
using System.Windows;

namespace S7ProtocolSimulator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is ViewModels.MainViewModel vm)
        {
            ((INotifyCollectionChanged)vm.LogEntries).CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            };
        }
    }

    private void EditMemory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            var editWindow = new S7MemoryEditWindow(vm.Memory, vm.SelectedAreaIndex, vm.SelectedDbNumber) { Owner = this };
            editWindow.ShowDialog();
        }
    }
}
