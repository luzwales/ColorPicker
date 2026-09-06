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
using ColorPicker.UserControls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorPicker.Pages;

public partial class BookmarksPage : Page
{
	internal Button CheckedButton = null!;

    // 依赖属性：颜色卡片列数 (最多 5 列)
    public static readonly DependencyProperty GridColumnsProperty =
        DependencyProperty.Register(
            nameof(GridColumns),
            typeof(int),
            typeof(BookmarksPage),
            new PropertyMetadata(4));

    public int GridColumns
    {
        get => (int)GetValue(GridColumnsProperty);
        set => SetValue(GridColumnsProperty, value);
    }

    // 依赖属性：调色板、渐变、文本卡片列数 (只能 2 列)
    public static readonly DependencyProperty PaletteGridColumnsProperty =
        DependencyProperty.Register(
            nameof(PaletteGridColumns),
            typeof(int),
            typeof(BookmarksPage),
            new PropertyMetadata(2));

    public int PaletteGridColumns
    {
        get => (int)GetValue(PaletteGridColumnsProperty);
        set => SetValue(PaletteGridColumnsProperty, value);
    }

	public BookmarksPage()
	{
		InitializeComponent();
		CheckButton(ColorsBtn);
		Loaded += (o, e) => InitUI();
	}

	// ============ 书签控件缓存 + 分批异步构建（彻底解决进入书签页卡死） ============
	// 双重问题：
	//  (a) InitUI 原先每次进入都把数百个重型 UserControl 全部同步 new 一遍
	//      (UniformGrid 不参与 UI 虚拟化) → 每次进入卡数十秒；
	//  (b) 即便加缓存，首次进入仍在 UI 线程一次性构建 500+ 卡片 → 点击导航即"卡死"。
	// 解决：
	//  1) 页面为全局单例，用实例级缓存：内容签名未变则直接跳过重建（反复进入秒开）；
	//  2) 首次/变更后构建改为【分批异步】——每批建完就 Task.Delay 让出 UI 线程，
	//     卡片逐批出现，界面全程不冻结；构建期间数据变化会最多再重跑一次对齐。
	private List<ColorItem>? _colorCache;
	private string? _colorSig;
	private string? _colorBuildingSig;
	private bool _colorBuilding;
	private bool _colorReady;
	private bool _colorRestartNeeded;
	private List<PaletteItem>? _paletteCache;
	private string? _paletteSig;
	private string? _paletteBuildingSig;
	private bool _paletteBuilding;
	private bool _paletteReady;
	private bool _paletteRestartNeeded;
	private List<GradientItem>? _gradientCache;
	private string? _gradientSig;
	private List<TextItem>? _textCache;
	private string? _textSig;

	private static string MakeSig<T>(System.Collections.Generic.IReadOnlyList<T> list)
	{
		unchecked
		{
			int h = 17;
			for (int i = 0; i < list.Count; i++)
			{
				h = h * 31 + (list[i]?.GetHashCode() ?? 0);
			}
			return $"{list.Count}:{h}";
		}
	}

	private void EnsureColors()
	{
		if (_colorBuilding)
		{
			// 构建期间数据若确实变化，才在完成后用最新数据重建一次
			if (_colorBuildingSig != MakeSig(Global.Bookmarks.ColorBookmarks))
			{
				_colorRestartNeeded = true;
			}
			return;
		}
		if (_colorCache != null && _colorSig == MakeSig(Global.Bookmarks.ColorBookmarks))
		{
			_colorReady = true; // 内容未变：直接保留现有控件，秒开
			return;
		}
		_ = BuildColorsAsync();
	}

