using CommunityToolkit.Mvvm.ComponentModel;
using Dolphy_AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Numerics;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Services.Store;
using Windows.UI;
using Windows.UI.WindowManagement;
using WindowsInput;
using WindowsInput.Native;
using WinRT.Interop;
using static System.Net.Mime.MediaTypeNames;
using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls.Primitives;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ChatAppGenAI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    public sealed partial class MainWindow : Window
    {
        private VM VM;
        public string tone = "neutral";
        public SizeInt32 original;
        public ColumnDefinition columndepth;
        public bool isspeak = false;
        public Brush neutralgradient;
        public Brush creativegradient;
        public Brush informativegradient;
        public SizeInt32 size;
        private static StoreContext storeContext = StoreContext.GetDefault();
        private float time = 0;
        private DispatcherTimer timer = new DispatcherTimer();

        public static class NativeMethods
        {
            [DllImport("User32.dll")]
            public static extern int GetDpiForWindow(IntPtr hWnd);
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            var action = ((Button)sender).Content.ToString();

            switch (action)
            {
                case "Translate text":
                    textbox1.Text = "Translate this text into ";
                    break;
                case "Write a story":
                    textbox1.Text = "Write a short story about ";
                    break;
                case "Summarize":
                    textbox1.Text = "Summarize this ";
                    break;
                case "Fix grammar":
                    textbox1.Text = "Correct the grammar of ";
                    break;
                case "Explain code":
                    textbox1.Text = "Explain what this code does ";
                    break;
            }
        }

        public MainWindow()

        {
            this.ExtendsContentIntoTitleBar = true;
            this.InitializeComponent();

            // ViewModel setup
            VM = new VM(DispatcherQueue);
            VM.TriggerAction += start;

            // Set window title
            this.Title = "Dolphy AI";

            // Get AppWindow handle and resize according to DPI
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            int dpi = NativeMethods.GetDpiForWindow(hWnd);
            float scaleFactor = dpi / 96f;

            // Target size in DIPs
            int targetWidthDip = 1050;
            int targetHeightDip = 550;

            // Convert to physical pixels
            int scaledWidth = (int)(targetWidthDip * scaleFactor);
            int scaledHeight = (int)(targetHeightDip * scaleFactor);

            appWindow.Resize(new SizeInt32 { Width = scaledWidth, Height = scaledHeight });

            // UI personalization
            output.Text = $"Welcome {Environment.UserName}. How can I help?";
            textbox1.Shadow = new ThemeShadow();

            textbox1.Translation += new Vector3(0, 0, 42);
            textbox2.Translation += new Vector3(0, 0, 42);
            button4.Translation += new Vector3(0, 0, 32);
            flipview1.Translation += new Vector3(0, 0, 52);
            grid1.Translation += new Vector3(0, 0, 52);
            border1.Translation += new Vector3(0, 0, 32);

            InvertedListView.MaxHeight = this.Bounds.Height - 160;

            size.Height = (int)(this.Bounds.Height * 1.5);
            size.Width = (int)this.Bounds.Width * 3;

            // Flipview and listbox setup
            neutralgradient = color123;
            creativegradient = creative.Background;
            informativegradient = informative.Background;

            creative.Background = null;
            informative.Background = null;

            flipview1.SelectedItem = creative;
            flipview1.SelectedItem = neutral;

            listbox1.SelectedIndex = 1;

            // Detect theme and apply background if needed
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            bool isDarkTheme = backgroundColor.R < 128 && backgroundColor.G < 128 && backgroundColor.B < 128;

            if (isDarkTheme)
            {
                Color obsidian = ColorHelper.FromArgb(255, 20, 24, 28);
                options.Background = new SolidColorBrush(obsidian);
            }

            // Handle window close
        }




        public static async Task<string> GetLicenseStatus()
        {
            var license = await storeContext.GetAppLicenseAsync();


            if (!license.IsActive)
                return "Expired";

            if (license.IsTrial)
                return "Trial";


            else
            {
                return "Active";
            }
        }


        private async void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (textBox.Text.Length > 0)
                {
                    VM.AddMessage(textBox.Text);
                    textBox.Text = string.Empty;
                    output.Visibility = Visibility.Collapsed;
                    listbox1.Visibility = Visibility.Collapsed;
                    VM.notacceptsmessage = true;
                    textbox1.Focus(FocusState.Pointer);
                    Gallery.Visibility = Visibility.Collapsed;
                    go.Visibility = Visibility.Collapsed;
                    stop.Visibility = Visibility.Visible;
                }
            }
        }
        public static SolidColorBrush PhiMessageTypeToColor(PhiMessageType type)
        {
            return (type == PhiMessageType.User) ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 228, 255));
        }

        public static SolidColorBrush PhiMessageTypeToForeground(PhiMessageType type)
        {
            return (type == PhiMessageType.User) ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 80, 80));
        }

        public static Visibility BoolToVisibleInversed(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            InvertedListView.MaxHeight = this.Bounds.Height - 100;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            VM.tones = combo.SelectedValue.ToString().ToLower();
        }

        private void combobox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            VM.length = combo.SelectedValue.ToString().ToLower();
        }

        private void combobox3_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo.SelectedValue is "Yes")
            {
                VM.thinkmore = true;
            }
            else
            {
                VM.thinkmore = false;
            }
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            change.Text = "🪟 Return to Window";
            reccomondation.Height = new GridLength(0);
            change.Click += button4_Click;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Get screen size
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            int screenWidth = displayArea.WorkArea.Width;
            int screenHeight = displayArea.WorkArea.Height;

            // Resize to one-third of screen width, full height
            int targetWidth = screenWidth / 3 + screenHeight / 5;
            int targetHeight = screenHeight;

            appWindow.Resize(new SizeInt32(targetWidth, targetHeight));
            appWindow.Move(new PointInt32(0, 0)); // Position at top-left

            // Make window always on top
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;

            DesktopAcrylicBackdrop acrylicBrush = new DesktopAcrylicBackdrop
            {

            };
            this.SystemBackdrop = acrylicBrush;
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            change.Text = "🗨️ Quick Chat";

            reccomondation.Height = new GridLength(70);

        
            change.Click += button3_Click;
            button4.Visibility = Visibility.Collapsed;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter.IsAlwaysOnTop = false;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsResizable = true;
            // Resize to default dimensions
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            int dpi = NativeMethods.GetDpiForWindow(hWnd);
            float scaleFactor = dpi / 96f;

            // Target size in DIPs
            int targetWidthDip = 1050;
            int targetHeightDip = 550;

            // Convert to physical pixels
            int scaledWidth = (int)(targetWidthDip * scaleFactor);
            int scaledHeight = (int)(targetHeightDip * scaleFactor);

            appWindow.Resize(new SizeInt32 { Width = scaledWidth, Height = scaledHeight });

            this.SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
        }

        private void speech_Click(object sender, RoutedEventArgs e)
        {
            textbox1.Focus(FocusState.Pointer);
            InputSimulator input = new InputSimulator();
            input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.VK_H);
        }

        private void flipview1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (flipview1.SelectedItem == neutral)
            {
                flipview1.Background = neutralgradient;
                if (VM is not null)
                {
                    VM.tones = "You are Dolphy AI in Neutral mode. Speak clearly, calmly, and respectfully. Use markdown and emojis to enhance the conversation to be more human like. Please use markdown.";
                }
            }
            if (flipview1.SelectedItem == creative)
            {
                flipview1.Background = creativegradient;
                VM.tones = " Respond playfully and expressively. Use vivid metaphors, quirky humor, and poetic language. Inspire and entertain with emojis and unexpected ideas. Use markdown and emojis to enhance the conversation.";
            }
            if (flipview1.SelectedItem == informative)
            {
                flipview1.Background = informativegradient;
                VM.tones = "You are Dolphy AI in Informative mode. Respond clearly and accurately. Use headings, bullet points, and tables when useful. Avoid emotion and humor; stay respectful and approachable. Help users understand complex topics easily. Use markup and emojis to enhance the conversation.";
            }
        }

        private void listbox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM is not null)
            {
                VM.length = listbox1.SelectedValue.ToString().ToLower();
            }
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {

            this.Close();
            await Task.Delay(500);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            new MainWindow().Activate();

        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            textbox1.Focus(FocusState.Keyboard);
            if (textbox1.Text.Length > 0)
            {
                var input = new InputSimulator();
                input.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                go.Visibility = Visibility.Collapsed;
                stop.Visibility = Visibility.Visible;
            }

        }

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            VM.phi3.streamingGenerator.RequestStop();
            go.Visibility = Visibility.Visible;
            stop.Visibility = Visibility.Collapsed;
        }

        private void go_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (go.Visibility is Visibility.Visible)
            {
                stop.Visibility = Visibility.Collapsed;
            }
            else
            {
                stop.Visibility = Visibility.Visible;
            }
        }

        public void start()
        {
            go.Visibility = Visibility.Visible;
            stop.Visibility = Visibility.Collapsed;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
        }

        welcome window = new welcome();
        private void MenuFlyoutItem_Click_2(object sender, RoutedEventArgs e)
        {
            window.Activate();

        }



        private void Bubble_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // You can make the bubble lift slightly or glow subtly here
            if (sender is FrameworkElement bubble)
            {
                bubble.Opacity = 1; // Example effect
            }
        }

        private void Bubble_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement bubble)
            {
                bubble.Opacity = 0.99; // Restore original opacity
            }
        }

        private void Border_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if(toggle.IsChecked is true)
            {
                VM.thinkdeep = true;
            }
            if (toggle.IsChecked is false)
            {
                VM.thinkdeep = false;
            }
        }
    }


    public partial class Message : ObservableObject
    {
        [ObservableProperty]
        private string text;

        public DateTime MsgDateTime { get; private set; }
        public PhiMessageType Type { get; private set; }

        public HorizontalAlignment MsgAlignment =>
            Type == PhiMessageType.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        public Brush BubbleBrush => new LinearGradientBrush
        {
            StartPoint = new Point(0.2, 0),
            EndPoint = new Point(0.8, 1),
            GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = Color.FromArgb(180, 255, 255, 255), Offset = 0.0 },
            new GradientStop
            {
                Color = Type == PhiMessageType.User
                    ? Color.FromArgb(160, 210, 240, 255)
                    : Color.FromArgb(160, 240, 240, 240),
                Offset = 1.0
            }
        }
        };

        public Brush BorderGlow => new SolidColorBrush(
            Type == PhiMessageType.User ? Colors.LightSteelBlue : Colors.Gainsboro);

        public Brush TextColor => new SolidColorBrush(
            Type == PhiMessageType.User ? Colors.Black : Colors.DarkSlateGray);

        public Message(string initialText, DateTime dateTime, PhiMessageType type)
        {
            text = initialText;
            MsgDateTime = dateTime;
            Type = type;
        }

        public override string ToString() => $"{MsgDateTime:HH:mm:ss} • {text}";
    }


    public partial class VM : ObservableObject
    {
        public ObservableCollection<Message> Messages = new();
        private MainWindow _mainWindow;

        [ObservableProperty]
        public bool acceptsMessages;
        public bool notacceptsmessage = true;
        public event Action TriggerAction;

        public Phi3Runner phi3 = new();
        public Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
        public bool thinkmore = false;
        public string tones = null;
        public string length = null;

        public string system = null;
        public double temperature = 0;
        public int maxTokens = 0;
        public bool useEmojis = false;
        public bool useBullets = false;
        public bool compactReplies = false;

        public bool custom = false;
        public bool thinkdeep = false;


        public void customs()
        {
            acceptsMessages = false;
            custom = true;
            phi3.Dispose();
            phi3.ModelLoaded += Phi3_ModelLoaded;
            phi3.temperature = temperature;
            phi3.length = maxTokens;
            phi3.InitializeAsync();
            this.dispatcherQueue = dispatcherQueue;
        }

        public VM(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        {
            phi3.ModelLoaded += Phi3_ModelLoaded;
            phi3.InitializeAsync();
            this.dispatcherQueue = dispatcherQueue;
        }

        private void Phi3_ModelLoaded(object sender, EventArgs e)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                AcceptsMessages = true;
                notacceptsmessage = false;
            });
        }

        public void AddMessage(string text)
        {
            AcceptsMessages = false;
            notacceptsmessage = true;
            Messages.Add(new Message(text, DateTime.Now, PhiMessageType.User));


            phi3.thinkdeeper = thinkdeep;


            Task.Factory.StartNew(async () =>
            {
                string promptText = text.Trim();
                string systemPrompt = (custom == true && system is not null)
                    ? system
                    : "You are Dolphy AI." + tones;

                var responseMessage = new Message("💭Thinking...", DateTime.Now, PhiMessageType.Assistant);
                dispatcherQueue.TryEnqueue(() => Messages.Add(responseMessage));

                var responseBuilder = new StringBuilder();
                var throttleBuffer = new StringBuilder(); // batching segments
                var stopwatch = Stopwatch.StartNew();
                bool firstPart = true;

                const int ThrottleIntervalMs = 20; // 🔥 Max throttle

                await foreach (var part in phi3.InferStreaming(systemPrompt, promptText))
                {
                    string segment = firstPart ? part.TrimStart() : part;
                    firstPart = false;

                    responseBuilder.Append(segment);
                    throttleBuffer.Append(segment);

                    if (stopwatch.ElapsedMilliseconds >= ThrottleIntervalMs)
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            responseMessage.Text = responseBuilder.ToString();
                        });
                        throttleBuffer.Clear();
                        stopwatch.Restart();
                    }
                }

                if (throttleBuffer.Length > 0)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        responseMessage.Text = responseBuilder.ToString();
                    });
                }

                dispatcherQueue.TryEnqueue(() =>
                {
                    AcceptsMessages = true;
                    TriggerAction?.Invoke();
                    phi3.stop = false;
                    notacceptsmessage = false;
                });
            }, TaskCreationOptions.LongRunning);
        }
    }


}


