using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebSocketSharp;
using WebSocketSharp.Server;

using Machina;

namespace MachinaBridge
{
    class Program
    {
        static Robot arm;
        static List<Tool> tools = new List<Tool>();

        static void Main(string[] args)
        {
            //var wssv = new WebSocketServer(6999);
            //var wssv = new WebSocketServer("ws://localhost:6999");  // for some reason, this doesn't work... :(
            var wssv = new WebSocketServer("ws://127.0.0.1:6999");  // but this does...

#if DEBUG
            wssv.Log.Level = LogLevel.Trace;
#endif

            wssv.AddWebSocketService<BridgeBehavior>("/Bridge");
            wssv.Start();
            if (wssv.IsListening)
            {
                Console.WriteLine("Listening on port {0}, and providing WebSocket services:", wssv.Port);
                foreach (var path in wssv.WebSocketServices.Paths) Console.WriteLine("- {0}", path);
            }

            arm = Robot.Create("Bridged_Robot", RobotType.ABB);
            arm.ControlMode(ControlType.Stream);

            bool validInput = false;
            bool machinaManagement = false;
            while (!validInput)
            {
                Console.WriteLine("  --> Press M or U for 'Machina' or 'User' ConnectionManagement.");
                ConsoleKeyInfo result = Console.ReadKey();
                
                if (result.KeyChar == 'm' || result.KeyChar == 'M')
                {
                    machinaManagement = true;
                    validInput = true;
                }
                else if (result.KeyChar == 'u' || result.KeyChar == 'U')
                {
                    machinaManagement = false;
                    validInput = true;
                }
            }

            if (machinaManagement)
            {
                arm.ConnectionManager(ConnectionType.Machina);
                arm.Connect();
            } 
            else
            {
                validInput = false;
                while (!validInput)
                {
                    Console.WriteLine("  --> Press L or N for 'Local' or 'Network' controller.");
                    ConsoleKeyInfo result = Console.ReadKey();

                    if (result.KeyChar == 'l' || result.KeyChar == 'L')
                    {
                        arm.Connect("127.0.0.1", 7000);
                        validInput = true;
                    }
                    else if (result.KeyChar == 'n' || result.KeyChar == 'N')
                    {
                        arm.Connect("192.168.125.1", 7000);
                        validInput = true;
                    }
                }
            }

            do
            {
                Console.WriteLine("  --> CONNECTED, please press ENTER to STOP Machina Bridge app");
            }
            while (Console.ReadKey().Key != ConsoleKey.Enter);

            Console.WriteLine("Disconnecting...");

            arm.Disconnect();
            wssv.Stop();
        }















        public static bool ExecuteInstruction(string[] args)
        {
            if (args[0].Equals("Move", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Move(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
            }
            else if (args[0].Equals("MoveTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.MoveTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
            }
            //else if (args[0].Equals("Transform", StringComparison.CurrentCultureIgnoreCase))
            //{
            //    return arm.Transform(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
            //        Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]),
            //        Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]));
            //}
            else if (args[0].Equals("TransformTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.TransformTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]),
                    Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]));
            }
            else if (args[0].Equals("Rotate", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Rotate(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4]));
            }
            else if (args[0].Equals("RotateTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.RotateTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("Axes", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Axes(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("AxesTo", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.AxesTo(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
                    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]));
            }
            else if (args[0].Equals("Speed", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.Speed(Convert.ToInt32(args[1]));
                return true;
            }
            else if (args[0].Equals("SpeedTo", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.SpeedTo(Convert.ToInt32(args[1]));
                return true;
            }
            else if (args[0].Equals("Precision", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.Precision(Convert.ToInt32(args[1]));
                return true;
            }
            else if (args[0].Equals("PrecisionTo", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.PrecisionTo(Convert.ToInt32(args[1]));
                return true;
            }
            else if (args[0].Equals("MotionMode", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.MotionMode(args[1]);
            }
            else if (args[0].Equals("PushSettings", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.PushSettings();
                return true;
            }
            else if (args[0].Equals("PopSettings", StringComparison.CurrentCultureIgnoreCase))
            {
                arm.PopSettings();
                return true;
            }
            else if (args[0].Equals("Wait", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Wait(Convert.ToInt32(args[1]));
            }
            else if (args[0].Equals("Message", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Message(args[1]);
            }

            // For the time being, new Tool will not be
            //      Tool(string name, Point TCPPosition, Orientation TCPOrientation, double weightKg, Point centerOfGravity)
            // but an itemized version of it:
            //      Tool(name, x, y, z, x0, x1, x2, y0, y1, y2, wightkg, gx, gy, gz);
            else if (args[0].Equals("new Tool", StringComparison.CurrentCultureIgnoreCase))
            {
                Tool t = new Tool(args[1],
                    new Point(Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4])),
                    new Orientation(Convert.ToDouble(args[5]), Convert.ToDouble(args[6]), Convert.ToDouble(args[7]), Convert.ToDouble(args[8]), Convert.ToDouble(args[9]), Convert.ToDouble(args[10])),
                    Convert.ToDouble(args[11]),
                    new Point(Convert.ToDouble(args[12]), Convert.ToDouble(args[13]), Convert.ToDouble(args[14])));

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

                return arm.Attach(t);
            }
            else if (args[0].Equals("Detach", StringComparison.CurrentCultureIgnoreCase))
            {
                return arm.Detach();
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
            while(s[0] == ' ')
            {
                s = s.Remove(0, 1);
            }

            while(s[s.Length - 1] == ' ')
            {
                s = s.Remove(s.Length - 1); 
            }

            return s;
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
            Program.ExecuteInstruction(Program.ParseMessage(e.Data));
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
    }


}
