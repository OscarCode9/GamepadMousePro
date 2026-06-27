using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

public class GamepadMouseForm : Form {
    [StructLayout(LayoutKind.Sequential)]
    public struct JOYINFOEX {
        public int dwSize;
        public int dwFlags;
        public int dwXpos;
        public int dwYpos;
        public int dwZpos;
        public int dwRpos;
        public int dwUpos;
        public int dwVpos;
        public int dwButtons;
        public int dwButtonNumber;
        public int dwPOV;
        public int dwReserved1;
        public int dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    [DllImport("winmm.dll")]
    public static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    private const int MOUSEEVENTF_LEFTDOWN = 0x02;
    private const int MOUSEEVENTF_LEFTUP = 0x04;
    private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const int MOUSEEVENTF_RIGHTUP = 0x10;
    private const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
    private const int MOUSEEVENTF_MIDDLEUP = 0x40;
    private const int MOUSEEVENTF_WHEEL = 0x0800;
    private const int MOUSEEVENTF_HWHEEL = 0x01000;

    // Configuración ajustable
    private static int Deadzone = 8000;
    private static double MaxSpeedLeft = 32.0;
    private static double ScrollSpeedScale = 35.0; // Multiplicador para scroll
    private static int PollIntervalMs = 12;

    private static double _scrollAccumulatorY = 0;
    private static double _scrollAccumulatorX = 0;

    private static Thread _pollThread;
    private static bool _running = false;

    // Elementos de la interfaz de usuario
    private Panel headerPanel;
    private Label titleLabel;
    private Button closeButton;
    private Button toggleButton;
    private Label statusLabel;
    private TrackBar speedTrackBar;
    private Label speedValLabel;
    private TrackBar scrollTrackBar;
    private Label scrollValLabel;
    private System.Windows.Forms.Timer uiTimer;

