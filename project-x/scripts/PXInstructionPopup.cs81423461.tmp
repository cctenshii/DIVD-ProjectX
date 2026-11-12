using Godot;
using System;

namespace ProjectXBossArenaPrototype;

// Reusable tutorial component. It has no dependency on the arena, player or boss.
public partial class PXInstructionPopup : Control
{
    public bool Active => Visible;
    private string[] _titles = Array.Empty<string>(), _pages = Array.Empty<string>();
    private int _index;
    private Label _title, _body, _count;
    private Button _next;
    private Action _finished;
    public override void _Ready()
    {
        Size = new(1920, 1080); Theme = PXRetroUI.MakeTheme(); MouseFilter = MouseFilterEnum.Stop;
        AddChild(new ColorRect { Size = Size, Color = new Color(0.02f, 0.04f, 0.07f, 0.72f), MouseFilter = MouseFilterEnum.Ignore });
        var panel = PXRetroUI.Panel(this, new(420, 235, 1080, 610));
        PXRetroUI.Label(panel, "OMNI-TOOL  /  TRANSMISSION", new(52, 34, 976, 40), 24, PXRetroUI.Accent);
        _title = PXRetroUI.Label(panel, "", new(52, 100, 976, 64), 48);
        _body = PXRetroUI.Label(panel, "", new(52, 188, 976, 268), 32);
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _count = PXRetroUI.Label(panel, "", new(52, 500, 450, 48), 24, PXRetroUI.Muted);
        _next = PXRetroUI.Button(panel, "CONTINUE", new(612, 490, 416, 68), Advance);
        Hide();
    }
    public void ShowPages(string[] titles, string[] pages, Action finished)
    {
        if (titles.Length == 0 || titles.Length != pages.Length) throw new ArgumentException("Titles and pages must match.");
        _titles = titles; _pages = pages; _finished = finished; _index = 0; Refresh(); Show();
    }
    private void Refresh()
    {
        _title.Text = _titles[_index]; _body.Text = _pages[_index];
        _count.Text = $"{_index + 1} / {_pages.Length}   |   ENTER = CONTINUE";
        _next.Text = _index == _pages.Length - 1 ? "START" : "CONTINUE";
    }
    public void Advance()
    {
        if (!Visible) return;
        if (++_index < _pages.Length) { Refresh(); return; }
        Hide(); var callback = _finished; _finished = null; callback?.Invoke();
    }
    public void CloseSilently() { _finished = null; Hide(); }
    public override void _Input(InputEvent inputEvent)
    {
        if (Visible && inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Enter)
        { Advance(); GetViewport().SetInputAsHandled(); }
    }
}
