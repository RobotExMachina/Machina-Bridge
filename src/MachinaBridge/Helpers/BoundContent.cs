﻿using System;
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
        MachinaBridgeWindow _parent;

        public BoundContent(MachinaBridgeWindow parent)
        {
            this._parent = parent;
            _consoleInputBuffer.Add("");
        }


        //   ██████╗ ██████╗ ███╗   ██╗███████╗ ██████╗ ██╗     ███████╗
        //  ██╔════╝██╔═══██╗████╗  ██║██╔════╝██╔═══██╗██║     ██╔════╝
        //  ██║     ██║   ██║██╔██╗ ██║███████╗██║   ██║██║     █████╗  
        //  ██║     ██║   ██║██║╚██╗██║╚════██║██║   ██║██║     ██╔══╝  
        //  ╚██████╗╚██████╔╝██║ ╚████║███████║╚██████╔╝███████╗███████╗
        //   ╚═════╝ ╚═════╝ ╚═╝  ╚═══╝╚══════╝ ╚═════╝ ╚══════╝╚══════╝
        //                                                              

        private const int MAX_CONSOLE_ELEMENTS = 1000;
        private const int MIN_CONSOLE_ELEMENTS = 500;

        ObservableCollection<LoggerArgs> _consoleOutput = new ObservableCollection<LoggerArgs>() { new LoggerArgs(null, Machina.LogLevel.INFO, "## MACHINA Console ##") };

        public ObservableCollection<LoggerArgs> ConsoleOutput
        {
            get
            {
                return _consoleOutput;
            }
            set
            {
                _consoleOutput = value;
                OnPropertyChanged("ConsoleOutput");
            }
        }

        /// <summary>
        /// Add a LoggerArgs item to the console.
        /// </summary>
        /// <param name="e"></param>
        public void AddConsoleOutput(LoggerArgs e)
        {
            _consoleOutput.Add(e);

            if (_consoleOutput.Count > MAX_CONSOLE_ELEMENTS)
            {
                Logger.Debug("Reducing console buffer with " + _consoleOutput.Count + " to " + MIN_CONSOLE_ELEMENTS + " entries.");

                // Is this really the most optimal way of doing this? Is there no `RemoveRange`?
                while (_consoleOutput.Count > MIN_CONSOLE_ELEMENTS)
                {
                    _consoleOutput.RemoveAt(0);
                }
            }

            _parent.ConsoleScroller.ScrollToBottom();
        }


        public void ClearConsoleOutput()
        {
            _consoleOutput.Clear();
            OnPropertyChanged("ConsoleOutput");
        }

        private List<string> _consoleInputBuffer = new List<string>();
        private int _bufferPointer = -1;
        private string _unfinished;  // stores an unfinished instruction while navigating the buffer

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
            {
                _bufferPointer = _consoleInputBuffer.Count - 1;
            }
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

            // Move caret to end
            _parent.InputBlock.CaretIndex = _parent.InputBlock.Text.Length;

            return true;
        }

        public void RunConsoleInput()
        {
            if (_parent.bot == null)
            {
                //MainWindow.wssv.WebSocketServices.Broadcast($"{{\"msg\":\"disconnected\",\"data\":[]}}");
                Machina.Logger.Error("Not connected to any Robot...");
            }
            else
            {
                string[] instructions = Machina.Utilities.Parsing.SplitStatements(ConsoleInput, ';', "//");

                foreach (var instruction in instructions)
                {
                    ConsoleOutput.Add(new LoggerArgs(null, Machina.LogLevel.INFO, $"Issuing \"{instruction}\""));
                    _parent.ExecuteStatement(instruction);
                }

                _parent.ConsoleScroller.ScrollToBottom();
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

        private const int MAX_EXECUTED_ACTIONS = 10;
        private const int MIN_EXECUTED_ACTIONS = 5;
        internal bool queueClearExecuted = true;
        internal bool queueFollowPointer = true;

        private ObservableCollection<ActionWrapper> _actionsQueue = new ObservableCollection<ActionWrapper>();

        /// <summary>
        /// An Observable Collection of Actions, to be kept track of.
        /// </summary>
        public ObservableCollection<ActionWrapper> ActionsQueue
        {
            get
            {
                return _actionsQueue;
            }
            set
            {
                _actionsQueue = value;
                OnPropertyChanged("ActionsQueue");
            }
        }

        /// <summary>
        /// Keeps track of the last index where an action in such state was found on the queue. 
        /// Useful to optimize searching actions by id without having to roll through the whole list...
        /// </summary>
        private Dictionary<ExecutionState, int> _lastIndex = new Dictionary<ExecutionState, int>()
        {
            { ExecutionState.Issued, 0 },
            { ExecutionState.Released, 0 },
            { ExecutionState.Executing, 0 },
            { ExecutionState.Executed, 0 }
        };

        private Dictionary<ExecutionState, int> _actionsStateCount = new Dictionary<ExecutionState, int>()
        {
            { ExecutionState.Issued, 0 },
            { ExecutionState.Released, 0 },
            { ExecutionState.Executing, 0 },
            { ExecutionState.Executed, 0 }
        };


        /// <summary>
        /// Adds a wrapped Action to the queue.
        /// </summary>
        /// <param name="aw"></param>
        public void AddActionToQueue(ActionWrapper aw) 
        {
            _actionsQueue.Add(aw);

            _actionsStateCount[aw.State]++;
            
            OnPropertyChanged("ActionsQueue");
        }

        /// <summary>
        /// Searches the action queue for an Action by id, and changes its display state.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public int FlagActionAs(MAction action, ExecutionState state)
        {
            // Since ids are usually correlative, start by last state index to quickly find the searched action,
            // and loop back into start if necessary. 
            int it = _lastIndex[state];
            if (it >= _actionsQueue.Count) it = 0;
            for (int i = 0; i < _actionsQueue.Count; i++)
            {
                if (_actionsQueue[it].Id == action.Id)
                {
                    _actionsStateCount[_actionsQueue[it].State]--;
                    _actionsStateCount[state]++;

                    _actionsQueue[it].State = state;
                    _lastIndex[state] = i;

                    if (state == ExecutionState.Executed)
                    {
                        FlagNextActionAsExecuting(it);
                    }

                    return it;
                }

                it++;
                if (it >= _actionsQueue.Count)
                {
                    it = 0;
                }

            }

            return -1;
        }



        public void FlagNextActionAsExecuting(int index)
        {
            if (index < _actionsQueue.Count - 1)
            {
                _actionsStateCount[_actionsQueue[index + 1].State]--;
                
                _actionsQueue[index + 1].State = ExecutionState.Executing;
                _actionsStateCount[ExecutionState.Executing]++;
            }
        }


        /// <summary>
        /// Clears all actions from the queue UI.
        /// </summary>
        public void ClearActionsQueueAll()
        {
            _actionsQueue.Clear();
            OnPropertyChanged("ActionsQueue");
        }

        //public void CheckMaxExecutedActions()
        //{
        //    if (queueClearExecuted && _actionsStateCount[ExecutionState.Executed] > MAX_EXECUTED_ACTIONS)
        //    {
        //        int count = MAX_EXECUTED_ACTIONS - MIN_EXECUTED_ACTIONS;
        //        RemoveActionsByState(ExecutionState.Executed, count);
        //    }
        //}

        /// <summary>
        /// Will remove the first count Actions that match this state. 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="count"></param>
        public void RemoveActionsByState(ExecutionState state, int count)
        {

            int i = 0,
              removed = 0;

            while (removed < count && i < _actionsQueue.Count)
            {
                var a = _actionsQueue[i];
                if (a.State == state)
                {
                    ActionsQueue.RemoveAt(i);
                    _actionsStateCount[state]--;
                    removed++;
                }
                else
                {
                    i++;
                }
            }

            Logger.Debug("Removed " + removed + " " + state.ToString() + " from queue buffer.");
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }




    //   █████╗  ██████╗████████╗██╗ ██████╗ ███╗   ██╗           
    //  ██╔══██╗██╔════╝╚══██╔══╝██║██╔═══██╗████╗  ██║           
    //  ███████║██║        ██║   ██║██║   ██║██╔██╗ ██║           
    //  ██╔══██║██║        ██║   ██║██║   ██║██║╚██╗██║           
    //  ██║  ██║╚██████╗   ██║   ██║╚██████╔╝██║ ╚████║           
    //  ╚═╝  ╚═╝ ╚═════╝   ╚═╝   ╚═╝ ╚═════╝ ╚═╝  ╚═══╝           
    //                                                            
    //  ██╗    ██╗██████╗  █████╗ ██████╗ ██████╗ ███████╗██████╗ 
    //  ██║    ██║██╔══██╗██╔══██╗██╔══██╗██╔══██╗██╔════╝██╔══██╗
    //  ██║ █╗ ██║██████╔╝███████║██████╔╝██████╔╝█████╗  ██████╔╝
    //  ██║███╗██║██╔══██╗██╔══██║██╔═══╝ ██╔═══╝ ██╔══╝  ██╔══██╗
    //  ╚███╔███╔╝██║  ██║██║  ██║██║     ██║     ███████╗██║  ██║
    //   ╚══╝╚══╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝     ╚══════╝╚═╝  ╚═╝
    //                                                            
    /// <summary>
    /// Just a quick wrapper to add some UI-related properties to Action objects.
    /// </summary>
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

        public int TextMode
        {
            get
            {
                return _textMode; 
            }
            set
            {
                _textMode = value;
                OnPropertyChanged("QueueName");
            }
        }
        private int _textMode = 1;  // 0 for .ToString, 1 for ToInstruction

        public ActionWrapper(MAction action)
        {
            this._action = action;
        }

        private string ToQueueString()
        {
            return $"[{StateChar()}] #{Id} {(_textMode == 1 ? this._action.ToInstruction() : this._action.ToString())}";
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


