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
using MAction = Machina.Action;

namespace MachinaBridge
{

    // https://stackoverflow.com/a/14957478/1934487
    public class BoundContent : INotifyPropertyChanged
    {
        MainWindow _parent;

        //   ██████╗ ██████╗ ███╗   ██╗███████╗ ██████╗ ██╗     ███████╗
        //  ██╔════╝██╔═══██╗████╗  ██║██╔════╝██╔═══██╗██║     ██╔════╝
        //  ██║     ██║   ██║██╔██╗ ██║███████╗██║   ██║██║     █████╗  
        //  ██║     ██║   ██║██║╚██╗██║╚════██║██║   ██║██║     ██╔══╝  
        //  ╚██████╗╚██████╔╝██║ ╚████║███████║╚██████╔╝███████╗███████╗
        //   ╚═════╝ ╚═════╝ ╚═╝  ╚═══╝╚══════╝ ╚═════╝ ╚══════╝╚══════╝
        //                                                              
        private List<string> _consoleInputBuffer = new List<string>();
        private int _bufferPointer = -1;
        private string _unfinished;  // stores an unfinished instruction while navigating the buffer

        ObservableCollection<LoggerArgs> consoleOutput = new ObservableCollection<LoggerArgs>() { new LoggerArgs(null, Machina.LogLevel.INFO, "## MACHINA Console ##") };

        public string ConsoleInput
        {
            get
            {
                return _consoleInputBuffer.Last();
            }
            set
            {
                _consoleInputBuffer.Add(value);
                _bufferPointer = -1;
                _unfinished = "";
                OnPropertyChanged("ConsoleInput");
            }
        }

        public bool ConsoleInputBack()
        {
            // If traversing the buffer for first time, store unfinished string
            if (_bufferPointer == -1)
            {
                _unfinished = _parent.InputBlock.Text;
            }
            _bufferPointer++;
            if (_bufferPointer > _consoleInputBuffer.Count - 1)
                _bufferPointer = _consoleInputBuffer.Count - 1;
            _parent.InputBlock.Text = _consoleInputBuffer[_consoleInputBuffer.Count - 1 - _bufferPointer];
            return true;
        }

        public bool ConsoleInputForward()
        {
            _bufferPointer--;
            if (_bufferPointer < 0)
            {
                // Go back to unfinished line if applicable
                _bufferPointer = -1;
                _parent.InputBlock.Text = _unfinished;
            }
            else
            {
                _parent.InputBlock.Text = _consoleInputBuffer[_consoleInputBuffer.Count - 1 - _bufferPointer];
            }
            return true;
        }


        public ObservableCollection<LoggerArgs> ConsoleOutput
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

        public BoundContent(MainWindow parent)
        {
            this._parent = parent;
            _consoleInputBuffer.Add("");
        }

        public void ClearConsoleOutput()
        {
            consoleOutput.Clear();
            OnPropertyChanged("ConsoleOutput");
        }


        //public void WriteLine(string line)
        //{
        //    ConsoleOutput.Add(new LoggerArgs(null, Machina.LogLevel.INFO, line));
        //    _parent.Scroller.ScrollToBottom();
        //}


        public void RunCommand()
        {
            //this.WriteLine(ConsoleInput);
            ConsoleOutput.Add(new LoggerArgs(null, Machina.LogLevel.INFO, $"Issuing \"{ConsoleInput}\""));
            _parent.ConsoleScroller.ScrollToBottom();

            if (_parent.bot == null)
            {
                //MainWindow.wssv.WebSocketServices.Broadcast($"{{\"msg\":\"disconnected\",\"data\":[]}}");
                Machina.Logger.Error("Not connected to any Robot...");
            }
            else
            {
                _parent.ExecuteInstruction(ConsoleInput);
            }

            this._parent.InputBlock.Text = String.Empty;
        }



        //   ██████╗ ██╗   ██╗███████╗██╗   ██╗███████╗
        //  ██╔═══██╗██║   ██║██╔════╝██║   ██║██╔════╝
        //  ██║   ██║██║   ██║█████╗  ██║   ██║█████╗  
        //  ██║▄▄ ██║██║   ██║██╔══╝  ██║   ██║██╔══╝  
        //  ╚██████╔╝╚██████╔╝███████╗╚██████╔╝███████╗
        //   ╚══▀▀═╝  ╚═════╝ ╚══════╝ ╚═════╝ ╚══════╝
        //                                             
        ObservableCollection<ActionWrapper> actionsQueue = new ObservableCollection<ActionWrapper>();
        private int _lastReleasedIndex = 0;

        public ObservableCollection<ActionWrapper> ActionsQueue
        {
            get
            {
                return actionsQueue;
            }
            set
            {
                actionsQueue = value;
                OnPropertyChanged("ActionsQueue");
            }
        }

        public int FlagActionAs(MAction action, ExecutionState state)
        {
            bool found = false;
            for (int i = _lastReleasedIndex; i < actionsQueue.Count; i++)
            {
                if (actionsQueue[i].Id == action.Id)
                {
                    actionsQueue[i].State = state;
                    found = true;
                    _lastReleasedIndex = i;
                    return i;
                }
            }

            // If something went wrong with indexing, give it another round just in case
            if (!found)
            {
                Logger.Debug($"Second round on FlagActionAs {state} for {action}");
                for (int i = 0; i < actionsQueue.Count; i++)
                {
                    if (actionsQueue[i].Id == action.Id)
                    {
                        actionsQueue[i].State = state;
                        found = true;
                        _lastReleasedIndex = i;
                        return i;
                    }
                }
            }

            return -1;
        }






        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }



    public class ActionWrapper : INotifyPropertyChanged
    {
        private MAction _action;

        public string QueueName => ToQueueString();
        public ExecutionState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                OnPropertyChanged("State");
                OnPropertyChanged("QueueName");
            }
        }
        public int Id => this._action.Id;

        private ExecutionState _state = ExecutionState.Issued;

        public ActionWrapper(MAction action)
        {
            this._action = action;
        }

        private string ToQueueString()
        {
            return $"[{StateChar()}] #{Id} {this._action.ToInstruction()}";
        }

        private char StateChar()
        {
            switch(State)
            {
                case ExecutionState.Released:
                    return '.';

                case ExecutionState.Executing:
                    return '>';

                case ExecutionState.Executed:
                    return 'x';

                default:
                    return ' ';
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}


