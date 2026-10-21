using Godot;

namespace ProjectXBossArenaPrototype;

public partial class PXBossArena : Node2D
{
    [Export] public int HackDamage { get; set; } = 40;
    [Export] public int FailedHackDamage { get; set; } = 20;

    private PXArenaPlayer _player;
    private PXArenaBoss _boss;
    private PXHackPanel _hack;
    private ProgressBar _playerBar;
    private ProgressBar _bossBar;
    private Label _playerLabel;
    private Label _bossLabel;
    private Label _message;
    private Label _context;
    private PanelContainer _endPanel;
    private Label _endLabel;
    private Vector2 _playerSpawn;
    private Vector2 _bossSpawn;
    private bool _roundOver;
    private float _hackCooldown;

    public override void _Ready()
    {
        GetWindow().ContentScaleSize = new Vector2I(1120, 800);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        GetWindow().ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;

        _player = GetNode<PXArenaPlayer>("Player");
        _boss = GetNode<PXArenaBoss>("Boss");
        _hack = GetNode<PXHackPanel>("HUD/HackPanel");
        _playerBar = GetNode<ProgressBar>("HUD/PlayerBar");
        _bossBar = GetNode<ProgressBar>("HUD/BossBar");
        _playerLabel = GetNode<Label>("HUD/PlayerHealth");
        _bossLabel = GetNode<Label>("HUD/BossHealth");
        _message = GetNode<Label>("HUD/Message");
        _context = GetNode<Label>("HUD/Context");
        _endPanel = GetNode<PanelContainer>("HUD/EndPanel");
        _endLabel = GetNode<Label>("HUD/EndPanel/Text");
        _playerSpawn = _player.GlobalPosition;
        _bossSpawn = _boss.GlobalPosition;

        _player.HealthChanged += UpdatePlayerHealth;
        _boss.HealthChanged += UpdateBossHealth;
        _player.Died += () => EndRound(false);
        _boss.Died += () => EndRound(true);
        _boss.ContactHit += damage => _message.Text = $"BOTSING: -{damage} HP";
        _hack.Finished += OnHackFinished;
        GetNode<Button>("HUD/Restart").Pressed += RestartRound;
        UpdatePlayerHealth(_player.Health, _player.MaxHealth);
        UpdateBossHealth(_boss.Health, _boss.MaxHealth);
        _endPanel.Hide();
    }

    public override void _Process(double delta)
    {
        _hackCooldown = Mathf.Max(0f, _hackCooldown - (float)delta);
        bool nearBoss = _player.GlobalPosition.DistanceTo(_boss.GlobalPosition) <= _boss.HackRange;
        bool available = nearBoss && !_roundOver && !_hack.Active && _hackCooldown <= 0f;
        _boss.SetHackAvailable(available);
        _context.Text = _roundOver ? "R = OPNIEUW" : _hack.Active ? "OMNI-TOOL ACTIEF" :
            available ? "[ E ] START HACK" : _hackCooldown > 0f ? "TOOL HERLAADT..." : "LOOP DICHTER NAAR DE BOSS";
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code == Key.R)
        {
            RestartRound();
            GetViewport().SetInputAsHandled();
        }
        else if (code == Key.E && !_hack.Active)
        {
            TryStartHack();
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TryStartHack()
    {
        if (_roundOver || _hack.Active || _hackCooldown > 0f) return false;
        if (_player.GlobalPosition.DistanceTo(_boss.GlobalPosition) > _boss.HackRange)
        {
            _message.Text = "TE VER WEG. LOOP NAAR DE BOSS.";
            return false;
        }
        _player.Locked = true;
        _boss.Locked = true;
        _player.Velocity = Vector2.Zero;
        _boss.Velocity = Vector2.Zero;
        _boss.SetHackAvailable(false);
        _message.Text = "HACK GESTART. MAAK DE OPDRACHT AF.";
        _hack.StartRandomHack();
        return true;
    }

    public void RestartRound()
    {
        _roundOver = false;
        _hackCooldown = 0f;
        _hack.CancelSilently();
        _endPanel.Hide();
        _player.ResetPlayer(_playerSpawn);
        _boss.ResetBoss(_bossSpawn);
        _message.Text = "LOOP NAAR DE BOSS EN DRUK OP E.";
    }

    private void OnHackFinished(bool success)
    {
        if (_roundOver) return;
        if (success)
        {
            _message.Text = $"HACK GELUKT: BOSS -{HackDamage} HP";
            _boss.TakeDamage(HackDamage);
            _player.ProtectAfterHack();
        }
        else
        {
            _message.Text = $"HACK MISLUKT: SPELER -{FailedHackDamage} HP";
            _player.TakeDamage(FailedHackDamage, _boss.GlobalPosition, true);
        }
        _hackCooldown = 0.7f;
        if (!_roundOver)
        {
            _player.Locked = false;
            _boss.Locked = false;
        }
    }

    private void EndRound(bool victory)
    {
        _roundOver = true;
        _player.Locked = true;
        _boss.Locked = true;
        _boss.SetHackAvailable(false);
        _hack.CancelSilently();
        _endLabel.Text = victory ? "BOSS VERSLAGEN\n\nR = opnieuw proberen" : "JE BENT VERSLAGEN\n\nR = opnieuw proberen";
        _endPanel.Show();
    }

    private void UpdatePlayerHealth(int current, int maximum)
    {
        _playerBar.MaxValue = maximum;
        _playerBar.Value = current;
        _playerLabel.Text = $"SPELER   {current} / {maximum} HP";
    }

    private void UpdateBossHealth(int current, int maximum)
    {
        _bossBar.MaxValue = maximum;
        _bossBar.Value = current;
        _bossLabel.Text = $"BOSS   {current} / {maximum} HP";
    }
}
