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
using ColorPicker.Models;
using ColorPicker.UserControls;
using ColorPicker.Windows;
using Gma.System.MouseKeyHook;
using Synethia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ColorHelper;

namespace ColorPicker.Pages;

public partial class SelectorPage : Page
{
	bool code = !Global.Settings.UseSynethia; // checks if the code as already been implemented
	readonly DispatcherTimer timer = new();
	private IKeyboardMouseEvents keyboardEvents = Hook.GlobalEvents();
	internal MiniPicker miniPicker = new(); // MiniPicker window
	ColorInfo ColorInfo { get; set; } = null!;

	DetailsControl DetailsControl { get; set; } = new(new(new(0, 0, 0))); // Details control
	internal Button SelectedColorBtn { get; set; } = null!;
	internal ColorTypes SelectedColorType { get; set; } = ColorTypes.RGB;

    private bool isUpdatingFromSliders = false;
    private bool isUpdatingFromInputs = false;
    private bool isUpdatingFromHexInput = false;

	public SelectorPage()
	{
		InitializeComponent();
		InitUI();

		Loaded += (o, e) => {
            SynethiaManager.InjectSynethiaCode(this, Global.SynethiaConfig.PagesInfo, 0, ref code); // injects the code in the page
        };
	}

	private void InitUI()
	{
		TitleTxt.Text = $"{Properties.Resources.Picker} > {Properties.Resources.Selector}";
		DetailsWrap.Children.Add(DetailsControl); // Add details control to the page	
		(RedSlider.Value, GreenSlider.Value, BlueSlider.Value) = (30, 130, 220); // fixed default (no random generation)

		// 绑定色轮 ColorChanged 路由事件 -> 实时联动滑块 + 文本输入框 + 预览
		WheelPicker.ColorChanged += (s, e) =>
		{
			if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;
			var picker = (ColorWheelControl)s;
			byte r = picker.SelectedColor.R;
			byte g = picker.SelectedColor.G;
			byte b = picker.SelectedColor.B;

            SyncSlidersFromRgb(r, g, b);

            Color color = Color.FromRgb(r, g, b);
            ColorBorder.Background = new SolidColorBrush { Color = color };
            ColorBorder.Effect = new DropShadowEffect() { BlurRadius = 15, ShadowDepth = 0, Color = color };
            
            ColorInfo = new ColorInfo(new(r, g, b));
            LoadDetails();
		};

		// 设定默认按钮高亮
		SelectedColorType = Global.Settings.DefaultColorType;
		SelectedColorBtn = SelectedColorType switch
		{
			ColorTypes.HEX => HexBtn,
			ColorTypes.HSV => HsvBtn,
			ColorTypes.HSL => HslBtn,
			ColorTypes.CMYK => CmykBtn,
			ColorTypes.XYZ => XyzBtn,
			ColorTypes.YIQ => YiqBtn,
			ColorTypes.YUV => YuvBtn,
			ColorTypes.DEC => DecBtn,
			_ => RgbBtn
		};

		LoadDetails();
        UnCheckAllButtons();
        CheckButton(SelectedColorBtn);
        LoadSliders();

		timer.Interval = new(0, 0, 0, 0, 1); // Interval
		timer.Tick += (o, e) =>
		{
			// Get the pixel from the screen
			System.Drawing.Bitmap bitmap = new(1, 1); // Create a bitmap where the color of the pixel is going to be copied
			System.Drawing.Graphics GFX = System.Drawing.Graphics.FromImage(bitmap);
			GFX.CopyFromScreen(System.Windows.Forms.Cursor.Position, new System.Drawing.Point(0, 0), bitmap.Size); // Get the color of the pixel at the mouse position
			var pixel = bitmap.GetPixel(0, 0); // Copy to the bitmap

            SyncSlidersFromRgb(pixel.R, pixel.G, pixel.B);

			LoadDetails();

			// MiniPicker DPI 比例计算
			float dpiX, dpiY;
			double scaling = 100; // Default scaling = 100%

			using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
			{
				dpiX = graphics.DpiX;
				dpiY = graphics.DpiY;

				scaling = dpiX switch
				{
					96 => 100,
					120 => 125,
					144 => 150,
					168 => 175,
					192 => 200, 
					_ => 100
				};
			}

			double factor = scaling / 100d; // Calculate factor

			// Position the MiniPicker next to the cursor, but flip to the other side
			System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
			double screenW = workingArea.Width / factor;
			double screenH = workingArea.Height / factor;

			double pickerW = miniPicker.Width;   // 270 (logical units)
			double pickerH = miniPicker.Height;  // 120 (logical units)
			double cursorX = System.Windows.Forms.Cursor.Position.X / factor;
			double cursorY = System.Windows.Forms.Cursor.Position.Y / factor;

			double left = cursorX + 16;
			double top = cursorY + 16;

			if (left + pickerW > screenW) left = cursorX - pickerW - 16; // flip to the left of the cursor
			if (top + pickerH > screenH) top = cursorY - pickerH - 16;   // flip above the cursor

			miniPicker.Left = Math.Max(0, left);
			miniPicker.Top = Math.Max(0, top);
		};

		// 注册快捷键
		try
		{
			keyboardEvents = Hook.GlobalEvents();
			keyboardEvents.KeyDown += (s, e) =>
			{
				if (e.KeyCode == System.Windows.Forms.Keys.Escape && selecting)
				{
					selecting = false;
					UpdateSelectionState(false);
				}
			};
			Hook.GlobalEvents().OnCombination(new Dictionary<Combination, Action>
			{
				{ Combination.FromString(Global.Settings.CopyKeyboardShortcut), HandleCopyKeyboard },
				{ Combination.FromString(Global.Settings.SelectKeyboardShortcut), HandleSelectKeyboard }
			});
		}
		catch { }

		LoadBookmarkMenu();
	}

