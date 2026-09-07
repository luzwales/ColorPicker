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
using ColorHelper;
using ColorPicker.Classes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ColorPicker.UserControls;

/// <summary>
/// Reusable content shown inside a lightweight <see cref="System.Windows.Controls.Primitives.Popup"/>
/// (instead of the heavy modal-less <c>ColorDetailsWindow</c>): lists every color format with a copy
/// button and exposes the very same bookmark button used on the other pages.
/// </summary>
public partial class ColorDetailsPopup : UserControl
{
	private ColorInfo _colorInfo = null!;
	private string _hex = "";

	public ColorDetailsPopup()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Loads a color into the popup. Call this right before setting <c>Popup.IsOpen = true</c>.
	/// </summary>
	public void LoadColor(Color color)
	{
		_colorInfo = new ColorInfo(new RGB(color.R, color.G, color.B));
		_hex = $"#{_colorInfo.HEX.Value}";

		ColorBorder.Background = new SolidColorBrush { Color = color };
		ColorBorder.Effect = new DropShadowEffect() { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.4, Color = color };
		TitleTxt.Text = (Global.Settings.UseUpperCasesHex ?? false) ? _hex.ToUpper() : _hex.ToLower();

		RowsPanel.Children.Clear();

		string sep = Global.Settings.RgbSeparator ?? ";";
		AddRow(Properties.Resources.RGB, $"{_colorInfo.RGB.R}{sep}{_colorInfo.RGB.G}{sep}{_colorInfo.RGB.B}");
		AddRow(Properties.Resources.HEX, _hex);
		AddRow(Properties.Resources.HSV, $"{_colorInfo.HSV.H}, {_colorInfo.HSV.S}, {_colorInfo.HSV.V}");
		AddRow(Properties.Resources.HSL, $"{_colorInfo.HSL.H}, {_colorInfo.HSL.S}, {_colorInfo.HSL.L}");
		AddRow(Properties.Resources.CMYK, $"{_colorInfo.CMYK.C}, {_colorInfo.CMYK.M}, {_colorInfo.CMYK.Y}, {_colorInfo.CMYK.K}");
		AddRow(Properties.Resources.DEC, $"{_colorInfo.DEC.Value}");
		AddRow(Properties.Resources.XYZ, $"{_colorInfo.XYZ.X}; {_colorInfo.XYZ.Y}; {_colorInfo.XYZ.Z}");
		AddRow(Properties.Resources.YIQ, $"{_colorInfo.YIQ.Y}; {_colorInfo.YIQ.I}; {_colorInfo.YIQ.Q}");
		AddRow(Properties.Resources.YUV, $"{_colorInfo.YUV.Y}; {_colorInfo.YUV.U}; {_colorInfo.YUV.V}");

		RefreshBookmarkState();
	}

	private void AddRow(string title, string value)
	{
		Border border = new() { Padding = new Thickness(5), CornerRadius = new CornerRadius(8) };

		Grid grid = new();
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		TextBlock titleTxt = new()
		{
			Text = title,
			FontWeight = FontWeights.ExtraBold,
			Foreground = Global.GetColorFromResource("Foreground1")
		};
		grid.Children.Add(titleTxt);

		TextBlock valueTxt = new()
		{
			Text = value,
			FontSize = 15,
			FontWeight = FontWeights.ExtraBold,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = Global.GetColorFromResource("Foreground1")
		};
		Grid.SetRow(valueTxt, 1);
		grid.Children.Add(valueTxt);

		Button copyBtn = new()
		{
			Content = "\uF32C",
			Padding = new Thickness(5),
			Margin = new Thickness(6, 3, 0, 3),
			VerticalAlignment = VerticalAlignment.Center,
			Background = Global.GetColorFromResource("Background3"),
			Foreground = Global.GetColorFromResource("Foreground1"),
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "Fonts/#FluentSystemIcons-Regular"),
			Style = (Style)FindResource("DefaultButton"),
			ToolTip = new ToolTip()
			{
				Background = Global.GetColorFromResource("Background1"),
				Foreground = Global.GetColorFromResource("Foreground1"),
				Content = "Copy"
			}
		};
		Grid.SetRow(copyBtn, 1);
		Grid.SetColumn(copyBtn, 1);
		copyBtn.Click += (o, e) => Clipboard.SetDataObject(value);
		grid.Children.Add(copyBtn);

		border.Child = grid;
		RowsPanel.Children.Add(border);
	}

	private void RefreshBookmarkState()
	{
		bool isBookmarked = Global.Bookmarks.ColorBookmarks.Contains(_hex)
			|| Global.Bookmarks.ColorBookmarks.Contains(_hex.ToLower())
			|| Global.Bookmarks.ColorBookmarks.Contains(_hex.ToUpper())
			|| Global.Bookmarks.ColorBookmarks.Contains(_colorInfo.HEX.Value);

		BookmarkBtn.Content = isBookmarked ? "\uF1F8" : "\uF1F6";
		BookmarkToolTip.Content = isBookmarked ? Properties.Resources.RemoveBookmark : Properties.Resources.AddBookmark;
	}

	private void BookmarkBtn_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			int index = Global.Bookmarks.ColorBookmarks.IndexOf(_hex);
			if (index == -1) index = Global.Bookmarks.ColorBookmarks.IndexOf(_hex.ToLower());
			if (index == -1) index = Global.Bookmarks.ColorBookmarks.IndexOf(_hex.ToUpper());
			if (index == -1) index = Global.Bookmarks.ColorBookmarks.IndexOf(_colorInfo.HEX.Value);

			if (index != -1)
			{
				Global.Bookmarks.ColorBookmarks.RemoveAt(index);
				Global.Bookmarks.ColorBookmarksNotes.RemoveAt(index);
			}
			else
			{
				Global.Bookmarks.ColorBookmarks.Add(_hex);
				Global.Bookmarks.ColorBookmarksNotes.Add("");
			}

			RefreshBookmarkState();
			Global.SelectorPage.LoadDetails();
			Global.ConverterPage.LoadDetails();
		}
		catch { }
	}
}
