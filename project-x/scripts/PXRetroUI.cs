using Godot;
using System;

namespace ProjectXBossArenaPrototype;

public static class PXRetroUI
{
	private static Font _font;
	public static Font Font
	{
		get
		{
			if (_font == null)
			{
				const string path = "res://assets/PixelifySans.ttf";
				FontFile font = null;
				// F6 can be pressed before the editor imports a newly copied font.
				// In an editor build we can read the original TTF directly.
				if (OS.HasFeature("editor") && FileAccess.FileExists(path))
				{
					var rawFont = new FontFile();
					if (rawFont.LoadDynamicFont(path) == Error.Ok) font = rawFont;
					else rawFont.Dispose();
				}
				else if (ResourceLoader.Exists(path, "FontFile"))
				{
					font = GD.Load<FontFile>(path);
				}
				if (font != null)
				{
					font.Oversampling = 2;
					_font = font;
				}
				else
				{
					// Cache the fallback as well: never retry a missing file each frame.
					_font = ThemeDB.FallbackFont ?? new SystemFont();
					GD.Print("Project X: PixelifySans unavailable; using the default font.");
				}
			}
			return _font;
		}
	}
	public static readonly Color Ink = new("f3e600"), Accent = new("c5003c"), Muted = new("880425");
	public static Theme MakeTheme()
	{
	var theme = new Theme { DefaultFont = Font, DefaultFontSize = 28 };
	theme.SetColor("font_color", "Label", Ink);
	theme.SetColor("font_color", "Button", Ink);
	theme.SetColor("font_hover_color", "Button", new Color("55ead4"));
	theme.SetStylebox("normal", "Button", Box(new("1a1a1a"), new("880425")));
	theme.SetStylebox("hover", "Button", Box(Accent, Ink));
	theme.SetStylebox("pressed", "Button", Box(new("55ead4"), Accent));
	theme.SetStylebox("focus", "Button", Box(new(0, 0, 0, 0), Accent));
	return theme;
	}

	public static StyleBoxFlat Box(Color fill, Color edge, int width = 3) => new()
	{
		BgColor = fill, BorderColor = edge, BorderWidthLeft = width, BorderWidthRight = width,
		BorderWidthTop = width, BorderWidthBottom = width, ContentMarginLeft = 18,
		ContentMarginRight = 18, ContentMarginTop = 10, ContentMarginBottom = 10
	};
	public static Label Label(Control parent, string text, Rect2 rect, int size = 28, Color? color = null)
	{
		var label = new Label { Text = text, Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color ?? Ink);
		parent.AddChild(label); return label;
	}
	public static Button Button(Control parent, string text, Rect2 rect, Action pressed)
	{
		var button = new Button { Text = text, Position = rect.Position, Size = rect.Size, FocusMode = Control.FocusModeEnum.None };
		button.Pressed += pressed; parent.AddChild(button); return button;
	}
	public static Panel Panel(Control parent, Rect2 rect, Color? fill = null)
	{
		var panel = new Panel { Position = rect.Position, Size = rect.Size };
		panel.AddThemeStyleboxOverride("panel", Box(fill ?? new("142434"), new("5d908f")));
		parent.AddChild(panel); return panel;
	}
	public static ProgressBar Bar(Control parent, Rect2 rect, Color color)
	{
		var bar = new ProgressBar { Position = rect.Position, Size = rect.Size, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.AddThemeStyleboxOverride("background", Box(new("1c3040"), new("31515f"), 1));
		bar.AddThemeStyleboxOverride("fill", Box(color, color, 0)); parent.AddChild(bar); return bar;
	}
}
