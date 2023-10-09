using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SharpHook;
using System.Numerics;
using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;

namespace Windows_Window_Position_Bug {

    public partial class App : Application {
        Window window = null;
        TaskPoolGlobalHook hook = null;
        ContentPage mainPage;
        private IntPtr hWnd = IntPtr.Zero;
        Border clickPosBorder;

#if WINDOWS
        Microsoft.UI.Windowing.AppWindow appWindow = null;
        Microsoft.UI.WindowId windowId;
        Microsoft.UI.Xaml.Window windowPlatformView;
#endif
        public App() {
            InitializeComponent();
            mainPage = new();
            this.MainPage = mainPage;

            AbsoluteLayout abs = new();
            abs.BackgroundColor = Colors.Bisque;
            mainPage.Content = abs;

            //border should follow your clicks and be centered over them, but will instead be off by the x-deviation of the window position and y-deviation of the title bar
            clickPosBorder = new();
            clickPosBorder.WidthRequest = clickPosBorder.HeightRequest = 20;
            clickPosBorder.StrokeShape = new RoundRectangle() { CornerRadius = 10 };
            clickPosBorder.StrokeThickness = 2;
            clickPosBorder.Stroke = Colors.DarkCyan;
            clickPosBorder.BackgroundColor = Colors.Red;
            abs.Children.Add(clickPosBorder);
            
        }

        protected override Window CreateWindow(IActivationState activationState) {
            if (window != null) {
                window.HandlerChanged -= windowHandlerChanged;
            }
            window = base.CreateWindow(activationState);

            window.HandlerChanged += windowHandlerChanged;
            window.Destroying += windowDestroyed;
            return window;
        }

        private void windowDestroyed(object sender, EventArgs e) {
            if (hook != null) {
                hook.MousePressed -= hookMousePressed;
                hook.Dispose();
            }
        }

        public void windowHandlerChanged(object sender, EventArgs e) {
#if WINDOWS
            if (hook == null) {
                hook = new TaskPoolGlobalHook();
                hook.RunAsync();
                hook.MousePressed += hookMousePressed;
            }
            if (window != null && window.Handler !=null) {
                windowPlatformView = (window.Handler as WindowHandler).PlatformView;
                hWnd = WinRT.Interop.WindowNative.GetWindowHandle(windowPlatformView);
                windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                //setting title name should show up in the title bar of the project but it does not, this will however show up in the name in the status bar
                appWindow.Title = "WINDOW TITLE";

                //this should move the window to 0,0 but there will be an x-deviation away from this position (glitch)
                appWindow.Move(new Windows.Graphics.PointInt32((int)0, (int)0));
                Debug.WriteLine("INITIALIZED WINDOW TO POINT: " + appWindow.Position.X + " " + appWindow.Position.Y);
            }
#endif
        }

        void hookMousePressed(object sender, MouseHookEventArgs e) {
#if WINDOWS
            MainThread.BeginInvokeOnMainThread(() => {
                if (appWindow != null) {

                    //find window position
                    Vector2 windowPos = new Vector2(appWindow.Position.X, appWindow.Position.Y); //find window position in raw piels
                    Vector2 windowPosMinusTitleBar = windowPos - new Vector2(0, appWindow.TitleBar.Height);

                    //find relative position of click
                    Vector2 rawHookClickPos = new Vector2(e.RawEvent.Mouse.X, e.RawEvent.Mouse.Y); // get raw click from mouse hook in raw pixels
                    double rasterizationScale = mainPage.ToPlatform(mainPage.Handler.MauiContext).XamlRoot.RasterizationScale; //get percent screen scaling
                    Vector2 clickRelative = rawHookClickPos - windowPosMinusTitleBar;
                    clickRelative *= 1f /(float)rasterizationScale;

                    if (clickPosBorder != null) {
                        clickPosBorder.TranslationX = clickRelative.X - clickPosBorder.Width * 0.5;
                        clickPosBorder.TranslationY = clickRelative.Y - clickPosBorder.Height * 0.5;
                    }

                    //debug output
                    string debugText = "MOUSE PRESSED";
                    debugText += "\n- windowPos: " + windowPos;
                    debugText += "\n- rawHookClickPos: " + rawHookClickPos;
                    debugText += "\n- clickRelative: " + clickRelative;
                    debugText += "\n- TitleBar.Height: " + appWindow.TitleBar.Height; //title bar height will always debug out as zero although it is clearly not zero
                    Debug.WriteLine(debugText);
                }
            });
#endif
        }
    }
}