	private async System.Threading.Tasks.Task BuildColorsAsync()
	{
		if (_colorBuilding) return;
		_colorBuilding = true;
		_colorBuildingSig = MakeSig(Global.Bookmarks.ColorBookmarks);
		try
		{
			string sig = _colorBuildingSig;
			for (int attempt = 0; attempt < 2; attempt++)
			{
				ColorsBookmarks.Items.Clear();
				_colorCache = new List<ColorItem>(Global.Bookmarks.ColorBookmarks.Count);
				var snapshot = new string[Global.Bookmarks.ColorBookmarks.Count];
				for (int x = 0; x < snapshot.Length; x++) snapshot[x] = Global.Bookmarks.ColorBookmarks[x];

				const int chunk = 30;
				for (int i = 0; i < snapshot.Length; i++)
				{
					var item = new ColorItem(snapshot[i]);
					_colorCache.Add(item);
					ColorsBookmarks.Items.Add(item);
					if ((i + 1) % chunk == 0)
					{
						await System.Threading.Tasks.Task.Delay(1); // 让出 UI 线程，逐批填充不冻结
					}
				}

				// 构建期间书签又变了则用最新数据最多再重跑一次
				string now = MakeSig(Global.Bookmarks.ColorBookmarks);
				_colorSig = now;
				if (now == sig) break;
				sig = now;
				_colorBuildingSig = now;
			}
		}
		finally
		{
			_colorBuilding = false;
			_colorReady = true;
			RefreshPlaceholder();
			if (_colorRestartNeeded)
			{
				_colorRestartNeeded = false;
				EnsureColors();
			}
		}
	}

	private void EnsurePalettes()
	{
		if (_paletteBuilding)
		{
			if (_paletteBuildingSig != MakeSig(Global.Bookmarks.PaletteBookmarks))
			{
				_paletteRestartNeeded = true;
			}
			return;
		}
		if (_paletteCache != null && _paletteSig == MakeSig(Global.Bookmarks.PaletteBookmarks))
		{
			_paletteReady = true;
			return;
		}
		_ = BuildPalettesAsync();
	}

	private async System.Threading.Tasks.Task BuildPalettesAsync()
	{
		if (_paletteBuilding) return;
		_paletteBuilding = true;
		_paletteBuildingSig = MakeSig(Global.Bookmarks.PaletteBookmarks);
		try
		{
			PalettesBookmarks.Items.Clear();
			_paletteCache = new List<PaletteItem>(Global.Bookmarks.PaletteBookmarks.Count);
			var snapshot = new string[Global.Bookmarks.PaletteBookmarks.Count];
			for (int x = 0; x < snapshot.Length; x++) snapshot[x] = Global.Bookmarks.PaletteBookmarks[x];

			const int chunk = 8; // 每张调色板卡很重（含多条色板），批次更小
			for (int i = 0; i < snapshot.Length; i++)
			{
				var item = new PaletteItem(snapshot[i]);
				_paletteCache.Add(item);
				PalettesBookmarks.Items.Add(item);
				if ((i + 1) % chunk == 0)
				{
					await System.Threading.Tasks.Task.Delay(1);
				}
			}

			_paletteSig = MakeSig(Global.Bookmarks.PaletteBookmarks);
		}
		finally
		{
			_paletteBuilding = false;
			_paletteReady = true;
			RefreshPlaceholder();
			if (_paletteRestartNeeded)
			{
				_paletteRestartNeeded = false;
				EnsurePalettes();
			}
		}
	}

	private void EnsureGradients()
	{
		string sig = MakeSig(Global.Bookmarks.GradientBookmarks);
		if (_gradientCache != null && _gradientSig == sig)
		{
			return;
		}
		GradientsBookmarks.Items.Clear();
		_gradientCache = new List<GradientItem>(Global.Bookmarks.GradientBookmarks.Count);
		for (int i = 0; i < Global.Bookmarks.GradientBookmarks.Count; i++)
		{
			_gradientCache.Add(new GradientItem(Global.Bookmarks.GradientBookmarks[i]));
		}
		foreach (var item in _gradientCache)
		{
			GradientsBookmarks.Items.Add(item);
		}
		_gradientSig = sig;
	}

	private void EnsureTexts()
	{
		string sig = MakeSig(Global.Bookmarks.TextBookmarks);
		if (_textCache != null && _textSig == sig)
		{
			return;
		}
		TextBookmarks.Items.Clear();
		_textCache = new List<TextItem>(Global.Bookmarks.TextBookmarks.Count);
		for (int i = 0; i < Global.Bookmarks.TextBookmarks.Count; i++)
		{
			_textCache.Add(new TextItem(Global.Bookmarks.TextBookmarks[i]));
		}
		foreach (var item in _textCache)
		{
			TextBookmarks.Items.Add(item);
		}
		_textSig = sig;
	}

