using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Reflection;

using Machina;
using Machina.Types.Geometry;
using MVector = Machina.Types.Geometry.Vector;
using MOrientation = Machina.Types.Geometry.Orientation;
using MMath = Machina.Types.Geometry.Geometry;

using WebSocketSharp;
using WebSocketSharp.Server;
using Logger = Machina.Logger;
using Microsoft.Win32;
using LogLevel = Machina.LogLevel;
using System.Globalization;
//using Machina.EventArgs;

namespace MachinaBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MachinaBridgeWindow : Window
    {
        public static readonly string Version = "0.8.12c";

        public  Robot bot;
        public  List<Tool> tools = new List<Tool>();
        public  WebSocketServer wssv;
        //public  string wssvURL = "ws://127.0.0.1:6999";
        //public  string wssvBehavior = "/Bridge";
        public static string wssvURL, wssvBehavior;

        // Robot options (quick and dirty defaults)
        public  string _robotName;
        public  string _robotBrand;
        public  string _connectionManager;

        internal List<string> _connectedClients = new List<string>();
    
        // https://stackoverflow.com/a/18331866/1934487
        internal SynchronizationContext uiContext;
        
        BoundContent dc;

        public MachinaBridgeWindow()
        {
            InitializeComponent();

            dc = new BoundContent(this);

            DataContext = dc;

            uiContext = SynchronizationContext.Current;

            _maxLogLevel = Machina.LogLevel.VERBOSE;
            Logger.CustomLogging += Logger_CustomLogging;

            Logger.Info("Machina Bridge: " + Version + "; Core: " + Robot.Version);

            InitializeWebSocketServer();

            // Handle pasting text on the input
            DataObject.AddPastingHandler(InputBlock, InputBlock_Paste);

#if DEBUG
            var item = combo_LogLevel.Items.GetItemAt(5) as ComboBoxItem;
            item.IsSelected = true;
#endif
        }
        
        private void Logger_CustomLogging(LoggerArgs e)
        {
            if (e.Level <= _maxLogLevel)
            {
                // https://stackoverflow.com/a/18331866/1934487
                uiContext.Post(x =>
                {
                    dc.AddConsoleOutput(e);
                }, null);
            }
        }

        private bool ParseWebSocketURL()
        {
            string url = txtbox_WSServerURL.Text;
            string[] parts = url.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            // Should be something like {"ws:", "127.0.0.1", "route" [, ...] } 
            if (parts.Length < 3)
            {
                Logger.Error("Please add a route to the websocket url, like \"/Bridge\"");
                return false;
            }
            else if (!parts[0].Equals("ws:", StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.Error("WebSocket URL must start with \"ws:\"");
                return false;
            }
            else if (!Machina.Net.Net.ValidateIPv4Port(parts[1]))
            {
                Logger.Error("Invalid IP:Port address \"" + parts[1] + "\"");
                return false;
            }

            wssvURL = parts[0] + "//" + parts[1];
            wssvBehavior = "/" + String.Join("/", parts, 2, parts.Length - 2);

            Logger.Debug("WebSocket server URL: " + wssvURL);
            Logger.Debug("WebSocket server route: " + wssvBehavior);

            return true;
        }

        private void StopWebSocketServer()
        {
            if (wssv != null)
            {
                Logger.Verbose("Stopping WebSocket service on " + wssv.Address + ":" + wssv.Port + wssv.WebSocketServices.Paths.ElementAt(0));
                wssv.Stop();
                wssv = null;
            }
        }

        private bool InitializeWebSocketServer()
        {
            if (!ParseWebSocketURL())
            {
                Logger.Error("Invalid WebSocket URL \"" + txtbox_WSServerURL.Text + "\"; try something like \"ws://127.0.0.1/Bridge\"");
                return false;
            }

            if (wssv != null && wssv.IsListening)
            {
                StopWebSocketServer();
            }
            
            wssv = new WebSocketServer(wssvURL);
            wssv.AddWebSocketService(wssvBehavior, () => new BridgeBehavior(bot, this));
            
            // @TODO: add a check here if the port is in use, and try a different port instead
            try
            {
                wssv.Start();
            }
            catch
            {
                Logger.Error("Default websocket server is not available, please enter a new one manually...");
                return false;
            }

            if (wssv.IsListening)
            {
                //Machina.Logger.Info($"Listening on port {wssv.Port}, and providing WebSocket services:");
                //foreach (var path in wssv.WebSocketServices.Paths) Machina.Logger.Info($"- {path}");
                Logger.Info("Waiting for incoming connections on " + (wssvURL + wssvBehavior));
            }

            return true;
            //lbl_ServerURL.Content = wssvURL + wssvBehavior;
        }




        private bool InitializeRobot()
        {
            if (bot != null) DisposeRobot();

            bot = Robot.Create(_robotName, _robotBrand);

            bot.ActionIssued += BroadCastEvent;
            bot.ActionReleased += BroadCastEvent;
            bot.ActionExecuted += BroadCastEvent;
            bot.MotionUpdate += BroadCastEvent;

            bot.ActionIssued += Bot_ActionIssued;
            bot.ActionReleased += Bot_ActionReleased;
            bot.ActionExecuted += Bot_ActionExecuted;
            bot.MotionUpdate += Bot_MotionUpdate;

            bot.ControlMode(ControlType.Online);

            bool success;
            if (_connectionManager == "MACHINA")
            {
                bot.ConnectionManager(ConnectionType.Machina);
                success = bot.Connect();
            }
            else
            {
                success = bot.Connect(txtbox_IP.Text, Convert.ToInt32(txtbox_Port.Text));
            }

            if (success)
            {
                UpdateRobotStatus();
            }

            return success;
        }



        private void Bot_MotionUpdate(object sender, MotionUpdateArgs args)
        {
            Logger.Debug(args.ToString());
        }

        private void Bot_ActionExecuted(object sender, ActionExecutedArgs args)
        {
            int index = this.dc.FlagActionAs(args.LastAction, ExecutionState.Executed);

            // Couldn't figure out a good way to remove old executed actions but maintain good auto scrolling
            // at the same time; stupid difference between the dispatcher and the UI thread... 
            //uiContext.Post(x =>
            //{
            //    dc.CheckMaxExecutedActions();
            //}, null);

            if (dc.queueFollowPointer)
            {
                ScrollQueueToElement(index);
            }

            UpdateRobotStatus();
        }


        private void Bot_ActionReleased(object sender, ActionReleasedArgs args)
        {
            int index = this.dc.FlagActionAs(args.LastAction, ExecutionState.Released);
        }

        private void Bot_ActionIssued(object sender, ActionIssuedArgs args)
        {
            this.dc.AddActionToQueue(new ActionWrapper(args.LastAction));
        }
        

        public void BroadCastEvent(object sender, MachinaEventArgs e)
        {
            wssv.WebSocketServices.Broadcast(e.ToJSONString());
        }

        private void Disconnect()
        {
            dc.ClearActionsQueueAll();
            DisposeRobot();
            UpdateRobotStatus();
            Thread.Sleep(500);
        }

        private void DisposeRobot()
        {
            if (bot != null)
                bot.Disconnect();
            bot = null;
        }

        internal void DownloadDrivers()
        {
            Logger.Info("Downloading Machina Drivers for " + _robotBrand + " robot on " + txtbox_IP.Text + ":" + txtbox_Port.Text);

            // Create a fake robot not to interfere with the main one
            Robot driverBot = Robot.Create(_robotName, _robotBrand);
            driverBot.ControlMode(ControlType.Online);
            var parameters = new Dictionary<string, string>()
            {
                {"HOSTNAME", txtbox_IP.Text},
                {"PORT", txtbox_Port.Text}
            };

            var files = driverBot.GetDeviceDriverModules(parameters);

            // Clear temp folder
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "machina_modules");

            //https://stackoverflow.com/a/1288747/1934487
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                Logger.Debug("Clearing " + path);
                foreach (FileInfo file in di.GetFiles())
                {
                    Logger.Debug("Deleting " + file.FullName);
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    Logger.Debug("Deleting " + dir.FullName);
                    dir.Delete(true);
                }
            }
            else
            {
                di.Create();
                Logger.Debug("Created " + path);
            }

            // Save temp files
            foreach (var pair in files)
            {
                string filename = pair.Key;
                string content = pair.Value;
                

                string filepath = System.IO.Path.Combine(path, filename);
                try
                {
                    System.IO.File.WriteAllText(filepath, content, Encoding.ASCII);
                }
                catch
                {
                    Logger.Error("Could not save " + filename + " to "  + filepath);
                    Logger.Error("Could not download drivers");
                    return;
                }

                Logger.Debug("Saved module to " + filepath);
            }

            // Zip the file
            string zipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "machina_modules.zip");
            System.IO.FileInfo fi = new FileInfo(zipPath);
            if (fi.Exists)
            {
                fi.Delete();
                Logger.Debug("Deleted previous " + zipPath);
            }
            ZipFile.CreateFromDirectory(path, zipPath);
            Logger.Debug("Zipped files to " + zipPath);

            // Prompt file save dialog: https://www.wpf-tutorial.com/dialogs/the-savefiledialog/
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Zip file (*.zip)|*.zip";
            saveFileDialog.DefaultExt = "zip";
            saveFileDialog.AddExtension = true;
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            saveFileDialog.FileName = "machina_modules.zip";

            if (saveFileDialog.ShowDialog() == true)
            {
                fi = new FileInfo(saveFileDialog.FileName);
                if (fi.Exists)
                {
                    fi.Delete();
                    Logger.Debug("Deleted previous " + saveFileDialog.FileName);
                }
                File.Copy(zipPath, saveFileDialog.FileName);
                Logger.Debug("Copied " + zipPath + " to " + saveFileDialog.FileName);

                Logger.Info("Drivers saved to " + saveFileDialog.FileName);
            }

        }

        internal void UpdateClientBox()
        {
            txtblock_Bridge_Clients.Text = "";
            if (_connectedClients.Count == 0)
            {
                txtblock_Bridge_Clients.Text += "No clients connected";
            }
            else
            {
                foreach (var name in _connectedClients)
                {
                    txtblock_Bridge_Clients.Text += name + " ";
                }
            }
        }

        internal void UpdateRobotStatus()
        {
            if (bot == null)
            {
                ResetRobotStatus();
                return;
            }

            uiContext.Post(x =>
            {
                MVector pos = bot.GetCurrentPosition();
                string posStr = pos?.ToString(true) ?? "-";
                lbl_Status_TCP_Position_Value.Content = posStr;

                MOrientation ori = bot.GetCurrentRotation();
                string oriStr = ori?.ToString(true) ?? "-";
                lbl_Status_TCP_Orientation_Value.Content = oriStr;

                Joints axes = bot.GetCurrentAxes();
                string axesStr = axes?.ToString(true) ?? "-";
                lbl_Status_Axes_Value.Content = axesStr;

                ExternalAxes extax = bot.GetCurrentExternalAxes();
                bool nullext = true;
                if (extax != null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (extax[i] != null)
                        {
                            nullext = false;
                            break;
                        }
                    }
                }
                lbl_Status_Ext_Axes_Value.Content = nullext ? "-" : extax.ToString(true);

                double speed = bot.GetCurrentSpeed();
                double acc = bot.GetCurrentAcceleration();
                string speedacc = Math.Round(speed, MMath.STRING_ROUND_DECIMALS_MM) + " mm/s / " + Math.Round(acc, MMath.STRING_ROUND_DECIMALS_MM) + " mm/s^2";
                lbl_Status_SpeedAcceleration_Value.Content = speedacc;

                double precision = bot.GetCurrentPrecision();
                lbl_Status_Precision_Value.Content =
                    Math.Round(precision, MMath.STRING_ROUND_DECIMALS_MM) + " mm";

                MotionType mtype = bot.GetCurrentMotionMode();
                lbl_Status_MotionMode_Value.Content = mtype.ToString();
                
                lbl_Status_Tool_Value.Content = bot.GetCurrentTool()?.name ?? "(no tool)";

            }, null);
        }

        internal void ResetRobotStatus()
        {
            uiContext.Post(x =>
            {
                lbl_Status_TCP_Position_Value.Content = "-";
                lbl_Status_TCP_Orientation_Value.Content = "-";
                lbl_Status_Axes_Value.Content = "-";
                lbl_Status_Ext_Axes_Value.Content = "-";
                lbl_Status_SpeedAcceleration_Value.Content = "-";
                lbl_Status_Precision_Value.Content = "-";
                lbl_Status_MotionMode_Value.Content = "-";
                lbl_Status_Tool_Value.Content = "-";
            }, null);
        }


        /// <summary>
        /// Scroll QueueScroller to item index.
        /// </summary>
        /// <param name="index"></param>
        public void ScrollQueueToElement(int index)
        {
            uiContext.Post(x =>
            {
                //dc.CheckMaxExecutedActions();

                // https://stackoverflow.com/a/603227/1934487
                var item = QueueItemControl.Items.GetItemAt(index);
                ItemsControl ic = QueueStackPanel.Children[0] as ItemsControl;
                var container = ic.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                container.ApplyTemplate();  // Solve the 'This operation is valid only on elements that have this template applied.' error? https://stackoverflow.com/a/15467687/1934487
                var tb = ic.ItemTemplate.FindName("QueueStackLine", container) as TextBlock;
                
                var t = tb.TransformToVisual(QueueScroller);
                var tt = t.TransformBounds(new Rect(0, 0, 0, 1));
                var b = tt.Bottom;

                var screenOffset = 0.33 * QueueScroller.ActualHeight;
                int targetOffset = (int) (b + QueueScroller.VerticalOffset - screenOffset);

                isUserScroll = false;
                QueueScroller.ScrollToVerticalOffset(targetOffset);

            }, null);
        }


        public bool ExecuteInstructionOnContext(string instruction)
        {
            uiContext.Post(x =>
            {
                ExecuteStatement(instruction);
            }, null);
            return false;
        }

        public static T GetTfromString<T>(string mystring)
        {
            var foo = TypeDescriptor.GetConverter(typeof(T));
            return (T)(foo.ConvertFromInvariantString(mystring));
        }

        private void cbx_FollowPointer_Checked(object sender, RoutedEventArgs e)
        {
            if (dc != null)
            {
                Logger.Debug("Follow program pointer activated.");
                dc.queueFollowPointer = true;
            }
        }

        private void cbx_FollowPointer_Unchecked(object sender, RoutedEventArgs e)
        {
            if (dc != null)
            {
                Logger.Debug("Follow program pointer deactivated.");
                dc.queueFollowPointer = false;
            }
        }

        // A flag to signal if the scroll was caused by auto or user
        private volatile bool isUserScroll = false;

        // Figures out if auto-scroll, and deactivates follow pointer if not
        private void QueueScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Figure out if this was related to auto scrolling
            if (e.VerticalChange == 0.0 ||
                e.ExtentHeight == 0 ||
                e.ExtentHeightChange > 0)
                return;

            if (isUserScroll && dc.queueFollowPointer)
            {
                Logger.Debug("Manual scroll detected, deactivating 'Follow Pointer'.");
                dc.queueFollowPointer = false;
                cbx_FollowPointer.IsChecked = false;
                cbx_FollowPointer_Unchecked(null, null);
            }

            isUserScroll = true;
        }

        public bool ExecuteStatement(string statement)
        {
            if (bot != null)
            {
                return bot.Do(statement);
            }
            return false;
        }
    }
}
