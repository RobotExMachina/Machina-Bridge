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

using Machina;
using MVector = Machina.Vector;

using WebSocketSharp;
using WebSocketSharp.Server;
using Logger = Machina.Logger;
using Microsoft.Win32;
using LogLevel = Machina.LogLevel;

namespace MachinaBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MachinaBridgeWindow : Window
    {
        public static readonly string Version = "0.8.6";

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
                    dc.ConsoleOutput.Add(e);
                    //dc.ConsoleOutput.Add(bot.GetCurrentPosition().X);
                    ConsoleScroller.ScrollToBottom();
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

            if (_connectionManager == "MACHINA")
            {
                bot.ConnectionManager(ConnectionType.Machina);
                return bot.Connect();
            }
            else
            {
                return bot.Connect(txtbox_IP.Text, Convert.ToInt32(txtbox_Port.Text));
            }

        }



        private void Bot_MotionUpdate(object sender, MotionUpdateArgs args)
        {
            Logger.Debug(args.ToString());
        }

        private void Bot_ActionExecuted(object sender, ActionExecutedArgs args)
        {
            int index = this.dc.FlagActionAs(args.LastAction, ExecutionState.Executed);

            UpdateRobotStatus();

            //ScrollQueueToElement(index);
        }
        

        private void Bot_ActionReleased(object sender, ActionReleasedArgs args)
        {
            int index = this.dc.FlagActionAs(args.LastAction, ExecutionState.Released);
        }

        private void Bot_ActionIssued(object sender, ActionIssuedArgs args)
        {
            this.dc.ActionsQueue.Add(new ActionWrapper(args.LastAction));
        }
        

        public void BroadCastEvent(object sender, MachinaEventArgs e)
        {
            wssv.WebSocketServices.Broadcast(e.ToJSONString());
        }

        private void Disconnect()
        {
            DisposeRobot();
            Thread.Sleep(1000);
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
                Machina.Vector pos = bot.GetCurrentPosition();
                string posStr = pos?.ToString(true) ?? "-";
                lbl_Status_TCP_Position_Value.Content = posStr;

                Machina.Orientation ori = bot.GetCurrentRotation();
                string oriStr = ori?.ToString(true) ?? "-";
                lbl_Status_TCP_Orientation_Value.Content = oriStr;

                Machina.Joints axes = bot.GetCurrentAxes();
                string axesStr = axes?.ToString(true) ?? "-";
                lbl_Status_Axes_Value.Content = axesStr;

                Machina.ExternalAxes extax = bot.GetCurrentExternalAxes();
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
                string speedacc = Math.Round(speed, Machina.Geometry.STRING_ROUND_DECIMALS_MM) + " mm/s / " + Math.Round(acc, Machina.Geometry.STRING_ROUND_DECIMALS_MM) + " mm/s^2";
                lbl_Status_SpeedAcceleration_Value.Content = speedacc;

                double precision = bot.GetCurrentPrecision();
                lbl_Status_Precision_Value.Content =
                    Math.Round(precision, Machina.Geometry.STRING_ROUND_DECIMALS_MM) + " mm";

                Machina.MotionType mtype = bot.GetCurrentMotionMode();
                lbl_Status_MotionMode_Value.Content = mtype.ToString();
                
                lbl_Status_Tool_Value.Content = bot.GetCurrentTool().name;

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



        //// Doesn't really work well
        //public void ScrollQueueToElement(int index)
        //{
        //    uiContext.Post(x => {
        //        // https://stackoverflow.com/a/603227/1934487
        //        var item = QueueItemControl.Items.GetItemAt(index);
        //        ItemsControl ic = QueueStackPanel.Children[0] as ItemsControl;
        //        var container = ic.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
        //        var tb = ic.ItemTemplate.FindName("QueueStackLine", container) as TextBlock;

        //        QueueScroller.ScrollToVerticalOffset(tb.TransformToVisual(QueueScroller).TransformBounds(new Rect(0, 0, 1, 1)).Bottom);
        //    }, null);
        //}


        public bool ExecuteInstructionOnContext(string instruction)
        {
            uiContext.Post(x =>
            {
                ExecuteInstruction(instruction);
            }, null);
            return false;
        }


        public bool ExecuteInstruction(string instruction)
        {
            string[] args = Machina.Utilities.Parsing.ParseStatement(instruction);
            if (args == null || args.Length == 0)
            {
                Machina.Logger.Error($"I don't understand \"{instruction}\"...");
                return false;
            }

            // This is horrible, but oh well...
            if (args[0].Equals("MotionMode", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.MotionMode(args[1]);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Speed", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Speed(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("SpeedTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.SpeedTo(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Acceleration", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Acceleration(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("AccelerationTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.AccelerationTo(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Precision", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Precision(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("PrecisionTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.PrecisionTo(Convert.ToDouble(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Coordinates", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is still not implemented.");   
                return false;
            }
            else if (args[0].Equals("Temperature", StringComparison.CurrentCultureIgnoreCase) || args[0].Equals("TemperatureTo", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is still not implemented.");
                return false;
            }
            else if (args[0].Equals("ExtrusionRate", StringComparison.CurrentCultureIgnoreCase) || args[0].Equals("ExtrusionRateTo", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is still not implemented.");
                return false;
            }
            else if (args[0].Equals("PushSettings", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.PushSettings();
                return true;
            }
            else if (args[0].Equals("PopSettings", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.PopSettings();
                return true;
            }
            else if (args[0].Equals("Move", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Move(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("MoveTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.MoveTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Rotate", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Rotate(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("RotateTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.RotateTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Transform", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is still not implemented.");
                return false;
            }
            else if (args[0].Equals("TransformTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.TransformTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]),
                        Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Axes", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Axes(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("AxesTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.AxesTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("ExternalAxis", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    int axisNumber;
                    double increment;
                    if (!Int32.TryParse(args[1], out axisNumber) || axisNumber < 1 || axisNumber > 6)
                    {
                        Logger.Error($"Invalid axis number");
                        return false;
                    }

                    if (!Double.TryParse(args[2], out increment))
                    {
                        Logger.Error($"Invalid increment value");
                        return false;
                    }

                    string target = "All";
                    if (args.Length > 3)
                    {
                        target = args[3];
                    }

                    return bot.ExternalAxis(axisNumber, increment, target);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("ExternalAxisTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    int axisNumber;
                    double val;
                    if (!Int32.TryParse(args[1], out axisNumber) || axisNumber < 1 || axisNumber > 6)
                    {
                        Logger.Error($"Invalid axis number");
                        return false;
                    }

                    if (!Double.TryParse(args[2], out val))
                    {
                        Logger.Error($"Invalid value " + args[2]);
                        return false;
                    }

                    string target = "All";
                    if (args.Length > 3)
                    {
                        target = args[3];
                    }

                    return bot.ExternalAxisTo(axisNumber, val, target);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("ArmAngle", StringComparison.CurrentCultureIgnoreCase))
            {
                Logger.Warning(
                    "Relative `ArmAngle` is temporarily disabled, please use the absolute `ArmAngleTo` version instead.");
                //try
                //{
                //    double val;

                //    if (!Double.TryParse(args[1], out val))
                //    {
                //        Console.WriteLine($"ERROR: Invalid value " + args[1]);
                //        return false;
                //    }

                //    return bot.ArmAngle(val);
                //}
                //catch (Exception ex)
                //{
                //    BadFormatInstruction(instruction, ex);
                //    return false;
                //}
            }
            else if (args[0].Equals("ArmAngleTo", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    double val;

                    if (!Double.TryParse(args[1], out val))
                    {
                        Logger.Error($"Invalid value " + args[1]);
                        return false;
                    }

                    return bot.ArmAngleTo(val);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Wait", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Wait(Convert.ToInt32(args[1]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Message", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.Message(args[1]);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Comment", StringComparison.CurrentCultureIgnoreCase))
            {
                // Do noLightg here, just go through with it.
                return true;
            }
            else if (args[0].Equals("CustomCode", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Warning("\"CustomCode\" can lead to unexpected results, use with caution and only if you know what you are doing.");

                try
                {
                    string statement = args[1];
                    bool dec = false;
                    if (args.Length > 2)
                    {
                        dec = bool.Parse(args[2]);  // if not good, throw and explain
                    }

                    return bot.CustomCode(statement, dec);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                }

                return false;
            }
            else if (args[0].Equals("new Tool", StringComparison.CurrentCultureIgnoreCase) ||
                     args[0].Equals("Tool.Create", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is deprecated, please update your client and use \"DefineTool\" instead. Action will have no effect.");
                return false;
            }
            else if (args[0].Equals("DefineTool", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.DefineTool(args[1],
                        Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4]),
                        Convert.ToDouble(args[5]), Convert.ToDouble(args[6]), Convert.ToDouble(args[7]),
                        Convert.ToDouble(args[8]), Convert.ToDouble(args[9]), Convert.ToDouble(args[10]),
                        Convert.ToDouble(args[11]),
                        Convert.ToDouble(args[12]), Convert.ToDouble(args[13]), Convert.ToDouble(args[14]));
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Attach", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Error($"\"{args[0]}\" is deprecated, please update your client and use \"AttachTool\" instead. Action will have no effect.");
                return false;
            }
            else if (args[0].Equals("AttachTool", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    return bot.AttachTool(args[1]);
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Detach", StringComparison.CurrentCultureIgnoreCase))
            {
                bot.Logger.Warning($"\"{args[0]}\" is deprecated, please update your client and use \"DetachTool\" instead.");
                return bot.DetachTool();
            }
            else if (args[0].Equals("DetachTool", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.DetachTool();
            }
            else if (args[0].Equals("WriteDigital", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    int count = args.Length;
                    int pin = 0;
                    bool digitalPin = Int32.TryParse(args[1], out pin);

                    // Numeric pins
                    if (digitalPin)
                    {
                        switch (count)
                        {
                            case 3: return bot.WriteDigital(pin, Convert.ToBoolean(args[2]));
                            case 4: return bot.WriteDigital(pin, Convert.ToBoolean(args[2]), Convert.ToBoolean(args[3]));
                        }
                    }
                    // Named pins (ABB)
                    else
                    {
                        switch (count)
                        {
                            case 3: return bot.WriteDigital(args[1], Convert.ToBoolean(args[2]));
                            case 4: return bot.WriteDigital(args[1], Convert.ToBoolean(args[2]), Convert.ToBoolean(args[3]));
                        }
                    }

                    // If here, something went wrong...
                    Machina.Logger.Error($"Badly formatted instruction: \"{instruction}\"");
                    return false;
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("WriteAnalog", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    int count = args.Length;
                    int pin = 0;
                    bool digitalPin = Int32.TryParse(args[1], out pin);

                    if (digitalPin)
                    {
                        switch (count)
                        {
                            case 3: return bot.WriteAnalog(pin, Convert.ToDouble(args[2]));
                            case 4: return bot.WriteAnalog(pin, Convert.ToDouble(args[2]), Convert.ToBoolean(args[3]));
                        }
                    }
                    else
                    {
                        switch (count)
                        {
                            case 3: return bot.WriteAnalog(args[1], Convert.ToDouble(args[2]));
                            case 4: return bot.WriteAnalog(args[1], Convert.ToDouble(args[2]), Convert.ToBoolean(args[3]));
                        }
                    }

                    // If here, something went wrong...
                    Machina.Logger.Error($"Badly formatted instruction: \"{instruction}\"");
                    return false;
                }
                catch (Exception ex)
                {
                    BadFormatInstruction(instruction, ex);
                    return false;
                }
            }
            else if (args[0].Equals("Extrude", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Extrude();
            }

            // If here, instruction is not available or something went wrong...
            Machina.Logger.Error($"I don't understand \"{instruction}\"...");
            return false;
        }

        public static void BadFormatInstruction(string message, Exception ex)
        {
            Machina.Logger.Error($"Badly formatted instruction: \"{message}\"");
            Machina.Logger.Error(ex.ToString());
        }



    }







}
