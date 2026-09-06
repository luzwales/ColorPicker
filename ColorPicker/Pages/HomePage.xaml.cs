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
using ColorPicker.Classes;
using ColorPicker.Enums;
using ColorPicker.UserControls;
using ColorPicker.Windows;
using Synethia;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ColorPicker.Pages;

/// <summary>
/// Interaction logic for HomePage.xaml
/// </summary>
public partial class HomePage : Page
{
	public HomePage()
	{
		InitializeComponent();
		InitUI();
	}

	// 依赖属性：主页导航块列数（最多 5 列，随宽度弹性变化）
	public static readonly DependencyProperty NavColumnsProperty =
		DependencyProperty.Register(
			nameof(NavColumns),
			typeof(int),
			typeof(HomePage),
			new PropertyMetadata(5));

	public int NavColumns
	{
		get => (int)GetValue(NavColumnsProperty);
		set => SetValue(NavColumnsProperty, value);
	}

	internal void InitUI()
	{
		NavPanel.Items.Clear();

		// 不再分组：所有功能导航统一放一排，先 5 个一行、铺满整行、随宽度弹性变化
		List<PageInfo> relevantPages = new(Global.SynethiaConfig.MostRelevantPages);
		for (int i = 0; i < relevantPages.Count; i++)
		{
			if (relevantPages[i].Name == "Harmonies") continue;
			NavPanel.Items.Add(new PageCard(Global.PageInfoToAppPages(relevantPages[i])));
		}

		LoadPaletteUI();
	}

	private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		// 与颜色书签一致：每行最多 5 个；当单块可分配宽度 < 220px 时减少列数
		double width = e.NewSize.Width - 10;
		if (width <= 0) return;
		int cols = 5;
		while (cols > 1 && width / cols < 220)
		{
			cols--;
		}
		NavColumns = cols;
	}

	private void GetContrastBtn_Click(object sender, System.Windows.RoutedEventArgs e)
	{
		try
		{
			var color1 = ColorHelper.ColorConverter.HexToRgb(new(Color1Txt.Text));
			var color2 = ColorHelper.ColorConverter.HexToRgb(new(Color2Txt.Text));
			var contrast = Global.GetContrast([color1.R, color1.G, color1.B], [color2.R, color2.G, color2.B]);
			ContrastTxt.Text = $"{Properties.Resources.Contrast}: {contrast.Item1}";
			ContrastBorder.Background = contrast.Item2 switch
			{
				0 => Global.GetColorFromResource("Green"),
				1 => Global.GetColorFromResource("SliderBlue"),
				2 => Global.GetColorFromResource("Orange"),
				_ => Global.GetColorFromResource("Red"),
			};
		}
		catch { }
	}

	private void LoadPaletteUI(Color? targetColor = null)
	{
		PalettePanel.Children.Clear();

		ColorInfo color;
        if (targetColor.HasValue)
        {
            color = new(new(targetColor.Value.R, targetColor.Value.G, targetColor.Value.B));
        }
        else
        {
		    (int r, int g, int b) = Global.GenerateRandomColor();
		    color = new(new((byte)r, (byte)g, (byte)b));
        }

		ColorBorder.Background = new SolidColorBrush { Color = Color.FromRgb(color.RGB.R, color.RGB.G, color.RGB.B) };
		ColorBorder.Effect = new DropShadowEffect() { BlurRadius = 15, ShadowDepth = 0, Color = Color.FromRgb(color.RGB.R, color.RGB.G, color.RGB.B) };
		ColorBorder.ToolTip = new ToolTip()
		{
			Background = Global.GetColorFromResource("Background1"),
			Foreground = Global.GetColorFromResource("Foreground1"),
			Content = color.ToString()
		};

		var hues = Global.GetHues(color.HSL);

		for (int i = 0; i < hues.Length; i++)
		{
			CornerRadius radius = i == 0 ? new(10, 0, 0, 10) : new(0);
			if (i == hues.Length - 1) radius = new(0, 10, 10, 0);

			Border border = new()
			{
				Cursor = Cursors.Hand,
				Height = 50,
				Width = 50,
				CornerRadius = radius,
				Background = new SolidColorBrush { Color = Color.FromRgb(hues[i].R, hues[i].G, hues[i].B) },
				Effect = new DropShadowEffect()
				{
					BlurRadius = 15,
					Opacity = 0.2,
					ShadowDepth = 0,
					Color = Color.FromRgb(hues[i].R, hues[i].G, hues[i].B)
				},
				ToolTip = new ToolTip()
				{
					Background = Global.GetColorFromResource("Background1"),
					Foreground = Global.GetColorFromResource("Foreground1"),
					Content = new ColorInfo(new(hues[i].R, hues[i].G, hues[i].B)).ToString()
				},
			};
			int j = i > hues.Length ? hues.Length - 1 : i; // Avoid index out of range
			border.MouseLeftButtonUp += (o, e) =>
			{
				var info = new ColorInfo(new(hues[j].R, hues[j].G, hues[j].B));
				Clipboard.SetDataObject(Global.Settings.DefaultColorType switch
				{
					ColorTypes.HEX => info.HEX.Value,
					ColorTypes.HSV => $"{info.HSV.H},{info.HSV.S},{info.HSV.V}",
					ColorTypes.HSL => $"{info.HSL.H},{info.HSL.S},{info.HSL.L}",
					ColorTypes.CMYK => $"{info.CMYK.C},{info.CMYK.M},{info.CMYK.Y},{info.CMYK.K}",
					ColorTypes.XYZ => $"{info.XYZ.X}; {info.XYZ.Y}; {info.XYZ.Z}",
					ColorTypes.YIQ => $"{info.YIQ.Y}; {info.YIQ.I}; {info.YIQ.Q}",
					ColorTypes.YUV => $"{info.YUV.Y}; {info.YUV.U}; {info.YUV.V}",
					ColorTypes.DEC => info.DEC.Value.ToString(),
					_ => $"{hues[j].R}{Global.Settings.RgbSeparator}{hues[j].G}{Global.Settings.RgbSeparator}{hues[j].B}"
				});
			};

			border.MouseRightButtonUp += (o, e) =>
			{
				new ColorDetailsWindow(new SolidColorBrush { Color = Color.FromRgb(hues[j].R, hues[j].G, hues[j].B) }).Show();
				PalettePopup.IsOpen = false;
			};

			PalettePanel.Children.Add(border);
		}
	}

	private void ColorBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
        var bg = ColorBorder.Background as SolidColorBrush;
        var owner = Window.GetWindow(this);
        var dialog = new Windows.ColorPickerDialog(
            bg?.Color ?? Colors.Black,
            owner,
            (c) => {
                LoadPaletteUI(c);
            }
        );
        dialog.Show();
	}

	// ============================
	// Helpers invoked by MainWindow nav bar buttons
	// ============================

	public void Nav_OpenColorSelector()
	{
		Global.SelectorPage?.SelectBtn_Click(this, new RoutedEventArgs());
	}

	public void Nav_OpenContrastPopup()
	{
		ContrastPopup.IsOpen = true;
	}

	public void Nav_OpenPalettePopup()
	{
		PalettePopup.IsOpen = true;
	}
}