	internal void InitUI()
	{
		// 集合类书签数量少，始终重建
		Collections.Children.Clear();
		for (int i = 0; i < Global.Bookmarks.ColorCollections.Count; i++)
		{
			Collections.Children.Add(new CollectionItem(Global.Bookmarks.ColorCollections[i], i));
		}

		// 四类书签：内容未变时直接复用上次控件。
		// 颜色与调色板卡片很重（各 500+），默认只异步构建当前激活页签对应的列表，
		// 另一份在用户首次切换到该页签时才构建（EnsurePalettes 由 PaletteBtn_Click 触发）。
		EnsureColors();
		if (CheckedButton == PaletteBtn) EnsurePalettes();
		EnsureGradients();
		EnsureTexts();

		// 依据当前激活的页签展示对应列表，空列表则显示占位符
		RefreshPlaceholder();

		Global.SelectorPage.LoadBookmarkMenu();
		Global.ConverterPage.LoadBookmarkMenu();

        UpdateGridColumns();
	}

	// 当前激活页签是否为空（异步构建未完成时不视为"空"，避免误显占位符）
	private bool ActiveEmpty() => CheckedButton switch
	{
		Button b when b == ColorsBtn => ColorsBookmarks.Items.Count == 0 && _colorReady,
		Button b when b == PaletteBtn => PalettesBookmarks.Items.Count == 0 && _paletteReady,
		Button b when b == GradientsBtn => GradientsBookmarks.Items.Count == 0,
		Button b when b == TextBtn => TextBookmarks.Items.Count == 0,
		_ => Collections.Children.Count == 0
	};

	// 按当前页签刷新各面板与占位符的可见性（InitUI 与异步构建完成后都会调用）
	private void RefreshPlaceholder()
	{
		if (!IsLoaded || CheckedButton == null) return;
		ColorsBookmarks.Visibility = CheckedButton == ColorsBtn ? Visibility.Visible : Visibility.Collapsed;
		PalettesBookmarks.Visibility = CheckedButton == PaletteBtn ? Visibility.Visible : Visibility.Collapsed;
		GradientsBookmarks.Visibility = CheckedButton == GradientsBtn ? Visibility.Visible : Visibility.Collapsed;
		TextBookmarks.Visibility = CheckedButton == TextBtn ? Visibility.Visible : Visibility.Collapsed;
		CollectionsGrid.Visibility = CheckedButton == CollectionBtn ? Visibility.Visible : Visibility.Collapsed;
		Placeholder.Visibility = ActiveEmpty() ? Visibility.Visible : Visibility.Collapsed;
	}

	internal void ColorsBtn_Click(object sender, RoutedEventArgs e)
	{
		UnCheckAllButtons();
		CheckButton(ColorsBtn);
		if (ColorsBookmarks.Items.Count > 0)
		{
			ColorsBookmarks.Visibility = Visibility.Visible;
			return;
		}
		Placeholder.Visibility = Visibility.Visible;
	}

	internal void PaletteBtn_Click(object sender, RoutedEventArgs e)
	{
		UnCheckAllButtons();
		CheckButton(PaletteBtn);
		EnsurePalettes(); // 首次切换到调色板页签时才后台分批构建（不阻塞 UI）
		if (PalettesBookmarks.Items.Count > 0)
		{
			PalettesBookmarks.Visibility = Visibility.Visible;
			return;
		}
		Placeholder.Visibility = Visibility.Visible;
	}

	private void EmptyHistoryBtn_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show(Properties.Resources.EmptyHistoryMsg, Properties.Resources.EmptyBookmarks, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
			return;

		if (ColorsBookmarks.Visibility == Visibility.Visible)
		{
			Global.Bookmarks.ColorBookmarks.Clear();
		}
		else if (PalettesBookmarks.Visibility == Visibility.Visible)
		{
			Global.Bookmarks.PaletteBookmarks.Clear();
		}
		else if (TextBookmarks.Visibility == Visibility.Visible)
		{
			Global.Bookmarks.TextBookmarks.Clear();
		}
		else
		{
			Global.Bookmarks.GradientBookmarks.Clear();
		}

		InitUI();
		Global.SelectorPage.LoadDetails();
		Global.GradientPage.LoadGradientUI();
		Global.PalettePage.InitPaletteUI();
		Global.TextPage.InitUI();
	}