    public GamepadMouseForm() {
        this.Width = 400;
        this.Height = 360;
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.FromArgb(30, 30, 46); // Catppuccin Mocha style dark theme
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Gamepad Mouse Controller";
        this.DoubleBuffered = true;

        // Panel de encabezado (Barra de título personalizada)
        headerPanel = new Panel();
        headerPanel.Size = new Size(this.Width, 55);
        headerPanel.Location = new Point(0, 0);
        headerPanel.BackColor = Color.FromArgb(17, 17, 27);
        headerPanel.MouseDown += HeaderPanel_MouseDown;

        titleLabel = new Label();
        titleLabel.Text = "Gamepad Mouse Pro";
        titleLabel.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        titleLabel.ForeColor = Color.FromArgb(205, 214, 244);
        titleLabel.Location = new Point(15, 16);
        titleLabel.AutoSize = true;
        titleLabel.MouseDown += HeaderPanel_MouseDown;

        closeButton = new Button();
        closeButton.Text = "✕";
        closeButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        closeButton.ForeColor = Color.FromArgb(205, 214, 244);
        closeButton.BackColor = Color.FromArgb(243, 139, 168); // Soft Red
        closeButton.FlatStyle = FlatStyle.Flat;
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Size = new Size(30, 30);
        closeButton.Location = new Point(this.Width - 45, 12);
        closeButton.Cursor = Cursors.Hand;
        closeButton.Click += (s, e) => this.Close();

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(closeButton);
        this.Controls.Add(headerPanel);

        // Tarjeta de estado de conexión
        Panel statusCard = new Panel();
        statusCard.Size = new Size(360, 60);
        statusCard.Location = new Point(20, 75);
        statusCard.BackColor = Color.FromArgb(24, 24, 37);
        
        statusLabel = new Label();
        statusLabel.Text = "Control: No detectado";
        statusLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        statusLabel.ForeColor = Color.FromArgb(243, 139, 168);
        statusLabel.Location = new Point(15, 18);
        statusLabel.AutoSize = true;
        statusCard.Controls.Add(statusLabel);

        toggleButton = new Button();
        toggleButton.Text = "Conectar";
        toggleButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        toggleButton.ForeColor = Color.White;
        toggleButton.BackColor = Color.FromArgb(137, 180, 250); // Sky Blue
        toggleButton.FlatStyle = FlatStyle.Flat;
        toggleButton.FlatAppearance.BorderSize = 0;
        toggleButton.Size = new Size(110, 34);
        toggleButton.Location = new Point(235, 13);
        toggleButton.Cursor = Cursors.Hand;
        toggleButton.Click += ToggleButton_Click;
        statusCard.Controls.Add(toggleButton);

        this.Controls.Add(statusCard);

        // Barra de velocidad de cursor
        Label speedLabel = new Label();
        speedLabel.Text = "Velocidad del Cursor:";
        speedLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        speedLabel.ForeColor = Color.FromArgb(205, 214, 244);
        speedLabel.Location = new Point(20, 155);
        speedLabel.AutoSize = true;
        this.Controls.Add(speedLabel);

        speedTrackBar = new TrackBar();
        speedTrackBar.Minimum = 5;
        speedTrackBar.Maximum = 60;
        speedTrackBar.Value = (int)MaxSpeedLeft;
        speedTrackBar.Size = new Size(290, 45);
        speedTrackBar.Location = new Point(20, 180);
        speedTrackBar.TickStyle = TickStyle.None;
        speedTrackBar.Scroll += SpeedTrackBar_Scroll;
        this.Controls.Add(speedTrackBar);

        speedValLabel = new Label();
        speedValLabel.Text = speedTrackBar.Value.ToString();
        speedValLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        speedValLabel.ForeColor = Color.FromArgb(137, 180, 250);
        speedValLabel.Location = new Point(320, 180);
        speedValLabel.AutoSize = true;
        this.Controls.Add(speedValLabel);

        // Barra de velocidad de scroll
        Label scrollLabel = new Label();
        scrollLabel.Text = "Velocidad de Scroll:";
        scrollLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        scrollLabel.ForeColor = Color.FromArgb(205, 214, 244);
        scrollLabel.Location = new Point(20, 235);
        scrollLabel.AutoSize = true;
        this.Controls.Add(scrollLabel);

        scrollTrackBar = new TrackBar();
        scrollTrackBar.Minimum = 10;
        scrollTrackBar.Maximum = 80;
        scrollTrackBar.Value = (int)ScrollSpeedScale;
        scrollTrackBar.Size = new Size(290, 45);
        scrollTrackBar.Location = new Point(20, 260);
        scrollTrackBar.TickStyle = TickStyle.None;
        scrollTrackBar.Scroll += ScrollTrackBar_Scroll;
        this.Controls.Add(scrollTrackBar);

        scrollValLabel = new Label();
        scrollValLabel.Text = scrollTrackBar.Value.ToString();
        scrollValLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        scrollValLabel.ForeColor = Color.FromArgb(137, 180, 250);
        scrollValLabel.Location = new Point(320, 260);
        scrollValLabel.AutoSize = true;
        this.Controls.Add(scrollValLabel);

        // Timer para monitorear la conexión física del control
        uiTimer = new System.Windows.Forms.Timer();
        uiTimer.Interval = 1000;
        uiTimer.Tick += UiTimer_Tick;
        uiTimer.Start();

        // Footer
        Label footerLabel = new Label();
        footerLabel.Text = "Desarrollado con ♥ nativamente en Windows";
        footerLabel.Font = new Font("Segoe UI", 8, FontStyle.Italic);
        footerLabel.ForeColor = Color.FromArgb(108, 112, 134);
        footerLabel.Size = new Size(this.Width, 20);
        footerLabel.Location = new Point(0, 330);
        footerLabel.TextAlign = ContentAlignment.MiddleCenter;
        this.Controls.Add(footerLabel);

        if (CheckControllerConnection()) {
            StartControllerMapping();
        }
    }

    private void HeaderPanel_MouseDown(object sender, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }

    private void SpeedTrackBar_Scroll(object sender, EventArgs e) {
        MaxSpeedLeft = speedTrackBar.Value;
        speedValLabel.Text = speedTrackBar.Value.ToString();
    }

    private void ScrollTrackBar_Scroll(object sender, EventArgs e) {
        ScrollSpeedScale = scrollTrackBar.Value;
        scrollValLabel.Text = scrollTrackBar.Value.ToString();
    }

    private void ToggleButton_Click(object sender, EventArgs e) {
        if (_running) {
            StopControllerMapping();
        } else {
            StartControllerMapping();
        }
    }

    private void UiTimer_Tick(object sender, EventArgs e) {
        bool isConnected = CheckControllerConnection();
        if (isConnected && !_running) {
            StartControllerMapping();
        }
    }

    private bool CheckControllerConnection() {
        JOYINFOEX joyInfo = new JOYINFOEX();
        joyInfo.dwSize = Marshal.SizeOf(joyInfo);
        joyInfo.dwFlags = 1; // JOY_RETURNX
        int result = joyGetPosEx(0, ref joyInfo);

        if (result == 0) {
            if (!_running) {
                statusLabel.Text = "Control: Detectado";
                statusLabel.ForeColor = Color.FromArgb(166, 227, 161); // Light Green
                toggleButton.Enabled = true;
                toggleButton.BackColor = Color.FromArgb(137, 180, 250);
            }
            return true;
        } else {
            if (_running) {
                StopControllerMapping();
            }
            statusLabel.Text = "Control: Desconectado";
            statusLabel.ForeColor = Color.FromArgb(243, 139, 168); // Soft Red
            toggleButton.Enabled = false;
            toggleButton.BackColor = Color.FromArgb(88, 91, 112);
            return false;
        }
    }

    private void StartControllerMapping() {
        if (_running) return;
        _running = true;
        _pollThread = new Thread(GamepadLoop);
        _pollThread.IsBackground = true;
        _pollThread.Start();

        statusLabel.Text = "Estado: ACTIVO";
        statusLabel.ForeColor = Color.FromArgb(166, 227, 161); // Bright Green
        toggleButton.Text = "Desconectar";
        toggleButton.BackColor = Color.FromArgb(243, 139, 168); // Red
    }

    private void StopControllerMapping() {
        if (!_running) return;
        _running = false;
        if (_pollThread != null && _pollThread.IsAlive) {
            _pollThread.Join(200);
        }

        statusLabel.Text = "Estado: Desconectado";
        statusLabel.ForeColor = Color.FromArgb(203, 166, 247); // Violet
        toggleButton.Text = "Conectar";
        toggleButton.BackColor = Color.FromArgb(166, 227, 161); // Green
        CheckControllerConnection();
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        StopControllerMapping();
        base.OnFormClosing(e);
    }

