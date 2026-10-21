using Godot;
using System.Collections.Generic;

namespace ProjectXBossArenaPrototype;

public partial class PXHackPanel : Control
{
	public enum HackKind { Timing, Targets, Memory, Wires, Order, Circuit, Fingerprint, Pressure }
	[Signal] public delegate void FinishedEventHandler(bool success);
	public bool Active { get; private set; }
	public HackKind CurrentKind { get; private set; }
	public float TimeLimit { get; private set; }
	private readonly RandomNumberGenerator _random = new();
	private readonly List<int> _bag = new();
	private readonly int[] _sequence = new int[3], _wires = new int[3], _order = new int[6];
	private readonly bool[] _connected = new bool[3];
	private readonly int[] _circuit = new int[4], _fingerTarget = new int[3], _fingerCurrent = new int[3];
	private static readonly int[] CircuitSolution = { 1, 2, 0, 3 };
	private static readonly Color Ink = new("263c45"), Green = new("a3e3c2"), Paper = new("f4f7f7");
	private static readonly Rect2 Field = new(24, 116, 312, 122);
	private Label _title, _instruction, _status;
	private ProgressBar _timeBar;
	private Button _submit;
	private HBoxContainer _numberButtons;
	private float _elapsed, _preview, _needle, _targetCenter, _timingWindow, _pressure, _stableTime;
	private Vector2 _clickTarget;
	private int _completed, _selectedWire = -1, _stage = 1, _lastKind = -1;
	private bool _spaceHeld, _mouseHeld;

	public override void _Ready()
	{
		_random.Randomize();
		_title = GetNode<Label>("Title");
		_instruction = GetNode<Label>("Instruction");
		_status = GetNode<Label>("Status");
		_timeBar = GetNode<ProgressBar>("TimeBar");
		_submit = GetNode<Button>("Submit");
		_numberButtons = GetNode<HBoxContainer>("NumberButtons");
		_submit.Pressed += Submit;
		_submit.ButtonDown += () => _mouseHeld = true;
		_submit.ButtonUp += () => _mouseHeld = false;
		for (int i = 0; i < 4; i++)
		{
			int number = i + 1;
			_numberButtons.GetChild<Button>(i).Pressed += () => SubmitNumber(number);
		}
		Hide();
	}

	public void StartRandomHack(int stage = 1)
	{
		if (Active) return;
		if (_bag.Count == 0)
		{
			for (int i = 0; i < 8; i++) _bag.Add(i);
			for (int i = _bag.Count - 1; i > 0; i--)
			{ int j = _random.RandiRange(0, i); (_bag[i], _bag[j]) = (_bag[j], _bag[i]); }
			if (_bag[^1] == _lastKind) (_bag[0], _bag[^1]) = (_bag[^1], _bag[0]);
		}
		int kind = _bag[^1];
		_bag.RemoveAt(_bag.Count - 1);
		_lastKind = kind;
		BeginHack((HackKind)kind, stage);
	}

