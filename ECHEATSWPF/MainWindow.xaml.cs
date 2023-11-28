﻿using AimmyWPF.Class;
using AimmyWPF.UserController;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using AimmyAimbot;
using Class;
using Newtonsoft.Json;
using System.Diagnostics;

namespace AimmyWPF
{
    public partial class MainWindow : Window
    {
        private PredictionManager predictionManager;
        private OverlayWindow FOVOverlay;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher ConfigfileWatcher;

        private string lastLoadedModel = "N/A";
        private string lastLoadedConfig = "N/A";

        private readonly BrushConverter brushcolor = new BrushConverter();

        private int TimeSinceLastClick = 0;
        private DateTime LastClickTime = DateTime.MinValue;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001; // Movement flag

        private static int ScreenWidth = Screen.PrimaryScreen.Bounds.Width;
        private static int ScreenHeight = Screen.PrimaryScreen.Bounds.Height;

        private AIModel _onnxModel;
        private InputBindingManager bindingManager;
        private bool IsHolding_Binding = false;
        private CancellationTokenSource cts;

        private enum MenuPosition
        {
            AimMenu,
            TriggerMenu,
            SelectorMenu,
            SettingsMenu
        }

        public Dictionary<string, double> aimmySettings = new Dictionary<string, double>
        {
            { "FOV_Size", 640 },
            { "Mouse_Sens", 0.80 },
            { "Y_Offset", 0 },
            { "X_Offset", 0 },
            { "Trigger_Delay", 0.1 },
            { "AI_Min_Conf", 50 }
        };


        private Dictionary<string, bool> toggleState = new Dictionary<string, bool>
        {
            { "AimbotToggle", false },
            { "PredictionToggle", false },
            { "AimViewToggle", false },
            { "TriggerBot", false },
            { "CollectData", false },
            { "TopMost", false }
        };


        Thickness WinTooLeft = new Thickness(-1680, 0, 1680, 0);
        Thickness WinVeryLeft = new Thickness(-1120, 0, 1120, 0);
        Thickness WinLeft = new Thickness(-560, 0, 560, 0);

        Thickness WinCenter = new Thickness(0, 0, 0, 0);

        Thickness WinRight = new Thickness(560, 0, -560, 0);
        Thickness WinVeryRight = new Thickness(1120, 0, -1120, 0);
        Thickness WinTooRight = new Thickness(1680, 0, -1680, 0);

        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        public MainWindow()
        {
            InitializeComponent();

            // Check to see if certain items are installed
            RequirementsManager RM = new RequirementsManager();
            if (!RM.IsVCRedistInstalled())
            {
                System.Windows.MessageBox.Show("Visual C++ Redistributables x64 are not installed on this device, please install them before using Aimmy to avoid issues.", "Load Error");
                System.Diagnostics.Process.Start("https://aka.ms/vs/17/release/vc_redist.x64.exe");
            }

            // Check for required folders
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = { "bin", "bin/models", "bin/images", "bin/configs" };

            foreach (string dir in dirs)
            {
                string fullPath = Path.Combine(baseDir, dir);
                if (!Directory.Exists(fullPath))
                {
                    System.Windows.MessageBox.Show($"The '{dir}' folder does not exist, please ensure the folder is in the same directory as the exe.", "Load Error");
                    System.Windows.Application.Current.Shutdown();
                }
            }

            // Setup key/mouse hook
            bindingManager = new InputBindingManager();
            bindingManager.SetupDefault("Right");
            bindingManager.OnBindingPressed += (binding) => { IsHolding_Binding = true; };
            bindingManager.OnBindingReleased += (binding) => { IsHolding_Binding = false; };

            // Load settings
            if (!string.IsNullOrEmpty(AimmyWPF.Properties.Settings.Default.AppData))
            {
                foreach (var pair in AimmyWPF.Properties.Settings.Default.AppData.Split(';'))
                {
                    var parts = pair.Split('=');
                    if (aimmySettings.ContainsKey(parts[0]) && double.TryParse(parts[1], out double value))
                    {
                        aimmySettings[parts[0]] = value;
                    }
                    else if (parts[0] == "TopMost" && bool.TryParse(parts[1], out bool topMostValue))
                    {
                        toggleState["TopMost"] = topMostValue;
                        this.Topmost = topMostValue;
                    }
                }
            }

            // Load UI
            InitializeMenuPositions();
            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
            InitializeFileWatcher();
            InitializeConfigWatcher();

            // Load PredictionManager
            predictionManager = new PredictionManager();
            predictionManager.InitializeKalmanFilter();

            // Load all models into listbox
            LoadModelsIntoListBox();
            LoadConfigsIntoListBox();

            SelectorListBox.SelectionChanged += new SelectionChangedEventHandler(SelectorListBox_SelectionChanged);
            ConfigSelectorListBox.SelectionChanged += new SelectionChangedEventHandler(ConfigSelectorListBox_SelectionChanged);

            // Create FOV Overlay
            FOVOverlay = new OverlayWindow();
            FOVOverlay.Hide();
            FOVOverlay.FovSize = (int)aimmySettings["FOV_Size"];

            // Start the loop that runs the model
            Task.Run(() => StartModelCaptureLoop());
        }

