﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Digital_Indicator.Logic.Helpers;

namespace Digital_Indicator.Logic.SerialCommunications
{
    public class SerialService : ISerialService
    {
        private SerialPort serialPort;

        public event EventHandler DiameterChanged;
        public event EventHandler SpoolerDataChanged;
        public event EventHandler TraverseDataChanged;
        public event EventHandler GeneralDataChanged;

        private bool bHandshake = false;

        public SerialService()
        {
            if (!IsSimulationModeActive)
                serialPort = new SerialPort();
        }

        private string portName;
        public string PortName
        {
            get { return portName; }
            set
            {
                portName = value;
                UnbindHandlers();
                SetPort();
                BindHandlers();
            }
        }

        public bool IsSimulationModeActive { get; set; }

        public bool PortDataIsSet { get; private set; }



        public void BindHandlers()
        {

        }
        public void UnbindHandlers()
        {

        }

        public void ConnectToSerialPort(string portName)
        {
            if (IsSimulationModeActive && (portName != null || portName == string.Empty))
            {
                PortName = portName;
                serialPort.Open();
                RunSimulation();

                return;
            }
            if (!IsSimulationModeActive)
            {
                PortName = portName;
                serialPort.Open();
            }
            else
            {
                RunSimulation();
            }

            StartSerialReceive();

        }

        private void SetPort()
        {
            serialPort.PortName = portName;
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.BaudRate = 115200;
            PortDataIsSet = true;
        }

        private void StartSerialReceive()
        {
            Task.Factory.StartNew(() =>
            {
                if (PortDataIsSet)
                {
                    while (true)
                    {
                        try
                        {
                            string dataIn = serialPort.ReadLine();

                            Console.WriteLine(dataIn);

                            string[] splitData = dataIn.Replace("\r", "").Replace("\n", "").Replace("\0", "").Split(';');
                            if (splitData.Length == 5)
                            {
                                if (!ChecksumPassed(splitData[3], dataIn))
                                {
                                    Console.WriteLine("Checksum Error");
                                    return;
                                }

                                Type type = this.GetType();
                                MethodInfo method = type.GetMethod(splitData[1]);


                                if (bHandshake)
                                {
                                    if (method == null) //try indirect GetMethod
                                    {
                                        Console.WriteLine(splitData[1] + " Trying indirect method, no function defined");
                                        method = type.GetMethod("ProcessIndirectFunction");
                                    }
                                    if (method == null) { throw new Exception(); }
                                    method.Invoke(this, new object[] { splitData });
                                }
                                else
                                {
                                    if (splitData[1] == "Handshake")
                                    {
                                        method = type.GetMethod("Handshake");
                                        method.Invoke(this, new object[] { splitData });
                                    }
                                }
                            }
                        }
                        catch (Exception oe)
                        {
                            Console.WriteLine("Serial Error: " + oe.Message);

                        }

                        Thread.Sleep(10); //polling is faster than trying to use the serial ondata event ;(
                    }
                }
            });
        }



        private void RunSimulation()
        {
            Task.Factory.StartNew(() =>
            {
                if (PortDataIsSet)
                {
                    serialPort.WriteLine("1;IsInSimulationMode = true");
                }
                else
                {
                    while (true)
                    {
                        double diameter = GetRandomNumber(1.7000, 1.8000, 4);

                        string formatString = "0.";
                        for (int i = 0; i < 4; i++)
                        {
                            formatString += "0";
                        }

                        DiameterChanged?.Invoke(diameter.ToString(formatString), null);
                        Thread.Sleep(50);
                    }
                }
            });
        }



        private double GetRandomNumber(double minimum, double maximum, int decimalPlaces)
        {
            int dPlaces = (int)Math.Pow(10, decimalPlaces);

            Random random = new Random();
            int r = random.Next((int)(minimum * dPlaces), (int)(maximum * dPlaces)); //+1 as end is excluded.
            return (Double)r / dPlaces;
        }

