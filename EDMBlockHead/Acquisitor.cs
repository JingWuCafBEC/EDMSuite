using System;
using System.Collections;
using System.Threading;
using NationalInstruments.DAQmx;

using DAQ.Environment;
using DAQ.FakeData;
using DAQ.HAL;
using Data;
using Data.EDM;
using EDMConfig;

using EDMBlockHead.Acquire.Channels;
using EDMBlockHead.Acquire.Input;

namespace EDMBlockHead.Acquire
{
	/// <summary>
	/// This class is the backend that takes EDM blocks.
	/// 
	/// One thing that is not obvious is the way that pattern output works. BlockHead asks ScanMaster
	/// to output the pattern. It selects the "Scan B" profile (heaven forbid if there isn't such a profile).
	/// This way there is no tedious mucking around with copying parameters. BlockHead copies out the
	/// pg settings from the profile into the block config. 
	/// </summary>
	public class Acquisitor
	{
		// stuff to coordinate the backend and frontend
		private Thread acquireThread;
		enum AcquisitorState {stopped, running, stopping};
		private AcquisitorState backendState = AcquisitorState.stopped;
		public Object MonitorLockObject = new Object();

		// the configuration for the block that is being taken
		private BlockConfig config;

		// daq variables
		ArrayList switchedChannels;
		ScannedAnalogInputCollection inputs;
        ScannedAnalogInputCollection magInputs;
		Task inputTask;
        Task singlePointInputTask;
 		AnalogMultiChannelReader inputReader;
        AnalogMultiChannelReader singlePointInputReader;
        ScanMaster.Controller scanMaster;
		EDMPhaseLock.MainForm phaseLock;
        EDMHardwareControl.Controller hardwareController;

		// calling this method starts acquisition
		public void Start(BlockConfig config) 
		{
			this.config = config;
			acquireThread = new Thread(new ThreadStart(this.Acquire));
			acquireThread.Name = "BlockHead Acquisitor";
			acquireThread.Priority = ThreadPriority.Highest;
			backendState = AcquisitorState.running;
			acquireThread.Start();
		}

        public void StartTargetStepper(BlockConfig config)
        {
            this.config = config;
            acquireThread = new Thread(new ThreadStart(this.AcquireAndStepTarget));
            acquireThread.Name = "BlockHead Acquisitor";
            acquireThread.Priority = ThreadPriority.Highest;
            backendState = AcquisitorState.running;
            acquireThread.Start();
        }

        public void StartMagDataAcquisition(BlockConfig config)
        {
            this.config = config;
            acquireThread = new Thread(new ThreadStart(this.AcquireMagData));
            acquireThread.Name = "BlockHead Acquisitor";
            acquireThread.Priority = ThreadPriority.Highest;
            backendState = AcquisitorState.running;
            acquireThread.Start();
        }

		// calling this method stops acquisition as soon as possible (usually after the current shot)
		public void Stop() 
		{
			lock(this)
			{
				backendState = AcquisitorState.stopping;
			}
		}