        List<String> AvailableModels = new List<String>();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // This is a proof of concept atm so I yanked the code from this:
            // https://stackoverflow.com/questions/46302570/how-to-get-list-of-files-from-a-specific-github-repo-given-a-link-in-c
            // nori

            IEnumerable<string> ContentResults = await RetrieveGithubFiles.ListContents();
            foreach (var file in ContentResults)
            {
                if (!AvailableModels.Contains(file) && !System.IO.File.Exists($"bin\\models\\{file}"))
                {
                    AvailableModels.Add(file);
                }
            }

            LoadModelStoreMenu();
        }

        #region Mouse Movement / Clicking Handler

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private static double Lerp(double start, double end, double alpha)
        {
            return start + alpha * (end - start);
        }

        private async Task DoTriggerClick()
        {
            TimeSinceLastClick = (int)(DateTime.Now - LastClickTime).TotalMilliseconds;
            int Trigger_Delay_Milliseconds = (int)(aimmySettings["Trigger_Delay"] * 1000);

            if (TimeSinceLastClick >= Trigger_Delay_Milliseconds || LastClickTime == DateTime.MinValue)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(20);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                LastClickTime = DateTime.Now;
            }

            return;
        }

        private void MoveCrosshair(int detectedX, int detectedY)
        {
            double Alpha = aimmySettings["Mouse_Sens"];

            int halfScreenWidth = ScreenWidth / 2;
            int halfScreenHeight = ScreenHeight / 2;

            int moveX = (int)Lerp(0, detectedX - halfScreenWidth, 1 - Alpha);
            int moveY = (int)Lerp(0, detectedY - halfScreenHeight, 1 - Alpha);

            mouse_event(MOUSEEVENTF_MOVE, (uint)moveX, (uint)moveY, 0, 0);

            if (toggleState["TriggerBot"])
            {
                Task.Run(() => DoTriggerClick());
            }
        }

        #endregion

        #region Aim Aligner Main and Loop
        public async Task ModelCapture(bool TriggerOnly = false)
        {
            var closestPrediction = await _onnxModel.GetClosestPredictionToCenterAsync();
            if (closestPrediction == null)
            {
                return;
            }

            if (!TriggerOnly)
            {
                float scaleX = (float)ScreenWidth / 640f;
                float scaleY = (float)ScreenHeight / 640f;

                double YOffset = aimmySettings["Y_Offset"];
                double XOffset = aimmySettings["X_Offset"];
                int detectedX = (int)((closestPrediction.Rectangle.X * scaleX) + XOffset);
                int detectedY = (int)((closestPrediction.Rectangle.Y * scaleY) + YOffset);

                // Handle Prediction
                if (toggleState["PredictionToggle"])
                {
                    predictionManager.UpdateKalmanFilter(detectedX, detectedY);
                    var predictedPosition = predictionManager.GetEstimatedPosition();
                    MoveCrosshair(predictedPosition.X, predictedPosition.Y);
                }
                else 
                {
                    MoveCrosshair(detectedX, detectedY);
                }
            }
            else
            {
                Task.Run(() => DoTriggerClick());
            }
        }

        private async Task StartModelCaptureLoop()
        {
            // Create a new CancellationTokenSource
            cts = new CancellationTokenSource();

            while (!cts.Token.IsCancellationRequested)
            {
                if (toggleState["AimbotToggle"] && IsHolding_Binding)
                {
                    await ModelCapture();
                }
                else if (!toggleState["AimbotToggle"] && toggleState["TriggerBot"] && IsHolding_Binding) // Triggerbot Only
                {
                    await ModelCapture(true);
                }

                await Task.Delay(1);
            }
        }

        public void StopModelCaptureLoop()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts = null;
            }
        }
        #endregion

        #region Menu Initialization and Setup

        private void InitializeMenuPositions()
        {
            AimMenu.Margin = new Thickness(0, 0, 0, 0);
            TriggerMenu.Margin = new Thickness(560, 0, -560, 0);
            SelectorMenu.Margin = new Thickness(560, 0, -560, 0);
            SettingsMenu.Margin = new Thickness(1680, 0, -1680, 0);
        }

        private void SetupToggle(AToggle toggle, Action<bool> action, bool initialState)
        {
            toggle.Reader.Tag = initialState;
            (initialState ? (Action)(() => toggle.EnableSwitch()) : () => toggle.DisableSwitch())();

            toggle.Reader.Click += (s, x) =>
            {
                bool currentState = (bool)toggle.Reader.Tag;
                toggle.Reader.Tag = !currentState;
                SetToggleState(toggle);
                action.Invoke(!currentState);
            };
        }

        private void SetToggleState(AToggle toggle)
        {
            bool state = (bool)toggle.Reader.Tag;

            // Stop them from turning on anything until model has been selected.
            if ((toggle.Reader.Name == "AimbotToggle" || toggle.Reader.Name == "TriggerBot" || toggle.Reader.Name == "CollectData") && lastLoadedModel == "N/A")
            {
                System.Windows.MessageBox.Show("Please select a model in the Model Selector before toggling.", "Toggle Error");
                return;
            }

            (state ? (Action)(() => toggle.EnableSwitch()) : () => toggle.DisableSwitch())();

            toggleState[toggle.Reader.Name] = state;

            if (toggle.Reader.Name == "CollectData")
            {
                _onnxModel.CollectData = state;
            }
            else if (toggle.Reader.Name == "ShowFOV")
            {
                (state ? (Action)(() => FOVOverlay.Show()) : () => FOVOverlay.Hide())();
            }
            else if (toggle.Reader.Name == "TopMost")
            {
                this.Topmost = state;
            }
        }

        #endregion

        #region Menu Controls

        private async void Selection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button clickedButton)
            {
                MenuPosition position = (MenuPosition)Enum.Parse(typeof(MenuPosition), clickedButton.Tag.ToString());
                ResetMenuColors();
                clickedButton.Foreground = (System.Windows.Media.Brush)brushcolor.ConvertFromString("#3e8fb0");
                ApplyMenuAnimations(position);
                UpdateMenuVisibility(position);
            }
        }

        private void ResetMenuColors()
        {
            Selection1.Foreground = Selection2.Foreground = Selection3.Foreground = Selection4.Foreground =
                (System.Windows.Media.Brush)brushcolor.ConvertFromString("#ffffff");
        }

        private void ApplyMenuAnimations(MenuPosition position)
        {
            Thickness highlighterMargin = new Thickness(0, 30, 414, 0);
            switch (position)
            {
                case MenuPosition.AimMenu:
                    highlighterMargin = new Thickness(10, 30, 414, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinVeryRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinTooRight);
                    break;

                case MenuPosition.TriggerMenu:
                    highlighterMargin = new Thickness(144, 30, 278, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinVeryRight);
                    break;

                case MenuPosition.SelectorMenu:
                    highlighterMargin = new Thickness(280, 30, 144, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinRight);
                    break;

                case MenuPosition.SettingsMenu:
                    highlighterMargin = new Thickness(414, 30, 10, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinTooLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinCenter);
                    break;
            }
        }

        private void UpdateMenuVisibility(MenuPosition position)
        {
            AimMenu.Visibility = (position == MenuPosition.AimMenu) ? Visibility.Visible : Visibility.Collapsed;
            TriggerMenu.Visibility = (position == MenuPosition.TriggerMenu) ? Visibility.Visible : Visibility.Collapsed;
            SelectorMenu.Visibility = (position == MenuPosition.SelectorMenu) ? Visibility.Visible : Visibility.Collapsed;
            SettingsMenu.Visibility = (position == MenuPosition.SettingsMenu) ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion
        
        #region More Info Function
        public void ActivateMoreInfo(string info)
        {
            SetMenuState(false);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(1000), MoreInfoBox, MoreInfoBox.Margin, new Thickness(5, 0, 5, 5));
            MoreInfoBox.Visibility = Visibility.Visible;
            InfoText.Text = info;
        }

        private async void MoreInfoExit_Click(object sender, RoutedEventArgs e)
        {
            Animator.ObjectShift(TimeSpan.FromMilliseconds(1000), MoreInfoBox, MoreInfoBox.Margin, new Thickness(5, 0, 5, -180));
            await Task.Delay(1000);
            MoreInfoBox.Visibility = Visibility.Collapsed;
            SetMenuState(true);
        }

        private void SetMenuState(bool state)
        {
            AimMenu.IsEnabled = state;
            TriggerMenu.IsEnabled = state;
            SelectorMenu.IsEnabled = state;
            SettingsMenu.IsEnabled = state;
        }
        #endregion

        void LoadAimMenu()
        {
            AToggle Enable_AIAimAligner = new AToggle(this, "Enable AI Aim Aligner", 
                "This will enable the AI's ability to align the aim.");
            Enable_AIAimAligner.Reader.Name = "AimbotToggle";
            SetupToggle(Enable_AIAimAligner, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Enable_AIAimAligner);

            AKeyChanger Change_KeyPress = new AKeyChanger("Change Keybind", "Right");
            Change_KeyPress.Reader.Click += (s, x) =>
            {
                Change_KeyPress.KeyNotifier.Content = "Listening..";
                bindingManager.StartListeningForBinding();
            };

            bindingManager.OnBindingSet += (binding) =>
            {
                Change_KeyPress.KeyNotifier.Content = binding;
            };

            AimScroller.Children.Add(Change_KeyPress);

            AToggle Enable_AIPredictions = new AToggle(this, "Enable Predictions",
               "This will use a KalmanFilter algorithm to predict aim patterns for better tracing of enemies.");
            Enable_AIPredictions.Reader.Name = "PredictionToggle";
            SetupToggle(Enable_AIPredictions, state => Bools.AIPredictions = state, Bools.AIPredictions);
            AimScroller.Children.Add(Enable_AIPredictions);

            AToggle Show_FOV = new AToggle(this, "Show FOV", 
                "This will show a circle around your screen that show what the AI is considering on the screen at a given moment.");
            Show_FOV.Reader.Name = "ShowFOV";
            SetupToggle(Show_FOV, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Show_FOV);

            ASlider FovSlider = new ASlider(this, "FOV Size", "Size of FOV",
                "This setting controls how much of your screen is considered in the AI's decision making and how big the circle on your screen will be.",
                1);

            FovSlider.Slider.Minimum = 10;
            FovSlider.Slider.Maximum = 640;
            FovSlider.Slider.Value = aimmySettings["FOV_Size"];
            FovSlider.Slider.TickFrequency = 1;
            FovSlider.Slider.ValueChanged += (s, x) =>
            {
                double FovSize = FovSlider.Slider.Value;
                aimmySettings["FOV_Size"] = FovSize;
                if (_onnxModel != null)
                {
                    _onnxModel.FovSize = (int)FovSize;
                }

                // Update the overlay's FOV size
                FOVOverlay.FovSize = (int)FovSize;
            };

            AimScroller.Children.Add(FovSlider);

            ASlider MouseSensitivty = new ASlider(this, "Mouse Sensitivty", "Sensitivty",
                "This setting controls how fast your mouse moves to a detection, if it moves too fast you need to set it to a higher number.",
                0.01);

            MouseSensitivty.Slider.Minimum = 0.01;
            MouseSensitivty.Slider.Maximum = 1;
            MouseSensitivty.Slider.Value = aimmySettings["Mouse_Sens"];
            MouseSensitivty.Slider.TickFrequency = 0.01;
            MouseSensitivty.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Mouse_Sens"] = MouseSensitivty.Slider.Value;
            };

            AimScroller.Children.Add(MouseSensitivty);

            ASlider YOffset = new ASlider(this, "Y Offset (Up/Down)", "Offset",
                "This setting controls how high / low you aim. A lower number will result in a higher aim. A higher number will result in a lower aim.", 
                1);

            YOffset.Slider.Minimum = -150;
            YOffset.Slider.Maximum = 150;
            YOffset.Slider.Value = aimmySettings["Y_Offset"];
            YOffset.Slider.TickFrequency = 1;
            YOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Y_Offset"] = YOffset.Slider.Value;
            };

            AimScroller.Children.Add(YOffset);

            ASlider XOffset = new ASlider(this, "X Offset (Left/Right)", "Offset",
                "This setting controls which way your aim leans. A lower number will result in an aim that leans to the left. A higher number will result in an aim that leans to the right",
                1);

            XOffset.Slider.Minimum = -150;
            XOffset.Slider.Maximum = 150;
            XOffset.Slider.Value = aimmySettings["X_Offset"];
            XOffset.Slider.TickFrequency = 1;
            XOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["X_Offset"] = XOffset.Slider.Value;
            };

            AimScroller.Children.Add(XOffset);
        }

        void LoadTriggerMenu()
        {
            AToggle Enable_TriggerBot = new AToggle(this, "Enable Auto Trigger",
                "This will enable the AI's ability to shoot whenever it sees a target.");
            Enable_TriggerBot.Reader.Name = "TriggerBot";
            SetupToggle(Enable_TriggerBot, state => Bools.Triggerbot = state, Bools.Triggerbot);
            TriggerScroller.Children.Add(Enable_TriggerBot);

            ASlider TriggerBot_Delay = new ASlider(this, "Auto Trigger Delay", "Seconds", 
                "This slider will control how many miliseconds it will take to initiate a trigger.",
                0.1);

            TriggerBot_Delay.Slider.Minimum = 0.01;
            TriggerBot_Delay.Slider.Maximum = 1;
            TriggerBot_Delay.Slider.Value = aimmySettings["Trigger_Delay"];
            TriggerBot_Delay.Slider.TickFrequency = 0.01;
            TriggerBot_Delay.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Trigger_Delay"] = TriggerBot_Delay.Slider.Value;
            };

            TriggerScroller.Children.Add(TriggerBot_Delay);

        }

        private void FileWatcher_Reload(object sender, FileSystemEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                LoadModelsIntoListBox();
                InitializeModel();
            });
        }

        private void InitializeFileWatcher()
        {
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = "bin/models";
            fileWatcher.Filter = "*.onnx";
            fileWatcher.EnableRaisingEvents = true;
            fileWatcher.Created += FileWatcher_Reload;
            fileWatcher.Deleted += FileWatcher_Reload;
            fileWatcher.Renamed += FileWatcher_Reload;
        }

        private void InitializeModel()
        {
            if (SelectorListBox.SelectedItem != null)
            {
                string modelFileName = SelectorListBox.SelectedItem.ToString();
                string modelPath = Path.Combine("bin/models", modelFileName);

                _onnxModel?.Dispose();

                _onnxModel = new AIModel(modelPath);
                _onnxModel.ConfidenceThreshold = (float)(aimmySettings["AI_Min_Conf"] / 100.0f);
                _onnxModel.CollectData = toggleState["CollectData"];
                _onnxModel.FovSize = (int)aimmySettings["FOV_Size"];
                SelectedModelNotifier.Content = "Loaded Model: " + modelFileName;
                lastLoadedModel = modelFileName;
            }
        }

        private void LoadModelsIntoListBox()
        {
            string[] onnxFiles = Directory.GetFiles("bin/models", "*.onnx");
            SelectorListBox.Items.Clear();

            foreach (string filePath in onnxFiles)
            {
                string fileName = Path.GetFileName(filePath);
                SelectorListBox.Items.Add(fileName);
            }

            if (SelectorListBox.Items.Count > 0)
            {
                if (!SelectorListBox.Items.Contains(lastLoadedModel) && lastLoadedModel != "N/A")
                {
                    SelectorListBox.SelectedIndex = 0;
                    lastLoadedModel = SelectorListBox.Items[0].ToString();
                }
                else
                {
                    SelectorListBox.SelectedItem = lastLoadedModel;
                }
                SelectedModelNotifier.Content = "Loaded Model: " + lastLoadedModel;
            }
        }

        private void SelectorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeModel();
        }

        void LoadConfig(string path)
        {
            if (ConfigSelectorListBox.SelectedItem != null && lastLoadedModel != "N/A")
            {
                dynamic AimmyJSON = JsonConvert.DeserializeObject(File.ReadAllText(path));

                System.Windows.MessageBox.Show("The creator of this model suggests you use this model:" +
                    "\n" +
                    AimmyJSON.Suggested_Model, "Suggested Model - Aimmy");

                aimmySettings["FOV_Size"] = (int)AimmyJSON.FOV_Size;
                FOVOverlay.FovSize = (int)aimmySettings["FOV_Size"];
                _onnxModel.FovSize = (int)aimmySettings["FOV_Size"];

                aimmySettings["Mouse_Sens"] = AimmyJSON.Mouse_Sensitivity;
                aimmySettings["Y_Offset"] = AimmyJSON.Y_Offset;
                aimmySettings["X_Offset"] = AimmyJSON.X_Offset;
                aimmySettings["Trigger_Delay"] = AimmyJSON.Auto_Trigger_Delay;

                aimmySettings["AI_Min_Conf"] = AimmyJSON.AI_Minimum_Confidence;
                _onnxModel.ConfidenceThreshold = (float)(aimmySettings["AI_Min_Conf"] / 100.0f);

                lastLoadedConfig = ConfigSelectorListBox.SelectedItem.ToString();

                // Reload the UI
                ReloadMenu();
            }
            else 
            {
                ConfigSelectorListBox.SelectedItem = null;
                System.Windows.MessageBox.Show("Please select a model in the Model Selector before loading a config.", "Config Error");
            }
        }

        void ReloadMenu()
        {
            AimScroller.Children.Clear();
            TriggerScroller.Children.Clear();
            SettingsScroller.Children.Clear();

            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
        }

        private void ConfigWatcher_Reload(object sender, FileSystemEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                LoadConfigsIntoListBox();
            });
        }

        private void InitializeConfigWatcher()
        {
            ConfigfileWatcher = new FileSystemWatcher();
            ConfigfileWatcher.Path = "bin/configs";
            ConfigfileWatcher.Filter = "*.json";
            ConfigfileWatcher.EnableRaisingEvents = true;
            ConfigfileWatcher.Created += ConfigWatcher_Reload;
            ConfigfileWatcher.Deleted += ConfigWatcher_Reload;
            ConfigfileWatcher.Renamed += ConfigWatcher_Reload;
        }

        private void LoadConfigsIntoListBox()
        {
            string[] jsonFiles = Directory.GetFiles("bin/configs", "*.json");
            ConfigSelectorListBox.Items.Clear();

            foreach (string filePath in jsonFiles)
            {
                string fileName = Path.GetFileName(filePath);
                ConfigSelectorListBox.Items.Add(fileName);
            }

            if (ConfigSelectorListBox.Items.Count > 0)
            {
                if (!ConfigSelectorListBox.Items.Contains(lastLoadedConfig) && lastLoadedConfig != "N/A")
                {
                    ConfigSelectorListBox.SelectedIndex = 0;
                    lastLoadedConfig = ConfigSelectorListBox.Items[0].ToString();
                }
                else
                {
                    ConfigSelectorListBox.SelectedItem = lastLoadedConfig;
                }
                SelectedConfigNotifier.Content = "Loaded Config: " + lastLoadedConfig;
            }
        }

        private void ConfigSelectorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigSelectorListBox.SelectedItem != null)
            {
                LoadConfig($"bin/configs/{ConfigSelectorListBox.SelectedItem.ToString()}");
            }
        }

        void LoadModelStoreMenu()
        {
            if (AvailableModels.Count > 0)
            {
                foreach (var entries in AvailableModels)
                {
                    ModelStoreScroller.Children.Add(new ADownloadGateway(entries));
                }
            }
            else
            {
                LackOfModelsText.Visibility = Visibility.Visible;
            }
        }

        void LoadSettingsMenu()
        {
            SettingsScroller.Children.Add(new AInfoSection());

            AToggle CollectDataWhilePlaying = new AToggle(this, "Collect Data While Playing",
                "This will enable the AI's ability to take a picture of your screen when the trigger key is pressed.");
            CollectDataWhilePlaying.Reader.Name = "CollectData";
            SetupToggle(CollectDataWhilePlaying, state => Bools.CollectDataWhilePlaying = state, Bools.CollectDataWhilePlaying);
            SettingsScroller.Children.Add(CollectDataWhilePlaying);

            ASlider AIMinimumConfidence = new ASlider(this, "AI Minimum Confidence", "% Confidence", 
                "This setting controls how confident the AI needs to be before making the decision to aim.", 
                1);

            AIMinimumConfidence.Slider.Minimum = 1;
            AIMinimumConfidence.Slider.Maximum = 100;
            AIMinimumConfidence.Slider.Value = aimmySettings["AI_Min_Conf"];
            AIMinimumConfidence.Slider.TickFrequency = 1;
            AIMinimumConfidence.Slider.ValueChanged += (s, x) =>
            {
                if (lastLoadedModel != "N/A")
                {
                    double ConfVal = ((double)AIMinimumConfidence.Slider.Value);
                    aimmySettings["AI_Min_Conf"] = ConfVal;
                    _onnxModel.ConfidenceThreshold = (float)(ConfVal / 100.0f);
                }
                else 
                {
                    // Prevent double messageboxes..
                    if (AIMinimumConfidence.Slider.Value != aimmySettings["AI_Min_Conf"])
                    {
                        System.Windows.MessageBox.Show("Unable to set confidence, please select a model and try again.", "Slider Error");
                        AIMinimumConfidence.Slider.Value = aimmySettings["AI_Min_Conf"];
                    }
                }
            };

            SettingsScroller.Children.Add(AIMinimumConfidence);

            bool topMostInitialState = toggleState.ContainsKey("TopMost") ? toggleState["TopMost"] : false;

            AToggle TopMost = new AToggle(this, "UI TopMost",
                "This will toggle the UI's TopMost, meaning it can hide behind other windows vs always being on top.");
            TopMost.Reader.Name = "TopMost";
            SetupToggle(TopMost, state => Bools.TopMost = state, topMostInitialState);

            SettingsScroller.Children.Add(TopMost);
        }

        #region Window Controls
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private static bool SavedData = false;
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Prevent saving overwrite
            if (SavedData) return;

            // Unhook keybind/mousehook
            bindingManager.StopListening();
            FOVOverlay.Close();

            // Save settings
            string serializedData = string.Join(";", aimmySettings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            serializedData += $";TopMost={toggleState["TopMost"]}";

            AimmyWPF.Properties.Settings.Default.AppData = serializedData;
            AimmyWPF.Properties.Settings.Default.Save();
            SavedData = true;

            // Close
            System.Windows.Application.Current.Shutdown();
        }
        #endregion
    }
}