    // 实现顶部返回按钮，一键返回上一个页面
    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService.CanGoBack)
        {
            NavigationService.GoBack();
        }
    }

    private void SyncSlidersFromRgb(byte r, byte g, byte b)
    {
        isUpdatingFromSliders = true;
        try {
            if (SelectedColorType == ColorTypes.HSV)
            {
                var hsv = ColorHelper.ColorConverter.RgbToHsv(new(r, g, b));
                RedSlider.Value = hsv.H;
                GreenSlider.Value = hsv.S;
                BlueSlider.Value = hsv.V;
            }
            else if (SelectedColorType == ColorTypes.HSL)
            {
                var hsl = ColorHelper.ColorConverter.RgbToHsl(new(r, g, b));
                RedSlider.Value = hsl.H;
                GreenSlider.Value = hsl.S;
                BlueSlider.Value = hsl.L;
            }
            else if (SelectedColorType == ColorTypes.CMYK)
            {
                var cmyk = ColorHelper.ColorConverter.RgbToCmyk(new(r, g, b));
                RedSlider.Value = cmyk.C;
                GreenSlider.Value = cmyk.M;
                BlueSlider.Value = cmyk.Y;
                KSlider.Value = cmyk.K;
            }
            else // RGB, HEX, DEC, XYZ, YIQ, YUV 均使用标准 RGB 滑块
            {
                RedSlider.Value = r;
                GreenSlider.Value = g;
                BlueSlider.Value = b;
            }
        } finally {
            isUpdatingFromSliders = false;
        }
    }

	internal void LoadBookmarkMenu()
	{
		CollectionsPanel.Children.Clear();
		for (int i = 0; i < Global.Bookmarks.ColorCollections.Count; i++)
		{
			bool isAddedAlready = Global.Bookmarks.ColorCollections[i].Colors.Contains(ColorInfo.HEX.Value);
			Button button = new()
			{
				Content = isAddedAlready ? string.Format(Properties.Resources.RemoveFrom, Global.Bookmarks.ColorCollections[i].Name) : string.Format(Properties.Resources.AddTo, Global.Bookmarks.ColorCollections[i].Name),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				Background = new SolidColorBrush() { Color = Colors.Transparent },
				FontWeight = FontWeights.Bold,
				Style = (Style)FindResource("DefaultButton"),
				Foreground = Global.GetColorFromResource("Foreground1"),
			};
			int j = i; // Avoid index out of range issues
			button.Click += (o, e) =>
			{
				if (Global.Bookmarks.ColorCollections[j].Colors.Contains(ColorInfo.HEX.Value))
				{
					Global.Bookmarks.ColorCollections[j].Colors.Remove(ColorInfo.HEX.Value);
					button.Content = string.Format(Properties.Resources.AddTo, Global.Bookmarks.ColorCollections[j].Name);
				}
				else
				{
					Global.Bookmarks.ColorCollections[j].Colors.Add(ColorInfo.HEX.Value);
					button.Content = string.Format(Properties.Resources.RemoveFrom, Global.Bookmarks.ColorCollections[j].Name);
				}
			};

			CollectionsPanel.Children.Add(button);
		}
	}

	private void HandleSelectKeyboard()
	{
		if (!Global.Settings.UseKeyboardShortcuts) return;

		selecting = !selecting;
		UpdateSelectionState(selecting);
		Global.SynethiaConfig.ActionsInfo[0].UsageCount++;
	}

	readonly List<string> RecentColors = [];
	private void HandleCopyKeyboard()
	{
		try
		{
			if (!Global.Settings.UseKeyboardShortcuts) return;

			Clipboard.SetDataObject(SelectedColorType switch
			{
				ColorTypes.HEX => $"#{ColorInfo.HEX.Value}",
				ColorTypes.HSL => $"{ColorInfo.HSL.H}, {ColorInfo.HSL.S}, {ColorInfo.HSL.L}",
				ColorTypes.HSV => $"{ColorInfo.HSV.H}, {ColorInfo.HSV.S}, {ColorInfo.HSV.V}",
				ColorTypes.CMYK => $"{ColorInfo.CMYK.C}, {ColorInfo.CMYK.M}, {ColorInfo.CMYK.Y}, {ColorInfo.CMYK.K}",
				ColorTypes.XYZ => $"{ColorInfo.XYZ.X}; {ColorInfo.XYZ.Y}; {ColorInfo.XYZ.Z}",
				ColorTypes.YIQ => $"{ColorInfo.YIQ.Y}; {ColorInfo.YIQ.I}; {ColorInfo.YIQ.Q}",
				ColorTypes.YUV => $"{ColorInfo.YUV.Y}; {ColorInfo.YUV.U}; {ColorInfo.YUV.V}",
				_ => $"{ColorInfo.RGB.R}{Global.Settings.RgbSeparator}{ColorInfo.RGB.G}{Global.Settings.RgbSeparator}{ColorInfo.RGB.B}"
			});

			if (RecentColors.Contains(ColorInfo.HEX.ToString())) return;
			RecentColors.Add(ColorInfo.HEX.ToString());

			Border border = new()
			{
				Height = 25,
				Width = 25,
				CornerRadius = new(15),
				Cursor = Cursors.Hand,
				Margin = new(2),
				Background = new SolidColorBrush { Color = Color.FromRgb(ColorInfo.RGB.R, ColorInfo.RGB.G, ColorInfo.RGB.B) }
			};

			border.MouseLeftButtonUp += (o, e) =>
			{
				var c = ((SolidColorBrush)border.Background).Color;
				ColorInfo = new(new(c.R, c.G, c.B));
				LoadSliders();
			};

			RecentColorsPanel.Children.Add(border);
		}
		catch { }
	}

	private static Color GetRgb(int h, int s, int v, bool isHsl = false)
	{
		if (isHsl)
		{
			var rgb = ColorHelper.ColorConverter.HslToRgb(new(h, (byte)s, (byte)v));
			return Color.FromRgb(rgb.R, rgb.G, rgb.B);
		}
		var c = ColorHelper.ColorConverter.HsvToRgb(new(h, (byte)s, (byte)v));
		return Color.FromRgb(c.R, c.G, c.B);
	}

	private static Color GetRgb(byte c, byte m, byte y, byte k)
	{
		var rgb = ColorHelper.ColorConverter.CmykToRgb(new(c, m, y, k));
		return Color.FromRgb(rgb.R, rgb.G, rgb.B);
	}

    private void UpdateFromSliderChange()
    {
		Color color;
        if (SelectedColorType == ColorTypes.HSV) color = GetRgb((int)RedSlider.Value, (int)GreenSlider.Value, (int)BlueSlider.Value);
        else if (SelectedColorType == ColorTypes.HSL) color = GetRgb((int)RedSlider.Value, (int)GreenSlider.Value, (int)BlueSlider.Value, true);
        else if (SelectedColorType == ColorTypes.CMYK) color = GetRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value, (byte)KSlider.Value);
        else color = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);

		ColorBorder.Background = new SolidColorBrush { Color = color };
		ColorBorder.Effect = new DropShadowEffect() { BlurRadius = 15, ShadowDepth = 0, Color = color };
		
        RedValueTxt.Text = RedSlider.Value.ToString();
        GreenValueTxt.Text = GreenSlider.Value.ToString();
        BlueValueTxt.Text = BlueSlider.Value.ToString();
        KValueTxt.Text = KSlider.Value.ToString();

		LoadDetails();
    }

	private void RedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
        if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;
        UpdateFromSliderChange();
	}

	private void GreenSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
        if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;
        UpdateFromSliderChange();
	}

	private void BlueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
        if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;
        UpdateFromSliderChange();
	}

	private void KSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
        if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;
        UpdateFromSliderChange();
	}

	bool selecting = false;
	internal void SelectBtn_Click(object sender, RoutedEventArgs e)
	{
		selecting = !selecting;
		UpdateSelectionState(selecting);
		Global.SynethiaConfig.ActionsInfo[0].UsageCount++;
	}

	private void ColorBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		LoadDetails();
	}

	internal void LoadDetails()
	{
        if (SelectedColorType == ColorTypes.HSV)
            ColorInfo = new ColorInfo(ColorHelper.ColorConverter.HsvToRgb(new((int)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value)));
        else if (SelectedColorType == ColorTypes.HSL)
            ColorInfo = new ColorInfo(ColorHelper.ColorConverter.HslToRgb(new((int)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value)));
        else if (SelectedColorType == ColorTypes.CMYK)
            ColorInfo = new ColorInfo(ColorHelper.ColorConverter.CmykToRgb(new((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value, (byte)KSlider.Value)))
            { CMYK = new((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value, (byte)KSlider.Value) };
        else
            ColorInfo = new ColorInfo(new((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value));

		DetailsControl.SetColorInfo(ColorInfo);
		LoadBookmarkMenu();

		// 同步更新色轮（左下角独立 Hex 输入框已按需求移除）
        SyncWheelFromColorInfo();

        // 同步刷新常驻多 TextBox 输入框
        if (!isUpdatingFromInputs)
        {
            LoadInputUI();
        }

		// 刷新书签图标
		if (!Global.Bookmarks.ColorBookmarks.Contains($"#{ColorInfo.HEX.Value}"))
		{
			BookmarkBtn.Content = "\uF1F6";
			BookmarkToolTip.Content = Properties.Resources.AddBookmark;
			AddRemoveBookmarkBtn.Content = Properties.Resources.AddBookmark;
			return;
		}
		BookmarkBtn.Content = "\uF1F8";
		BookmarkToolTip.Content = Properties.Resources.RemoveBookmark;
		AddRemoveBookmarkBtn.Content = Properties.Resources.RemoveBookmark;
	}

	private void BookmarkBtn_Click(object sender, RoutedEventArgs e)
	{
		// Sync the "Add/Remove" label with the current bookmark state
		AddRemoveBookmarkBtn.Content = Global.Bookmarks.ColorBookmarks.Contains($"#{ColorInfo.HEX.Value}")
			? Properties.Resources.RemoveBookmark
			: Properties.Resources.AddBookmark;
		CollectionsPopup.IsOpen = true;
	}

	private void UpdateSelectionState(bool selectionOn)
	{
		if (selectionOn)
		{
			timer.Start();
			miniPicker.timer.Start();
			miniPicker.Show();
		}
		else
		{
			timer.Stop();
			miniPicker.timer.Stop();
			miniPicker.Hide();
		}
	}

	private void SyncWheelFromColorInfo()
	{
		isUpdatingFromSliders = true;
		try
		{
			WheelPicker.SelectedColor = Color.FromRgb(ColorInfo.RGB.R, ColorInfo.RGB.G, ColorInfo.RGB.B);
		}
		finally
		{
			isUpdatingFromSliders = false;
		}
	}

    // 扁平 Tab 按钮的切换高亮逻辑
	private void UnCheckAllButtons()
	{
		RgbBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		HexBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		HsvBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		HslBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		CmykBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		XyzBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		YiqBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		YuvBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		DecBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
	}

	internal void CheckButton(Button button) => button.Background = Global.GetColorFromResource("LightAccentColor");

	internal void RgbBtn_Click(object sender, RoutedEventArgs? e)
	{
		var btn = (Button)sender;

		UnCheckAllButtons();
		CheckButton(btn);
		SelectedColorBtn = btn;
        
        SelectedColorType = btn == RgbBtn ? ColorTypes.RGB
                            : btn == HexBtn ? ColorTypes.HEX
                            : btn == HsvBtn ? ColorTypes.HSV
                            : btn == HslBtn ? ColorTypes.HSL
                            : btn == CmykBtn ? ColorTypes.CMYK
                            : btn == DecBtn ? ColorTypes.DEC
                            : btn == XyzBtn ? ColorTypes.XYZ
                            : btn == YiqBtn ? ColorTypes.YIQ
                            : ColorTypes.YUV;

		LoadSliders();
	}

	private void LoadSliders()
	{
		var current = ColorInfo;
		KSlider.Visibility = Visibility.Collapsed;
		KValueTxt.Visibility = Visibility.Collapsed;
		
		isUpdatingFromSliders = true;
		try
		{
			var mediaColor = Color.FromRgb(current.RGB.R, current.RGB.G, current.RGB.B);
			if (WheelPicker.SelectedColor != mediaColor)
			{
				WheelPicker.SelectedColor = mediaColor;
			}

			if (SelectedColorType == ColorTypes.HSV)
			{
				RedSlider.Foreground = Global.GetColorFromResource("AccentColor");
				GreenSlider.Foreground = Global.GetColorFromResource("AccentColor");
				BlueSlider.Foreground = Global.GetColorFromResource("AccentColor");

				RedSlider.Maximum = 360;
				GreenSlider.Maximum = 100;
				BlueSlider.Maximum = 100;

				RedSlider.Value = current.HSV.H;
				GreenSlider.Value = current.HSV.S;
				BlueSlider.Value = current.HSV.V;
			}
			else if (SelectedColorType == ColorTypes.HSL)
			{
				RedSlider.Foreground = Global.GetColorFromResource("AccentColor");
				GreenSlider.Foreground = Global.GetColorFromResource("AccentColor");
				BlueSlider.Foreground = Global.GetColorFromResource("AccentColor");

				RedSlider.Maximum = 360;
				GreenSlider.Maximum = 100;
				BlueSlider.Maximum = 100;

				RedSlider.Value = current.HSL.H;
				GreenSlider.Value = current.HSL.S;
				BlueSlider.Value = current.HSL.L;
			}
			else if (SelectedColorType == ColorTypes.CMYK)
			{
				RedSlider.Foreground = Global.GetColorFromResource("AccentColor");
				GreenSlider.Foreground = Global.GetColorFromResource("AccentColor");
				BlueSlider.Foreground = Global.GetColorFromResource("AccentColor");
				KSlider.Foreground = Global.GetColorFromResource("AccentColor");

				RedSlider.Maximum = 100;
				GreenSlider.Maximum = 100;
				BlueSlider.Maximum = 100;
				KSlider.Maximum = 100;

				RedSlider.Value = current.CMYK.C;
				GreenSlider.Value = current.CMYK.M;
				BlueSlider.Value = current.CMYK.Y;
				KSlider.Value = current.CMYK.K;

				KSlider.Visibility = Visibility.Visible;
				KValueTxt.Visibility = Visibility.Visible;
			}
			else // RGB, HEX, DEC, XYZ, YIQ, YUV 均统一在滑块上使用 RGB 轴调节（滑块常驻不动，不隐藏）
			{
				RedSlider.Foreground = Global.GetColorFromResource("SliderRed");
				GreenSlider.Foreground = Global.GetColorFromResource("SliderGreen");
				BlueSlider.Foreground = Global.GetColorFromResource("SliderBlue");

				RedSlider.Maximum = 255;
				GreenSlider.Maximum = 255;
				BlueSlider.Maximum = 255;

				RedSlider.Value = current.RGB.R;
				GreenSlider.Value = current.RGB.G;
				BlueSlider.Value = current.RGB.B;
			}

            LoadInputUI(); // 同步重置下方的 TextBox 容器
		}
		finally
		{
			isUpdatingFromSliders = false;
		}
	}

	private void AddRemoveBookmarkBtn_Click(object sender, RoutedEventArgs e)
	{
		if (Global.Bookmarks.ColorBookmarks.Contains($"#{ColorInfo.HEX.Value}"))
		{
			int i = Global.Bookmarks.ColorBookmarks.IndexOf($"#{ColorInfo.HEX.Value}");
			Global.Bookmarks.ColorBookmarks.RemoveAt(i);
			Global.Bookmarks.ColorBookmarksNotes.RemoveAt(i);
			BookmarkBtn.Content = "\uF1F6";
			AddRemoveBookmarkBtn.Content = Properties.Resources.AddBookmark;
			BookmarkToolTip.Content = Properties.Resources.AddBookmark;

			return;
		}
		Global.Bookmarks.ColorBookmarks.Add($"#{ColorInfo.HEX.Value}");
		Global.Bookmarks.ColorBookmarksNotes.Add("");
		BookmarkBtn.Content = "\uF1F8";
		AddRemoveBookmarkBtn.Content = Properties.Resources.RemoveBookmark;
		BookmarkToolTip.Content = Properties.Resources.RemoveBookmark;
	}

	private void HideAllInput()
	{
		DisplayText1.Visibility = Visibility.Collapsed;
		DisplayText2.Visibility = Visibility.Collapsed;
		DisplayText3.Visibility = Visibility.Collapsed;
		DisplayText4.Visibility = Visibility.Collapsed;
		DisplayText5.Visibility = Visibility.Collapsed; // HEX/DEC 特殊大输入框

		// 清空
		Txt1.Text = "";
		Txt2.Text = "";
		Txt3.Text = "";
		Txt4.Text = "";
		Txt5.Text = "";

		B1.Visibility = Visibility.Collapsed;
		B2.Visibility = Visibility.Collapsed;
		B3.Visibility = Visibility.Collapsed;
		B4.Visibility = Visibility.Collapsed;
		B5.Visibility = Visibility.Collapsed;
	}

    // 重构：与调色板（PalettePage）完全一致的多文本框输入渲染
	private void LoadInputUI()
	{
		HideAllInput();
        var current = ColorInfo;

		if (SelectedColorType != ColorTypes.HEX && SelectedColorType != ColorTypes.DEC)
		{
			DisplayText1.Visibility = Visibility.Visible;
			DisplayText2.Visibility = Visibility.Visible;
			DisplayText3.Visibility = Visibility.Visible;
			DisplayText4.Visibility = SelectedColorType == ColorTypes.CMYK ? Visibility.Visible : Visibility.Collapsed;

			B1.Visibility = Visibility.Visible;
			B2.Visibility = Visibility.Visible;
			B3.Visibility = Visibility.Visible;
			B4.Visibility = SelectedColorType == ColorTypes.CMYK ? Visibility.Visible : Visibility.Collapsed;
		}

		if (SelectedColorType == ColorTypes.RGB)
		{
			DisplayText1.Text = "R";
			DisplayText2.Text = "G";
			DisplayText3.Text = "B";

			Txt1.Text = current.RGB.R.ToString();
			Txt2.Text = current.RGB.G.ToString();
			Txt3.Text = current.RGB.B.ToString();
		}
		else if (SelectedColorType == ColorTypes.HEX)
		{
			DisplayText5.Visibility = Visibility.Visible;
			DisplayText5.Text = Properties.Resources.HEX;
			B5.Visibility = Visibility.Visible;

			Txt5.Text = current.HEX.Value;
		}
		else if (SelectedColorType == ColorTypes.HSV)
		{
			DisplayText1.Text = "H";
			DisplayText2.Text = "S";
			DisplayText3.Text = "V";

			Txt1.Text = current.HSV.H.ToString();
			Txt2.Text = current.HSV.S.ToString();
			Txt3.Text = current.HSV.V.ToString();
		}
		else if (SelectedColorType == ColorTypes.HSL)
		{
			DisplayText1.Text = "H";
			DisplayText2.Text = "S";
			DisplayText3.Text = "L";

			Txt1.Text = current.HSL.H.ToString();
			Txt2.Text = current.HSL.S.ToString();
			Txt3.Text = current.HSL.L.ToString();
		}
		else if (SelectedColorType == ColorTypes.CMYK)
		{
			DisplayText1.Text = "C";
			DisplayText2.Text = "M";
			DisplayText3.Text = "Y";
			DisplayText4.Text = "K";

			Txt1.Text = current.CMYK.C.ToString();
			Txt2.Text = current.CMYK.M.ToString();
			Txt3.Text = current.CMYK.Y.ToString();
			Txt4.Text = current.CMYK.K.ToString();
		}
		else if (SelectedColorType == ColorTypes.XYZ)
		{
			DisplayText1.Text = "X";
			DisplayText2.Text = "Y";
			DisplayText3.Text = "Z";

			Txt1.Text = current.XYZ.X.ToString();
			Txt2.Text = current.XYZ.Y.ToString();
			Txt3.Text = current.XYZ.Z.ToString();
		}
		else if (SelectedColorType == ColorTypes.YIQ)
		{
			DisplayText1.Text = "Y";
			DisplayText2.Text = "I";
			DisplayText3.Text = "Q";

			Txt1.Text = current.YIQ.Y.ToString();
			Txt2.Text = current.YIQ.I.ToString();
			Txt3.Text = current.YIQ.Q.ToString();
		}
		else if (SelectedColorType == ColorTypes.YUV)
		{
			DisplayText1.Text = "Y";
			DisplayText2.Text = "U";
			DisplayText3.Text = "V";

			Txt1.Text = current.YUV.Y.ToString();
			Txt2.Text = current.YUV.U.ToString();
			Txt3.Text = current.YUV.V.ToString();
		}
		else if (SelectedColorType == ColorTypes.DEC)
		{
			DisplayText5.Visibility = Visibility.Visible;
            // 汉化适配：DEC 的上游翻译是“十六进制”
			DisplayText5.Text = Properties.Resources.DEC;
			B5.Visibility = Visibility.Visible;

			Txt5.Text = current.DEC.Value.ToString();
		}
	}

    // 重构：多文本框的手动输入双向绑定
	private void Txt1_TextChanged(object sender, TextChangedEventArgs e)
	{
        if (isUpdatingFromSliders || isUpdatingFromHexInput || isUpdatingFromInputs) return;

        try {
            RGB rgb;
            isUpdatingFromInputs = true;

            if (SelectedColorType == ColorTypes.HEX)
            {
                string hex = Txt5.Text.Trim();
                if (hex.StartsWith("#")) hex = hex[1..];
                if (hex.Length != 6) { isUpdatingFromInputs = false; return; }
                rgb = ColorHelper.ColorConverter.HexToRgb(new(hex));
            }
            else if (SelectedColorType == ColorTypes.DEC)
            {
                if (!int.TryParse(Txt5.Text.Trim(), out int decVal)) { isUpdatingFromInputs = false; return; }
                rgb = new DEC(decVal).ToRgb();
            }
            else if (SelectedColorType == ColorTypes.CMYK)
            {
                if (string.IsNullOrEmpty(Txt1.Text) || string.IsNullOrEmpty(Txt2.Text) || string.IsNullOrEmpty(Txt3.Text) || string.IsNullOrEmpty(Txt4.Text)) 
                { isUpdatingFromInputs = false; return; }
                rgb = ColorHelper.ColorConverter.CmykToRgb(new(
                    (byte)int.Parse(Txt1.Text),
                    (byte)int.Parse(Txt2.Text),
                    (byte)int.Parse(Txt3.Text),
                    (byte)int.Parse(Txt4.Text)
                ));
            }
            else 
            {
                if (string.IsNullOrEmpty(Txt1.Text) || string.IsNullOrEmpty(Txt2.Text) || string.IsNullOrEmpty(Txt3.Text)) 
                { isUpdatingFromInputs = false; return; }
                
                double v1 = double.Parse(Txt1.Text);
                double v2 = double.Parse(Txt2.Text);
                double v3 = double.Parse(Txt3.Text);

                if (SelectedColorType == ColorTypes.RGB) rgb = new((byte)v1, (byte)v2, (byte)v3);
                else if (SelectedColorType == ColorTypes.HSV) rgb = ColorHelper.ColorConverter.HsvToRgb(new((int)v1, (byte)v2, (byte)v3));
                else if (SelectedColorType == ColorTypes.HSL) rgb = ColorHelper.ColorConverter.HslToRgb(new((int)v1, (byte)v2, (byte)v3));
                else if (SelectedColorType == ColorTypes.XYZ) rgb = ColorHelper.ColorConverter.XyzToRgb(new(v1, v2, v3));
                else if (SelectedColorType == ColorTypes.YIQ) rgb = ColorHelper.ColorConverter.YiqToRgb(new(v1, v2, v3));
                else rgb = ColorHelper.ColorConverter.YuvToRgb(new(v1, v2, v3));
            }

            Color color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
            ColorBorder.Background = new SolidColorBrush { Color = color };
            ColorBorder.Effect = new DropShadowEffect() { BlurRadius = 15, ShadowDepth = 0, Color = color };
            
            SyncSlidersFromRgb(rgb.R, rgb.G, rgb.B);
            
            ColorInfo = new ColorInfo(rgb);
            DetailsControl.SetColorInfo(ColorInfo);
            LoadBookmarkMenu();
            SyncWheelFromColorInfo();

            isUpdatingFromInputs = false;
        } catch {
            isUpdatingFromInputs = false;
        }
	}

	private void Txt1_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		if (e.Command == ApplicationCommands.Paste)
		{
			e.CanExecute = true;
			e.Handled = true;
		}
	}

	private void Txt1_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			if (e.Command == ApplicationCommands.Paste)
			{
				string text = Clipboard.GetText()
					.Replace("(", "")
					.Replace(")", "")
					.Replace(" ", "");
				if (SelectedColorType == ColorTypes.HSV || SelectedColorType == ColorTypes.HSL || SelectedColorType == ColorTypes.CMYK)
				{
					var split = text.Split(",");
					Txt1.Text = split[0];
					Txt2.Text = split[1];
					Txt3.Text = split[2];
					Txt4.Text = split.Length > 3 ? split[3] : "";
				}
				else if (SelectedColorType == ColorTypes.HEX || SelectedColorType == ColorTypes.DEC)
				{
					Txt5.Text = text;
				}
				else
				{
					var split = text.Split(new string[] { ";", Global.Settings.RgbSeparator ?? ";" }, StringSplitOptions.None);
					Txt1.Text = split[0];
					Txt2.Text = split[1];
					Txt3.Text = split[2];
				}

				e.Handled = true;
			}
		}
		catch { }
	}

	public static event EventHandler<PageEventArgs>? GoClick;

	private void PaletteBtn_Click(object sender, RoutedEventArgs e)
	{
		Global.PalettePage.InitFromColor(ColorInfo);
		GoClick?.Invoke(this, new(AppPages.ColorPalette));
	}
}
