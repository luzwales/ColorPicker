using System;
using System.Windows;
using System.Windows.Media;
using ColorPicker.UserControls;

namespace ColorPicker.Windows;

public partial class ColorPickerDialog : Window
{
    private readonly Action<Color> _onColorChanged;
    private bool _isInitialized = false;

    public ColorPickerDialog(Color initialColor, Window owner, Action<Color> onColorChanged)
    {
        InitializeComponent();
        this.Owner = owner; // 必须设置 Owner，使其成为非模态子浮层
        this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (Application.Current != null)
        {
            this.Resources.MergedMergedDictionaries_Add(Application.Current.Resources);
        }
        
        _onColorChanged = onColorChanged;
        Picker.SelectedColor = initialColor;
        _isInitialized = true;

        this.Loaded += (s, e) => {
            this.Activate();
            this.Focus();
        };
    }

    private void Picker_ColorChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        var picker = (ColorWheelControl)sender;
        _onColorChanged?.Invoke(picker.SelectedColor); // 点击/拖动色轮时实时回调，修改主色块
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // 点击主窗口、点击空白、只要失去焦点，立刻自动关闭弹窗
        Close();
    }
}

// 辅助方法，安全合并资源字典
static class ResourcesHelper
{
    public static void MergedMergedDictionaries_Add(this ResourceDictionary dest, ResourceDictionary src)
    {
        try {
            dest.MergedDictionaries.Add(src);
        } catch {}
    }
}