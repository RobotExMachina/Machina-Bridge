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
using System.IO.Compression;
using System.Text.RegularExpressions;


namespace MachinaBridge
{
    public partial class MachinaBridgeWindow : Window
    {
        private bool wasInputBlockClicked = false;
        private Machina.LogLevel _maxLogLevel;
        private bool sendOnEnter = true;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InputBlock.PreviewKeyDown += InputBlock_PreviewKeyDown;
            InputBlock.KeyDown += InputBlock_KeyDown;
            InputBlock.PreviewMouseDown += InputBlock_PreviewMouseDown;
            //InputBlock.Focus();  // want the user to click on the InputBlock to delete text

            InputBlock.Text = "Enter any command to stream it to the robot...";
        }

        /// <summary>
        /// Enables pasting text with newline characters.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputBlock_Paste(object sender, DataObjectPastingEventArgs e)
        {
            // Tried https://stackoverflow.com/a/3061506/1934487 btu e.Handled wouldn't work
            // Implemented by toggling AcceptsReturn on for proper system handling of text pasting,
            // and posting a threaded call to turn it off. Not great, but does the trick... 
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) return;

            InputBlock.AcceptsReturn = true;
            uiContext.Post(x => { InputBlock.AcceptsReturn = false; }, null);
        }


        /// <summary>
        /// Arrow keys are not handled by KeyDown, must use PreviewKeyDown instead.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputBlock_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Don't do this for every single keystroke!
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                int dir = 0;
                bool isInputMultiline = Regex.IsMatch(InputBlock.Text, Environment.NewLine);

                if (isInputMultiline)
                {
                    // Figure out what to do here
                    var caretIndex = InputBlock.CaretIndex;
                    string[] lines = InputBlock.Text.Split(new string[] {Environment.NewLine}, StringSplitOptions.None);
                    // If caret on top line
                    if (e.Key == Key.Up && caretIndex < lines[0].Length)
                    {
                        dir = -1;
                    }
                    // If caret on last line
                    else if (e.Key == Key.Down && caretIndex >= (InputBlock.Text.Length - lines[lines.Length - 1].Length))
                    {
                        dir = 1;
                    }
                }
                else
                {
                    if (e.Key == Key.Up)
                    {
                        dir = -1;
                    }
                    else if (e.Key == Key.Down)
                    {
                        dir = 1;
                    }
                }

                // Move or not
                switch (dir)
                {
                    case -1:
                        if (this.dc.ConsoleInputBack())
                        {
                            //ChangeCaretToEnd(InputBlock);
                        }
                        break;

                    case 1:
                        if (this.dc.ConsoleInputForward())
                        {
                            //ChangeCaretToBeginning(InputBlock);
                        }
                        break;

                    default:
                        // do nothing!
                        break;
                }
            }
            
        }

        public void ChangeCaretToBeginning(TextBox txtBox)
        {
            txtBox.CaretIndex = 0;
        }

        public void ChangeCaretToEnd(TextBox txtBox)
        {
            txtBox.CaretIndex = txtBox.Text.Length - 1;
        }


        private void InputBlock_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (InputBlock.Text.Length == 0) return;

                if (sendOnEnter)
                {
                    IssueInputBlock();
                }
                else
                {
                    // If Ctrl+Enter, issue commands
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        IssueInputBlock();
                    }
                    // Otherwise, new line
                    else
                    {
                        // https://stackoverflow.com/a/10549299/1934487
                        var caretIndex = InputBlock.CaretIndex;
                        InputBlock.Text = InputBlock.Text.Insert(caretIndex, System.Environment.NewLine);
                        InputBlock.CaretIndex = caretIndex + 1;
                    }
                }
            }
        }
        
        internal void IssueInputBlock()
        {
            dc.ConsoleInput = InputBlock.Text;
            dc.RunConsoleInput();
            InputBlock.Focus();
        }



        /// <summary>
        /// Quick and dirty clear the ConsoleInput on first click.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputBlock_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!wasInputBlockClicked)
            {
                InputBlock.Text = "";
                wasInputBlockClicked = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Only give warnings if connected
            if (bot == null)
            {
                StopWebSocketServer();
                return;
            }

            string msg = "WARNING: Closing Machina Bridge. Unpredicted behavior may occur from unfinished programs. Are you sure?";
            MessageBoxResult result =
              MessageBox.Show(
                msg,
                "Exiting Machina",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                StopWebSocketServer();
                Disconnect();
            }
        }

        private void combo_Brand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (combo_Robot_Brand == null) return;

            //Console.WriteLine("BRAND CHANGED");
            var comboitem = combo_Robot_Brand.SelectedItem as ComboBoxItem;
            _robotBrand = comboitem.Content.ToString();
            //Console.WriteLine(_robotBrand);

            if (combo_Manager == null) return;

            // For UR, there is no support for machina manager
            if (_robotBrand.Equals("UR", StringComparison.CurrentCultureIgnoreCase))
            {
                foreach (ComboBoxItem item in combo_Manager.Items)
                {
                    if (item.Content.ToString() == "USER")
                    {
                        combo_Manager.SelectedValue = item;
                        EnableElement(combo_Manager, false);
                        break;
                    }
                }
            }
            else
            {
                EnableElement(combo_Manager, true);
                foreach (ComboBoxItem item in combo_Manager.Items)
                {
                    if (item.Content.ToString() == "USER")
                    {
                        combo_Manager.SelectedValue = item;
                        break;
                    }
                }
            }

            ManageDownloadButtonVisibility();
        }

        private void txtbox_Name_SelectionChanged(object sender, RoutedEventArgs e)
        {
            _robotName = txtbox_Robot_Name.Text;
        }

        private void combo_Manager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (combo_Manager == null) return;

            //Console.WriteLine("MANAGER CHANGED");
            var comboitem = combo_Manager.SelectedItem as ComboBoxItem;
            _connectionManager = comboitem.Content.ToString();
            //Console.WriteLine(_connectionManager);

            bool enable = _connectionManager.Equals("user", StringComparison.CurrentCultureIgnoreCase);
            EnableElement(txtbox_IP, enable);
            EnableElement(txtbox_Port, enable);

            ManageDownloadButtonVisibility();
        }

        private void btn_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (btn_Connect.Content.ToString() == "CONNECT")
            {
                if (InitializeRobot())
                {
                    btn_Connect.Content = "DISCONNECT";
                    EnableElement(txtbox_Robot_Name, false);
                    EnableElement(combo_Robot_Brand, false);
                    EnableElement(combo_Manager, false);
                    EnableElement(txtbox_IP, false);
                    EnableElement(txtbox_Port, false);
                    UpdateRobotStatus();
                }
            }
            else if (btn_Connect.Content.ToString() == "DISCONNECT")
            {
                DisposeRobot();
                EnableElement(txtbox_Robot_Name, true);
                EnableElement(combo_Robot_Brand, true);
                if (_robotBrand != "UR")
                {
                    EnableElement(combo_Manager, true);
                }
                if (_connectionManager != "MACHINA")
                {
                    EnableElement(txtbox_IP, true);
                    EnableElement(txtbox_Port, true);
                }
                btn_Connect.Content = "CONNECT";
                UpdateRobotStatus();
            }
        }

        private void combo_LogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            ComboBoxItem item = box.SelectedItem as ComboBoxItem;
            _maxLogLevel = (Machina.LogLevel)Convert.ToInt32(item.Tag);
        }

        private void combo_QueueTextMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            ComboBoxItem item = box.SelectedItem as ComboBoxItem;
            try
            {
                string mode = item.Content.ToString();
                int tm = 0;
                if (mode.Equals("INSTRUCTION"))
                {
                    tm = 1;
                }

                foreach (ActionWrapper a in this.dc.ActionsQueue)
                {
                    a.TextMode = tm;
                }
            }
            catch (Exception ex)
            {
                Machina.Logger.Error("Something went wrong changing Queue text mode:");
                Machina.Logger.Error(ex.ToString());
            }

        }

        private void cbx_ClearExecuted_Checked(object sender, RoutedEventArgs e)
        {
            Machina.Logger.Warning("Clear executed actions still not implemented...");
        }

        private void cbx_ClearExecuted_Unchecked(object sender, RoutedEventArgs e)
        {
            Machina.Logger.Warning("Clear executed actions still not implemented...");
        }

        private void btn_ConsoleClear_Click(object sender, RoutedEventArgs e)
        {
            dc.ClearConsoleOutput();
        }

        private void btn_QueueClear_Click(object sender, RoutedEventArgs e)
        {
            dc.ClearActionsQueueAll();
        }

        private void btn_DownloadDrivers_Click(object sender, RoutedEventArgs e)
        {
            DownloadDrivers();
        }

        private void btn_ResetBridge_Click(object sender, RoutedEventArgs e)
        {
            InitializeWebSocketServer();
        }

        private void btn_InputBlock_Send_Click(object sender, RoutedEventArgs e)
        {
            IssueInputBlock();
        }

        private void cbx_EnterSends_Checked(object sender, RoutedEventArgs e)
        {
            sendOnEnter = true;
        }

        private void cbx_EnterSends_Unchecked(object sender, RoutedEventArgs e)
        {
            sendOnEnter = false;
        }






        private void ManageDownloadButtonVisibility()
        {
            if (btn_DownloadDrivers == null) return;

            if (_robotBrand.Equals("ABB") && _connectionManager.Equals("USER"))
            {
                btn_DownloadDrivers.Visibility = Visibility.Visible;
            }
            else
            {
                btn_DownloadDrivers.Visibility = Visibility.Hidden;
            }
        }

        private void EnableElement(ComboBox comboBox, bool enabled)
        {
            if (comboBox != null)
            {
                comboBox.IsEnabled = enabled;
                //comboBox.IsEditable = enabled;
                comboBox.IsHitTestVisible = enabled;
                comboBox.Focusable = enabled;
            }
        }

        private void EnableElement(TextBox textBox, bool enabled)
        {
            if (textBox != null)
            {
                textBox.IsEnabled = enabled;
            }
        }




    }
}
