﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using NationalInstruments.UI.WindowsForms;
using NationalInstruments.UI;

namespace TransferCavityLock2012
{
    class UIHelper
    {
        public static void SetCheckBox(CheckBox box, bool state)
        {
            box.Invoke(new SetCheckDelegate(SetCheckHelper), new object[] { box, state });
        }
        private delegate void SetCheckDelegate(CheckBox box, bool state);
        private static void SetCheckHelper(CheckBox box, bool state)
        {
            box.Checked = state;
        }

        public static void SetTextBox(TextBox box, string text)
        {
            box.Invoke(new SetTextDelegate(SetTextHelper), new object[] { box, text });
        }
        private delegate void SetTextDelegate(TextBox box, string text);
        private static void SetTextHelper(TextBox box, string text)
        {
            box.Text = text;
        }

        public static void SetLED(Led led, bool val)
        {
            led.Invoke(new SetLedDelegate(SetLedHelper), new object[] { led, val });
        }
        private delegate void SetLedDelegate(Led led, bool val);
        private static void SetLedHelper(Led led, bool val)
        {
            led.Value = val;
        }

        public static void EnableControl(Control control, bool enabled)
        {
            control.Invoke(new EnableControlDelegate(EnableControlHelper), new object[] { control, enabled });
        }
        private delegate void EnableControlDelegate(Control control, bool enabled);
        private static void EnableControlHelper(Control control, bool enabled)
        {
            control.Enabled = enabled;
        }

        private delegate void plotScatterGraphDelegate(ScatterPlot plot, double[] x, double[] y);
        private static void plotScatterGraphHelper(ScatterPlot plot, double[] x, double[] y)
        {
            plot.ClearData();
            plot.PlotXY(x, y);
        }

        public static void scatterGraphPlot(ScatterGraph graph, ScatterPlot plot, double[] x, double[] y)
        {
            graph.Invoke(new UIHelper.plotScatterGraphDelegate(plotScatterGraphHelper), new object[] { plot, x, y });
        }


        private delegate void PlotXYDelegate(double[] x, double[] y);
        public static void appendPointToScatterGraph(Graph graph, ScatterPlot plot, double x, double y)
        {
            graph.Invoke(new PlotXYDelegate(plot.PlotXYAppend), new Object[] { new double[] { x }, new double[] { y } });
        }

        private delegate void ClearDataDelegate();
        public static void ClearGraph(Graph graph)
        {
            graph.Invoke(new ClearDataDelegate(graph.ClearData));
        }
    }
}