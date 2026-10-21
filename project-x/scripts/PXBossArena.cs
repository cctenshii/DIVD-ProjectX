using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXBossArena : Node2D
{
    [Export] public int FailedHackDamage { get; set; } = 20;
    [Export] public float HackCooldownSeconds { get; set; } = 3f;
    public bool RoundOver => _roundOver;
    public bool BattlePaused => _roundOver || _hack.Active || _phaseDelay > 0;
    private PXArenaPlayer _player;
    private PXArenaBoss _boss;
    private PXHackPanel _hack;
    private Node2D _lasers;
    private ProgressBar _playerBar, _bossBar;
    private Label _playerLabel, _bossLabel, _message, _context, _phaseLabel, _phaseBanner;
    private PanelContainer _endPanel;
    private Label _endLabel, _idle;
    private Vector2 _playerSpawn, _bossSpawn;
    private bool _roundOver, _hackRequested, _restartRequested;
    private float _hackCooldown, _phaseDelay;

    public override void _Ready()
    {
        GetWindow().ContentScaleSize = new Vector2I(1120, 800);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        GetWindow().ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        _player = GetNode<PXArenaPlayer>("Player");
        _boss = GetNode<PXArenaBoss>("Boss");
        _boss.SetTarget(_player);
        _hack = GetNode<PXHackPanel>("HUD/HackPanel");
        _lasers = new Node2D { Name = "Lasers" };
        AddChild(_lasers);
        _playerBar = GetNode<ProgressBar>("HUD/PlayerBar");
        _bossBar = GetNode<ProgressBar>("HUD/BossBar");
        _playerLabel = GetNode<Label>("HUD/PlayerHealth");
        _bossLabel = GetNode<Label>("HUD/BossHealth");
        _message = GetNode<Label>("HUD/Message");
        _context = GetNode<Label>("HUD/Context");
        _phaseLabel = GetNode<Label>("HUD/Subtitle");
        _phaseBanner = GetNode<Label>("HUD/StageBanner");
        _idle = GetNode<Label>("HUD/IdleInfo");
        _endPanel = GetNode<PanelContainer>("HUD/EndPanel");
        _endLabel = GetNode<Label>("HUD/EndPanel/Text");
        _playerSpawn = _player.GlobalPosition;
        _bossSpawn = _boss.GlobalPosition;
        _player.HealthChanged += UpdatePlayerHealth;
        _boss.HealthChanged += UpdateBossHealth;
        _player.Died += () => EndRound(false);
        _boss.Died += () => EndRound(true);
        _boss.ContactHit += damage => _message.Text = $"BOTSING: -{damage} HP. ZOEK DEKKING.";
        _boss.StageChanged += OnStageChanged;
        _boss.LaserRequested += SpawnLaser;
        _hack.Finished += OnHackFinished;
        GetNode<Button>("HUD/Restart").Pressed += () => _restartRequested = true;
        RestartRound();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_restartRequested) { _restartRequested = false; RestartRound(); }
        if (_phaseDelay > 0)
        {
            _phaseDelay = Mathf.Max(0, _phaseDelay - (float)delta);
            if (_phaseDelay <= 0 && !_roundOver)
            {
                _phaseBanner.Hide();
                _boss.Locked = _player.Locked = false;
                _player.ProtectAfterHack();
            }
        }
        if (!_hack.Active && _phaseDelay <= 0) _hackCooldown = Mathf.Max(0, _hackCooldown - (float)delta);
        if (_hackRequested) { _hackRequested = false; TryStartHack(); }
        bool near = _player.GlobalPosition.DistanceTo(_boss.GlobalPosition) <= _boss.HackRange;
        bool available = near && _boss.CanSeePlayer && !BattlePaused && _hackCooldown <= 0;
        _boss.SetHackAvailable(available);
        _context.Text = _roundOver ? "R = OPNIEUW" : _phaseDelay > 0 ? "NIEUWE FASE" : _hack.Active ? "OMNI-TOOL ACTIEF / ARENA GEPAUZEERD" :
            _hackCooldown > 0 ? $"TOOL HERLAADT: {_hackCooldown:0.0}s / BLIJF BEWEGEN" :
            available ? "[ E ] START HACK" : near && !_boss.CanSeePlayer ? "OBSTAKEL BLOKKEERT DE HACK" : "NADER DE BOSS / E = HACK";
        _phaseLabel.Text = $"FASE {_boss.Stage}/3  /  {StageName(_boss.Stage)}   |   {_boss.Intent}";
        _idle.Visible = !_hack.Active && !_roundOver;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code == Key.R) { _restartRequested = true; GetViewport().SetInputAsHandled(); }
        else if (code == Key.E && !_hack.Active) { _hackRequested = true; GetViewport().SetInputAsHandled(); }
    }

    public bool TryStartHack()
    {
        if (BattlePaused || _hackCooldown > 0) return false;
        if (_player.GlobalPosition.DistanceTo(_boss.GlobalPosition) > _boss.HackRange)
        { _message.Text = "TE VER WEG. NADER DE BOSS."; return false; }
        if (!_boss.HasClearSight(_player.GlobalPosition))
        { _message.Text = "GEEN ZICHT: ER STAAT IETS TUSSEN."; return false; }
        _player.Locked = _boss.Locked = true;
        _player.Velocity = _boss.Velocity = Vector2.Zero;
        _boss.SetHackAvailable(false);
        SetLasersFrozen(true);
        _message.Text = "WILLEKEURIGE HACK. VOLTOOI DE OPDRACHT.";
        _hack.StartRandomHack(_boss.Stage);
        return true;
    }

    public void RestartRound()
    {
        _roundOver = _hackRequested = false;
        _phaseDelay = _hackCooldown = 0;
        ClearLasers();
        _hack.CancelSilently();
        _hack.ResetDeck();
        _endPanel.Hide();
        _phaseBanner.Hide();
        _player.ResetPlayer(_playerSpawn);
        _boss.ResetBoss(_bossSpawn);
        _message.Text = "15 HACKS. ELKE 5 HACKS EEN NIEUWE FASE.";
    }

    private void OnHackFinished(bool success)
    {
        if (_roundOver) return;
        if (success)
        {
            _message.Text = $"HACK GELUKT: {_boss.SuccessfulHacks + 1} / 15";
            _boss.CompleteHack();
            _player.ProtectAfterHack();
        }
        else
        {
            _message.Text = $"HACK MISLUKT: -{FailedHackDamage} HP";
            _player.TakeDamage(FailedHackDamage, _boss.GlobalPosition, true);
        }
        _hackCooldown = HackCooldownSeconds;
        _boss.AfterHack();
        if (!_roundOver && _phaseDelay <= 0)
        {
            _player.Locked = _boss.Locked = false;
            SetLasersFrozen(false);
        }
    }

    private void OnStageChanged(int stage)
    {
        _phaseDelay = 1.7f;
        _player.Locked = _boss.Locked = true;
        ClearLasers();
        _phaseBanner.Text = $"FASE {stage}\n{StageName(stage)}";
        _phaseBanner.Show();
        _message.Text = stage == 2 ? "PAS OP: GERICHTE LASERS. GEBRUIK DEKKING." : "TWEE RINGEN + SPREIDSCHOTEN. SHIFT = DASH.";
    }

    private void SpawnLaser(Vector2 origin, Vector2 direction, float speed, int damage)
    {
        if (BattlePaused) return;
        var laser = new PXArenaLaser { Position = origin, Direction = direction, Speed = speed, Damage = damage, Arena = this };
        _lasers.AddChild(laser);
    }

    private void ClearLasers()
    {
        foreach (Node child in _lasers.GetChildren())
        { if (child is PXArenaLaser laser) laser.Frozen = true; child.QueueFree(); }
    }

    private void SetLasersFrozen(bool frozen)
    {
        foreach (Node child in _lasers.GetChildren()) if (child is PXArenaLaser laser) laser.Frozen = frozen;
    }

    public void NotifyLaserHit(int damage) => _message.Text = $"LASER: -{damage} HP. DASH OF ZOEK DEKKING.";

    private void EndRound(bool victory)
    {
        _roundOver = true;
        _phaseDelay = 0;
        _phaseBanner.Hide();
        _player.Locked = _boss.Locked = true;
        _boss.SetHackAvailable(false);
        _hack.CancelSilently();
        ClearLasers();
        _endLabel.Text = victory ? "BOSS VERSLAGEN\n15 / 15 HACKS\n\nR = opnieuw" : $"JE BENT VERSLAGEN\n{_boss.SuccessfulHacks} / 15 HACKS\n\nR = opnieuw";
        _endPanel.Show();
    }

    private static string StageName(int stage) => stage switch { 1 => "DE JACHT", 2 => "LASERS", _ => "OVERDRIVE" };
    private void UpdatePlayerHealth(int current, int maximum)
    {
        _playerBar.MaxValue = maximum; _playerBar.Value = current;
        _playerLabel.Text = $"SPELER   {current} / {maximum} HP";
    }
    private void UpdateBossHealth(int current, int maximum)
    {
        _bossBar.MaxValue = maximum; _bossBar.Value = current;
        _bossLabel.Text = $"HACKS   {_boss.SuccessfulHacks} / 15   |   FASE {_boss.Stage}";
    }
}
