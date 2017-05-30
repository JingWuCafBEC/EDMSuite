﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using DAQ.Environment;

namespace MOTMaster2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Controller controller;
        public MainWindow()
        {
            InitializeComponent();
            InitVisuals();           
        }

        public static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                  new Action(delegate { }));
        }

        public void InitVisuals()
        {
            controller = new Controller();
            controller.StartApplication();
            btnRefresh_Click(null,null);
            tcMain.SelectedIndex = 0;
        }
        
        private string[] ParamsArray
        {
            get
            {
                string[] pa = { "param1", "param2", "param3" };
                if (controller.script != null)
                     pa= controller.script.Parameters.Keys.ToArray(); 
                return pa;               
            }
        }
        //TODO fun one cycle with defined parameters
        private bool SingleShot() // true if OK
        {
            //Would like to use RunStart as this Runs in a new thread
            if (controller.IsRunning())
            {
                controller.WaitForRunToFinish();
            }
            controller.RunStart();
 
                
            return true;
        }

        private static string
            scriptCsPath = (string)Environs.FileSystem.Paths["scriptListPath"];
        private static string
            scriptPyPath = (string)Environs.FileSystem.Paths["scriptListPath"]; 

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".cs"; // Default file extension
            dlg.Filter = "Pattern script (.cs)|*.cs"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                string filename = dlg.FileName;

                cbPatternScript.Items.Insert(0, filename); cbPatternScript.SelectedIndex = 0;
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            cbPatternScript.Items.Clear();

            string[] css = Directory.GetFiles(scriptCsPath, "*.cs");
            foreach (string cs in css)
            {
                cbPatternScript.Items.Add(cs);
            }

            string[] pys = Directory.GetFiles(scriptPyPath, "*.py");
            foreach (string py in pys)
            {
                cbPatternScript.Items.Add(py);
            }

            cbPatternScript.SelectedIndex = 0;
        }
        // RUNNING THINGS

        private bool ScanFlag = false;
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (btnRun.Content.Equals("Run"))
            {
                Interferometer.Patterns p = new Interferometer.Patterns();
                p.GetHSDIOPattern();
                btnRun.Content = "Stop";
                btnRun.Background = Brushes.LightYellow;
                ScanFlag = true;
                // Start repeat
                int n = int.Parse(tbIterNumb.Text);
                if (n <= 0)
                {
                    MessageBox.Show("<Iteration Number> must be of positive value.");
                    return;
                }

                for (int i = 0; i < n; i++)
                {
                    // single shot
                    SingleShot();

                    DoEvents();
                    if (!ScanFlag) break;
                }
                btnRun.Content = "Run";
                btnRun.Background = Brushes.LightGreen;
                ScanFlag = false;
                return;
            }

            if (btnRun.Content.Equals("Stop"))
            {
                btnRun.Content = "Run";
                btnRun.Background = Brushes.LightGreen;
                ScanFlag = false;
                // End repeat
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            if (btnScan.Content.Equals("Scan"))
            {
                btnScan.Content = "Cancel";
                btnScan.Background = Brushes.LightYellow;
                ScanFlag = true;
                
                string parameter = cbParamsScan.Text;
                object defaultValue = controller.script.Parameters[parameter];
                int scanLength;
                object[] scanArray;
                if (defaultValue is int)
                {
                    int fromScanI = int.Parse(tbFromScan.Text);
                    int toScanI = int.Parse(tbToScan.Text);
                    int byScanI = int.Parse(tbByScan.Text);
                    scanLength = (toScanI - fromScanI) /byScanI+1;
                    if (scanLength < 0)
                        {
                            MessageBox.Show("Incorrect looping parameters. <From> value must be smaller than <To> value if it increases per shot.");
                            return;
                        }
                    scanArray = new object[scanLength+1];
                    for (int i = 0; i<scanLength;i++ )
                    {
                        scanArray[i] = fromScanI;
                        fromScanI+=byScanI;
                    }   
                
                }
                else
                {
                    double fromScanD = double.Parse(tbFromScan.Text);
                    double toScanD = double.Parse(tbToScan.Text);
                    double byScanD = double.Parse(tbByScan.Text);
                    scanLength = (int)((toScanD - fromScanD) / byScanD)+1;
                    if (scanLength < 0)
                    {
                        MessageBox.Show("Incorrect looping parameters. <From> value must be smaller than <To> value if it increases per shot.");
                        return;
                    }
                    scanArray = new object[scanLength];

                    for (int i = 0; i<scanLength;i++ )
                    {
                        scanArray[i] = fromScanD;
                        fromScanD+=byScanD;
                    }
                
                 }
                foreach (object scanParam in scanArray)
                {
                    controller.script.Parameters[parameter] = scanParam;
                    SingleShot();
                    tbCurValue.Content = scanParam.ToString();
                    DoEvents();
                    if (!ScanFlag) break;
                }
                controller.script.Parameters[parameter] = defaultValue;
                tbCurValue.Content = defaultValue.ToString();
                btnScan.Content = "Scan";
                btnScan.Background = Brushes.LightGreen;
                ScanFlag = false;
                return;
            }

            if (btnScan.Content.Equals("Cancel"))
            {
                btnScan.Content = "Scan";
                btnScan.Background = Brushes.LightGreen;
                ScanFlag = false;
            }
        }

        private void cbPatternScript_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Load the new script
            if (cbPatternScript.Text != "")
            {
                controller.script = controller.prepareScript((string)cbPatternScript.SelectedItem, null);
                controller.SetScriptPath((string)cbPatternScript.SelectedItem);
            }
            //Change parameters
            tcMain.SelectedIndex = 0;           
        }

        private bool paramCheck=false;
        private void tcMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(TabControl))
            {
                if (paramCheck)
                    return;
                paramCheck = true;
                if (tcMain.SelectedIndex==1)
                {
                    cbParamsScan.Items.Clear();
                    foreach (string param in ParamsArray)
                        cbParamsScan.Items.Add(param);
                   cbParamsScan.SelectedIndex = 0;
                }
                if (tcMain.SelectedIndex == 2)
                {
                    cbParamsManual.Items.Clear();
                    foreach (string param in ParamsArray)
                        cbParamsManual.Items.Add(param);
                    cbParamsManual.Text = ParamsArray[0];
                   cbParamsManual.SelectedIndex = 0;
               
                }
                paramCheck = false;
            }

        }

        private void LoadParameters_Click(object sender, RoutedEventArgs e)
        {
            if (controller.script != null)
            { // Configure open file dialog box
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".csv"; // Default file extension
                dlg.Filter = "Parameters (.csv)|*.csv,*.txt"; // Filter files by extension

                // Show open file dialog box
                Nullable<bool> result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    string filename = dlg.FileName;
                    string[] text = File.ReadAllLines(filename);
                    
                    Dictionary<String,Object> LoadedParameters = new Dictionary<string,object>();
                    string json = File.ReadAllText(filename);
                    LoadedParameters = (Dictionary<String,Object>)JsonConvert.DeserializeObject(json,typeof(Dictionary<String,Object>));
                    if (controller.script != null)
                        foreach (string key in LoadedParameters.Keys)
                            controller.script.Parameters[key] = LoadedParameters[key];
                    else
                        MessageBox.Show("You have tried to load parameters without loading a script");
                }
            }
        }


        private void SaveParameters_Click(object sender, RoutedEventArgs e)
        {
            if (controller.script != null)
            { // Configure open file dialog box
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".csv"; // Default file extension
                dlg.Filter = "Parameters (.csv)|*.csv,*.txt"; // Filter files by extension

                // Show open file dialog box
                Nullable<bool> result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    string filename = dlg.FileName;
                    string json = JsonConvert.SerializeObject(controller.script.Parameters,Formatting.Indented);
                    File.WriteAllText(filename, json);
                }
            }
            else
                MessageBox.Show("You have tried to save parmaters before loading a script");

        }
        private void SaveEnvironment_Click(object sender, RoutedEventArgs e)
        {
            controller.SaveEnvironment();
        }
        private void LoadEnvironment_Click(object sender, RoutedEventArgs e)
        {
            controller.LoadEnvironment();
        }
        private void EditOptions_Click(object sender, RoutedEventArgs e)
        {
            Options optionsWindow = new Options();
            optionsWindow.Show();
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Latest MOTMaster Version");
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            string parameter = cbParamsManual.Text;
            if (controller.script.Parameters[parameter].GetType() == typeof(int))
            {
                controller.script.Parameters[parameter] = int.Parse(tbValue.Text);
            }
            else if (controller.script.Parameters[parameter].GetType() == typeof(double))
            {
                controller.script.Parameters[parameter] = double.Parse(tbValue.Text);
            }
        }

        private void cbParamsManual_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(ComboBox) && controller.script != null)
                tbValue.Text = controller.script.Parameters[cbParamsManual.SelectedItem.ToString()].ToString();
        }

        private void cbParamsScan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
             if (e.OriginalSource.GetType() == typeof(ComboBox) && controller.script != null)
                tbCurValue.Content = controller.script.Parameters[cbParamsScan.SelectedItem.ToString()].ToString();
        }
    }
}
