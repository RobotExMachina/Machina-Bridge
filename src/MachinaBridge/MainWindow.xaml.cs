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

using Machina;

using WebSocketSharp;
using WebSocketSharp.Server;

namespace MachinaBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MachinaBridgeWindow : Window
    {
        public static readonly string Version = "0.8.1.";

        public  Robot bot;
        public  List<Tool> tools = new List<Tool>();
        public  WebSocketServer wssv;
        public  string wssvURL = "ws://127.0.0.1:6999";
        public  string wssvBehavior = "/Bridge";

        // Robot options (quick and dirty defaults)
        public  string _robotName;
        public  string _robotBrand;
        public  string _connectionManager;

        // https://stackoverflow.com/a/18331866/1934487
        SynchronizationContext uiContext;
        
        BoundContent dc;

        public MachinaBridgeWindow()
        {
            InitializeComponent();

            InitializeWebSocketServer();

            dc = new BoundContent(this);

            DataContext = dc;

            uiContext = SynchronizationContext.Current;

            _maxLogLevel = Machina.LogLevel.DEBUG;
            Machina.Logger.CustomLogging += Logger_CustomLogging;
        }
        
        private void Logger_CustomLogging(LoggerArgs e)
        {
            if (e.Level <= _maxLogLevel)
            {
                // https://stackoverflow.com/a/18331866/1934487
                uiContext.Post(x =>
                {
                    dc.ConsoleOutput.Add(e);
                    ConsoleScroller.ScrollToBottom();
                    //ConsoleScroller
                }, null);
            }
        }

        private void InitializeWebSocketServer()
        {
            if (wssv != null && wssv.IsListening)
            {
                wssv.Stop();
                wssv = null;
            }

            wssv = new WebSocketServer(wssvURL);
            //#if DEBUG
            //            wssv.Log.Level = LogLevel.Trace;
            //#endif
            //wssv.AddWebSocketService<BridgeBehavior>(wssvBehavior);
            wssv.AddWebSocketService(wssvBehavior, () => new BridgeBehavior(bot, this));
            wssv.Start();
            if (wssv.IsListening)
            {
                Machina.Logger.Verbose($"Listening on port {wssv.Port}, and providing WebSocket services:");
                foreach (var path in wssv.WebSocketServices.Paths) Machina.Logger.Verbose($"- {path}");
            }

            lbl_ServerURL.Content = wssvURL + wssvBehavior;
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

            bot.ControlMode(ControlType.Stream);

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

        private void Bot_ActionExecuted(object sender, ActionExecutedArgs args)
        {
            int index = this.dc.FlagActionAs(args.LastAction, ExecutionState.Executed);

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

        private void StopWebSocketService()
        {
            wssv.Stop();
        }

        private void DisposeRobot()
        {
            if (bot != null)
                bot.Disconnect();
            bot = null;
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
            string[] args = ParseMessage(instruction);
            if (args == null || args.Length == 0)
            {
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
                        Console.WriteLine($"ERROR: Invalid axis number");
                        return false;
                    }

                    if (!Double.TryParse(args[2], out increment))
                    {
                        Console.WriteLine($"ERROR: Invalid increment value");
                        return false;
                    }

                    return bot.ExternalAxis(axisNumber, increment);
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
                    double increment;
                    if (!Int32.TryParse(args[1], out axisNumber) || axisNumber < 1 || axisNumber > 6)
                    {
                        Console.WriteLine($"ERROR: Invalid axis number");
                        return false;
                    }

                    if (!Double.TryParse(args[2], out increment))
                    {
                        Console.WriteLine($"ERROR: Invalid increment value");
                        return false;
                    }

                    return bot.ExternalAxisTo(axisNumber, increment);
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
                bot.Logger.Error("\"CustomCode\" is not a streamable Action, only available for offline code compilation");
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

            // If here, instruction is not available
            // If here, something went wrong...
            Machina.Logger.Error($"I don't understand \"{instruction}\"...");
            return false;
        }

        public static string[] ParseMessage(string msg)
        {
            try
            {
                // MEGA quick and idrty
                // assuming a msg int he form of "MoveTo(300, 400, 500);" with optional spaces here and there...  
                string[] split1 = msg.Split(new char[] { '(' });
                string[] split2 = split1[1].Split(new char[] { ')' });
                string[] args = split2[0].Split(new char[] { ',' });
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = RemoveDoubleQuotes(RemoveSideSpaces(args[i]));
                }
                string inst = RemoveSideSpaces(split1[0]);

                string[] ret = new string[args.Length + 1];
                ret[0] = inst;
                for (int i = 0; i < args.Length; i++)
                {
                    ret[i + 1] = args[i];
                }

                return ret;
            }
            catch
            {
                Machina.Logger.Error($"I don't understand \"{msg}\"...");
                return null;
            }
        }

        public static string RemoveWhiteSpaces(string str)
        {
            return str.Replace(" ", "");
        }

        public static string RemoveDoubleQuotes(string str)
        {
            return str.Replace("\"", "");
        }

        public static string RemoveSideSpaces(string str)
        {
            if (str == "" || str == null)
                return str;

            string s = str;
            while (s[0] == ' ')
            {
                s = s.Remove(0, 1);
            }

            while (s[s.Length - 1] == ' ')
            {
                s = s.Remove(s.Length - 1);
            }

            return s;
        }

        public static void BadFormatInstruction(string message, Exception ex)
        {
            Machina.Logger.Error($"Badly formatted instruction: \"{message}\"");
            Machina.Logger.Error(ex.ToString());
        }


    }


    public class BridgeBehavior : WebSocketBehavior
    {
        private Robot _robot;
        private MachinaBridgeWindow _parent;

        public BridgeBehavior(Robot robot, MachinaBridgeWindow parent)
        {
            this._robot = robot;
            this._parent = parent;
        }

        protected override void OnOpen()
        {
            //base.OnOpen();
            Console.WriteLine("  BRIDGE: opening bridge");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            //base.OnMessage(e);
            //Console.WriteLine("  BRIDGE: received message: " + e.Data);
            if (_robot == null) { 
                _parent.wssv.WebSocketServices.Broadcast($"{{\"event\":\"controller-disconnected\"}}");
                return;
            }

            _parent.ExecuteInstructionOnContext(e.Data);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            //base.OnError(e);
            Console.WriteLine("  BRIDGE ERROR: " + e.Message);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            //base.OnClose(e);
            Console.WriteLine($"  BRIDGE: closed bridge: {e.Code} {e.Reason}");

            _parent.wssv.WebSocketServices.Broadcast($"{{\"event\":\"client-disconnected\",\"user\":\"clientname\"}}");
        }

    }





}
