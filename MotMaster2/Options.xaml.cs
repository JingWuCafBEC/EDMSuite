﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilsNS;

namespace MOTMaster2
{

    public class GeneralOptions
    {
        public enum SaveOption { save, ask, nosave }

        public SaveOption saveSequence;

        public enum DataLogOption { allData, average}

        public DataLogOption dataLog;

        public enum M2CommOption { on, off}
        public M2CommOption m2Comm;

        public enum AISaveOption { rawData,average,both}
        public AISaveOption aiSaveMode;

        public int AISampleRate { get; set; }
        public int PreTrigSamples { get; set; }
        public double RiseTime { get; set; }

        public bool AIEnable { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "genOptions.cfg", fileJson);
        }

    }

    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class OptionWindow : Window
    {
        public OptionWindow()
        {
            InitializeComponent();

            string fileJson = JsonConvert.SerializeObject(DAQ.Environment.Environs.FileSystem);
            hardwareJson = JsonConvert.SerializeObject(DAQ.Environment.Environs.Hardware, Formatting.Indented);
            LoadJsonToTreeView(hardwareTreeView, hardwareJson);
            LoadJsonToTreeView(filesystemTreeView, fileJson);
        }       
        
        string hardwareJson = "";
        void LoadJsonToTreeView(TreeView treeView, string json)
        {
            var token = JToken.Parse(json);

            var children = new List<JToken>();
            if (token != null)
            {
                children.Add(token);
            }

            treeView.ItemsSource = null;
            treeView.Items.Clear();
            treeView.ItemsSource = children;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e) // visual to internal 
        {
            if (rbSaveSeqYes.IsChecked.Value) Controller.genOptions.saveSequence = GeneralOptions.SaveOption.save;
            if (rbSaveSeqAsk.IsChecked.Value) Controller.genOptions.saveSequence = GeneralOptions.SaveOption.ask;
            if (rbSaveSeqNo.IsChecked.Value) Controller.genOptions.saveSequence = GeneralOptions.SaveOption.nosave;

            if (m2Off.IsChecked.Value) Controller.genOptions.m2Comm = GeneralOptions.M2CommOption.off;
            if (m2On.IsChecked.Value) Controller.genOptions.m2Comm = GeneralOptions.M2CommOption.on;

            if (aiEnable.IsChecked.Value) {
                Controller.genOptions.AISampleRate = Convert.ToInt32(tbSampleRate.Text);
                Controller.genOptions.PreTrigSamples = Convert.ToInt32(tbPreTrig.Text);
                Controller.genOptions.RiseTime = Convert.ToDouble(tbRiseTime.Text);
                if (aiRaw.IsChecked.Value) Controller.genOptions.aiSaveMode = GeneralOptions.AISaveOption.rawData;
                if (aiAverage.IsChecked.Value) Controller.genOptions.aiSaveMode = GeneralOptions.AISaveOption.average;
                if (aiBoth.IsChecked.Value) Controller.genOptions.aiSaveMode = GeneralOptions.AISaveOption.both;

                Controller.UpdateAIValues();
            }

            Controller.genOptions.AIEnable = aiEnable.IsChecked.Value;
            Close();
        }

        private void tabCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private bool CheckHardwareJson()
        {
            rtbModify.SelectAll();
            try
            {//json consistency
                JContainer.Parse(rtbModify.Selection.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + " (try again)");
                return false;
            }
            return true;
        }

        private void frmOptions_Loaded(object sender, RoutedEventArgs e) // internal to visual
        {
            rbSaveSeqYes.IsChecked = Controller.genOptions.saveSequence.Equals(GeneralOptions.SaveOption.save);
            rbSaveSeqAsk.IsChecked = Controller.genOptions.saveSequence.Equals(GeneralOptions.SaveOption.ask);
            rbSaveSeqNo.IsChecked = Controller.genOptions.saveSequence.Equals(GeneralOptions.SaveOption.nosave);

            m2On.IsChecked = Controller.genOptions.m2Comm.Equals(GeneralOptions.M2CommOption.on);
            m2Off.IsChecked = Controller.genOptions.m2Comm.Equals(GeneralOptions.M2CommOption.off);

            aiEnable.IsChecked = Controller.genOptions.AIEnable;

            switch (Controller.genOptions.aiSaveMode)
            {
                case GeneralOptions.AISaveOption.rawData:
                    aiRaw.IsChecked = true;
                    break;
                case GeneralOptions.AISaveOption.average:
                    aiAverage.IsChecked = true;
                    break;
                case GeneralOptions.AISaveOption.both:
                    aiBoth.IsChecked = true;
                    break;
                default:
                    break;
            }

            tbSampleRate.Text = Controller.genOptions.AISampleRate.ToString();
            tbPreTrig.Text = Controller.genOptions.PreTrigSamples.ToString();
            tbRiseTime.Text = Controller.genOptions.RiseTime.ToString();
       }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void aiEnable_Click(object sender, RoutedEventArgs e)
        {
            bool state = aiEnable.IsChecked.Value;
            tbSampleRate.IsReadOnly = !state;
            tbRiseTime.IsReadOnly = !state;
            tbPreTrig.IsReadOnly = !state;
            

            aiRaw.IsEnabled = state;
            aiAverage.IsEnabled = state;
            aiBoth.IsEnabled = state;

        }
    }

    public sealed class MethodToValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var methodName = parameter as string;
            if (value == null || methodName == null)
                return null;
            var methodInfo = value.GetType().GetMethod(methodName, new Type[0]);
            if (methodInfo == null)
                return null;
            return methodInfo.Invoke(value, new object[0]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException(GetType().Name + " can only be used for one way conversion.");
        }
    }
}