		// this is the method that actually takes the data. It is called by Start() and shouldn't
		// be called directly
		public void Acquire()
		{
			// lock onto something that the front end can see
			Monitor.Enter(MonitorLockObject);

			scanMaster = ScanMaster.Controller.GetController();
			phaseLock = new EDMPhaseLock.MainForm();
            hardwareController = new EDMHardwareControl.Controller();
	
			// map modulations to physical channels
			MapChannels();

			// map the analog inputs
			MapAnalogInputs();

			Block b = new Block();
			b.Config = config;
			b.SetTimeStamp();
            foreach (ScannedAnalogInput channel in inputs.Channels)
            {
                b.detectors.Add(channel.Channel.Name);
            }
			
			try
			{
				// get things going
				AcquisitionStarting();
                
				// enter the main loop
				for (int point = 0 ; point < (int)config.Settings["numberOfPoints"] ; point++)
				{
					// set the switch states and impose the appropriate wait times
					ThrowSwitches(point);

					// take a point
					Shot s;
					EDMPoint p;
					if (Environs.Debug)
					{
						// just stuff a made up shot in
						//Thread.Sleep(10);
						s = DataFaker.GetFakeShot(1900,200,10,3,9);
						((TOF)s.TOFs[0]).Calibration = ((ScannedAnalogInput)inputs.Channels[0]).Calibration;
						p = new EDMPoint();
						p.Shot = s;
						//Thread.Sleep(20);
					}
					else
					{
 						// everything should be ready now so start the analog
						// input task (it will wait for a trigger)
						inputTask.Start();

						// get the raw data
                        double[,] analogData = inputReader.ReadMultiSample(inputs.GateLength);
                        inputTask.Stop();


						// extract the data for each scanned channel and put it in a TOF
						s = new Shot();
						for (int i = 0 ; i < inputs.Channels.Count ; i++)
						{
							// extract the raw data
							double[] rawData = new double[inputs.GateLength];
							for (int q = 0 ; q < inputs.GateLength ; q++) rawData[q] = analogData[i,q];
							
							ScannedAnalogInput ipt = (ScannedAnalogInput)inputs.Channels[i];
							// reduce the data
							double[] data = ipt.Reduce(rawData);
							TOF t = new TOF();
							t.Calibration = ipt.Calibration;
                            // the 1000000 is because clock period is in microseconds;
                            t.ClockPeriod = 1000000 / ipt.CalculateClockRate(inputs.RawSampleRate);
                            t.GateStartTime = inputs.GateStartTime;
                            // this is a bit confusing. The chop is measured in points, so the gate
                            // has to be adjusted by the number of points times the clock period!
                            if (ipt.ReductionMode == DataReductionMode.Chop)
                                t.GateStartTime += (ipt.ChopStart * t.ClockPeriod);
							t.Data = data;
							// the 1000000 is because clock period is in microseconds;
							t.ClockPeriod = 1000000 / ipt.CalculateClockRate(inputs.RawSampleRate);

							s.TOFs.Add(t);
						}

						p = new EDMPoint();
						p.Shot = s;

					}
					// do the "SinglePointData" (i.e. things that are measured once per point)
                    // We'll save the leakage monitor until right at the end.
					// keep an eye on what the phase lock is doing
                    p.SinglePointData.Add("PhaseLockFrequency", phaseLock.OutputFrequency);
                    p.SinglePointData.Add("PhaseLockError", phaseLock.PhaseError);
                    // scan the analog inputs
                    double[] spd;
                    // fake some data if we're in debug mode
                    if (Environs.Debug)
                    {
                        spd = new double[7];
                        spd[0] = 1;
                        spd[1] = 2;
                        spd[2] = 3;
                        spd[3] = 4;
                        spd[4] = 5;
                        spd[5] = 6;
                        spd[6] = 7;
                    }
                    else
                    {
                        singlePointInputTask.Start();
                        spd = singlePointInputReader.ReadSingleSample();
                        singlePointInputTask.Stop();
                    }
                    p.SinglePointData.Add("topPD", spd[0]);
                    p.SinglePointData.Add("bottomPD", spd[1]);
                    p.SinglePointData.Add("MiniFlux1", spd[2]);
                    p.SinglePointData.Add("MiniFlux2", spd[3]);
                    p.SinglePointData.Add("MiniFlux3", spd[4]);
                    //p.SinglePointData.Add("CplusV", spd[5]);
                    //p.SinglePointData.Add("CminusV", spd[6]);
                    p.SinglePointData.Add("ValveMonV", spd[5]);

                    //hardwareController.UpdateVMonitor();
                    //p.SinglePointData.Add("CplusV", hardwareController.CPlusMonitorVoltage);
                    //hardwareController.UpdateLaserPhotodiodes();
                    //p.SinglePointData.Add("ProbePD", hardwareController.ProbePDVoltage);
                    //p.SinglePointData.Add("PumpPD", hardwareController.PumpPDVoltage);
                    //hardwareController.UpdateMiniFluxgates();
                    //p.SinglePointData.Add("MiniFlux1", hardwareController.MiniFlux1Voltage);
                    //p.SinglePointData.Add("MiniFlux2", hardwareController.MiniFlux2Voltage);
                    //p.SinglePointData.Add("MiniFlux3", hardwareController.MiniFlux3Voltage);
                    hardwareController.ReadIMonitor();
                    p.SinglePointData.Add("NorthCurrent", hardwareController.NorthCurrent);
                    p.SinglePointData.Add("SouthCurrent", hardwareController.SouthCurrent);
                    //hardwareController.UpdatePiMonitor();
                    //p.SinglePointData.Add("piMonitor", hardwareController.PiFlipMonVoltage);
                    //p.SinglePointData.Add("CminusV", hardwareController.CMinusMonitorVoltage);

                    // Hopefully the leakage monitors will have finished reading by now.
                    // We join them, read out the data, and then launch another asynchronous
                    // acquisition. [If this is the first shot of the block, the leakage monitor
                    // measurement will have been launched in AcquisitionStarting() ].
                    //hardwareController.WaitForIMonitorAsync();
                    //p.SinglePointData.Add("NorthCurrent", hardwareController.NorthCurrent); 
                    //p.SinglePointData.Add("SouthCurrent", hardwareController.SouthCurrent);
                    //hardwareController.UpdateIMonitorAsync();

                    // randomise the Ramsey phase
                    // TODO: check whether the .NET rng is good enough
                    // TODO: reference where this number comes from
                    //double d = 2.3814 * (new Random().NextDouble());
                    //hardwareController.SetScramblerVoltage(d);

					b.Points.Add(p);

					// update the front end
					Controller.GetController().GotPoint(point, p);

					if (CheckIfStopping()) 
					{
						// release hardware
						AcquisitionStopping();
						// signal anybody waiting on the lock that we're done
						Monitor.Pulse(MonitorLockObject);
						Monitor.Exit(MonitorLockObject);
						return;
					}
				}
			}		
			catch (Exception e)
			{
				// try and stop the experiment gracefully
				try
				{
					AcquisitionStopping();
				}
				catch (Exception) {}				// about the best that can be done at this stage
				Monitor.Pulse(MonitorLockObject);
				Monitor.Exit(MonitorLockObject);
				throw e;
			}

			AcquisitionStopping();
			
			// hand the new block back to the controller
			Controller.GetController().AcquisitionFinished(b);

			// signal anybody waiting on the lock that we're done
			Monitor.Pulse(MonitorLockObject);
			Monitor.Exit(MonitorLockObject);

		}