	internal void GradientsBtn_Click(object sender, RoutedEventArgs e)
	{
		UnCheckAllButtons();
		CheckButton(GradientsBtn);
		if (GradientsBookmarks.Items.Count > 0)
		{
			GradientsBookmarks.Visibility = Visibility.Visible;
			return;
		}
		Placeholder.Visibility = Visibility.Visible;
	}

	private void UnCheckAllButtons()
	{
		ColorsBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		PaletteBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		GradientsBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		TextBtn.Background = new SolidColorBrush { Color = Colors.Transparent };
		CollectionBtn.Background = new SolidColorBrush { Color = Colors.Transparent };

		ColorsBookmarks.Visibility = Visibility.Collapsed;
		PalettesBookmarks.Visibility = Visibility.Collapsed;
		GradientsBookmarks.Visibility = Visibility.Collapsed;
		TextBookmarks.Visibility = Visibility.Collapsed;
		CollectionsGrid.Visibility = Visibility.Collapsed;
		Placeholder.Visibility = Visibility.Collapsed;
	}

	internal void CheckButton(Button button) { button.Background = Global.GetColorFromResource("LightAccentColor"); CheckedButton = button; }

	internal void TextBtn_Click(object sender, RoutedEventArgs e)
	{
		UnCheckAllButtons();
		CheckButton(TextBtn);
		if (TextBookmarks.Items.Count > 0)
		{
			TextBookmarks.Visibility = Visibility.Visible;
			return;
		}
		Placeholder.Visibility = Visibility.Visible;
	}

	internal void CollectionBtn_Click(object sender, RoutedEventArgs e)
	{
		UnCheckAllButtons();
		CheckButton(CollectionBtn);
		if (Collections.Children.Count == 0)
		{
			Placeholder.Visibility = Visibility.Visible;
		}
		CollectionsGrid.Visibility = Visibility.Visible;
	}

	private void OpenCollectionPopupBtn_Click(object sender, RoutedEventArgs e)
	{
		CollectionCreatorPopup.IsOpen = true;
	}

	private void AddCollectionBtn_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(CollectionNameTxt.Text)) return;
		Global.Bookmarks.ColorCollections.Add(new(CollectionNameTxt.Text));
		CollectionCreatorPopup.IsOpen = false;
		InitUI();
		Placeholder.Visibility = Visibility.Collapsed;
	}

	private void ImportBtn_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new()
		{
			Filter = "XML|*.xml",
			Title = Properties.Resources.Import
		};

		if (openFileDialog.ShowDialog() ?? true)
		{
			Global.Bookmarks = XmlSerializerManager.LoadFromXml<Bookmarks>(openFileDialog.FileName) ?? new();
			XmlSerializerManager.SaveToXml(Global.Bookmarks, Global.BookmarksPath);
			MessageBox.Show(Properties.Resources.ImportBookmarksSucess, Properties.Resources.ColorPickerMax, MessageBoxButton.OK, MessageBoxImage.Information);

			InitUI();
		}
	}

	private void ExportBtn_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog saveFileDialog = new()
		{
			FileName = "Bookmarks.xml",
			Filter = "XML|*.xml",
			Title = Properties.Resources.Export
		};

		if (saveFileDialog.ShowDialog() ?? true)
		{
			XmlSerializerManager.SaveToXml(Global.Bookmarks, saveFileDialog.FileName);
			MessageBox.Show(Properties.Resources.ExportBookmarksSuccess, Properties.Resources.ColorPickerMax, MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

    // 实现标题栏返回按钮
    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService.CanGoBack)
        {
            NavigationService.GoBack();
        }
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGridColumns();
    }

    private void UpdateGridColumns()
    {
        if (HistoryGrid == null) return;
        double width = HistoryGrid.ActualWidth - 20;
        if (width <= 0) return;

        // 颜色卡片：最多 5 列；当单块可分配宽度 < 300px 时立即减少列数（对小屏收缩响应更快）
        int colorCols = 5;
        while (colorCols > 1 && width / colorCols < 300)
        {
            colorCols--;
        }
        GridColumns = colorCols;

        // 调色板/渐变/文本卡片：内容较宽，最多 2 列；每块宽 < 380px 时降到 1 列（避免挤压换行）
        int paletteCols = 2;
        while (paletteCols > 1 && width / paletteCols < 380)
        {
            paletteCols--;
        }
        PaletteGridColumns = paletteCols;
    }
}