        private bool ChecksumPassed(string checksum, string stringToCheck)
        {
            int tokenCount = 0;
            int tokenPosition = 0;
            string checksumString = string.Empty;
            tokenCount = stringToCheck.Split(';').Length;

            if (tokenCount == 5)
            {

                int tCount = 0;

                for (int i = 0; i < stringToCheck.Length; ++i)
                {
                    if (tCount != 3)
                    {
                        checksumString += stringToCheck[i];
                    }
                    else
                    {
                        if (stringToCheck[i] == ';')
                        {
                            tCount++;
                        }
                    }
                    if (stringToCheck[i] == ';' && tCount < 3)
                    {
                        tokenPosition = i;
                        tCount++;
                    }


                }
            }

            checksumString = checksumString.Replace("\r", "").Replace("\0", "");

            int checksumValue = GetCheckSum(checksumString);

            Int16 sum = -1;
            Int16.TryParse(checksum, out sum);

            return checksumValue == sum;
        }

        private int GetCheckSum(string checksumString)
        {
            int checksumValue = 0;
            for (int i = 0; i < checksumString.Length; ++i)
            {
                checksumValue = checksumValue ^ checksumString[i];
            }

            return checksumValue;
        }

        public List<SerialPortClass> GetSerialPortList()
        {
            List<SerialPortClass> ListOfSerialPortClass = new List<SerialPortClass>();

            ManagementObjectCollection managementObjectCollection = null;
            ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("root\\cimv2",
            "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");

            managementObjectCollection = managementObjectSearcher.Get();

            foreach (ManagementObject managementObject in managementObjectCollection)
            {
                string friendlyName = managementObject["Name"].ToString();

                Match match = new Regex(@"(?!\()COM([0-9]|[0-9][0-9])(?=\))").Match(friendlyName);

                if (match.Success)
                    ListOfSerialPortClass.Add(new SerialPortClass()
                    {
                        SerialPort_FriendlyName = friendlyName,
                        SerialPort_PortName = match.Value,
                    });
            }

            return ListOfSerialPortClass;
        }

        string GetFunctionName(string serialString)
        {

            try
            {
                if (serialString != string.Empty)
                {
                    string[] stringArray = serialString.ToString().Replace("\r", "").Split(';');

                    if (stringArray.Length >= 1)
                        if (stringArray.Length >= 1)
                        {
                            return stringArray[1];
                        }
                }
            }
            catch
            {
                Console.WriteLine("Error: " + serialString);
            }
            return string.Empty;
        }

        public void SendSerialData(SerialCommand command)
        {

            string serialCommand = command.AssembleCommand();

            int checksum = GetCheckSum(serialCommand);

            serialCommand += checksum.ToString();

            Console.WriteLine(serialCommand);

            serialPort.WriteLine(serialCommand);
        }

        public void Diameter(string[] splitData) //reflection calls this
        {
            double diameter = 0;

            try //if anything fails, skip it and wait for the next serial event
            {

                if (Double.TryParse(splitData[2], out diameter)) //if it can convert to double, do it
                {
                    string formatString = "0.";
                    splitData[2] = splitData[2].Replace("\0", string.Empty); //remove nulls
                    for (int i = 0; i < splitData[2].Split('.')[1].ToString().Length; i++) //format the string for number of decimal places
                    {
                        formatString += "0";
                    }

                    DiameterChanged?.Invoke(diameter.ToString(formatString), null);
                }
            }
            catch (Exception oe)
            {

            }
        }

        public void Handshake(string[] splitData) //reflection calls this
        {
            if (!bHandshake)
            {
                SerialCommand command = new SerialCommand();
                command.DeviceID = "100";
                command.Command = "Handshake";
                command.Value = "";

                SendSerialData(command);

                command.Command = "FilamentCapture";

                Thread.Sleep(100);

                SendSerialData(command);

            }
            bHandshake = true;
        }

        public void ProcessIndirectFunction(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void SpoolRPM(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void velocity(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void InnerOffset(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void SpoolWidth(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void RunMode(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void StartPosition(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void SpoolTicks(string[] splitData) //reflection calls this
        {
            if (splitData.Length >= 3)
            {
                try
                {
                    int ticks = (int)Convert.ChangeType(splitData[2], typeof(int));
                }
                catch { }
            }

            processSerialCommand(splitData);
        }

        public void PullerRPM(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        public void FilamentLength(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
        }

        private void processSerialCommand(string[] splitData)
        {
            SerialCommand command = new SerialCommand();


            if (splitData.Length >= 3)
            {
                command.DeviceID = splitData[0];
                command.Command = splitData[1];
                command.Value = splitData[2].Replace("\0", string.Empty); //remove nulls




                GeneralDataChanged?.Invoke(command, null);

            }
        }
    }
}
