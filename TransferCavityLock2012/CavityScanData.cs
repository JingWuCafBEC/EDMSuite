﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using Data;
using Data.Scans;
using NationalInstruments.DAQmx;
using DAQ.Environment;
using DAQ.HAL;
using DAQ.Remoting;
using System.Windows.Forms;

namespace TransferCavityLock2012
{

    //I tried to bundle everything to do with a single scan (scan parameters as well as data) into the same class.
    //I might have overdone the inheritance thing, but I was practising. The base class keeps parameters 
    //concerning the cavity ramp. Then a few other parameters about the scan get added to the 
    //scan parameters, then this bit holds the data.

    class CavityScanData
    {
        public double[,] AIData;
        public ScanParameters parameters = new ScanParameters();
        Dictionary<string, string> slaveLookupTable;

        public CavityScanData(int steps, Dictionary<string, int> channels, Dictionary<string, string> lookupTable)
        {
            AIData = new double[channels.Count, steps];
            parameters.Steps = steps;
            parameters.NumberOfAIChannels = channels.Count;
            parameters.Channels = channels;
            slaveLookupTable = lookupTable;
       }
        public double[] GetCavityData()
        {
            double[] temp = new double[AIData.GetLength(1)];
            for (int i = 0; i < AIData.GetLength(1); i++)
            {
                temp[i] = AIData[parameters.Channels["cavity"], i];
            }
            return temp;
        }
        public double[] GetMasterData()
        {
            double[] temp = new double[AIData.GetLength(1)];
            for (int i = 0; i < AIData.GetLength(1); i++)
            {
                temp[i] = AIData[parameters.Channels["master"], i];
            }
            return temp;
        }
        public double[] GetSlaveData(string name)
        {
            double[] temp = new double[AIData.GetLength(1)];
            int channel = parameters.Channels[slaveLookupTable[name]];
            for (int i = 0; i < AIData.GetLength(1); i++)
            {
                temp[i] = AIData[channel, i];
            }
            return temp;
        }    

 

    }
}