        public void AcquireAndStepTarget()
        {
            // lock onto something that the front end can see
            Monitor.Enter(MonitorLockObject);

            scanMaster = ScanMaster.Controller.GetController();
            phaseLock = new EDMPhaseLock.MainForm();
            hardwareController = new EDMHardwareControl.Controller();

            // map modulations to physical channels
            MapChannels();

            // map the analog inputs
            MapAnalogInputs();

            Block b = new Block();
            b.Config = config;
            b.SetTimeStamp();

             

            foreach (ScannedAnalogInput channel in inputs.Channels)
            {
                b.detectors.Add(channel.Channel.Name);
            }

            try
            {
                // get things going
                AcquisitionStarting();
                int point = 0;
                // enter the main loop
                for (point = 0; point < (int)config.Settings["maximumNumberOfTimesToStepTarget"]; point++)
                {

                    // take a point
                    Shot s;
                    EDMPoint p;
                    if (Environs.Debug)
                    {
                        // just stuff a made up shot in
                        //Thread.Sleep(10);
                        s = DataFaker.GetFakeShot(1900, 50, 10, 3, 3);
                        ((TOF)s.TOFs[0]).Calibration = ((ScannedAnalogInput)inputs.Channels[0]).Calibration;
                        p = new EDMPoint();
                        p.Shot = s;
                        //Thread.Sleep(20);
                    }
                    else
                    {
                        // everything should be ready now so start the analog
                        // input task (it will wait for a trigger)
                        inputTask.Start();

                        // get the raw data
                        double[,] analogData = inputReader.ReadMultiSample(inputs.GateLength);
                        inputTask.Stop();


                        // extract the data for each scanned channel and put it in a TOF
                        s = new Shot();
                        for (int i = 0; i < inputs.Channels.Count; i++)
                        {
                            // extract the raw data
                            double[] rawData = new double[inputs.GateLength];
                            for (int q = 0; q < inputs.GateLength; q++) rawData[q] = analogData[i, q];

                            ScannedAnalogInput ipt = (ScannedAnalogInput)inputs.Channels[i];
                            // reduce the data
                            double[] data = ipt.Reduce(rawData);
                            TOF t = new TOF();
                            t.Calibration = ipt.Calibration;
                            // the 1000000 is because clock period is in microseconds;
                            t.ClockPeriod = 1000000 / ipt.CalculateClockRate(inputs.RawSampleRate);
                            t.GateStartTime = inputs.GateStartTime;
                            // this is a bit confusing. The chop is measured in points, so the gate
                            // has to be adjusted by the number of points times the clock period!
                            if (ipt.ReductionMode == DataReductionMode.Chop)
                                t.GateStartTime += (ipt.ChopStart * t.ClockPeriod);
                            t.Data = data;
                            // the 1000000 is because clock period is in microseconds;
                            t.ClockPeriod = 1000000 / ipt.CalculateClockRate(inputs.RawSampleRate);

                            s.TOFs.Add(t);
                        }

                        p = new EDMPoint();
                        p.Shot = s;

                    }
                    // do the "SinglePointData" (i.e. things that are measured once per point)
                    // We'll save the leakage monitor until right at the end.
                    // keep an eye on what the phase lock is doing
                    p.SinglePointData.Add("PhaseLockFrequency", phaseLock.OutputFrequency);
                    p.SinglePointData.Add("PhaseLockError", phaseLock.PhaseError);
                    // scan the analog inputs
                    double[] spd;
                    // fake some data if we're in debug mode
                    if (Environs.Debug)
                    {
                        spd = new double[7];
                        spd[0] = 1;
                        spd[1] = 2;
                        spd[2] = 3;
                        spd[3] = 4;
                        spd[4] = 5;
                        spd[5] = 6;
                        spd[6] = 7;
                    }
                    else
                    {
                        singlePointInputTask.Start();
                        spd = singlePointInputReader.ReadSingleSample();
                        singlePointInputTask.Stop();
                    }
                    p.SinglePointData.Add("topPD", spd[0]);
                    p.SinglePointData.Add("bottomPD", spd[1]);
                    p.SinglePointData.Add("MiniFlux1", spd[2]);
                    p.SinglePointData.Add("MiniFlux2", spd[3]);
                    p.SinglePointData.Add("MiniFlux3", spd[4]);
                    //p.SinglePointData.Add("CplusV", spd[5]);
                    //p.SinglePointData.Add("CminusV", spd[6]);
                    p.SinglePointData.Add("ValveMonV", spd[5]);

                    //hardwareController.UpdateVMonitor();
                    //p.SinglePointData.Add("CplusV", hardwareController.CPlusMonitorVoltage);
                    //hardwareController.UpdateLaserPhotodiodes();
                    //p.SinglePointData.Add("ProbePD", hardwareController.ProbePDVoltage);
                    //p.SinglePointData.Add("PumpPD", hardwareController.PumpPDVoltage);
                    //hardwareController.UpdateMiniFluxgates();
                    //p.SinglePointData.Add("MiniFlux1", hardwareController.MiniFlux1Voltage);
                    //p.SinglePointData.Add("MiniFlux2", hardwareController.MiniFlux2Voltage);
                    //p.SinglePointData.Add("MiniFlux3", hardwareController.MiniFlux3Voltage);
                    hardwareController.ReadIMonitor();
                    p.SinglePointData.Add("NorthCurrent", hardwareController.NorthCurrent);
                    p.SinglePointData.Add("SouthCurrent", hardwareController.SouthCurrent);

                    b.Points.Add(p);

                    // update the front end
                    Controller.GetController().GotPoint(point, p);


                    // Integrate the first toff and stop the sequence if the signal is sufficiently high
                    TOF detectorATOF = new TOF(); 
                    detectorATOF = (TOF)s.TOFs[0];
                    double sig = detectorATOF.Integrate((double)config.Settings["targetStepperGateStartTime"], (double)config.Settings["targetStepperGateEndTime"]);
                    if (sig > (double)config.Settings["minimumSignalToRun"])
                    {
                        Controller.GetController().TargetHealthy = true; 
                        Stop();
                        point = (int)config.Settings["maximumNumberOfTimesToStepTarget"] + 1;

                    }
                    else
                    {
                        Controller.GetController().TargetHealthy = false;
                        hardwareController.StepTarget(2);
                    }
                    //if (CheckIfStopping())
                    //{
                        // release hardware
                    //    AcquisitionStopping();
                        // signal anybody waiting on the lock that we're done
                    //    Monitor.Pulse(MonitorLockObject);
                    //    Monitor.Exit(MonitorLockObject);
                    //    point = (int)config.Settings["maximumNumberOfTimesToStepTarget"];
                    //    return;
                    //}
                }
                AcquisitionStopping();

                // hand the new block back to the controller
                Controller.GetController().IntelligentAcquisitionFinished();

                // signal anybody waiting on the lock that we're done
                Monitor.Pulse(MonitorLockObject);
                Monitor.Exit(MonitorLockObject);
            }
            catch (Exception e)
            {
                // try and stop the experiment gracefully
                try
                {
                    AcquisitionStopping();
                }
                catch (Exception) { }				// about the best that can be done at this stage
                Monitor.Pulse(MonitorLockObject);
                Monitor.Exit(MonitorLockObject);
                throw e;
            }

        }

