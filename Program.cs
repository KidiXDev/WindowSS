using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowSS
{
    internal class Program
    {
        // Existing imports
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        static extern int BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        // Low-level keyboard hook imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        // Constants
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_F12 = 0x7B; // Virtual key code for F12

        // Delegate and hook variables
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        // Structs
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Low-level keyboard hook callback
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Debug output to see key presses
                Console.WriteLine($"Key pressed: {hookStruct.vkCode:X}");

                if (hookStruct.vkCode == VK_F12)
                {
                    Console.WriteLine("\n--- F12 pressed! Taking screenshot... ---");

                    // Use a separate thread to avoid blocking the hook
                    Thread captureThread = new Thread(new ThreadStart(CaptureFocusedWindow));
                    captureThread.Start();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        static Bitmap CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("Invalid window handle!");
                return null;
            }

            // Get window title for logging
            int length = GetWindowTextLength(hWnd);
            if (length > 0)
            {
                System.Text.StringBuilder windowTitle = new System.Text.StringBuilder(length + 1);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                Console.WriteLine($"Capturing window: {windowTitle}");
            }

            // Get window area (including title bar and border)
            GetWindowRect(hWnd, out RECT windowRect);
            int windowWidth = windowRect.Right - windowRect.Left;
            int windowHeight = windowRect.Bottom - windowRect.Top;

            // Get client area (window content without frame and title bar)
            GetClientRect(hWnd, out RECT clientRect);
            int clientWidth = clientRect.Right - clientRect.Left;
            int clientHeight = clientRect.Bottom - clientRect.Top;

            if (clientWidth == 0 || clientHeight == 0)
            {
                Console.WriteLine("Client area not found or has size 0x0!\nHint: might be because it's running in exclusive fullscreen mode");
                return null;
            }

            Console.WriteLine($"Window dimensions: {windowWidth}x{windowHeight}");
            Console.WriteLine($"Client dimensions: {clientWidth}x{clientHeight}");

            // Calculate offset from window frame to client area
            POINT clientOrigin = new POINT { X = 0, Y = 0 };
            ClientToScreen(hWnd, ref clientOrigin);

            // Calculate border and title bar area
            int borderX = clientOrigin.X - windowRect.Left;
            int borderY = clientOrigin.Y - windowRect.Top;

            if (clientOrigin.Y < 0)
            {
                Console.WriteLine("There is an anomaly with client origin outside window bounds, the window may be outside the screen.");
                return null;
            }

            Console.WriteLine($"Client origin on screen: X = {clientOrigin.X}, Y = {clientOrigin.Y}");
            Console.WriteLine($"Border offsets: X = {borderX}, Y = {borderY}");

            // Create bitmap for screenshot result
            Bitmap bmp = new Bitmap(clientWidth, clientHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdcDest = g.GetHdc();

                try
                {
                    // Use GetDC to get client area only
                    IntPtr hdcSource = GetDC(hWnd);

                    // Use coordinates 0,0 for source because GetDC already produces DC for client area
                    int result = BitBlt(hdcDest, 0, 0, clientWidth, clientHeight, hdcSource, 0, 0, CopyPixelOperation.SourceCopy);

                    if (result == 0)
                    {
                        Console.WriteLine("BitBlt failed!");
                    }
                    else
                    {
                        Console.WriteLine("BitBlt succeeded!");
                    }
                    ReleaseDC(hWnd, hdcSource);
                }
                finally
                {
                    g.ReleaseHdc(hdcDest);
                }
            }

            return bmp;
        }

        // Capture current focused window
        static void CaptureFocusedWindow()
        {
            try
            {
                // Get foreground window handle
                IntPtr hWnd = GetForegroundWindow();

                if (hWnd == IntPtr.Zero)
                {
                    Console.WriteLine("No focused window found.");
                    return;
                }

                // Get process ID from window handle
                int processId;
                GetWindowThreadProcessId(hWnd, out processId);

                Console.WriteLine($"Capturing window with process ID: {processId}");

                Bitmap screenshot = CaptureWindow(hWnd);
                if (screenshot != null)
                {
                    SaveAndShowScreenshot(screenshot);
                }
                else
                {
                    Console.WriteLine("\n=================\nFailed to take screenshot.\n=================");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing window: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static bool IsExclusiveFullscreen(IntPtr hwnd)
        {
            // Check if window exists and is visible
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return false;

            RECT windowRect = new RECT();
            if (!GetWindowRect(hwnd, out windowRect))
                return false;

            // Get the monitor where this window is displayed
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return false;

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(monitor, ref monitorInfo))
                return false;

            // Check if window covers the entire monitor area
            RECT monitorRect = monitorInfo.rcMonitor;

            // Check window style for borderless
            long style = GetWindowLong(hwnd, GWL_STYLE);
            bool borderless = (style & (WS_CAPTION | WS_THICKFRAME)) == 0;

            // Check if window has focus
            bool hasFocus = GetForegroundWindow() == hwnd;

            // Check if window covers the entire monitor
            bool coversMonitor =
                windowRect.Left == monitorRect.Left &&
                windowRect.Top == monitorRect.Top &&
                windowRect.Right == monitorRect.Right &&
                windowRect.Bottom == monitorRect.Bottom;

            return coversMonitor && borderless && hasFocus;
        }

        static void Main()
        {
            try
            {
                Console.WriteLine("Screenshot utility running with global keyboard hook...");
                Console.WriteLine("Press F12 to capture the currently focused window");
                Console.WriteLine("Press Ctrl+C or close this window to exit");

                // Set up the keyboard hook
                _hookID = SetHook(_proc);

                Console.WriteLine("Keyboard hook installed successfully");
                Console.WriteLine("Waiting for F12 key press...");

                // Keep the application running
                Application.Run(new Form()
                {
                    WindowState = FormWindowState.Minimized,
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.FixedToolWindow,
                    Text = "Screenshot Tool"
                });

                // Clean up the hook
                UnhookWindowsHookEx(_hookID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void SaveAndShowScreenshot(Bitmap screenshot)
        {
            try
            {
                // Generate timestamp for unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"screenshot_{timestamp}.png";

                screenshot.Save(filename, ImageFormat.Png);
                Console.WriteLine($"Screenshot successfully saved as {filename}!");

                // Open screenshot file
                Process.Start(new ProcessStartInfo { FileName = filename, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving screenshot: {ex.Message}");
            }
        }
    }
}