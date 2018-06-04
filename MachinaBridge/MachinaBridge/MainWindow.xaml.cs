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

using Machina;

using WebSocketSharp;
using WebSocketSharp.Server;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace MachinaBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly string Version = "0.6.3";

        public static Robot bot;
        public static List<Tool> tools = new List<Tool>();
        public static WebSocketServer wssv;
        public static string wssvURL = "ws://127.0.0.1:6999";
        public static string wssvBehavior = "/Bridge";

        // Robot options (quick and dirty defaults
        public static string _robotName;
        public static string _robotBrand;
        //static string _robotModel = "";
        public static string _connectionManager;

        ConsoleContent dc = new ConsoleContent();

        public MainWindow()
        {
            InitializeComponent();

            InitializeWebSocketServer();

            //Loaded += MainWindow_Loaded;

            DataContext = dc;
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
            wssv.AddWebSocketService<BridgeBehavior>(wssvBehavior);
            wssv.Start();
            if (wssv.IsListening)
            {
                Console.WriteLine("Listening on port {0}, and providing WebSocket services:", wssv.Port);
                foreach (var path in wssv.WebSocketServices.Paths) Console.WriteLine("- {0}", path);
            }

            lbl_ServerURL.Content = wssvURL + wssvBehavior;
        }

        private bool InitializeRobot()
        {
            if (bot != null) DisposeRobot();

            bot = Robot.Create(_robotName, _robotBrand);

            bot.BufferEmpty += OnBufferEmpty;
            bot.MotionCursorUpdated += OnMotionCursorUpdated;
            bot.ActionCompleted += OnActionCompleted;

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


        public static void OnBufferEmpty(object sender, EventArgs e)
        {
            Console.WriteLine("SENDING BUFFER-EMPTY");
            wssv.WebSocketServices.Broadcast($"{{\"msg\":\"buffer-empty\",\"data\":[]}}");
        }

        public static void OnMotionCursorUpdated(object sender, EventArgs e)
        {
            Robot r = sender as Robot;
            Machina.Vector p = r.GetCurrentPosition();
            Machina.Rotation rot = r.GetCurrentRotation();
            Joints j = r.GetCurrentAxes();

            if (p != null && rot != null)
            {
                wssv.WebSocketServices.Broadcast($"{{\"msg\":\"pose\",\"data\":[{p.X},{p.Y},{p.Z},{rot.Q.W},{rot.Q.X},{rot.Q.Y},{rot.Q.Z}]}}");
            }
            else if (j != null)
            {
                wssv.WebSocketServices.Broadcast($"{{\"msg\":\"joints\",\"data\":[{j.J1},{j.J2},{j.J3},{j.J4},{j.J5},{j.J6}]}}");
            }
        }

        public static void OnActionCompleted(object sender, ActionCompletedArgs e)
        {
            wssv.WebSocketServices.Broadcast($"{{\"msg\":\"action-completed\",\"data\":[{e.RemainingActions}],\"remaining\":{e.RemainingActions},\"last-action\":\"{(e.LastAction == null ? "" : e.LastAction.ToInstruction())}\"}}");
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


        public static bool ExecuteInstruction(string[] args)
        {
            if (args[0].Equals("Move", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Move(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
            }
            else if (args[0].Equals("MoveTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.MoveTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
            }
            //else if (args[0].Equals("Transform", StringComparison.CurrentCultureIgnoreCase))
            //{
            //    return bot.Transform(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
            //        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]),
            //        Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]));
            //}
            else if (args[0].Equals("TransformTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.TransformTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]),
                    Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]));
            }
            else if (args[0].Equals("Rotate", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Rotate(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4]));
            }
            else if (args[0].Equals("RotateTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.RotateTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("Axes", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Axes(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("AxesTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.AxesTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("Speed", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Speed(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("SpeedTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.SpeedTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("Acceleration", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Acceleration(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("AccelerationTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.AccelerationTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("RotationSpeed", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.RotationSpeed(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("RotationSpeedTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.RotationSpeedTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("JointSpeed", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.JointSpeed(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("JointSpeedTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.JointSpeedTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("JointAcceleration", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.JointAcceleration(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("JointAccelerationTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.JointAccelerationTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("Precision", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Precision(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("PrecisionTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.PrecisionTo(Convert.ToDouble(args[1]));
            }
            else if (args[0].Equals("MotionMode", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.MotionMode(args[1]);
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
            else if (args[0].Equals("Wait", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Wait(Convert.ToInt32(args[1]));
            }
            else if (args[0].Equals("Message", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Message(args[1]);
            }

            // For the time being, new Tool will not be
            //      Tool(string name, Point TCPPosition, Orientation TCPOrientation, double weightKg, Point centerOfGravity)
            // but an itemized version of it:
            //      Tool(name, x, y, z, x0, x1, x2, y0, y1, y2, weightkg, gx, gy, gz);
            else if (args[0].Equals("new Tool", StringComparison.CurrentCultureIgnoreCase) || 
                     args[0].Equals("Tool.Create", StringComparison.CurrentCultureIgnoreCase))
            {
                Tool t = new Tool(args[1],
                    new Machina.Point(Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4])),
                    new Machina.Orientation(Convert.ToDouble(args[5]), Convert.ToDouble(args[6]), Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]), Convert.ToDouble(args[10])),
                    Convert.ToDouble(args[11]),
                    new Machina.Point(Convert.ToDouble(args[12]), Convert.ToDouble(args[13]), Convert.ToDouble(args[14])));

                bool found = false;
                for (int i = 0; i < tools.Count; i++)
                {
                    if (tools[i].name.Equals(args[1], StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine("Found tool with similar name, overwriting...");
                        tools[i] = t;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Console.WriteLine($"Adding Tool {t.name} to Machina...");
                    tools.Add(t);
                }

                return true;
            }
            else if (args[0].Equals("Attach", StringComparison.CurrentCultureIgnoreCase))
            {
                Tool t = null;

                foreach (var tool in tools)
                {
                    if (tool.name.Equals(args[1], StringComparison.CurrentCultureIgnoreCase))
                    {
                        t = tool;
                        break;
                    }
                }

                if (t == null)
                {
                    Console.WriteLine($"ERROR: Tool \"{args[1]}\" has not been defined");
                    return false;
                }

                return bot.Attach(t);
            }
            else if (args[0].Equals("Detach", StringComparison.CurrentCultureIgnoreCase))
            {
                return bot.Detach();
            }
            else if (args[0].Equals("WriteDigital", StringComparison.CurrentCultureIgnoreCase))
            {
                int count = args.Length;
                int pin = 0;
                bool digitalPin = Int32.TryParse(args[1], out pin);

                if (digitalPin)
                {
                    switch (count)
                    {
                        case 3: return bot.WriteDigital(pin, Convert.ToBoolean(args[2]));
                        case 4: return bot.WriteDigital(pin, Convert.ToBoolean(args[2]), Convert.ToBoolean(args[3]));
                    }
                }
                else
                {
                    switch (count)
                    {
                        case 3: return bot.WriteDigital(args[1], Convert.ToBoolean(args[2]));
                        case 4: return bot.WriteDigital(args[1], Convert.ToBoolean(args[2]), Convert.ToBoolean(args[3]));
                    }
                }
            }
            else if (args[0].Equals("WriteAnalog", StringComparison.CurrentCultureIgnoreCase))
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
            }

            return false;
        }

        public static string[] ParseMessage(string msg)
        {
            // MEGA quick and idrty
            // ssuming a msg int he form of "MoveTo(300, 400, 500);" with optional spaces here and there...  
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

        private void txtbox_IP_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }


    public class BridgeBehavior : WebSocketBehavior
    {

        protected override void OnOpen()
        {
            //base.OnOpen();
            Console.WriteLine("  BRIDGE: opening bridge");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            //base.OnMessage(e);
            Console.WriteLine("  BRIDGE: received message: " + e.Data);
            if (MainWindow.bot == null) { 
                MainWindow.wssv.WebSocketServices.Broadcast($"{{\"msg\":\"disconnected\",\"data\":[]}}");
                return;
            }

            MainWindow.ExecuteInstruction(MainWindow.ParseMessage(e.Data));
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
        }

        //internal void OnBufferEmpty(object sender, EventArgs e)
        //{
        //    Sessions.Broadcast("bufferEmpty");
        //}
    }






    // https://stackoverflow.com/a/14957478/1934487
    public class ConsoleContent : INotifyPropertyChanged
    {
        string consoleInput = string.Empty;
        ObservableCollection<string> consoleOutput = new ObservableCollection<string>() { "## MACHINA Console ##", "Enter any command to stream it to the robot..." };

        public string ConsoleInput
        {
            get
            {
                return consoleInput;
            }
            set
            {
                consoleInput = value;
                OnPropertyChanged("ConsoleInput");
            }
        }

        public ObservableCollection<string> ConsoleOutput
        {
            get
            {
                return consoleOutput;
            }
            set
            {
                consoleOutput = value;
                OnPropertyChanged("ConsoleOutput");
            }
        }

        public void RunCommand()
        {
            ConsoleOutput.Add(ConsoleInput);
            // do your stuff here.

            if (MainWindow.bot == null)
            {
                //MainWindow.wssv.WebSocketServices.Broadcast($"{{\"msg\":\"disconnected\",\"data\":[]}}");
                ConsoleOutput.Add("Disconnected from Robot...");
            }
            else
            {
                MainWindow.ExecuteInstruction(MainWindow.ParseMessage(ConsoleInput));
            }

            ConsoleInput = String.Empty;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
