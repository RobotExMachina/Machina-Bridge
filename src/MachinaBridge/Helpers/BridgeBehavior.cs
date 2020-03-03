using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Machina;
using Logger = Machina.Logger;
using WebSocketSharp;
using WebSocketSharp.Server;


namespace MachinaBridge
{

    public class BridgeBehavior : WebSocketBehavior
    {
        private Robot _robot;
        private MachinaBridgeWindow _parent;
        private string _clientName;

        public BridgeBehavior(Robot robot, MachinaBridgeWindow parent)
        {
            this._robot = robot;
            this._parent = parent;
        }

        protected override void OnOpen()
        {
            //base.OnOpen();
            //Console.WriteLine("  BRIDGE: opening bridge");
            _clientName = Context.QueryString["name"];
            _parent._connectedClients.Add(_clientName);
            //_parent.UpdateClientBox();
            _parent.uiContext.Post(x =>
            {
                _parent.UpdateClientBox();
            }, null);

            Logger.Info("Client \"" + _clientName + "\" connected...");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            //base.OnMessage(e);
            //Console.WriteLine("  BRIDGE: received message: " + e.Data);
            if (_robot == null || _parent.bot == null)
            {
                _parent.wssv.WebSocketServices.Broadcast($"{{\"event\":\"controller-disconnected\"}}");
                return;
            }

            Logger.Verbose("Action from \"" + _clientName + "\": " + e.Data);

            _parent.ExecuteInstructionOnContext(e.Data);
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            //base.OnError(e);
            //Console.WriteLine("  BRIDGE ERROR: " + e.Message);
            Logger.Error("WS error: " + e.Message);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            //base.OnClose(e);
            Logger.Debug($"WS client disconnected: {e.Code} {e.Reason}");
            Logger.Warning("Client \"" + _clientName + "\" disconnected...");
            _parent._connectedClients.Remove(_clientName);
            //_parent.UpdateClientBox();
            _parent.uiContext.Post(x =>
            {
                _parent.UpdateClientBox();
            }, null);

            _parent.wssv.WebSocketServices.Broadcast($"{{\"event\":\"client-disconnected\",\"user\":\"clientname\"}}");
        }

    }
}
