using McProtocolSimulator.Views;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace McProtocolSimulator;

/// <summary>
/// MainWindow.xaml에 대한 상호 작용 논리
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 로그 자동 스크롤 설정
        if (DataContext is ViewModels.MainViewModel vm)
        {
            ((INotifyCollectionChanged)vm.LogEntries).CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
            };
        }
    }

    private void EditMemory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            var editWindow = new MemoryEditWindow(vm.Memory)
            {
                Owner = this
            };
            editWindow.ShowDialog();
        }
    }
}
