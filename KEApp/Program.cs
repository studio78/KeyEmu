// KeyEmuController — Windows Forms приложение для управления платой MH-Tiny ATtiny88
//
// ЗАВИСИМОСТЬ: NuGet пакет HidLibrary
// Tools → NuGet Package Manager → Package Manager Console:
// Install-Package HidLibrary

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HidLibrary;

namespace KEApp
{
    // ==========================================================================
    // HID устройство
    // ==========================================================================
    public class KeyEmuDevice : IDisposable
    {
        private const int VID = 0x1781;
        private const int PID = 0x24AB;
        private const byte REPID_FEATURE = 0x05;  // ✅ Feature Report ID для команд

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

                // Диагностика
                Console.WriteLine($"Device opened: {_device.Description}");
                Console.WriteLine($"InputReportByteLength:  {_device.Capabilities.InputReportByteLength}");
                Console.WriteLine($"OutputReportByteLength: {_device.Capabilities.OutputReportByteLength}");
                Console.WriteLine($"FeatureReportByteLength: {_device.Capabilities.FeatureReportByteLength}");

                System.Threading.Thread.Sleep(100);
                return _device.IsConnected;
            }
            catch { return false; }
        }

        public string LastInfo { get; private set; } = "";

        /// <summary>
        /// Отправить команду через Feature Report.
        /// Feature Report не требует USAGE для каждого байта — Windows принимает без ошибок.
        /// </summary>
        public bool SendCommand(byte cmd)
        {
            if (!IsConnected) return false;
            try
            {
                // ✅ ИСПРАВЛЕНО: Feature Report через WriteFeatureData
                // buf[0] = Report ID (HidLibrary добавляет автоматически при WriteFeatureData)
                // buf[1] = команда
                byte[] buf = new byte[2];
                buf[0] = REPID_FEATURE;  // Report ID = 0x05
                buf[1] = cmd;            // Команда: 0x01/0x02/0x03

                bool ok = _device.WriteFeatureData(buf);
                LastInfo = $"FeatureWrite cmd=0x{cmd:X2} ok={ok} (buf={BitConverter.ToString(buf)})";
                return ok;
            }
            catch (Exception ex)
            {
                LastInfo = ex.Message;
                return false;
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
            set { if (_isActive != value) { _isActive = value; _animTimer.Start(); } }
        }

        public event EventHandler<bool> StateChanged;

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
                StateChanged?.Invoke(this, _isActive);
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
    // Главная форма
    // ==========================================================================
    public class MainForm : Form
    {
        private readonly KeyEmuDevice _dev = new KeyEmuDevice();
        private ToggleButton _btn;
        private Label _lblDevice, _lblState, _lblInfo;
        private System.Windows.Forms.Timer _watchTimer;
        private bool _connected = false;

        public MainForm() { BuildUI(); StartWatcher(); }

        private void BuildUI()
        {
            Text = "KEApp";
            Size = new Size(340, 490);
            MinimumSize = MaximumSize = new Size(340, 490);
            BackColor = Color.FromArgb(20, 20, 28);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            Add(MakeLabel("KEY EMU", new Font("Segoe UI", 20f, FontStyle.Bold), Color.FromArgb(225, 225, 240), 0, 18, 340, 44, ContentAlignment.MiddleCenter));
            Add(MakeLabel("ATtiny88 · MH-Tiny", new Font("Segoe UI", 9f), Color.FromArgb(90, 90, 110), 0, 60, 340, 22, ContentAlignment.MiddleCenter));

            _lblDevice = MakeLabel("● Устройство не найдено", new Font("Segoe UI", 9f), Color.FromArgb(200, 75, 75), 0, 88, 340, 24, ContentAlignment.MiddleCenter);
            Add(_lblDevice);

            Add(new Panel { BackColor = Color.FromArgb(42, 42, 56), Height = 1, Width = 260, Top = 118, Left = 40 });

            _btn = new ToggleButton { Left = (340 - 180) / 2, Top = 134, Enabled = false };
            _btn.StateChanged += OnToggle;
            Add(_btn);

            _lblState = MakeLabel("ВЫКЛЮЧЕНО", new Font("Segoe UI", 14f, FontStyle.Bold), Color.FromArgb(95, 95, 115), 0, 326, 340, 36, ContentAlignment.MiddleCenter);
            Add(_lblState);

            var panel = new Panel { BackColor = Color.FromArgb(30, 30, 42), Width = 280, Height = 68, Top = 374, Left = 30 };
            RoundRegion(panel, 10);
            _lblInfo = MakeLabel("Подключите плату к USB", new Font("Segoe UI", 8.5f), Color.FromArgb(115, 115, 135), 0, 0, 280, 68, ContentAlignment.MiddleCenter);
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
                SetInfo("VID: 0x1781 PID: 0x24AB\nГотово к работе");
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

        private async void OnToggle(object sender, bool active)
        {
            SetState(active);
            byte cmd = active ? (byte)0x01 : (byte)0x02;

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _watchTimer?.Stop();
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