	private void BeginHack(HackKind kind, int stage)
	{
		CurrentKind = kind;
		_stage = Mathf.Clamp(stage, 1, 3);
		TimeLimit = 11f - _stage; // 10 / 9 / 8 seconds; memory preview is free.
		_timingWindow = 0.14f - _stage * 0.015f;
		_elapsed = _needle = _stableTime = 0;
		_pressure = 0.25f;
		_preview = kind == HackKind.Memory ? 1.5f : 0;
		_completed = 0;
		_selectedWire = -1;
		_spaceHeld = _mouseHeld = false;
		_targetCenter = _random.RandfRange(0.4f, 0.7f);
		for (int i = 0; i < 3; i++)
		{
			_sequence[i] = _random.RandiRange(1, 4);
			_wires[i] = i;
			_connected[i] = false;
			_fingerTarget[i] = _random.RandiRange(0, 3);
			_fingerCurrent[i] = (_fingerTarget[i] + _random.RandiRange(1, 3)) % 4;
		}
		for (int i = 2; i > 0; i--) { int j = _random.RandiRange(0, i); (_wires[i], _wires[j]) = (_wires[j], _wires[i]); }
		for (int i = 0; i < 6; i++) _order[i] = i + 1;
		for (int i = 5; i > 0; i--) { int j = _random.RandiRange(0, i); (_order[i], _order[j]) = (_order[j], _order[i]); }
		for (int i = 0; i < 4; i++) _circuit[i] = (CircuitSolution[i] + _random.RandiRange(1, 3)) % 4;
		NewClickTarget();
		Active = true;
		_submit.Visible = kind is HackKind.Timing or HackKind.Fingerprint or HackKind.Pressure;
		_submit.Text = kind == HackKind.Timing ? "STOP / SPATIE" : kind == HackKind.Pressure ? "HOUD INGEDRUKT / SPATIE" : "CONTROLEER";
		_numberButtons.Visible = kind == HackKind.Memory;
		SetNumberButtonsEnabled(_preview <= 0);
		_title.Text = kind switch
		{
			HackKind.Timing => "01 / TIMING", HackKind.Targets => "02 / DOELWITTEN", HackKind.Memory => "03 / GEHEUGEN",
			HackKind.Wires => "04 / KABELS", HackKind.Order => "05 / VOLGORDE", HackKind.Circuit => "06 / CIRCUIT",
			HackKind.Fingerprint => "07 / VINGERAFDRUK", _ => "08 / DRUKREGELING"
		};
		_instruction.Text = kind switch
		{
			HackKind.Timing => "Stop de pijl in het groene vak.\nGebruik SPATIE of STOP.",
			HackKind.Targets => "Klik 3 cirkels aan.\nNaast een cirkel klikken = fout.",
			HackKind.Memory => "Onthoud 3 cijfers. Herhaal daarna\nmet 1-4 of de knoppen.",
			HackKind.Wires => "Klik links en dan rechts dezelfde\nvorm aan. Verbind alle 3 kabels.",
			HackKind.Order => "Klik de cijfers van laag naar hoog:\n1, 2, 3, 4, 5, 6.",
			HackKind.Circuit => "Klik om de 4 hoeken te draaien.\nMaak een gesloten vierkant.",
			HackKind.Fingerprint => "Klik de rechter stroken tot ze links\nmatchen. Klik dan CONTROLEER.",
			_ => "Houd SPATIE of de knop ingedrukt.\nBlijf 1,5 s groen: druk en laat los."
		};
		_timeBar.Value = 100;
		UpdateStatus();
		Show();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!Active) return;
		float dt = (float)delta;
		if (_preview > 0)
		{
			_preview = Mathf.Max(0, _preview - dt);
			if (_preview <= 0) SetNumberButtonsEnabled(true);
		}
		else
		{
			_elapsed += dt;
			_needle = Mathf.PingPong(_elapsed * (0.50f + _stage * 0.08f), 1);
			_timeBar.Value = Mathf.Max(0, 1 - _elapsed / TimeLimit) * 100;
			if (CurrentKind == HackKind.Pressure)
			{
				_pressure = Mathf.Clamp(_pressure + dt * (_spaceHeld || _mouseHeld ? 0.38f : -0.24f), 0, 1);
				bool safe = _pressure >= 0.40f && _pressure <= 0.68f;
				_stableTime = safe ? _stableTime + dt : 0;
				if (_pressure >= 0.99f) { Resolve(false); return; }
				if (_stableTime >= 1.5f) { Resolve(true); return; }
			}
			if (_elapsed >= TimeLimit) { Resolve(false); return; }
		}
		UpdateStatus();
		QueueRedraw();
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!Active || inputEvent is not InputEventKey key || key.Echo) return;
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		if (CurrentKind == HackKind.Pressure && code == Key.Space)
		{
			_spaceHeld = key.Pressed;
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!key.Pressed) return;
		if (code == Key.Escape) { Resolve(false); GetViewport().SetInputAsHandled(); }
		else if (CurrentKind == HackKind.Timing && code == Key.Space) { Submit(); GetViewport().SetInputAsHandled(); }
		else if (CurrentKind == HackKind.Memory && code >= Key.Key1 && code <= Key.Key4)
		{ SubmitNumber((int)code - (int)Key.Key1 + 1); GetViewport().SetInputAsHandled(); }
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (!Active || inputEvent is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left) return;
		Vector2 p = mouse.Position;
		if (!Field.HasPoint(p)) return;
		AcceptEvent();
		switch (CurrentKind)
		{
			case HackKind.Targets:
				if (p.DistanceTo(_clickTarget) > 23) { Resolve(false); return; }
				if (++_completed == 3) Resolve(true); else NewClickTarget();
				break;
			case HackKind.Wires:
				for (int i = 0; i < 3; i++)
				{
					if (p.DistanceTo(WirePoint(false, i)) < 18 && !_connected[i]) _selectedWire = i;
					if (p.DistanceTo(WirePoint(true, i)) < 18 && _selectedWire >= 0)
					{
						if (_wires[i] != _selectedWire) { Resolve(false); return; }
						_connected[_selectedWire] = true;
						_selectedWire = -1;
						if (++_completed == 3) Resolve(true);
					}
				}
				break;
			case HackKind.Order:
				for (int i = 0; i < 6; i++)
				{
					if (!OrderRect(i).HasPoint(p) || _order[i] <= _completed) continue;
					if (_order[i] != _completed + 1) { Resolve(false); return; }
					if (++_completed == 6) Resolve(true);
					break;
				}
				break;
			case HackKind.Circuit:
				for (int i = 0; i < 4; i++)
					if (CircuitRect(i).HasPoint(p)) _circuit[i] = (_circuit[i] + 1) % 4;
				bool solved = true;
				for (int i = 0; i < 4; i++) solved &= _circuit[i] == CircuitSolution[i];
				if (solved) Resolve(true);
				break;
			case HackKind.Fingerprint:
				for (int i = 0; i < 3; i++) if (new Rect2(190, 121 + i * 37, 137, 34).HasPoint(p))
					_fingerCurrent[i] = (_fingerCurrent[i] + 1) % 4;
				break;
		}
		UpdateStatus();
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(0, 0, 360, 352), new Color("e9eeef"));
		DrawRect(new Rect2(0, 0, 360, 352), new Color("41545b"), false, 2);
		if (!Active) return;
		DrawRect(Field, new Color("d8e1e3"));
		switch (CurrentKind)
		{
			case HackKind.Timing:
				DrawRect(new Rect2(40, 158, 280, 34), Ink);
				DrawRect(new Rect2(40 + (_targetCenter - _timingWindow) * 280, 158, _timingWindow * 560, 34), Green);
				float x = 40 + _needle * 280;
				DrawLine(new Vector2(x, 150), new Vector2(x, 200), Colors.White, 4);
				break;
			case HackKind.Targets:
				DrawCircle(_clickTarget, 23, new Color("2a7f96"));
				DrawArc(_clickTarget, 29, 0, Mathf.Tau, 40, Ink, 1, true);
				TextAt(_clickTarget + new Vector2(-6, 7), (_completed + 1).ToString(), 21, Colors.White);
				break;
			case HackKind.Memory:
				for (int i = 0; i < 3; i++)
				{
					float cx = 55 + i * 88;
					DrawRect(new Rect2(cx, 140, 74, 70), i < _completed ? Green : Paper);
					TextAt(new Vector2(cx + 27, 185), _preview > 0 || i < _completed ? _sequence[i].ToString() : "?", 32, Ink);
				}
				break;
			case HackKind.Wires:
				for (int i = 0; i < 3; i++)
				{
					int right = System.Array.IndexOf(_wires, i);
					if (_connected[i]) DrawLine(WirePoint(false, i), WirePoint(true, right), new Color("33876c"), 4, true);
					DrawSymbol(WirePoint(false, i), i, _selectedWire == i || _connected[i]);
					DrawSymbol(WirePoint(true, i), _wires[i], _connected[_wires[i]]);
				}
				break;
			case HackKind.Order:
				for (int i = 0; i < 6; i++)
				{
					Rect2 rect = OrderRect(i);
					DrawRect(rect, _order[i] <= _completed ? Green : Paper);
					TextAt(rect.Position + new Vector2(32, 32), _order[i].ToString(), 25, Ink);
				}
				break;
			case HackKind.Circuit:
				for (int i = 0; i < 4; i++)
				{
					Rect2 rect = CircuitRect(i);
					DrawRect(rect, Paper);
					Vector2 center = rect.GetCenter();
					Vector2 a = Vector2.Right.Rotated(_circuit[i] * Mathf.Pi / 2);
					Vector2 b = Vector2.Up.Rotated(_circuit[i] * Mathf.Pi / 2);
					DrawLine(center, center + a * 27, Ink, 5, true);
					DrawLine(center, center + b * 27, Ink, 5, true);
					DrawCircle(center, 5, new Color("328b85"));
				}
				TextAt(new Vector2(38, 179), "DRAAI", 14, Ink);
				TextAt(new Vector2(244, 179), "SLUIT", 14, Ink);
				break;
			case HackKind.Fingerprint:
				for (int i = 0; i < 3; i++)
				{
					DrawFingerprint(new Rect2(33, 121 + i * 37, 137, 34), _fingerTarget[i]);
					DrawFingerprint(new Rect2(190, 121 + i * 37, 137, 34), _fingerCurrent[i]);
				}
				break;
			case HackKind.Pressure:
				DrawRect(new Rect2(40, 147, 280, 34), Ink);
				DrawRect(new Rect2(40 + 0.4f * 280, 147, 0.28f * 280, 34), Green);
				DrawLine(new Vector2(40 + _pressure * 280, 140), new Vector2(40 + _pressure * 280, 189), Colors.White, 4);
				TextAt(new Vector2(65, 219), $"STABIEL: {_stableTime:0.0} / 1.5 s", 20, Ink);
				break;
		}
	}

	private void DrawSymbol(Vector2 p, int index, bool selected)
	{
		Color color = selected ? new Color("318368") : Ink;
		DrawCircle(p, 18, selected ? Green : Paper);
		if (index == 0) DrawCircle(p, 9, color);
		else if (index == 1) DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), color);
		else DrawColoredPolygon(new[] { p + new Vector2(0, -10), p + new Vector2(10, 9), p + new Vector2(-10, 9) }, color);
	}

	private void DrawFingerprint(Rect2 rect, int variant)
	{
		DrawRect(rect, Paper);
		for (int line = 0; line < 4; line++)
		{
			Vector2 previous = Vector2.Zero;
			for (int step = 0; step <= 24; step++)
			{
				float x = step / 24f;
				float y = 6 + line * 6 + Mathf.Sin(x * (variant + 1) * Mathf.Pi * 1.1f + variant * 0.8f) * 3;
				Vector2 point = rect.Position + new Vector2(7 + x * (rect.Size.X - 14), y);
				if (step > 0) DrawLine(previous, point, Ink, 1.5f, true);
				previous = point;
			}
		}
	}

	private void Submit()
	{
		if (!Active) return;
		if (CurrentKind == HackKind.Timing) Resolve(Mathf.Abs(_needle - _targetCenter) <= _timingWindow);
		if (CurrentKind == HackKind.Fingerprint)
		{
			bool match = true;
			for (int i = 0; i < 3; i++) match &= _fingerTarget[i] == _fingerCurrent[i];
			Resolve(match);
		}
	}

	private void SubmitNumber(int number)
	{
		if (!Active || CurrentKind != HackKind.Memory || _preview > 0) return;
		if (number != _sequence[_completed]) { Resolve(false); return; }
		if (++_completed == 3) Resolve(true);
		else { UpdateStatus(); QueueRedraw(); }
	}

	private static Vector2 WirePoint(bool right, int row) => new(right ? 298 : 62, 138 + row * 38);
	private static Rect2 OrderRect(int index) => new(44 + index % 3 * 94, 126 + index / 3 * 55, 84, 46);
	private static Rect2 CircuitRect(int index) => new(124 + index % 2 * 54, 122 + index / 2 * 54, 52, 52);
	private void TextAt(Vector2 pos, string text, int size, Color color) => DrawString(ThemeDB.FallbackFont, pos, text, fontSize: size, modulate: color);

	private void NewClickTarget()
	{
		Vector2 previous = _clickTarget;
		for (int i = 0; i < 12; i++)
		{
			_clickTarget = new Vector2(_random.RandfRange(57, 303), _random.RandfRange(149, 205));
			if (_clickTarget.DistanceTo(previous) >= 65) break;
		}
	}

	private void SetNumberButtonsEnabled(bool enabled)
	{
		foreach (Node child in _numberButtons.GetChildren()) if (child is Button button) button.Disabled = !enabled;
	}

	private void UpdateStatus()
	{
		string progress = CurrentKind switch
		{
			HackKind.Targets or HackKind.Memory or HackKind.Wires => $"{_completed} / 3",
			HackKind.Order => $"{_completed} / 6", _ => "VOLTOOI DE HACK"
		};
		_status.Text = _preview > 0 ? "KIJK GOED..." : $"{Mathf.Max(0, TimeLimit - _elapsed):0.0} s  /  {progress}";
	}

	public void ResetDeck() { _bag.Clear(); _lastKind = -1; }
	public void CancelSilently() { Active = false; _spaceHeld = _mouseHeld = false; Hide(); }
	private void Resolve(bool success)
	{
		if (!Active) return;
		CancelSilently();
		EmitSignal(SignalName.Finished, success);
	}
}
