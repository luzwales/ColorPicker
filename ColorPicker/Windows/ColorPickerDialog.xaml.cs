/*
MIT License

Copyright (c) Léo Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/
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