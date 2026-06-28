// KeyEmuController — Windows Forms приложение для управления платой MH-Tiny ATtiny88
//
// ЗАВИСИМОСТЬ: NuGet пакет HidLibrary

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using HidLibrary;

namespace KEApp
{
    // ==========================================================================
    // WinAPI для глобальных хуков (Low-level hooks)
    // ==========================================================================
    internal static class NativeMethods
    {
        public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    // ==========================================================================
    // Глобальный монитор активности (мышь + клавиатура)
    // ==========================================================================
    public class ActivityMonitor : IDisposable
    {
        private NativeMethods.LowLevelProc _keyboardProc;
        private NativeMethods.LowLevelProc _mouseProc;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;

        public event Action ActivityDetected;

        public ActivityMonitor()
        {
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;
            InstallHooks();
        }

        private void InstallHooks()
        {
            IntPtr hModule = NativeMethods.GetModuleHandle(null);
            _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, hModule, 0);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                ActivityDetected?.Invoke();
            }
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN ||
                    msg == NativeMethods.WM_MBUTTONDOWN || msg == NativeMethods.WM_MOUSEWHEEL ||
                    msg == NativeMethods.WM_MOUSEMOVE)
                {
                    ActivityDetected?.Invoke();
                }
            }
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_keyboardHook != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
        }
    }

    // ==========================================================================
    // HID устройство
    // ==========================================================================
    public class KeyEmuDevice : IDisposable
    {
        private const int VID = 0x1781;
        private const int PID = 0x24AB;
        private const byte REPID_FEATURE = 0x05;

        private HidDevice _device;
        private bool _disposed;

        public bool IsConnected => _device != null && _device.IsConnected;

        public bool Connect()
        {
            try
            {
                var list = HidDevices.Enumerate(VID, PID).ToList();
                if (list.Count == 0) return false;

                _device = list.First();
                _device.OpenDevice();

                System.Threading.Thread.Sleep(100);

                return _device.IsConnected;
            }
            catch { return false; }
        }

        public string LastInfo { get; private set; } = "";

        /// <summary>
        /// Отправить команду на плату (Feature Report)
        /// </summary>
        public bool SendCommand(byte cmd)
        {
            if (!IsConnected) return false;
            try
            {
                byte[] buf = new byte[2];
                buf[0] = REPID_FEATURE;
                buf[1] = cmd;

                bool ok = _device.WriteFeatureData(buf);
                LastInfo = $"Write cmd=0x{cmd:X2} ok={ok}";
                return ok;
            }
            catch (Exception ex)
            {
                LastInfo = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Читать состояние платы (Feature Report)
        /// </summary>
        public byte ReadState()
        {
            if (!IsConnected) return 0;
            try
            {
                bool ok = _device.ReadFeatureData(out byte[] data, REPID_FEATURE);

                if (ok && data != null && data.Length >= 2)
                {
                    LastInfo = $"ReadState ok={ok} data={BitConverter.ToString(data)}";
                    return data[1];
                }

                LastInfo = $"ReadState ok={ok} len={(data?.Length ?? 0)}";
                return 0;
            }
            catch (Exception ex)
            {
                LastInfo = $"ReadState error: {ex.Message}";
                return 0;
            }
        }

        public void Disconnect() => _device?.CloseDevice();

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _device?.Dispose();
                _disposed = true;
            }
        }
    }

    // ==========================================================================
    // Кастомная кнопка с анимацией
    // ==========================================================================
    public class ToggleButton : Control
    {
        private bool _isActive = false;
        private float _animProgress = 0f;
        private bool _isHovered = false;
        private bool _isPressed = false;

        private readonly System.Windows.Forms.Timer _animTimer;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    _animTimer.Start();
                }
            }
        }

        public event EventHandler StateChanged;

        public ToggleButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);
            Size = new Size(180, 180);
            Cursor = Cursors.Hand;

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                float target = _isActive ? 1f : 0f;
                _animProgress += (target - _animProgress) * 0.1f;
                if (Math.Abs(_animProgress - target) < 0.004f)
                {
                    _animProgress = target;
                    _animTimer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (Enabled) { _isPressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isPressed && Enabled)
            {
                _isPressed = false;
                IsActive = !IsActive;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = Width / 2, cy = Height / 2;
            int r = Math.Min(Width, Height) / 2 - 12;

            if (_animProgress > 0.05f)
            {
                int gs = (int)(35 * _animProgress);
                using (var b = new SolidBrush(Color.FromArgb((int)(45 * _animProgress), 30, 210, 100)))
                    g.FillEllipse(b, cx - r - gs, cy - r - gs, (r + gs) * 2, (r + gs) * 2);
            }

            int sh = _isPressed ? 1 : (_isActive ? 4 : 8);
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(b, cx - r + sh, cy - r + sh, r * 2, r * 2);

            Color col = Lerp(Color.FromArgb(55, 55, 68), Color.FromArgb(28, 195, 95), _animProgress);
            if (_isHovered && !_isPressed) col = Brighten(col, 18);
            if (!Enabled) col = Color.FromArgb(40, 40, 50);

            int po = _isPressed ? 2 : 0;
            var rc = new Rectangle(cx - r + po, cy - r + po, r * 2, r * 2);

            using (var b = new SolidBrush(col))
                g.FillEllipse(b, rc);

            Color ring = Lerp(Color.FromArgb(75, 75, 92), Color.FromArgb(45, 215, 120), _animProgress);
            using (var p = new Pen(ring, 2f))
                g.DrawEllipse(p, rc);

            Color ic = Enabled
                ? Lerp(Color.FromArgb(130, 130, 148), Color.White, _animProgress)
                : Color.FromArgb(70, 70, 80);
            int sz = (int)(r * 0.44f);
            using (var p = new Pen(ic, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(p, cx + po, cy + po - sz, cx + po, cy + po - sz / 3);
                int ar = (int)(sz * 0.73f);
                g.DrawArc(p, cx + po - ar, cy + po - ar, ar * 2, ar * 2, -240, 300);
            }
        }

        private static Color Lerp(Color a, Color b, float t) =>
            Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));

        private static Color Brighten(Color c, int v) =>
            Color.FromArgb(c.A,
                Math.Min(255, c.R + v),
                Math.Min(255, c.G + v),
                Math.Min(255, c.B + v));
    }

    // ==========================================================================
    // NumericUpDown кастомный (для таймера)
    // ==========================================================================
    public class StyledNumericUpDown : NumericUpDown
    {
        public StyledNumericUpDown()
        {
            BackColor = Color.FromArgb(30, 30, 42);
            ForeColor = Color.White;
            BorderStyle = BorderStyle.None;
            Controls[0].BackColor = Color.FromArgb(42, 42, 56);
            Controls[1].BackColor = Color.FromArgb(42, 42, 56);
            Font = new Font("Segoe UI", 10f);
            Minimum = 1;
            Maximum = 120;
            Value = 5;
        }
    }

    // ==========================================================================
    // Главная форма
    // ==========================================================================
    public class MainForm : Form
    {
        private readonly KeyEmuDevice _dev = new KeyEmuDevice();
        private ActivityMonitor _activityMonitor;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayItemToggle;  // пункт Включить/Выключить в трее

        private ToggleButton _btn;
        private Label _lblDevice, _lblState, _lblInfo, _lblIdleTimer;
        private StyledNumericUpDown _numIdleMinutes;
        private StyledNumericUpDown _numSoftDelay;   // ✅ задержка софт-кнопки
        private CheckBox _chkEnableIdle;
        private CheckBox _chkEnableSoftDelay;         // ✅ вкл/выкл задержку
        private System.Windows.Forms.Timer _watchTimer;
        private System.Windows.Forms.Timer _pollTimer;
        private System.Windows.Forms.Timer _idleTimer;

        private bool _connected = false;
        private bool _suppressEvent = false;
        private DateTime _lastActivityTime;
        private int _idleTimeoutSeconds = 300;          // 5 минут по умолчанию
        private int _softDelaySeconds = 3;              // ✅ 3 сек задержка по умолчанию
        private DateTime _softButtonPressedTime;      // ✅ время нажатия софт-кнопки
        private bool _softDelayActive = false;        // ✅ флаг активной задержки

        public MainForm()
        {
            BuildUI();
            SetupTray();
            SetupActivityMonitor();
            StartWatcher();
            StartPolling();
            StartIdleTimer();
        }

        // ======================== TRAY ========================
        private void SetupTray()
        {
            _trayMenu = new ContextMenuStrip();

            // ✅ НОВОЕ: пункт "Включить/Выключить"
            _trayItemToggle = new ToolStripMenuItem("Включить", null, (s, e) => TrayToggle());
            _trayMenu.Items.Add(_trayItemToggle);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayMenu.Items.Add("Открыть", null, (s, e) => ShowFromTray());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Выход", null, (s, e) => ExitApp());

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "KeyEmu Controller",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        // ✅ НОВОЕ: обработчик пункта трей-меню
        private void TrayToggle()
        {
            if (!_connected || !_dev.IsConnected) return;
            _btn.IsActive = !_btn.IsActive;
            OnToggle(this, EventArgs.Empty);
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
                _trayIcon.ShowBalloonTip(2000, "KeyEmu", "Приложение свёрнуто в трей", ToolTipIcon.Info);
            }
        }

        // ======================== UI ========================
        private void BuildUI()
        {
            Text = "KEApp";
            ClientSize = new Size(380, 620);  // ✅ увеличили высоту для новых элементов
            MinimumSize = new Size(400, 660);
            BackColor = Color.FromArgb(20, 20, 28);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            // Заголовок
            Add(MakeLabel("KEY EMU", new Font("Segoe UI", 20f, FontStyle.Bold), Color.FromArgb(225, 225, 240), 0, 18, 380, 44, ContentAlignment.MiddleCenter));
            Add(MakeLabel("ATtiny88 · MH-Tiny", new Font("Segoe UI", 9f), Color.FromArgb(90, 90, 110), 0, 60, 380, 22, ContentAlignment.MiddleCenter));

            // Статус устройства
            _lblDevice = MakeLabel("● Устройство не найдено", new Font("Segoe UI", 9f), Color.FromArgb(200, 75, 75), 0, 88, 380, 24, ContentAlignment.MiddleCenter);
            Add(_lblDevice);

            Add(new Panel { BackColor = Color.FromArgb(42, 42, 56), Height = 1, Width = 300, Top = 118, Left = 40 });

            // Кнопка вкл/выкл
            _btn = new ToggleButton { Left = (380 - 180) / 2, Top = 134, Enabled = false };
            _btn.StateChanged += OnToggle;
            Add(_btn);

            _lblState = MakeLabel("ВЫКЛЮЧЕНО", new Font("Segoe UI", 14f, FontStyle.Bold), Color.FromArgb(95, 95, 115), 0, 326, 380, 36, ContentAlignment.MiddleCenter);
            Add(_lblState);

            Add(new Panel { BackColor = Color.FromArgb(42, 42, 56), Height = 1, Width = 300, Top = 370, Left = 40 });

            // ===== НАСТРОЙКА ЗАДЕРЖКИ СОФТ-КНОПКИ =====
            var lblSoftDelay = MakeLabel("⏳ Задержка софт-кнопки", new Font("Segoe UI", 10f, FontStyle.Bold), Color.FromArgb(180, 180, 200), 0, 378, 380, 24, ContentAlignment.MiddleCenter);
            Add(lblSoftDelay);

            _chkEnableSoftDelay = new CheckBox
            {
                Text = "Игнорировать активность после включения",
                Left = 50,
                Top = 404,
                Width = 300,
                ForeColor = Color.FromArgb(180, 180, 200),
                BackColor = Color.Transparent,
                Checked = true
            };
            Add(_chkEnableSoftDelay);

            Add(MakeLabel("сек:", new Font("Segoe UI", 10f), Color.FromArgb(140, 140, 160), 50, 430, 40, 26, ContentAlignment.MiddleLeft));

            _numSoftDelay = new StyledNumericUpDown
            {
                Left = 90,
                Top = 428,
                Width = 70,
                Height = 26,
                Minimum = 1,
                Maximum = 30,
                Value = 3
            };
            _numSoftDelay.ValueChanged += (s, e) => _softDelaySeconds = (int)_numSoftDelay.Value;
            Add(_numSoftDelay);

            Add(new Panel { BackColor = Color.FromArgb(42, 42, 56), Height = 1, Width = 300, Top = 462, Left = 40 });

            // ===== ТАЙМЕР БЕЗДЕЙСТВИЯ =====
            var lblIdle = MakeLabel("⏱ Таймер бездействия", new Font("Segoe UI", 10f, FontStyle.Bold), Color.FromArgb(180, 180, 200), 0, 470, 380, 24, ContentAlignment.MiddleCenter);
            Add(lblIdle);

            _chkEnableIdle = new CheckBox
            {
                Text = "Автовключение при бездействии",
                Left = 50,
                Top = 496,
                Width = 280,
                ForeColor = Color.FromArgb(180, 180, 200),
                BackColor = Color.Transparent,
                Checked = false
            };
            Add(_chkEnableIdle);

            Add(MakeLabel("минут:", new Font("Segoe UI", 10f), Color.FromArgb(140, 140, 160), 50, 524, 60, 26, ContentAlignment.MiddleLeft));

            _numIdleMinutes = new StyledNumericUpDown
            {
                Left = 110,
                Top = 522,
                Width = 70,
                Height = 26
            };
            _numIdleMinutes.ValueChanged += (s, e) => _idleTimeoutSeconds = (int)_numIdleMinutes.Value * 60;
            Add(_numIdleMinutes);

            _lblIdleTimer = MakeLabel("Ожидание активности пользователя", new Font("Segoe UI", 9f), Color.FromArgb(100, 100, 120), 0, 554, 380, 22, ContentAlignment.MiddleCenter);
            Add(_lblIdleTimer);

            // Info панель
            var panel = new Panel { BackColor = Color.FromArgb(30, 30, 42), Width = 320, Height = 50, Top = 584, Left = 30 };
            RoundRegion(panel, 10);
            _lblInfo = MakeLabel("Подключите плату к USB", new Font("Segoe UI", 8.5f), Color.FromArgb(115, 115, 135), 0, 0, 320, 50, ContentAlignment.MiddleCenter);
            panel.Controls.Add(_lblInfo);
            Add(panel);
        }

        private void Add(Control c) => Controls.Add(c);

        private static Label MakeLabel(string text, Font font, Color fore, int x, int y, int w, int h, ContentAlignment align) =>
            new Label { Text = text, Font = font, ForeColor = fore, Left = x, Top = y, Width = w, Height = h, TextAlign = align, BackColor = Color.Transparent };

        private static void RoundRegion(Control c, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(0, 0, r, r, 180, 90);
            p.AddArc(c.Width - r, 0, r, r, 270, 90);
            p.AddArc(c.Width - r, c.Height - r, r, r, 0, 90);
            p.AddArc(0, c.Height - r, r, r, 90, 90);
            p.CloseFigure();
            c.Region = new Region(p);
        }

        // ======================== ACTIVITY MONITOR ========================
        private void SetupActivityMonitor()
        {
            _activityMonitor = new ActivityMonitor();
            _activityMonitor.ActivityDetected += OnUserActivity;
            _lastActivityTime = DateTime.Now;
        }

        // ✅ ИСПРАВЛЕНО: задержка при включении софт-кнопки
        private void OnUserActivity()
        {
            _lastActivityTime = DateTime.Now;

            // Если активна задержка софт-кнопки — игнорируем активность
            if (_softDelayActive)
            {
                var delayElapsed = (DateTime.Now - _softButtonPressedTime).TotalSeconds;
                if (delayElapsed < _softDelaySeconds)
                {
                    SetInfo($"Задержка: {delayElapsed:F0}/{_softDelaySeconds}с...");
                    return;  // игнорируем активность во время задержки
                }
                _softDelayActive = false;
            }

            if (_btn.IsActive)
            {
                TurnOff();
            }
        }

        // ======================== IDLE TIMER ========================
        private void StartIdleTimer()
        {
            _idleTimer = new System.Windows.Forms.Timer { Interval = 1000 }; // каждую секунду
            _idleTimer.Tick += (s, e) => CheckIdleTimeout();
            _idleTimer.Start();
        }

        private void CheckIdleTimeout()
        {
            // Обновляем трей-меню
            _trayItemToggle.Text = _btn.IsActive ? "Выключить" : "Включить";

            if (!_chkEnableIdle.Checked || !_connected || !_dev.IsConnected)
            {
                _lblIdleTimer.Text = _chkEnableIdle.Checked ? "Устройство не подключено" : "Автовключение отключено";
                _lblIdleTimer.ForeColor = Color.FromArgb(100, 100, 120);
                return;
            }

            if (_btn.IsActive)
            {
                var elapsed = DateTime.Now - _lastActivityTime;
                _lblIdleTimer.Text = $"Активно. Бездействие: {elapsed.TotalSeconds:F0}с";
                _lblIdleTimer.ForeColor = Color.FromArgb(45, 200, 95);
                return;
            }

            var idleTime = DateTime.Now - _lastActivityTime;
            var remaining = TimeSpan.FromSeconds(_idleTimeoutSeconds) - idleTime;

            if (remaining.TotalSeconds <= 0)
            {
                TurnOn();
                _lblIdleTimer.Text = "Включено по таймеру бездействия";
                _lblIdleTimer.ForeColor = Color.FromArgb(45, 200, 95);
            }
            else
            {
                _lblIdleTimer.Text = $"Бездействие: {idleTime.TotalSeconds:F0}с / {_idleTimeoutSeconds}с (осталось {remaining.TotalSeconds:F0}с)";
                _lblIdleTimer.ForeColor = Color.FromArgb(140, 140, 160);
            }
        }

        // ======================== DEVICE CONTROL ========================
        private void TurnOn()
        {
            if (_btn.IsActive) return;
            _suppressEvent = true;
            _btn.IsActive = true;
            SetState(true);
            _suppressEvent = false;
            Task.Run(() => _dev.SendCommand(0x01));
        }

        private void TurnOff()
        {
            if (!_btn.IsActive) return;
            _suppressEvent = true;
            _btn.IsActive = false;
            SetState(false);
            _suppressEvent = false;
            Task.Run(() => _dev.SendCommand(0x02));
        }

        // ======================== POLLING ========================
        private void StartPolling()
        {
            _pollTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _pollTimer.Tick += (s, e) => PollDeviceState();
            _pollTimer.Start();
        }

        private void PollDeviceState()
        {
            if (!_connected || !_dev.IsConnected) return;

            byte state = _dev.ReadState();
            if (state == 0) return;

            bool shouldBeActive = (state == 0x01);

            if (_btn.IsActive != shouldBeActive)
            {
                _suppressEvent = true;
                _btn.IsActive = shouldBeActive;
                SetState(shouldBeActive);
                _suppressEvent = false;
            }
        }

        // ======================== WATCHER ========================
        private void StartWatcher()
        {
            _watchTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _watchTimer.Tick += (s, e) => CheckConnection();
            _watchTimer.Start();
            CheckConnection();
        }

        private void CheckConnection()
        {
            bool ok = _dev.IsConnected || _dev.Connect();
            if (ok == _connected) return;
            _connected = ok;

            if (ok)
            {
                _lblDevice.Text = "● Устройство подключено";
                _lblDevice.ForeColor = Color.FromArgb(45, 200, 95);
                _btn.Enabled = true;
                SetInfo("Готово к работе");
            }
            else
            {
                _lblDevice.Text = "● Устройство не найдено";
                _lblDevice.ForeColor = Color.FromArgb(200, 75, 75);
                _btn.Enabled = false;
                _btn.IsActive = false;
                SetState(false);
                SetInfo("Подключите плату к USB");
            }
        }

        // ======================== EVENTS ========================
        private async void OnToggle(object sender, EventArgs e)
        {
            if (_suppressEvent) return;

            bool active = _btn.IsActive;
            SetState(active);
            byte cmd = active ? (byte)0x01 : (byte)0x02;

            // ✅ НОВОЕ: если включение софт-кнопкой — активируем задержку
            if (active && _chkEnableSoftDelay.Checked)
            {
                _softButtonPressedTime = DateTime.Now;
                _softDelayActive = true;
                SetInfo($"Задержка {_softDelaySeconds}с...");
            }

            bool ok = await Task.Run(() => _dev.SendCommand(cmd));
            SetInfo(_dev.LastInfo);
            _lblInfo.ForeColor = ok
                ? Color.FromArgb(45, 205, 105)
                : Color.FromArgb(200, 160, 50);
        }

        private void SetState(bool active)
        {
            _lblState.Text = active ? "АКТИВНО" : "ВЫКЛЮЧЕНО";
            _lblState.ForeColor = active ? Color.FromArgb(45, 205, 105) : Color.FromArgb(95, 95, 115);
        }

        private void SetInfo(string text)
        {
            _lblInfo.ForeColor = Color.FromArgb(115, 115, 135);
            _lblInfo.Text = text;
        }

        // ======================== CLEANUP ========================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _watchTimer?.Stop();
            _pollTimer?.Stop();
            _idleTimer?.Stop();
            _activityMonitor?.Dispose();
            _trayIcon?.Dispose();
            _dev?.Dispose();
            base.OnFormClosing(e);
        }
    }

    // ==========================================================================
    // Точка входа
    // ==========================================================================
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
