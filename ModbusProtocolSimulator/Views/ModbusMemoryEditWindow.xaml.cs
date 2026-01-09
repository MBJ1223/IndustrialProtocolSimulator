using ModbusProtocolSimulator.ViewModels;
using System.Windows;

namespace ModbusProtocolSimulator.Views;

public partial class ModbusMemoryEditWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ModbusMemoryEditWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // 현재 선택된 영역 표시
        AreaTypeText.Text = _viewModel.AreaTypes[_viewModel.SelectedAreaIndex];

        // 힌트 텍스트 설정
        if (_viewModel.IsBitArea)
        {
            HintText.Text = "비트 영역: 값은 0 (OFF) 또는 1 (ON)을 입력하세요.";
        }
        else
        {
            HintText.Text = "레지스터 영역: 값은 0 ~ 65535 범위의 정수를 입력하세요.\n16진수 입력: 0x1234 형식";
        }
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(AddressTextBox.Text, out int address) || address < 0)
            {
                MessageBox.Show("올바른 주소를 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string valueText = ValueTextBox.Text.Trim();
            ushort value;

            // 16진수 처리
            if (valueText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = Convert.ToUInt16(valueText, 16);
            }
            else
            {
                value = ushort.Parse(valueText);
            }

            _viewModel.WriteMemoryValue(address, value);
            MessageBox.Show($"주소 {address}에 값 {value}을(를) 썼습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"쓰기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