        public void AcquireMagData()
        {
            // lock onto something that the front end can see
            Monitor.Enter(MonitorLockObject);

            scanMaster = ScanMaster.Controller.GetController();
            phaseLock = new EDMPhaseLock.MainForm();
            hardwareController = new EDMHardwareControl.Controller();

            // map modulations to physical channels
            MapChannels();

            // map the analog inputs
            MapMagInputs();

            Block b = new Block();
            b.Config = config;
            b.SetTimeStamp();
            foreach (ScannedAnalogInput channel in magInputs.Channels)
            {
                b.detectors.Add(channel.Channel.Name);
            }

            try
            {
                // get things going
                MagAcquisitionStarting();

                // enter the main loop
                for (int point = 0; point < (int)config.Settings["numberOfPoints"]; point++)
                {
                    // set the switch states and impose the appropriate wait times
                    ThrowSwitches(point);

                    // take a point
                    Shot s;
                    EDMPoint p;
                    if (Environs.Debug)
                    {
                        // just stuff a made up shot in
                        //Thread.Sleep(10);
                        s = DataFaker.GetFakeShot(1900, 50, 10, 3, 3);
                        ((TOF)s.TOFs[0]).Calibration = ((ScannedAnalogInput)inputs.Channels[0]).Calibration;
                        p = new EDMPoint();
                        p.Shot = s;
                        //Thread.Sleep(20);
                    }
                    else
                    {
                        // everything should be ready now so start the analog
                        // input task (it will wait for a trigger)
                        inputTask.Start();

                        // get the raw data
                        double[,] analogData = inputReader.ReadMultiSample(magInputs.GateLength);
                        inputTask.Stop();


                        // extract the data for each scanned channel and put it in a TOF
                        s = new Shot();
                        for (int i = 0; i < magInputs.Channels.Count; i++)
                        {
                            // extract the raw data
                            double[] rawData = new double[magInputs.GateLength];
                            for (int q = 0; q < magInputs.GateLength; q++) rawData[q] = analogData[i, q];

                            ScannedAnalogInput ipt = (ScannedAnalogInput)magInputs.Channels[i];
                            // reduce the data
                            double[] data = ipt.Reduce(rawData);
                            TOF t = new TOF();
                            t.Calibration = ipt.Calibration;
                            // the 1000000 is because clock period is in microseconds;
                            t.ClockPeriod = 1000000 / ipt.CalculateClockRate(magInputs.RawSampleRate);
                            t.GateStartTime = magInputs.GateStartTime;
                            // this is a bit confusing. The chop is measured in points, so the gate
                            // has to be adjusted by the number of points times the clock period!
                            if (ipt.ReductionMode == DataReductionMode.Chop)
                                t.GateStartTime += (ipt.ChopStart * t.ClockPeriod);
                            t.Data = data;
                            // the 1000000 is because clock period is in microseconds;
                            t.ClockPeriod = 1000000 / ipt.CalculateClockRate(magInputs.RawSampleRate);

                            s.TOFs.Add(t);
                        }

                        p = new EDMPoint();
                        p.Shot = s;

                    }
                    // do the "SinglePointData" (i.e. things that are measured once per point)
                    // We'll save the leakage monitor until right at the end.
                    // keep an eye on what the phase lock is doing
                    p.SinglePointData.Add("PhaseLockFrequency", phaseLock.OutputFrequency);
                    p.SinglePointData.Add("PhaseLockError", phaseLock.PhaseError);
                    // scan the analog inputs
                    double[] spd;
                    // fake some data if we're in debug mode
                    if (Environs.Debug)
                    {
                        spd = new double[7];
                        spd[0] = 1;
                        spd[1] = 2;
                        spd[2] = 3;
                    }
                    else
                    {
                        singlePointInputTask.Start();
                        spd = singlePointInputReader.ReadSingleSample();
                        singlePointInputTask.Stop();
                    }
                    p.SinglePointData.Add("MiniFlux1", spd[0]);
                    p.SinglePointData.Add("MiniFlux2", spd[1]);
                    p.SinglePointData.Add("MiniFlux3", spd[2]);

                    hardwareController.ReadIMonitor();
                    p.SinglePointData.Add("NorthCurrent", hardwareController.NorthCurrent);
                    p.SinglePointData.Add("SouthCurrent", hardwareController.SouthCurrent);

                    b.Points.Add(p);

                    // update the front end
                    Controller.GetController().GotPoint(point, p);

                    if (CheckIfStopping())
                    {
                        // release hardware
                        AcquisitionStopping();
                        // signal anybody waiting on the lock that we're done
                        Monitor.Pulse(MonitorLockObject);
                        Monitor.Exit(MonitorLockObject);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                // try and stop the experiment gracefully
                try
                {
                    AcquisitionStopping();
                }
                catch (Exception) { }				// about the best that can be done at this stage
                Monitor.Pulse(MonitorLockObject);
                Monitor.Exit(MonitorLockObject);
                throw e;
            }

            AcquisitionStopping();

            // hand the new block back to the controller
            Controller.GetController().MagDataAcquisitionFinished(b);

            // signal anybody waiting on the lock that we're done
            Monitor.Pulse(MonitorLockObject);
            Monitor.Exit(MonitorLockObject);

        }

		private void MapChannels()
		{
			switchedChannels = new ArrayList();

            TTLSwitchedChannel bChan = new TTLSwitchedChannel();
            bChan.Channel = "b";
            bChan.Invert = false;
            bChan.Modulation = config.GetModulationByName("B");
            switchedChannels.Add(bChan);

            TTLSwitchedChannel notBChan = new TTLSwitchedChannel();
            notBChan.Channel = "notB";
            notBChan.Invert = true;
            notBChan.Modulation = config.GetModulationByName("B");
            switchedChannels.Add(notBChan);

            TTLSwitchedChannel dbChan = new TTLSwitchedChannel();
            dbChan.Channel = "db";
            dbChan.Invert = false;
            dbChan.Modulation = config.GetModulationByName("DB");
            switchedChannels.Add(dbChan);

            TTLSwitchedChannel notDBChan = new TTLSwitchedChannel();
            notDBChan.Channel = "notDB";
            notDBChan.Invert = true;
            notDBChan.Modulation = config.GetModulationByName("DB");
            switchedChannels.Add(notDBChan);

            TTLSwitchedChannel piChan = new TTLSwitchedChannel();
            piChan.Channel = "piFlipEnable";
            piChan.Invert = false;
            piChan.Modulation = config.GetModulationByName("PI");
            switchedChannels.Add(piChan);

            //TTLSwitchedChannel notPIChan = new TTLSwitchedChannel();
            //notPIChan.Channel = "notPIFlipEnable";
            //notPIChan.Invert = true;
            //notPIChan.Modulation = config.GetModulationByName("PI");
            //switchedChannels.Add(notPIChan);

            //ESwitchChannel eChan = new ESwitchChannel();
            //eChan.Invert = false;
            //eChan.Modulation = config.GetModulationByName("E");
            //switchedChannels.Add(eChan);

            //ESwitchRFChannel eChan = new ESwitchRFChannel();
            //eChan.Invert = false;
            //eChan.Modulation = config.GetModulationByName("E");
            //eChan.stepSizeRF = +0.25;
            //switchedChannels.Add(eChan);

            AnalogSwitchedChannel rf1AChannel = new AnalogSwitchedChannel();
            rf1AChannel.Channel = "rf1Attenuator";
            rf1AChannel.Modulation = config.GetModulationByName("RF1A");
            switchedChannels.Add(rf1AChannel);

            AnalogSwitchedChannel rf2AChannel = new AnalogSwitchedChannel();
            rf2AChannel.Channel = "rf2Attenuator";
            rf2AChannel.Modulation = config.GetModulationByName("RF2A");
            switchedChannels.Add(rf2AChannel);

            AnalogSwitchedChannel rf1FChannel = new AnalogSwitchedChannel();
            rf1FChannel.Channel = "rf1FM";
            rf1FChannel.Modulation = config.GetModulationByName("RF1F");
            switchedChannels.Add(rf1FChannel);

            AnalogSwitchedChannel rf2FChannel = new AnalogSwitchedChannel();
            rf2FChannel.Channel = "rf2FM";
            rf2FChannel.Modulation = config.GetModulationByName("RF2F");
            switchedChannels.Add(rf2FChannel);

            HardwareControllerSwitchChannel eChan = new HardwareControllerSwitchChannel();
            eChan.Channel = "eChan";
            eChan.Modulation = config.GetModulationByName("E");
            switchedChannels.Add(eChan);

            //HardwareControllerSwitchChannel mwChan = new HardwareControllerSwitchChannel();
            //mwChan.Channel = "mwChan";
            //mwChan.Modulation = config.GetModulationByName("MW");
            //switchedChannels.Add(mwChan);

            //AnalogSwitchedChannel lf1Channel = new AnalogSwitchedChannel();
            //lf1Channel.Channel = "flPZT";
            //lf1Channel.Modulation = config.GetModulationByName("LF1");
            //switchedChannels.Add(lf1Channel);

            HardwareControllerSwitchChannel lf1Channel = new HardwareControllerSwitchChannel();
            lf1Channel.Channel = "probeAOM";
            lf1Channel.Modulation = config.GetModulationByName("LF1");
            switchedChannels.Add(lf1Channel);

            //HardwareControllerSwitchChannel lf2Channel = new HardwareControllerSwitchChannel();
            //lf2Channel.Channel = "pumpAOM";
            //lf2Channel.Modulation = config.GetModulationByName("LF2");
            //switchedChannels.Add(lf2Channel);

            //AnalogSwitchedChannel lf3Channel = new HardwareControllerSwitchChannel();
            //lf3Channel.Channel = "LF3";
            //lf3Channel.Modulation = config.GetModulationByName("LF3");
            //switchedChannels.Add(lf3Channel);

        }

        #region Map Inputs
        /* THIS VERSION FOR AR */
        // this sets up the scanned analog inputs. It's complicated a bit by the fact that
        // each input would ideally have a different clock rate and gateLength. The board
        // doesn't support that though.
        public void MapAnalogInputs()
        {
            inputs = new ScannedAnalogInputCollection();
            inputs.RawSampleRate = 100000; 
            inputs.GateStartTime = (int)scanMaster.GetShotSetting("gateStartTime");
            inputs.GateLength = 280;

            ScannedAnalogInput bottomProbe = new ScannedAnalogInput();
            bottomProbe.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["bottomProbe"];
            bottomProbe.ReductionMode = DataReductionMode.Chop;
            bottomProbe.ChopStart = 140;
            bottomProbe.ChopLength = 80;
            bottomProbe.LowLimit = 0;
            bottomProbe.HighLimit = 10;
            bottomProbe.Calibration = 510; 
            inputs.Channels.Add(bottomProbe);

            ScannedAnalogInput topProbe = new ScannedAnalogInput();
            topProbe.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["topProbe"];
            topProbe.ReductionMode = DataReductionMode.Chop;
            topProbe.ChopStart = 180;
            topProbe.ChopLength = 80;
            topProbe.LowLimit = 0;
            topProbe.HighLimit = 10;
            topProbe.Calibration = 510;
            inputs.Channels.Add(topProbe);
            
            ScannedAnalogInput mag = new ScannedAnalogInput();
            mag.ReductionMode = DataReductionMode.Average;
            mag.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["magnetometer"];
            mag.AverageEvery = 20;
            mag.LowLimit = -10;
            mag.HighLimit = 10;
            mag.Calibration = 0.00001;
            inputs.Channels.Add(mag);


            ScannedAnalogInput gnd = new ScannedAnalogInput();
            gnd.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["gnd"];
            gnd.ReductionMode = DataReductionMode.Average;
            gnd.AverageEvery = 20;
            gnd.LowLimit = -1;
            gnd.HighLimit = 1;
            gnd.Calibration = 1;
            inputs.Channels.Add(gnd);


            ScannedAnalogInput battery = new ScannedAnalogInput();
            battery.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["battery"];
            battery.ReductionMode = DataReductionMode.Chop;
            battery.ChopStart = 140;
            battery.ChopLength = 120;
            battery.LowLimit = 0;
            battery.HighLimit = 10;
            battery.Calibration = 1;
            inputs.Channels.Add(battery);

            /*ScannedAnalogInput battery2 = new ScannedAnalogInput();
            battery2.ReductionMode = DataReductionMode.Average;
            battery2.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["battery2"];
            battery2.AverageEvery = 20;
            battery2.LowLimit = -10;
            battery2.HighLimit = 10;
            battery2.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            inputs.Channels.Add(battery2);*/

            //ScannedAnalogInput rfCurrent = new ScannedAnalogInput();
            //rfCurrent.ReductionMode = DataReductionMode.Average;
            //rfCurrent.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["rfCurrent"];
            //rfCurrent.AverageEvery = 10; //Bandwidth of the ammeter is aprox 12kHz
            //rfCurrent.LowLimit = -10;
            //rfCurrent.HighLimit = 10;
            //inputs.Channels.Add(rfCurrent);

            //ScannedAnalogInput reflectedrf1Amplitude = new ScannedAnalogInput();
            //reflectedrf1Amplitude.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["reflectedrf1Amplitude"];
            //reflectedrf1Amplitude.ReductionMode = DataReductionMode.Chop;
            //reflectedrf1Amplitude.ChopStart = 30;
            //reflectedrf1Amplitude.ChopLength = 130;
            //reflectedrf1Amplitude.LowLimit = -10;
            //reflectedrf1Amplitude.HighLimit = 1;
            //inputs.Channels.Add(reflectedrf1Amplitude);

            //ScannedAnalogInput reflectedrf2Amplitude = new ScannedAnalogInput();
            //reflectedrf2Amplitude.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["reflectedrf2Amplitude"];
            //reflectedrf2Amplitude.ReductionMode = DataReductionMode.Chop;
            //reflectedrf2Amplitude.ChopStart = 30;
            //reflectedrf2Amplitude.ChopLength = 130;
            //reflectedrf2Amplitude.LowLimit = -10;
            //reflectedrf2Amplitude.HighLimit = 1;
            //inputs.Channels.Add(reflectedrf2Amplitude);

            ScannedAnalogInput quSPinFSY = new ScannedAnalogInput();
            quSPinFSY.ReductionMode = DataReductionMode.Average;
            quSPinFSY.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinFS_Y"];
            quSPinFSY.AverageEvery = 20;
            quSPinFSY.LowLimit = -10;
            quSPinFSY.HighLimit = 10;
            quSPinFSY.Calibration = 1.0;
            inputs.Channels.Add(quSPinFSY);

            ScannedAnalogInput quSPinFSZ = new ScannedAnalogInput();
            quSPinFSZ.ReductionMode = DataReductionMode.Average;
            quSPinFSZ.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinFS_Z"];
            quSPinFSZ.AverageEvery = 20;
            quSPinFSZ.LowLimit = -10;
            quSPinFSZ.HighLimit = 10;
            quSPinFSZ.Calibration = 1.0;
            inputs.Channels.Add(quSPinFSZ);
             
        }

        // This version for magnetometer data taking with the QuSpins
        public void MapMagInputs()
        {
            magInputs = new ScannedAnalogInputCollection();
            magInputs.RawSampleRate = 100000;
            magInputs.GateStartTime = (int)scanMaster.GetShotSetting("gateStartTime");
            magInputs.GateLength = 280;

            ScannedAnalogInput mag = new ScannedAnalogInput();
            mag.ReductionMode = DataReductionMode.Average;
            mag.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["magnetometer"];
            mag.AverageEvery = 20;
            mag.LowLimit = -10;
            mag.HighLimit = 10;
            mag.Calibration = 1.0e-6; // bartington calibration is 1V = 10uT but we have 10x amplifier so it's 1V = 1uT
            magInputs.Channels.Add(mag);

            ScannedAnalogInput b0y = new ScannedAnalogInput();
            b0y.ReductionMode = DataReductionMode.Average;
            b0y.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinB0_Y"];
            b0y.AverageEvery = 20; 
            b0y.LowLimit = -10;
            b0y.HighLimit = 10;
            b0y.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(b0y);

            ScannedAnalogInput b0z = new ScannedAnalogInput();
            b0z.ReductionMode = DataReductionMode.Average;
            b0z.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinB0_Z"];
            b0z.AverageEvery = 20;
            b0z.LowLimit = -10;
            b0z.HighLimit = 10;
            b0z.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(b0z);

            ScannedAnalogInput evy = new ScannedAnalogInput();
            evy.ReductionMode = DataReductionMode.Average;
            evy.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEV_Y"];
            evy.AverageEvery = 20;
            evy.LowLimit = -10;
            evy.HighLimit = 10;
            evy.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(evy);

            ScannedAnalogInput evz = new ScannedAnalogInput();
            evz.ReductionMode = DataReductionMode.Average;
            evz.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEV_Z"];
            evz.AverageEvery = 20;
            evz.LowLimit = -10;
            evz.HighLimit = 10;
            evz.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(evz);

            //ScannedAnalogInput ewy = new ScannedAnalogInput();
            //ewy.ReductionMode = DataReductionMode.Average;
            //ewy.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEW_Y"];
            //ewy.AverageEvery = 20;
            //ewy.LowLimit = -10;
            //ewy.HighLimit = 10;
            //ewy.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            //magInputs.Channels.Add(ewy);

            //ScannedAnalogInput ewz = new ScannedAnalogInput();
            //ewz.ReductionMode = DataReductionMode.Average;
            //ewz.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEW_Z"];
            //ewz.AverageEvery = 20;
            //ewz.LowLimit = -10;
            //ewz.HighLimit = 10;
            //ewz.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            //magInputs.Channels.Add(ewz);

            ScannedAnalogInput exy = new ScannedAnalogInput();
            exy.ReductionMode = DataReductionMode.Average;
            exy.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEX_Y"];
            exy.AverageEvery = 20;
            exy.LowLimit = -10;
            exy.HighLimit = 10;
            exy.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(exy);

            ScannedAnalogInput exz = new ScannedAnalogInput();
            exz.ReductionMode = DataReductionMode.Average;
            exz.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEX_Z"];
            exz.AverageEvery = 20;
            exz.LowLimit = -10;
            exz.HighLimit = 10;
            exz.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(exz);

            ScannedAnalogInput euy = new ScannedAnalogInput();
            euy.ReductionMode = DataReductionMode.Average;
            euy.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEU_Y"];
            euy.AverageEvery = 20;
            euy.LowLimit = -10;
            euy.HighLimit = 10;
            euy.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(euy);

            ScannedAnalogInput euz = new ScannedAnalogInput();
            euz.ReductionMode = DataReductionMode.Average;
            euz.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinEU_Z"];
            euz.AverageEvery = 20;
            euz.LowLimit = -10;
            euz.HighLimit = 10;
            euz.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(euz);

            ScannedAnalogInput battery = new ScannedAnalogInput();
            battery.ReductionMode = DataReductionMode.Average;
            battery.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["battery"];
            battery.AverageEvery = 20;
            battery.LowLimit = -10;
            battery.HighLimit = 10;
            battery.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(battery);

            ScannedAnalogInput battery2 = new ScannedAnalogInput();
            battery2.ReductionMode = DataReductionMode.Average;
            battery2.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["battery2"];
            battery2.AverageEvery = 20;
            battery2.LowLimit = -10;
            battery2.HighLimit = 10;
            battery2.Calibration = 1.0e-9 / 0.9; // analog output calibration is 0.9 V/nT
            magInputs.Channels.Add(battery2);

            /*ScannedAnalogInput b921x = new ScannedAnalogInput();
            b921x.ReductionMode = DataReductionMode.Average;
            b921x.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart921_X"];
            b921x.AverageEvery = 20;
            b921x.LowLimit = -10;
            b921x.HighLimit = 10;
            b921x.Calibration = 1.0;
            magInputs.Channels.Add(b921x);

            ScannedAnalogInput b921y = new ScannedAnalogInput();
            b921y.ReductionMode = DataReductionMode.Average;
            b921y.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart921_Y"];
            b921y.AverageEvery = 20;
            b921y.LowLimit = -10;
            b921y.HighLimit = 10;
            b921y.Calibration = 1.0;
            magInputs.Channels.Add(b921y);

            ScannedAnalogInput b921z = new ScannedAnalogInput();
            b921z.ReductionMode = DataReductionMode.Average;
            b921z.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart921_Z"];
            b921z.AverageEvery = 20;
            b921z.LowLimit = -10;
            b921z.HighLimit = 10;
            b921z.Calibration = 1.0;
            magInputs.Channels.Add(b921z);*/

            /*ScannedAnalogInput b200x = new ScannedAnalogInput();
            b200x.ReductionMode = DataReductionMode.Average;
            b200x.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart200_X"];
            b200x.AverageEvery = 20;
            b200x.LowLimit = -10;
            b200x.HighLimit = 10;
            b200x.Calibration = 1.0;
            magInputs.Channels.Add(b200x);

            ScannedAnalogInput b200y = new ScannedAnalogInput();
            b200y.ReductionMode = DataReductionMode.Average;
            b200y.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart200_Y"];
            b200y.AverageEvery = 20;
            b200y.LowLimit = -10;
            b200y.HighLimit = 10;
            b200y.Calibration = 1.0;
            magInputs.Channels.Add(b200y);

            ScannedAnalogInput b200z = new ScannedAnalogInput();
            b200z.ReductionMode = DataReductionMode.Average;
            b200z.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["Bart200_Z"];
            b200z.AverageEvery = 20;
            b200z.LowLimit = -10;
            b200z.HighLimit = 10;
            b200z.Calibration = 1.0;
            magInputs.Channels.Add(b200z);*/

            ScannedAnalogInput quSPinFSY = new ScannedAnalogInput();
            quSPinFSY.ReductionMode = DataReductionMode.Average;
            quSPinFSY.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinFS_Y"];
            quSPinFSY.AverageEvery = 20;
            quSPinFSY.LowLimit = -10;
            quSPinFSY.HighLimit = 10;
            quSPinFSY.Calibration = 1.0e-9 / 0.9;
            magInputs.Channels.Add(quSPinFSY);

            ScannedAnalogInput quSPinFSZ = new ScannedAnalogInput();
            quSPinFSZ.ReductionMode = DataReductionMode.Average;
            quSPinFSZ.Channel = (AnalogInputChannel)Environs.Hardware.AnalogInputChannels["quSpinFS_Z"];
            quSPinFSZ.AverageEvery = 20;
            quSPinFSZ.LowLimit = -10;
            quSPinFSZ.HighLimit = 10;
            quSPinFSZ.Calibration = 1.0e-9 / 0.9;
            magInputs.Channels.Add(quSPinFSZ);

        }
        #endregion

        private void ConfigureSinglePointAnalogInputs()
        {
            // here we configure the scan of analog inputs that happens after each shot.
            singlePointInputTask = new Task("Blockhead single point inputs");
            AddChannelToSinglePointTask("topPD");
            //AddChannelToSinglePointTask("ground");
            AddChannelToSinglePointTask("bottomPD");
            //AddChannelToSinglePointTask("ground");
            AddChannelToSinglePointTask("miniFlux1");
            //AddChannelToSinglePointTask("ground");
            AddChannelToSinglePointTask("miniFlux2");
            //AddChannelToSinglePointTask("ground");
            AddChannelToSinglePointTask("miniFlux3");
            //AddChannelToSinglePointTask("ground");
            //AddChannelToSinglePointTask("cPlusMonitor");
            //AddChannelToSinglePointTask("ground");
            //AddChannelToSinglePointTask("cMinusMonitor");
            //AddChannelToSinglePointTask("northLeakage");
           //AddChannelToSinglePointTask("ground");
            //AddChannelToSinglePointTask("southLeakage");
            //AddChannelToSinglePointTask("ground");
            AddChannelToSinglePointTask("valveMonVoltage");

            //singlePointInputTask.Timing.ConfigureSampleClock(
            //        "",
            //        1000,
            //        SampleClockActiveEdge.Rising,
            //        SampleQuantityMode.FiniteSamples,
            //        1
            //     );

            //singlePointInputTask.Triggers.StartTrigger.ConfigureNone();

            if (!Environs.Debug) singlePointInputTask.Control(TaskAction.Verify);
            singlePointInputReader = new AnalogMultiChannelReader(singlePointInputTask.Stream);

        }

        private void ConfigureSinglePointAnalogInputsForMagBlocks()
        {
            // here we configure the scan of analog inputs that happens after each shot.
            singlePointInputTask = new Task("Blockhead single point inputs for magnetic field blocks");
            AddChannelToSinglePointTask("miniFlux1");
            AddChannelToSinglePointTask("miniFlux2");
            AddChannelToSinglePointTask("miniFlux3");

            if (!Environs.Debug) singlePointInputTask.Control(TaskAction.Verify);
            singlePointInputReader = new AnalogMultiChannelReader(singlePointInputTask.Stream);

        }

        private void AddChannelToSinglePointTask(string chan)
        {
            AnalogInputChannel aic = ((AnalogInputChannel)Environs.Hardware.AnalogInputChannels[chan]);
            aic.AddToTask(singlePointInputTask, 0, 10);
        }

		// configure hardware and start the pattern output
		private void AcquisitionStarting()
		{
			// iterate through the channels and ready them
			foreach (SwitchedChannel s in switchedChannels) s.AcquisitionStarting();

			// copy running parameters into the BlockConfig
			StuffConfig();

            // prepare the inputs
            inputTask = new Task("BlockHead analog input");

            foreach (ScannedAnalogInput i in inputs.Channels)
                i.Channel.AddToTask(
                    inputTask,
                    i.LowLimit,
                    i.HighLimit
                    );

            inputTask.Timing.ConfigureSampleClock(
                "",
                inputs.RawSampleRate,
                SampleClockActiveEdge.Rising,
                SampleQuantityMode.FiniteSamples,
                inputs.GateLength * inputs.Channels.Count
                );

            inputTask.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(
                (string)Environs.Hardware.GetInfo("analogTrigger0"),
                DigitalEdgeStartTriggerEdge.Rising
                );

            if (!Environs.Debug) inputTask.Control(TaskAction.Verify);
            inputReader = new AnalogMultiChannelReader(inputTask.Stream);

            ConfigureSinglePointAnalogInputs();

            // set the leakage monitor measurement time to 5ms.
            // With this setting it actually takes 26ms total to acquire two channels.
            //hardwareController.LeakageMonitorMeasurementTime = 0.005;
            hardwareController.ReconfigureIMonitors();
            // Start the first asynchronous acquisition
            //hardwareController.UpdateIMonitorAsync();
		}

        // configure hardware for magnetic field data taking
        private void MagAcquisitionStarting()
        {
            // iterate through the channels and ready them
            foreach (SwitchedChannel s in switchedChannels) s.AcquisitionStarting();

            // copy running parameters into the BlockConfig
            StuffConfig();

            // prepare the inputs
            inputTask = new Task("BlockHead magnetometer analog input");

            foreach (ScannedAnalogInput i in magInputs.Channels)
                i.Channel.AddToTask(
                    inputTask,
                    i.LowLimit,
                    i.HighLimit
                    );

            inputTask.Timing.ConfigureSampleClock(
                "",
                magInputs.RawSampleRate,
                SampleClockActiveEdge.Rising,
                SampleQuantityMode.FiniteSamples,
                magInputs.GateLength * magInputs.Channels.Count
                );

            inputTask.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(
                (string)Environs.Hardware.GetInfo("analogTrigger0"),
                DigitalEdgeStartTriggerEdge.Rising
                );

            if (!Environs.Debug) inputTask.Control(TaskAction.Verify);
            inputReader = new AnalogMultiChannelReader(inputTask.Stream);

            ConfigureSinglePointAnalogInputsForMagBlocks();

            // set the leakage monitor measurement time to 5ms.
            // With this setting it actually takes 26ms total to acquire two channels.
            //hardwareController.LeakageMonitorMeasurementTime = 0.005;
            hardwareController.ReconfigureIMonitors();
            // Start the first asynchronous acquisition
            //hardwareController.UpdateIMonitorAsync();
        }

		// If you want to store any information in the BlockConfig this is the place to do it.
		// This function is called at the start of every block.
		private void StuffConfig()
		{
			// dump all of the pg settings into the config - never know when they'll come in
			// handy !
			ScanMaster.Acquire.Plugin.PluginSettings pgSettings = scanMaster.GetPGSettings();
			foreach (String key in pgSettings.Keys) config.Settings[key] = pgSettings[key];

			// rf parameters
			EDMHardwareControl.Controller hwController = new EDMHardwareControl.Controller();
			config.Settings["greenSynthFrequency"] = hwController.GreenSynthOnFrequency;
			config.Settings["greenSynthDCFM"] = hwController.GreenSynthDCFM;
			config.Settings["greenSynthAmplitude"] = hwController.GreenSynthOnAmplitude;
		}

		private void ThrowSwitches(int point)
		{
			// this works out how long to wait before the switch - it looks at
			// all of the switched channels and figures out which ones are
			// switching. Of those channels it finds the longest wait.
			int delayBeforeSwitch = 0;
			foreach(SwitchedChannel s in switchedChannels)
			{
				int tempDelay = s.Modulation.DelayBeforeSwitch;
				// special case for the first point of a block - treat all channels as if they
				// are switching
				if (point != 0)
				{
					bool[] bits = s.Modulation.Waveform.Bits;
					// if this waveform is not switching just ignore its delay
					if ( bits[point] == bits[point - 1]) tempDelay = 0;
				}
				if (tempDelay > delayBeforeSwitch) delayBeforeSwitch = tempDelay;
			}
			// impose the delay
			if (delayBeforeSwitch != 0) Thread.Sleep(delayBeforeSwitch);

			// now actually throw the switches
			foreach(SwitchedChannel s in switchedChannels)
			{
				bool[] bits = s.Modulation.Waveform.Bits;
                if (point != 0)
                {
                    if (bits[point] != bits[point - 1]) s.State = bits[point];
                }
                else
                {
                    s.State = bits[point];
                }
			}

			// calculate and impose the post switching delays
			int delayAfterSwitch = 0;
			foreach(SwitchedChannel s in switchedChannels)
			{
				int tempDelay = s.Modulation.DelayAfterSwitch;
				// special case for the first point of a block - treat all channels as if they
				// are switching
				if (point != 0)
				{
					bool[] bits = s.Modulation.Waveform.Bits;
					// if this waveform is not switching just ignore its delay
					if ( bits[point] == bits[point - 1]) tempDelay = 0;
				}
				if (tempDelay > delayAfterSwitch) delayAfterSwitch = tempDelay;
			}
			// impose the delay
			if (delayAfterSwitch != 0) Thread.Sleep(delayAfterSwitch);			
		}

		// stop pattern output and release hardware
		private void AcquisitionStopping()
		{
			foreach( SwitchedChannel s in switchedChannels) s.AcquisitionFinishing();
			inputTask.Dispose();
            singlePointInputTask.Dispose();
		}

		private bool CheckIfStopping() 
		{
			lock(this) 
			{
				if (backendState == AcquisitorState.stopping)
				{
					backendState = AcquisitorState.stopped;
					return true;
				}
				else return false;
			}
		}

	}
}
