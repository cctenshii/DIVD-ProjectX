using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXHackPanel : Control
{
    public enum HackKind { Timing, Targets, Memory }
    [Signal] public delegate void FinishedEventHandler(bool success);
    [Export] public float TimeLimit { get; set; } = 6f;

    public bool Active { get; private set; }
    public HackKind CurrentKind { get; private set; }

    private readonly RandomNumberGenerator _random = new();
    private readonly int[] _sequence = new int[3];
    private Label _title;
    private Label _instruction;
    private Label _status;
    private ProgressBar _timeBar;
    private Button _submit;
    private HBoxContainer _numberButtons;
    private float _elapsed;
    private float _preview;
    private float _needle;
    private float _targetCenter;
    private Vector2 _clickTarget;
    private int _completed;
    private int _lastKind = -1;

    public override void _Ready()
    {
        _random.Randomize();
        _title = GetNode<Label>("Title");
        _instruction = GetNode<Label>("Instruction");
        _status = GetNode<Label>("Status");
        _timeBar = GetNode<ProgressBar>("TimeBar");
        _submit = GetNode<Button>("Submit");
        _numberButtons = GetNode<HBoxContainer>("NumberButtons");
        _submit.Pressed += SubmitTiming;
        for (int i = 0; i < 4; i++)
        {
            int number = i + 1;
            _numberButtons.GetChild<Button>(i).Pressed += () => SubmitNumber(number);
        }
        Hide();
    }

    public void StartRandomHack()
    {
        if (Active) return;
        int kind = _random.RandiRange(0, 2);
        if (kind == _lastKind) kind = (kind + _random.RandiRange(1, 2)) % 3;
        _lastKind = kind;
        CurrentKind = (HackKind)kind;
        _elapsed = 0f;
        _preview = CurrentKind == HackKind.Memory ? 1.4f : 0f;
        _completed = 0;
        _needle = 0f;
        _targetCenter = _random.RandfRange(0.42f, 0.72f);
        for (int i = 0; i < 3; i++) _sequence[i] = _random.RandiRange(1, 4);
        NewClickTarget();
        Active = true;
        _submit.Visible = CurrentKind == HackKind.Timing;
        _numberButtons.Visible = CurrentKind == HackKind.Memory;
        SetNumberButtonsEnabled(_preview <= 0f);
        _title.Text = CurrentKind switch
        {
            HackKind.Timing => "01 / TIMING HACK",
            HackKind.Targets => "02 / KLIK HACK",
            _ => "03 / GEHEUGEN HACK"
        };
        _instruction.Text = CurrentKind switch
        {
            HackKind.Timing => "Stop de pijl in het lichte vak.\nGebruik SPATIE of de knop.",
            HackKind.Targets => "Raak 3 cirkels met de muis.\nEen misser in het veld = fout.",
            _ => "Onthoud 3 cijfers. Herhaal ze daarna\nmet 1-4 of de knoppen."
        };
        _timeBar.Value = 100;
        UpdateStatus();
        Show();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        if (_preview > 0f)
        {
            _preview = Mathf.Max(0f, _preview - (float)delta);
            if (_preview <= 0f) SetNumberButtonsEnabled(true);
        }
        else
        {
            _elapsed += (float)delta;
            _needle = Mathf.PingPong(_elapsed * 0.68f, 1f);
            _timeBar.Value = Mathf.Max(0f, 1f - _elapsed / TimeLimit) * 100;
            if (_elapsed >= TimeLimit) { Resolve(false); return; }
        }
        UpdateStatus();
        QueueRedraw();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!Active || inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code == Key.Escape)
        {
            Resolve(false);
            GetViewport().SetInputAsHandled();
        }
        else if (CurrentKind == HackKind.Timing && code == Key.Space)
        {
            SubmitTiming();
            GetViewport().SetInputAsHandled();
        }
        else if (CurrentKind == HackKind.Memory && code >= Key.Key1 && code <= Key.Key4)
        {
            SubmitNumber((int)code - (int)Key.Key1 + 1);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (!Active || CurrentKind != HackKind.Targets ||
            inputEvent is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
            return;
        if (!new Rect2(24, 116, 312, 122).HasPoint(mouse.Position)) return;
        AcceptEvent();
        if (mouse.Position.DistanceTo(_clickTarget) > 23f) { Resolve(false); return; }
        _completed++;
        if (_completed == 3) Resolve(true);
        else { NewClickTarget(); UpdateStatus(); QueueRedraw(); }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 360, 352), new Color("e9eeef"));
        DrawRect(new Rect2(0, 0, 360, 352), new Color("41545b"), false, 2f);
        if (!Active) return;
        var ink = new Color("263c45");
        var field = new Rect2(24, 116, 312, 122);
        DrawRect(field, new Color("d8e1e3"));
        DrawRect(field, new Color("9badb3"), false, 1f);

        if (CurrentKind == HackKind.Timing)
        {
            DrawRect(new Rect2(40, 158, 280, 34), ink);
            DrawRect(new Rect2(40 + (_targetCenter - 0.12f) * 280, 158, 0.24f * 280, 34), new Color("a3e3c2"));
            float x = 40 + _needle * 280;
            DrawColoredPolygon(new[] { new Vector2(x, 157), new Vector2(x - 8, 144), new Vector2(x + 8, 144) }, ink);
            DrawLine(new Vector2(x, 158), new Vector2(x, 192), Colors.White, 3f);
        }
        else if (CurrentKind == HackKind.Targets)
        {
            DrawCircle(_clickTarget, 23, new Color("2a7f96"));
            DrawArc(_clickTarget, 30, 0, Mathf.Tau, 48, ink, 1f, true);
            DrawString(ThemeDB.FallbackFont, _clickTarget + new Vector2(-6, 7), (_completed + 1).ToString(), fontSize: 21, modulate: Colors.White);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 55 + i * 88;
                DrawRect(new Rect2(x, 140, 74, 70), i < _completed ? new Color("a3e3c2") : new Color("f4f7f7"));
                string number = _preview > 0f || i < _completed ? _sequence[i].ToString() : "?";
                DrawString(ThemeDB.FallbackFont, new Vector2(x + 27, 185), number, fontSize: 32, modulate: ink);
            }
        }
    }

    public void CancelSilently()
    {
        Active = false;
        Hide();
    }

    private void SubmitTiming()
    {
        if (Active && CurrentKind == HackKind.Timing)
            Resolve(Mathf.Abs(_needle - _targetCenter) <= 0.12f);
    }

    private void SubmitNumber(int number)
    {
        if (!Active || CurrentKind != HackKind.Memory || _preview > 0f) return;
        if (number != _sequence[_completed]) { Resolve(false); return; }
        _completed++;
        if (_completed == 3) Resolve(true);
        else { UpdateStatus(); QueueRedraw(); }
    }

    private void NewClickTarget()
    {
        Vector2 previous = _clickTarget;
        for (int i = 0; i < 12; i++)
        {
            _clickTarget = new Vector2(_random.RandfRange(57, 303), _random.RandfRange(149, 205));
            if (_clickTarget.DistanceTo(previous) >= 65f) break;
        }
    }

    private void SetNumberButtonsEnabled(bool enabled)
    {
        foreach (Node child in _numberButtons.GetChildren())
            if (child is Button button) button.Disabled = !enabled;
    }

    private void UpdateStatus()
    {
        _status.Text = _preview > 0f ? "KIJK GOED..." :
            $"{Mathf.Max(0f, TimeLimit - _elapsed):0.0}s" +
            (CurrentKind == HackKind.Timing ? "  /  STOP IN HET VAK" : $"  /  {_completed} VAN 3");
    }

    private void Resolve(bool success)
    {
        if (!Active) return;
        Active = false;
        Hide();
        EmitSignal(SignalName.Finished, success);
    }
}
