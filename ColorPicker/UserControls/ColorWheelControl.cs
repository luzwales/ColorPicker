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
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorPicker.UserControls;

public partial class ColorWheelControl : UserControl
{
	private static Assembly? _libAssembly;
	private static Type? _pickerType;
	private readonly object _picker;
    private static Type PickerType => _pickerType ??= _libAssembly!.GetType("ColorPicker.SquarePicker")!;

    private bool _isInternalUpdate = false;

	public ColorWheelControl()
	{
		try {
			if (_libAssembly == null) _libAssembly = LoadLibrary();
			if (_pickerType == null) _pickerType = _libAssembly.GetType("ColorPicker.SquarePicker")!;

			InitializeComponent();
			_picker = Activator.CreateInstance(PickerType)!;
			
			PickerContainer.Content = _picker;

			var colorChanged = PickerType.GetEvent("ColorChanged")!;
			colorChanged.AddEventHandler(_picker, new RoutedEventHandler(OnPickerColorChanged));
		} catch (Exception ex) {
			System.IO.File.WriteAllText(@"C:\tmp\colorpicker_error.txt", "Constructor: " + ex.ToString());
			throw;
		}
	}

	private void OnPickerColorChanged(object sender, RoutedEventArgs e)
	{
        if (_isInternalUpdate) return;

        // 强力反射提取内部选择的颜色，更新我们暴露的 SelectedColor 依赖属性
        try {
            var prop = PickerType.GetProperty("SelectedColor")!;
            var color = (Color)prop.GetValue(_picker)!;
            
            _isInternalUpdate = true;
            SelectedColor = color;
            _isInternalUpdate = false;

            // 触发事件通知 ColorPickerDialog 刷新颜色
            ColorChanged?.Invoke(this, new RoutedEventArgs());
        } catch {}
	}

	public static readonly DependencyProperty SelectedColorProperty =
		DependencyProperty.Register(
			nameof(SelectedColor),
			typeof(Color),
			typeof(ColorWheelControl),
			new PropertyMetadata(Colors.Black, OnSelectedColorChanged));

	public Color SelectedColor
	{
		get => (Color)GetValue(SelectedColorProperty);
		set => SetValue(SelectedColorProperty, value);
	}

	private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var ctrl = (ColorWheelControl)d;
        if (ctrl._isInternalUpdate) return;

        try {
		    var prop = PickerType.GetProperty("SelectedColor")!;
            ctrl._isInternalUpdate = true;
		    prop.SetValue(ctrl._picker, (Color)e.NewValue);
            ctrl._isInternalUpdate = false;
        } catch {}
	}

	private static Assembly LoadLibrary()
	{
		try
		{
			return Assembly.Load("ColorPicker, Version=3.4.1.0, Culture=neutral, PublicKeyToken=1c61eec504ce2276");
		}
		catch
		{
			string dir = AppDomain.CurrentDomain.BaseDirectory;
			return Assembly.LoadFrom(System.IO.Path.Combine(dir, "ColorPicker.dll"));
		}
	}

	public event RoutedEventHandler ColorChanged;
}
