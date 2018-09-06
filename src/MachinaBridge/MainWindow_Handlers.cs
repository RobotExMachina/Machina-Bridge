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


namespace MachinaBridge
{
    public partial class MainWindow : Window
    {
        int _lineId = -1;
        bool wasInputBlockClicked = false;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine("MAIN WINDOW LOADED");
            InputBlock.PreviewKeyDown += InputBlock_PreviewKeyDown;
            InputBlock.KeyDown += InputBlock_KeyDown;
            InputBlock.PreviewMouseDown += InputBlock_PreviewMouseDown;
            //InputBlock.Focus();  // want the user to click on the InputBlock to delete text
        }



        /// <summary>
        /// Arrow keys are not handled by KeyDown, must use PreviewKeyDown instead.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputBlock_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                //Console.WriteLine("UP " + _lineId);
                _lineId++;
                if (_lineId > dc.ConsoleOutput.Count - 1)
                    _lineId = dc.ConsoleOutput.Count - 1;
                InputBlock.Text = dc.ConsoleOutput[dc.ConsoleOutput.Count - 1 - _lineId];
            }
            else if (e.Key == Key.Down)
            {
                //Console.WriteLine("DOWN " + _lineId);
                _lineId--;
                if (_lineId < 0)
                    _lineId = 0;
                InputBlock.Text = dc.ConsoleOutput[dc.ConsoleOutput.Count - 1 - _lineId];
            }
        }

        private void InputBlock_KeyDown(object sender, KeyEventArgs e)
        {
            //Console.WriteLine("key " + e.Key.ToString());
            if (e.Key == Key.Enter)
            {
                if (InputBlock.Text.Length == 0) return;

                dc.ConsoleInput = InputBlock.Text;
                dc.RunCommand();
                InputBlock.Focus();
                //Scroller.ScrollToBottom();  // moved to ConsoleContent.Writeline
                _lineId = -1;
            }
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
                this.dc.ConsoleInput = "";
                wasInputBlockClicked = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Only give warnings if connected
            if (bot == null)
            {
                StopWebSocketService();
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
                StopWebSocketService();
                Disconnect();
            }
        }

        private void combo_Brand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (combo_Brand == null) return;

            //Console.WriteLine("BRAND CHANGED");
            var comboitem = combo_Brand.SelectedItem as ComboBoxItem;
            _robotBrand = comboitem.Content.ToString();
            //Console.WriteLine(_robotBrand);

            if (combo_Manager == null) return;

            // For UR, there is no support for machina manager
            if (_robotBrand.Equals("UR", StringComparison.CurrentCultureIgnoreCase))
            {
                foreach(ComboBoxItem item in combo_Manager.Items)
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
        }

        private void txtbox_Name_SelectionChanged(object sender, RoutedEventArgs e)
        {
            _robotName = txtbox_Name.Text;
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
        }

        private void btn_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (btn_Connect.Content.ToString() == "CONNECT")
            {
                if(InitializeRobot())
                {
                    btn_Connect.Content = "DISCONNECT";
                    EnableElement(txtbox_Name, false);
                    EnableElement(combo_Brand, false);
                    EnableElement(combo_Manager, false);
                    EnableElement(txtbox_IP, false);
                    EnableElement(txtbox_Port, false);
                }
            }
            else if (btn_Connect.Content.ToString() == "DISCONNECT")
            {
                DisposeRobot();
                EnableElement(txtbox_Name, true);
                EnableElement(combo_Brand, true);
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
