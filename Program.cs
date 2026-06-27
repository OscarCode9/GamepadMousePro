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

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA {
        public int cbSize;
        public Guid interfaceClassGuid;
        public int flags;
        public IntPtr reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES {
        public int cbSize;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    // P/Invoke Imports
    [DllImport("winmm.dll")]
    public static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern void HidD_GetGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    // Win32 Constants
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

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    // Configuración ajustable
    private static int Deadzone = 8000;
    private static double MaxSpeedLeft = 32.0;
    private static double ScrollSpeedScale = 35.0; 
    private static int PollIntervalMs = 12;

    // Control de color LED
    public static byte CurrentR = 0;
    public static byte CurrentG = 0;
    public static byte CurrentB = 255;
    public static bool LedUpdatePending = true;

    private static double _scrollAccumulatorY = 0;
    private static double _scrollAccumulatorX = 0;

    private static Thread _pollThread;
    private static bool _running = false;

    // UI Elements
    private Panel headerPanel;
    private Label titleLabel;
    private Button closeButton;
    private Button toggleButton;
    private Label statusLabel;
    private TrackBar speedTrackBar;
    private Label speedValLabel;
    private TrackBar scrollTrackBar;
    private Label scrollValLabel;
    private ComboBox ledComboBox;
    private System.Windows.Forms.Timer uiTimer;

    public GamepadMouseForm() {
        this.Width = 580;
        this.Height = 385;
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.FromArgb(30, 30, 46); // Catppuccin Mocha Dark
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Gamepad Mouse Controller";
        this.DoubleBuffered = true;

        // Custom Title Bar
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
        closeButton.BackColor = Color.FromArgb(243, 139, 168);
        closeButton.FlatStyle = FlatStyle.Flat;
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Size = new Size(30, 30);
        closeButton.Location = new Point(this.Width - 45, 12);
        closeButton.Cursor = Cursors.Hand;
        closeButton.Click += (s, e) => this.Close();

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(closeButton);
        this.Controls.Add(headerPanel);

        // LEFT COLUMN (Control Panel)

        // Status Card
        Panel statusCard = new Panel();
        statusCard.Size = new Size(260, 60);
        statusCard.Location = new Point(20, 75);
        statusCard.BackColor = Color.FromArgb(24, 24, 37);
        
        statusLabel = new Label();
        statusLabel.Text = "Control: Desconectado";
        statusLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        statusLabel.ForeColor = Color.FromArgb(243, 139, 168);
        statusLabel.Location = new Point(10, 20);
        statusLabel.AutoSize = true;
        statusCard.Controls.Add(statusLabel);

        toggleButton = new Button();
        toggleButton.Text = "Conectar";
        toggleButton.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        toggleButton.ForeColor = Color.White;
        toggleButton.BackColor = Color.FromArgb(137, 180, 250);
        toggleButton.FlatStyle = FlatStyle.Flat;
        toggleButton.FlatAppearance.BorderSize = 0;
        toggleButton.Size = new Size(95, 34);
        toggleButton.Location = new Point(155, 13);
        toggleButton.Cursor = Cursors.Hand;
        toggleButton.Click += ToggleButton_Click;
        statusCard.Controls.Add(toggleButton);

        this.Controls.Add(statusCard);

        // Speed Trackbar
        Label speedLabel = new Label();
        speedLabel.Text = "Velocidad del Cursor:";
        speedLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        speedLabel.ForeColor = Color.FromArgb(205, 214, 244);
        speedLabel.Location = new Point(20, 155);
        speedLabel.AutoSize = true;
        this.Controls.Add(speedLabel);

        speedTrackBar = new TrackBar();
        speedTrackBar.Minimum = 5;
        speedTrackBar.Maximum = 60;
        speedTrackBar.Value = (int)MaxSpeedLeft;
        speedTrackBar.Size = new Size(210, 45);
        speedTrackBar.Location = new Point(20, 180);
        speedTrackBar.TickStyle = TickStyle.None;
        speedTrackBar.Scroll += SpeedTrackBar_Scroll;
        this.Controls.Add(speedTrackBar);

        speedValLabel = new Label();
        speedValLabel.Text = speedTrackBar.Value.ToString();
        speedValLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        speedValLabel.ForeColor = Color.FromArgb(137, 180, 250);
        speedValLabel.Location = new Point(240, 180);
        speedValLabel.AutoSize = true;
        this.Controls.Add(speedValLabel);

        // Scroll Trackbar
        Label scrollLabel = new Label();
        scrollLabel.Text = "Velocidad de Scroll:";
        scrollLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        scrollLabel.ForeColor = Color.FromArgb(205, 214, 244);
        scrollLabel.Location = new Point(20, 235);
        scrollLabel.AutoSize = true;
        this.Controls.Add(scrollLabel);

        scrollTrackBar = new TrackBar();
        scrollTrackBar.Minimum = 10;
        scrollTrackBar.Maximum = 80;
        scrollTrackBar.Value = (int)ScrollSpeedScale;
        scrollTrackBar.Size = new Size(210, 45);
        scrollTrackBar.Location = new Point(20, 260);
        scrollTrackBar.TickStyle = TickStyle.None;
        scrollTrackBar.Scroll += ScrollTrackBar_Scroll;
        this.Controls.Add(scrollTrackBar);

        scrollValLabel = new Label();
        scrollValLabel.Text = scrollTrackBar.Value.ToString();
        scrollValLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        scrollValLabel.ForeColor = Color.FromArgb(137, 180, 250);
        scrollValLabel.Location = new Point(240, 260);
        scrollValLabel.AutoSize = true;
        this.Controls.Add(scrollValLabel);


        // RIGHT COLUMN (LED & Current Mapping View)

        // LED Color Selector
        Label ledLabel = new Label();
        ledLabel.Text = "Color de Luz LED:";
        ledLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        ledLabel.ForeColor = Color.FromArgb(205, 214, 244);
        ledLabel.Location = new Point(300, 75);
        ledLabel.AutoSize = true;
        this.Controls.Add(ledLabel);

        ledComboBox = new ComboBox();
        ledComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        ledComboBox.Items.AddRange(new string[] { "Azul (Original)", "Apagado", "Rojo", "Verde", "Morado", "Amarillo", "Celeste", "Rosado", "Blanco" });
        ledComboBox.SelectedIndex = 0;
        ledComboBox.Size = new Size(260, 25);
        ledComboBox.Location = new Point(300, 98);
        ledComboBox.BackColor = Color.FromArgb(24, 24, 37);
        ledComboBox.ForeColor = Color.FromArgb(205, 214, 244);
        ledComboBox.FlatStyle = FlatStyle.Flat;
        ledComboBox.SelectedIndexChanged += LedComboBox_SelectedIndexChanged;
        this.Controls.Add(ledComboBox);

        // Current Mapping GroupBox
        GroupBox mappingGroup = new GroupBox();
        mappingGroup.Text = "Mapeo de Controles";
        mappingGroup.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        mappingGroup.ForeColor = Color.FromArgb(137, 180, 250);
        mappingGroup.Size = new Size(260, 180);
        mappingGroup.Location = new Point(300, 140);
        mappingGroup.FlatStyle = FlatStyle.Flat;

        Label mapListLabel = new Label();
        mapListLabel.Text = "🕹️ Stick Izq.  ->  Mover Mouse\n" +
                           "🕹️ Stick Der.  ->  Scroll (V/H)\n" +
                           "❌ Cruz / R1   ->  Clic Izquierdo\n" +
                           "⭕ Círculo / L1 ->  Clic Derecho\n" +
                           "🔲 Cuadrado    ->  Clic Central\n" +
                           "🔺 Triángulo   ->  Abrir Chrome\n" +
                           "🎛️ D-pad V     ->  Volumen +/-";
        mapListLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Regular);
        mapListLabel.ForeColor = Color.FromArgb(205, 214, 244);
        mapListLabel.Location = new Point(15, 25);
        mapListLabel.Size = new Size(230, 145);
        mappingGroup.Controls.Add(mapListLabel);
        this.Controls.Add(mappingGroup);

        // Status Checking Timer
        uiTimer = new System.Windows.Forms.Timer();
        uiTimer.Interval = 1000;
        uiTimer.Tick += UiTimer_Tick;
        uiTimer.Start();

        // Footer Branding
        Label footerLabel = new Label();
        footerLabel.Text = "Desarrollado con ♥ nativamente en Windows";
        footerLabel.Font = new Font("Segoe UI", 8, FontStyle.Italic);
        footerLabel.ForeColor = Color.FromArgb(108, 112, 134);
        footerLabel.Size = new Size(this.Width, 20);
        footerLabel.Location = new Point(0, 355);
        footerLabel.TextAlign = ContentAlignment.MiddleCenter;
        this.Controls.Add(footerLabel);

        // Auto-connect if controller is connected
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
        joyInfo.dwFlags = 1; 
        int result = joyGetPosEx(0, ref joyInfo);

        if (result == 0) {
            if (!_running) {
                statusLabel.Text = "Control: Detectado";
                statusLabel.ForeColor = Color.FromArgb(166, 227, 161);
                toggleButton.Enabled = true;
                toggleButton.BackColor = Color.FromArgb(137, 180, 250);
            }
            return true;
        } else {
            if (_running) {
                StopControllerMapping();
            }
            statusLabel.Text = "Control: Desconectado";
            statusLabel.ForeColor = Color.FromArgb(243, 139, 168);
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
        statusLabel.ForeColor = Color.FromArgb(166, 227, 161);
        toggleButton.Text = "Desconectar";
        toggleButton.BackColor = Color.FromArgb(243, 139, 168);
        
        LedUpdatePending = true;
    }

    private void StopControllerMapping() {
        if (!_running) return;
        _running = false;
        if (_pollThread != null && _pollThread.IsAlive) {
            _pollThread.Join(200);
        }

        statusLabel.Text = "Estado: Desconectado";
        statusLabel.ForeColor = Color.FromArgb(203, 166, 247);
        toggleButton.Text = "Conectar";
        toggleButton.BackColor = Color.FromArgb(166, 227, 161);
        CheckControllerConnection();
    }

    private void LedComboBox_SelectedIndexChanged(object sender, EventArgs e) {
        UpdateLedColor();
    }

    private void UpdateLedColor() {
        if (ledComboBox == null) return;
        string color = ledComboBox.SelectedItem.ToString();
        byte r = 0, g = 0, b = 255;
        switch (color) {
            case "Apagado": r = 0; g = 0; b = 0; break;
            case "Rojo": r = 255; g = 0; b = 0; break;
            case "Verde": r = 0; g = 255; b = 0; break;
            case "Morado": r = 128; g = 0; b = 128; break;
            case "Amarillo": r = 255; g = 255; b = 0; break;
            case "Celeste": r = 0; g = 255; b = 255; break;
            case "Rosado": r = 255; g = 20; b = 147; break;
            case "Blanco": r = 255; g = 255; b = 255; break;
            default: r = 0; g = 0; b = 255; break; // Azul
        }
        CurrentR = r;
        CurrentG = g;
        CurrentB = b;
        LedUpdatePending = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        StopControllerMapping();
        base.OnFormClosing(e);
    }

    // Gamepad reader loop
    private static void GamepadLoop() {
        JOYINFOEX joyInfo = new JOYINFOEX();
        joyInfo.dwSize = Marshal.SizeOf(joyInfo);
        joyInfo.dwFlags = 255; 

        bool lastLeftPressed = false;
        bool lastRightPressed = false;
        bool lastMiddlePressed = false;
        bool lastTrianglePressed = false;
        int volumeCooldown = 0;

        while (_running) {
            int result = joyGetPosEx(0, ref joyInfo);
            if (result == 0) {
                // Update LED color if requested
                if (LedUpdatePending) {
                    SetDS4Led(CurrentR, CurrentG, CurrentB);
                    LedUpdatePending = false;
                }

                // --- STICK IZQUIERDO (Mouse movement) ---
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

                // --- STICK DERECHO (Scroll wheel) ---
                int rdxRaw = joyInfo.dwZpos - 32767;
                int rdyRaw = joyInfo.dwRpos - 32767;

                if (Math.Abs(rdyRaw) > Deadzone) {
                    double val = (double)(rdyRaw) / maxRange;
                    if (val > 1.0) val = 1.0;
                    else if (val < -1.0) val = -1.0;
                    _scrollAccumulatorY += -val * ScrollSpeedScale;
                } else {
                    _scrollAccumulatorY = 0;
                }

                if (Math.Abs(rdxRaw) > Deadzone) {
                    double val = (double)(rdxRaw) / maxRange;
                    if (val > 1.0) val = 1.0;
                    else if (val < -1.0) val = -1.0;
                    _scrollAccumulatorX += val * ScrollSpeedScale;
                } else {
                    _scrollAccumulatorX = 0;
                }

                if (Math.Abs(_scrollAccumulatorY) >= 120.0) {
                    int clicks = (int)(_scrollAccumulatorY / 120.0);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, clicks * 120, 0);
                    _scrollAccumulatorY -= clicks * 120;
                }

                if (Math.Abs(_scrollAccumulatorX) >= 120.0) {
                    int clicks = (int)(_scrollAccumulatorX / 120.0);
                    mouse_event(MOUSEEVENTF_HWHEEL, 0, 0, clicks * 120, 0);
                    _scrollAccumulatorX -= clicks * 120;
                }

                // --- MAPEO DE BOTONES ---
                bool leftPressed = (joyInfo.dwButtons & 2) != 0 || (joyInfo.dwButtons & 32) != 0; // Cruz o R1
                bool rightPressed = (joyInfo.dwButtons & 4) != 0 || (joyInfo.dwButtons & 16) != 0; // Círculo o L1
                bool middlePressed = (joyInfo.dwButtons & 1) != 0; // Cuadrado
                bool trianglePressed = (joyInfo.dwButtons & 8) != 0; // Triángulo

                // Left click
                if (leftPressed && !lastLeftPressed) {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                } else if (!leftPressed && lastLeftPressed) {
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }

                // Right click
                if (rightPressed && !lastRightPressed) {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                } else if (!rightPressed && lastRightPressed) {
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                }

                // Middle click
                if (middlePressed && !lastMiddlePressed) {
                    mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                } else if (!middlePressed && lastMiddlePressed) {
                    mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                }

                // Launch Chrome on Triangle Press (Single instance check)
                if (trianglePressed && !lastTrianglePressed) {
                    try {
                        System.Diagnostics.Process.Start("chrome.exe");
                    } catch {
                        try {
                            System.Diagnostics.Process.Start("cmd.exe", "/c start chrome");
                        } catch {}
                    }
                }

                lastLeftPressed = leftPressed;
                lastRightPressed = rightPressed;
                lastMiddlePressed = middlePressed;
                lastTrianglePressed = trianglePressed;

                // --- DPAD (Volume Up / Down) ---
                if (joyInfo.dwPOV == 0) { // Cruceta Arriba -> Subir Volumen
                    if (volumeCooldown <= 0) {
                        keybd_event(0xAF, 0, 0, 0); // VK_VOLUME_UP down
                        keybd_event(0xAF, 0, 2, 0); // VK_VOLUME_UP up
                        volumeCooldown = 8; // Cooldown frames (~100ms)
                    } else {
                        volumeCooldown--;
                    }
                } else if (joyInfo.dwPOV == 18000) { // Cruceta Abajo -> Bajar Volumen
                    if (volumeCooldown <= 0) {
                        keybd_event(0xAE, 0, 0, 0); // VK_VOLUME_DOWN down
                        keybd_event(0xAE, 0, 2, 0); // VK_VOLUME_DOWN up
                        volumeCooldown = 8;
                    } else {
                        volumeCooldown--;
                    }
                } else {
                    if (volumeCooldown > 0) volumeCooldown--;
                }
            }
            Thread.Sleep(PollIntervalMs);
        }
    }

    // Direct DualShock 4 HID LED Control Method
    private static void SetDS4Led(byte r, byte g, byte b) {
        try {
            Guid hidGuid;
            HidD_GetGuid(out hidGuid);
            IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (hDevInfo == (IntPtr)(-1)) return;

            SP_DEVICE_INTERFACE_DATA interfaceData = new SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = Marshal.SizeOf(interfaceData);
            uint index = 0;

            while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, index++, ref interfaceData)) {
                uint requiredSize = 0;
                SetupDiGetDeviceInterfaceDetail(hDevInfo, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
                if (requiredSize == 0) continue;

                IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
                Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);

                if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero)) {
                    IntPtr pathPtr = new IntPtr(detailData.ToInt64() + 4);
                    string path = Marshal.PtrToStringAuto(pathPtr);

                    // Open HID handle
                    IntPtr handle = CreateFile(path, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (handle != (IntPtr)(-1)) {
                        HIDD_ATTRIBUTES attrs = new HIDD_ATTRIBUTES();
                        attrs.cbSize = Marshal.SizeOf(attrs);

                        if (HidD_GetAttributes(handle, ref attrs)) {
                            // Check if Sony DualShock 4 (VID = 0x054C, PID = 0x05C4 or 0x09CC)
                            if (attrs.VendorID == 0x054C && (attrs.ProductID == 0x05C4 || attrs.ProductID == 0x09CC)) {
                                SendDS4LedReport(handle, r, g, b);
                            }
                        }
                        CloseHandle(handle);
                    }
                }
                Marshal.FreeHGlobal(detailData);
            }
            SetupDiDestroyDeviceInfoList(hDevInfo);
        } catch {
            // Silently ignore to avoid crash
        }
    }

    private static void SendDS4LedReport(IntPtr handle, byte r, byte g, byte b) {
        // USB Output Report (ID 0x05)
        byte[] usbBuf = new byte[32];
        usbBuf[0] = 0x05;
        usbBuf[1] = 0xFF; // Update flag
        usbBuf[4] = r;
        usbBuf[5] = g;
        usbBuf[6] = b;
        usbBuf[7] = 0xFF; // Flash On Time (255 = solid)
        usbBuf[8] = 0x00; // Flash Off Time (0 = solid)

        uint written = 0;
        WriteFile(handle, usbBuf, (uint)usbBuf.Length, out written, IntPtr.Zero);

        // Bluetooth Output Report (ID 0x11)
        byte[] btBuf = new byte[78];
        btBuf[0] = 0x11;
        btBuf[1] = 0xC0; // Flag
        btBuf[3] = 0x0F; // Enable LED and rumble
        btBuf[6] = r;
        btBuf[7] = g;
        btBuf[8] = b;
        btBuf[9] = 0xFF;  // Solid light
        btBuf[10] = 0x00; // No flash

        // Calculate CRC32 for Bluetooth report
        byte[] crcCalcBuf = new byte[75];
        crcCalcBuf[0] = 0xA2; // Bluetooth Output transaction header
        Array.Copy(btBuf, 0, crcCalcBuf, 1, 74);

        uint crc = CalculateCRC32(crcCalcBuf, crcCalcBuf.Length);

        // Set CRC32 at the end of output report
        btBuf[74] = (byte)(crc & 0xFF);
        btBuf[75] = (byte)((crc >> 8) & 0xFF);
        btBuf[76] = (byte)((crc >> 16) & 0xFF);
        btBuf[77] = (byte)((crc >> 24) & 0xFF);

        WriteFile(handle, btBuf, (uint)btBuf.Length, out written, IntPtr.Zero);
    }

    private static uint CalculateCRC32(byte[] buffer, int length) {
        uint crc = 0xffffffff;
        for (int i = 0; i < length; i++) {
            byte b = buffer[i];
            for (int j = 0; j < 8; j++) {
                if (((crc ^ (b >> j)) & 1) != 0) {
                    crc = (crc >> 1) ^ 0xedb88320;
                } else {
                    crc = crc >> 1;
                }
            }
        }
        return ~crc;
    }

    [STAThread]
    public static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GamepadMouseForm());
    }
}