    // Bucle secundario de lectura de gamepad
    private static void GamepadLoop() {
        JOYINFOEX joyInfo = new JOYINFOEX();
        joyInfo.dwSize = Marshal.SizeOf(joyInfo);
        joyInfo.dwFlags = 255; // JOY_RETURNALL

        bool lastLeftPressed = false;
        bool lastRightPressed = false;
        bool lastMiddlePressed = false;
        int dpadScrollCooldown = 0;

        while (_running) {
            int result = joyGetPosEx(0, ref joyInfo);
            if (result == 0) {
                // --- STICK IZQUIERDO (Movimiento de mouse) ---
                int dxRaw = joyInfo.dwXpos - 32767;
                int dyRaw = joyInfo.dwYpos - 32767;

                double speedX = 0;
                double speedY = 0;
                double maxRange = 32768.0 - Deadzone;

                if (Math.Abs(dxRaw) > Deadzone) {
                    double sign = Math.Sign(dxRaw);
                    double val = (Math.Abs(dxRaw) - Deadzone) / maxRange;
                    if (val > 1.0) val = 1.0;
                    speedX = sign * (val * 0.4 + val * val * 0.6) * MaxSpeedLeft;
                }
                if (Math.Abs(dyRaw) > Deadzone) {
                    double sign = Math.Sign(dyRaw);
                    double val = (Math.Abs(dyRaw) - Deadzone) / maxRange;
                    if (val > 1.0) val = 1.0;
                    speedY = sign * (val * 0.4 + val * val * 0.6) * MaxSpeedLeft;
                }

                if (speedX != 0 || speedY != 0) {
                    POINT p;
                    if (GetCursorPos(out p)) {
                        int newX = p.X + (int)Math.Round(speedX);
                        int newY = p.Y + (int)Math.Round(speedY);
                        SetCursorPos(newX, newY);
                    }
                }

                // --- STICK DERECHO (Scroll continuo) ---
                int rdxRaw = joyInfo.dwZpos - 32767;
                int rdyRaw = joyInfo.dwRpos - 32767;

                // Scroll Vertical
                if (Math.Abs(rdyRaw) > Deadzone) {
                    double val = (double)(rdyRaw) / maxRange;
                    if (val > 1.0) val = 1.0;
                    else if (val < -1.0) val = -1.0;
                    _scrollAccumulatorY += -val * ScrollSpeedScale;
                } else {
                    _scrollAccumulatorY = 0;
                }

                // Scroll Horizontal
                if (Math.Abs(rdxRaw) > Deadzone) {
                    double val = (double)(rdxRaw) / maxRange;
                    if (val > 1.0) val = 1.0;
                    else if (val < -1.0) val = -1.0;
                    _scrollAccumulatorX += val * ScrollSpeedScale;
                } else {
                    _scrollAccumulatorX = 0;
                }

                // Ejecutar scroll vertical
                if (Math.Abs(_scrollAccumulatorY) >= 120.0) {
                    int clicks = (int)(_scrollAccumulatorY / 120.0);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, clicks * 120, 0);
                    _scrollAccumulatorY -= clicks * 120;
                }

                // Ejecutar scroll horizontal
                if (Math.Abs(_scrollAccumulatorX) >= 120.0) {
                    int clicks = (int)(_scrollAccumulatorX / 120.0);
                    mouse_event(MOUSEEVENTF_HWHEEL, 0, 0, clicks * 120, 0);
                    _scrollAccumulatorX -= clicks * 120;
                }

                // --- MAPEO DE BOTONES ---
                bool leftPressed = (joyInfo.dwButtons & 2) != 0 || (joyInfo.dwButtons & 32) != 0; // Cruz o R1
                bool rightPressed = (joyInfo.dwButtons & 4) != 0 || (joyInfo.dwButtons & 16) != 0; // Círculo o L1
                bool middlePressed = (joyInfo.dwButtons & 1) != 0; // Cuadrado

                if (leftPressed && !lastLeftPressed) {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                } else if (!leftPressed && lastLeftPressed) {
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }

                if (rightPressed && !lastRightPressed) {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                } else if (!rightPressed && lastRightPressed) {
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                }

                if (middlePressed && !lastMiddlePressed) {
                    mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                } else if (!middlePressed && lastMiddlePressed) {
                    mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                }

                lastLeftPressed = leftPressed;
                lastRightPressed = rightPressed;
                lastMiddlePressed = middlePressed;

                // --- DPAD (Scroll de apoyo) ---
                if (joyInfo.dwPOV != 65535) {
                    if (dpadScrollCooldown <= 0) {
                        if (joyInfo.dwPOV == 0) {
                            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, 0);
                            dpadScrollCooldown = 12;
                        } else if (joyInfo.dwPOV == 18000) {
                            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120, 0);
                            dpadScrollCooldown = 12;
                        }
                    } else {
                        dpadScrollCooldown--;
                    }
                } else {
                    dpadScrollCooldown = 0;
                }
            }
            Thread.Sleep(PollIntervalMs);
        }
    }

    [STAThread]
    public static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GamepadMouseForm());
    }
}
