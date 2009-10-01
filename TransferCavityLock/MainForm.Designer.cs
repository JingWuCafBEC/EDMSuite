﻿namespace TransferCavityLock
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.voltageRampControl = new System.Windows.Forms.GroupBox();
            this.fitEnableCheck = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.rampLED = new NationalInstruments.UI.WindowsForms.Led();
            this.rampStopButton = new System.Windows.Forms.Button();
            this.textBox = new System.Windows.Forms.TextBox();
            this.rampStartButton = new System.Windows.Forms.Button();
            this.lockEnableCheck = new System.Windows.Forms.CheckBox();
            this.p1Intensity = new NationalInstruments.UI.WindowsForms.ScatterGraph();
            this.scatterPlot1 = new NationalInstruments.UI.ScatterPlot();
            this.xAxis1 = new NationalInstruments.UI.XAxis();
            this.yAxis1 = new NationalInstruments.UI.YAxis();
            this.p2Intensity = new NationalInstruments.UI.WindowsForms.ScatterGraph();
            this.scatterPlot2 = new NationalInstruments.UI.ScatterPlot();
            this.xAxis2 = new NationalInstruments.UI.XAxis();
            this.yAxis2 = new NationalInstruments.UI.YAxis();
            this.lockParams = new System.Windows.Forms.GroupBox();
            this.initLaserVoltageUpDownBox = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.setPointUpDownBox = new System.Windows.Forms.NumericUpDown();
            this.voltageToLaserBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.GainTrackBar = new System.Windows.Forms.TrackBar();
            this.label4 = new System.Windows.Forms.Label();
            this.voltageRampControl.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rampLED)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.p1Intensity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.p2Intensity)).BeginInit();
            this.lockParams.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.initLaserVoltageUpDownBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.setPointUpDownBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GainTrackBar)).BeginInit();
            this.SuspendLayout();
            // 
            // voltageRampControl
            // 
            this.voltageRampControl.Controls.Add(this.fitEnableCheck);
            this.voltageRampControl.Controls.Add(this.label1);
            this.voltageRampControl.Controls.Add(this.rampLED);
            this.voltageRampControl.Controls.Add(this.rampStopButton);
            this.voltageRampControl.Controls.Add(this.textBox);
            this.voltageRampControl.Controls.Add(this.rampStartButton);
            this.voltageRampControl.Location = new System.Drawing.Point(554, 3);
            this.voltageRampControl.Name = "voltageRampControl";
            this.voltageRampControl.Size = new System.Drawing.Size(244, 103);
            this.voltageRampControl.TabIndex = 2;
            this.voltageRampControl.TabStop = false;
            this.voltageRampControl.Text = "Voltage Ramp";
            this.voltageRampControl.Enter += new System.EventHandler(this.voltageRampControl_Enter);
            // 
            // fitEnableCheck
            // 
            this.fitEnableCheck.AutoSize = true;
            this.fitEnableCheck.Location = new System.Drawing.Point(102, 50);
            this.fitEnableCheck.Name = "fitEnableCheck";
            this.fitEnableCheck.Size = new System.Drawing.Size(37, 17);
            this.fitEnableCheck.TabIndex = 17;
            this.fitEnableCheck.Text = "Fit";
            this.fitEnableCheck.UseVisualStyleBackColor = true;
            this.fitEnableCheck.CheckedChanged += new System.EventHandler(this.fitEnableCheck_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(99, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Ramping:";
            // 
            // rampLED
            // 
            this.rampLED.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.rampLED.Location = new System.Drawing.Point(145, 13);
            this.rampLED.Name = "rampLED";
            this.rampLED.OffColor = System.Drawing.Color.Red;
            this.rampLED.Size = new System.Drawing.Size(31, 29);
            this.rampLED.TabIndex = 7;
            // 
            // rampStopButton
            // 
            this.rampStopButton.Location = new System.Drawing.Point(6, 46);
            this.rampStopButton.Name = "rampStopButton";
            this.rampStopButton.Size = new System.Drawing.Size(87, 23);
            this.rampStopButton.TabIndex = 6;
            this.rampStopButton.Text = "Stop ramping";
            this.rampStopButton.UseVisualStyleBackColor = true;
            this.rampStopButton.Click += new System.EventHandler(this.rampStopButton_Click);
            // 
            // textBox
            // 
            this.textBox.Location = new System.Drawing.Point(6, 75);
            this.textBox.Name = "textBox";
            this.textBox.Size = new System.Drawing.Size(162, 20);
            this.textBox.TabIndex = 3;
            this.textBox.TextChanged += new System.EventHandler(this.textBox_TextChanged);
            // 
            // rampStartButton
            // 
            this.rampStartButton.Location = new System.Drawing.Point(6, 19);
            this.rampStartButton.Name = "rampStartButton";
            this.rampStartButton.Size = new System.Drawing.Size(87, 23);
            this.rampStartButton.TabIndex = 2;
            this.rampStartButton.Text = "Start ramping";
            this.rampStartButton.UseVisualStyleBackColor = true;
            this.rampStartButton.Click += new System.EventHandler(this.rampStartButton_Click);
            // 
            // lockEnableCheck
            // 
            this.lockEnableCheck.AutoSize = true;
            this.lockEnableCheck.Location = new System.Drawing.Point(6, 18);
            this.lockEnableCheck.Name = "lockEnableCheck";
            this.lockEnableCheck.Size = new System.Drawing.Size(50, 17);
            this.lockEnableCheck.TabIndex = 9;
            this.lockEnableCheck.Text = "Lock";
            this.lockEnableCheck.UseVisualStyleBackColor = true;
            this.lockEnableCheck.CheckedChanged += new System.EventHandler(this.lockEnableCheck_CheckedChanged);
            // 
            // p1Intensity
            // 
            this.p1Intensity.Location = new System.Drawing.Point(0, 4);
            this.p1Intensity.Name = "p1Intensity";
            this.p1Intensity.Plots.AddRange(new NationalInstruments.UI.ScatterPlot[] {
            this.scatterPlot1});
            this.p1Intensity.Size = new System.Drawing.Size(548, 132);
            this.p1Intensity.TabIndex = 4;
            this.p1Intensity.XAxes.AddRange(new NationalInstruments.UI.XAxis[] {
            this.xAxis1});
            this.p1Intensity.YAxes.AddRange(new NationalInstruments.UI.YAxis[] {
            this.yAxis1});
            this.p1Intensity.PlotDataChanged += new NationalInstruments.UI.XYPlotDataChangedEventHandler(this.p1Intensity_PlotDataChanged);
            // 
            // scatterPlot1
            // 
            this.scatterPlot1.XAxis = this.xAxis1;
            this.scatterPlot1.YAxis = this.yAxis1;
            // 
            // p2Intensity
            // 
            this.p2Intensity.Location = new System.Drawing.Point(0, 140);
            this.p2Intensity.Name = "p2Intensity";
            this.p2Intensity.Plots.AddRange(new NationalInstruments.UI.ScatterPlot[] {
            this.scatterPlot2});
            this.p2Intensity.Size = new System.Drawing.Size(548, 112);
            this.p2Intensity.TabIndex = 5;
            this.p2Intensity.XAxes.AddRange(new NationalInstruments.UI.XAxis[] {
            this.xAxis2});
            this.p2Intensity.YAxes.AddRange(new NationalInstruments.UI.YAxis[] {
            this.yAxis2});
            this.p2Intensity.PlotDataChanged += new NationalInstruments.UI.XYPlotDataChangedEventHandler(this.p2Intensity_PlotDataChanged);
            // 
            // scatterPlot2
            // 
            this.scatterPlot2.XAxis = this.xAxis2;
            this.scatterPlot2.YAxis = this.yAxis2;
            // 
            // lockParams
            // 
            this.lockParams.Controls.Add(this.label4);
            this.lockParams.Controls.Add(this.GainTrackBar);
            this.lockParams.Controls.Add(this.initLaserVoltageUpDownBox);
            this.lockParams.Controls.Add(this.label2);
            this.lockParams.Controls.Add(this.setPointUpDownBox);
            this.lockParams.Controls.Add(this.voltageToLaserBox);
            this.lockParams.Controls.Add(this.label3);
            this.lockParams.Controls.Add(this.lockEnableCheck);
            this.lockParams.Location = new System.Drawing.Point(554, 112);
            this.lockParams.Name = "lockParams";
            this.lockParams.Size = new System.Drawing.Size(244, 140);
            this.lockParams.TabIndex = 10;
            this.lockParams.TabStop = false;
            this.lockParams.Text = "Lock Parameters";
            this.lockParams.Enter += new System.EventHandler(this.lockParams_Enter);
            // 
            // initLaserVoltageUpDownBox
            // 
            this.initLaserVoltageUpDownBox.Location = new System.Drawing.Point(151, 38);
            this.initLaserVoltageUpDownBox.Name = "initLaserVoltageUpDownBox";
            this.initLaserVoltageUpDownBox.Size = new System.Drawing.Size(78, 20);
            this.initLaserVoltageUpDownBox.TabIndex = 18;
            this.initLaserVoltageUpDownBox.ValueChanged += new System.EventHandler(this.initLaserVoltageUpDownBox_ValueChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(142, 13);
            this.label2.TabIndex = 17;
            this.label2.Text = "Approx. Voltage to Laser (V):";
            this.label2.Click += new System.EventHandler(this.label2_Click);
            // 
            // setPointUpDownBox
            // 
            this.setPointUpDownBox.Location = new System.Drawing.Point(151, 15);
            this.setPointUpDownBox.Name = "setPointUpDownBox";
            this.setPointUpDownBox.Size = new System.Drawing.Size(78, 20);
            this.setPointUpDownBox.TabIndex = 16;
            this.setPointUpDownBox.ValueChanged += new System.EventHandler(this.setPointUpDownBox_ValueChanged);
            // 
            // voltageToLaserBox
            // 
            this.voltageToLaserBox.Location = new System.Drawing.Point(151, 64);
            this.voltageToLaserBox.Name = "voltageToLaserBox";
            this.voltageToLaserBox.Size = new System.Drawing.Size(57, 20);
            this.voltageToLaserBox.TabIndex = 14;
            this.voltageToLaserBox.TextChanged += new System.EventHandler(this.voltageToLaserBox_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(62, 19);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(69, 13);
            this.label3.TabIndex = 13;
            this.label3.Text = "Set Point (V):";
            this.label3.Click += new System.EventHandler(this.label3_Click);
            // 
            // GainTrackBar
            // 
            this.GainTrackBar.Location = new System.Drawing.Point(6, 89);
            this.GainTrackBar.Name = "GainTrackBar";
            this.GainTrackBar.Size = new System.Drawing.Size(232, 45);
            this.GainTrackBar.TabIndex = 19;
            this.GainTrackBar.Scroll += new System.EventHandler(this.GainTrackBar_Scroll);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 71);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(29, 13);
            this.label4.TabIndex = 20;
            this.label4.Text = "Gain";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(807, 260);
            this.Controls.Add(this.lockParams);
            this.Controls.Add(this.p2Intensity);
            this.Controls.Add(this.p1Intensity);
            this.Controls.Add(this.voltageRampControl);
            this.Name = "MainForm";
            this.Text = "Transfer Cavity Lock";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.voltageRampControl.ResumeLayout(false);
            this.voltageRampControl.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rampLED)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.p1Intensity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.p2Intensity)).EndInit();
            this.lockParams.ResumeLayout(false);
            this.lockParams.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.initLaserVoltageUpDownBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.setPointUpDownBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GainTrackBar)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox voltageRampControl;
        private System.Windows.Forms.Button rampStartButton;
        private System.Windows.Forms.Button rampStopButton;
        private NationalInstruments.UI.WindowsForms.Led rampLED;
        private System.Windows.Forms.TextBox textBox;
        private NationalInstruments.UI.WindowsForms.ScatterGraph p1Intensity;
        private NationalInstruments.UI.ScatterPlot scatterPlot1;
        private NationalInstruments.UI.XAxis xAxis1;
        private NationalInstruments.UI.YAxis yAxis1;
        private NationalInstruments.UI.WindowsForms.ScatterGraph p2Intensity;
        private NationalInstruments.UI.ScatterPlot scatterPlot2;
        private NationalInstruments.UI.XAxis xAxis2;
        private NationalInstruments.UI.YAxis yAxis2;
        private System.Windows.Forms.CheckBox lockEnableCheck;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox lockParams;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox voltageToLaserBox;
        private System.Windows.Forms.NumericUpDown setPointUpDownBox;
        private System.Windows.Forms.CheckBox fitEnableCheck;
        private System.Windows.Forms.NumericUpDown initLaserVoltageUpDownBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TrackBar GainTrackBar;
        private System.Windows.Forms.Label label4;
    }
}
