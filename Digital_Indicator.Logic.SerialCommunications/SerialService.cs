using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Digital_Indicator.Logic.SerialCommunications
{
    public class SerialService : ISerialService
    {
        class NativeMethods
        {


            [DllImport("Kernel32.dll")]
            public static extern int GetLastError();

            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SetupDiGetClassDevs(
                                              ref Guid ClassGuid,
                                              [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
                                              IntPtr hwndParent,
                                              uint Flags
                                             );
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SetupDiGetClassDevs(
                                             IntPtr intptr,
                                             [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
                                             IntPtr hwndParent,
                                             uint Flags
                                            );

            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SetupDiGetClassDevs(           // 1st form using a ClassGUID only, with null Enumerator
                                               ref Guid ClassGuid,
                                               IntPtr Enumerator,
                                               IntPtr hwndParent,
                                               int Flags
                                            );
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]     // 2nd form uses an Enumerator only, with null ClassGUID
            public static extern IntPtr SetupDiGetClassDevs(
                                               IntPtr ClassGuid,
                                               string Enumerator,
                                               IntPtr hwndParent,
                                               int Flags
                                            );

            [DllImport("Setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern unsafe bool SetupDiEnumDeviceInfo(IntPtr handle, int index, ref SP_DEVINFO_DATA dia);

            [DllImport("Setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern unsafe bool SetupDiEnumDeviceInterfaces(IntPtr handle, IntPtr Enumerator, ref Guid ClassGuid, uint index, ref SP_DEVICE_INTERFACE_DATA spDevInfoData);

            [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern Boolean SetupDiGetDeviceInterfaceDetail(
                                               IntPtr hDevInfo,
                                               ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
                                               ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
                                               UInt32 deviceInterfaceDetailDataSize,
                                               ref UInt32 requiredSize,
                                               ref SP_DEVINFO_DATA deviceInfoData
                                            );

            [DllImport("setupapi.dll")]
            public static extern int CM_Get_Parent(
                                                out UInt32 pdnDevInst,
                                                UInt32 dnDevInst,
                                                int ulFlags
                                             );

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern int CM_Get_Device_ID_Size(out uint pulLen, UInt32 dnDevInst, int flags = 0);
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            public static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags = 0);

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern unsafe bool SetupDiGetDevicePropertyW(
                IntPtr deviceInfoSet,
                ref SP_DEVINFO_DATA DeviceInfoData,
                ref PropertyKey propertyKey,
                out UInt64 propertyType, // or Uint32 ?
                 byte[] propertyBuffer, // or byte[]
                Int32 propertyBufferSize,
                out uint requiredSize, // <----
                UInt32 flags);



            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern unsafe bool SetupDiGetDevicePropertyW(
                IntPtr deviceInfoSet,
                ref SP_DEVINFO_DATA DeviceInfoData,
                ref PropertyKey propertyKey,
                out UInt64 propertyType, // or Uint32 ?
                IntPtr propertyBuffer, // or byte[]
                Int32 propertyBufferSize,
                int* requiredSize, //<----

                UInt32 flags);

        }
        const int BUFFER_SIZE = 256;

        /// <summary>
        /// PROPERTYKEY is defined in wtypes.h
        /// </summary>
        public struct PropertyKey
        {
            /// <summary>
            /// Format ID
            /// </summary>
            public Guid formatId;
            /// <summary>
            /// Property ID
            /// </summary>
            public int propertyId;

            // http://msdn.microsoft.com/en-us/library/windows/desktop/ff384862(v=vs.85).aspx
            // https://subversion.assembla.com/svn/portaudio/portaudio/trunk/src/hostapi/wasapi/mingw-include/propkey.h

            public PropertyKey(Guid guid, int propertyId)
            {
                this.formatId = guid;
                this.propertyId = propertyId;
            }
            public PropertyKey(string formatId, int propertyId)
                : this(new Guid(formatId), propertyId)
            {
            }
            public PropertyKey(uint a, uint b, uint c, uint d, uint e, uint f, uint g, uint h, uint i, uint j, uint k, int propertyId)
                : this(new Guid((uint)a, (ushort)b, (ushort)c, (byte)d, (byte)e, (byte)f, (byte)g, (byte)h, (byte)i, (byte)j, (byte)k), propertyId)
            {
            }
            public string GetBaseString()
            {
                return string.Format("{0},{1}", formatId.ToString(), propertyId.ToString());
            }
            public override string ToString()
            {
                try
                {
                    string basekey;
                    PropertyKey val;
                    if (predefinedkeys == null)
                    {
                        predefinedkeys = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, PropertyKey> kvp in typeof(PropertyKey).ListTypes<PropertyKey>())
                        {
                            val = kvp.Value;
                            basekey = val.GetBaseString();
                            if (!predefinedkeys.ContainsKey(basekey))
                                predefinedkeys.Add(basekey, kvp.Key);
                        }
                    }
                    basekey = this.GetBaseString();
                    if (predefinedkeys.ContainsKey(basekey))
                        return predefinedkeys[basekey]; // return "PKEY_Device_DeviceDesc" if known

                    return basekey; // otherwise, return "a45c254e-df1c-4efd-8020-67d146a850e0,2"
                }
                catch (Exception)
                {
                    return string.Format("{0}.{1}", formatId.ToString(), propertyId.ToString());
                }
            }

            //sample ("a45c254e-df1c-4efd-8020-67d146a850e0,2", "PKEY_Device_DeviceDesc")
            static Dictionary<string, string> predefinedkeys = null;
            //
            // Device properties
            // These PKEYs correspond to the old setupapi SPDRP_XXX properties
            //

            //DEVPROP_TYPE_STRING_LIST
            public static PropertyKey PKEY_Device_BusReportedDeviceDesc = new PropertyKey(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 4);




        }


        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public Int32 cbSize;
            public Guid interfaceClassGuid;
            public Int32 flags;
            private UIntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BUFFER_SIZE)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        public struct GUID
        {
            public Int64 Data1;
            public Int16 Data2;
            public Int16 Data3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Data4;

            public GUID(Int64 d1, Int16 d2, Int16 d3, byte[] d4)
            {
                Data1 = d1;
                Data2 = d2;
                Data3 = d3;
                Data4 = new byte[8];
                Array.Copy(d4, Data4, d4.Length);
            }
        }

        // Device Property
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct DEVPROPKEY
        {
            public Guid fmtid;
            public UInt32 pid;
        }

        enum DigcfConstants : int
        {
            DIGCF_DEFAULT = 0x00000001,
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010
        }

        private SerialPort serialPort;
        private SplashScreen splashScreen;

        public bool IsConnected { get; private set; }

        public event EventHandler DiameterChanged;
        public event EventHandler SpoolerDataChanged;
        public event EventHandler TraverseDataChanged;
        public event EventHandler GeneralDataChanged;

        private bool bHandshake;
        private Int32 watchdogTimer;

        public SerialService()
        {
            if (!IsSimulationModeActive)
                serialPort = new SerialPort();

            splashScreen = new SplashScreen("FILABOT_LOGO_2017 splash1.png");
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

                try
                {


                    splashScreen.Show(false, false);


                    int timeLimit = 5; //seconds
                    Task.Factory.StartNew(() =>
                    {


                        while (timeLimit > 0 && !IsConnected)
                        {

                            Thread.Sleep(1000);
                            timeLimit--;
                        }
                        if (!IsConnected)
                        {

                            //splashScreen.Close(TimeSpan.FromMilliseconds(5));
                            Thread.Sleep(1000);
                            serialPort.Close();

                            MessageBox.Show("Unable to connect to Filalogger, please restart and try again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            splashScreen.Close(TimeSpan.FromMilliseconds(5));

                            Environment.Exit(0);
                            //throw new Exception("Unable to connect to Filalogger, please restart and try again");
                        }

                    });
                    Task.Factory.StartNew(() =>
                    {
                        serialPort.Open();
                        IsConnected = true;
                        splashScreen.Close(TimeSpan.FromMilliseconds(5));

                    });

                }
                catch (Exception oe)
                {
                    throw new Exception(oe.Message);
                }
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
            //serialPort.ReadTimeout = 10000;
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

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Thread.Sleep(1);
                    watchdogTimer++;
                    if (watchdogTimer >= 600)
                    {
                        SerialCommand command = new SerialCommand() {  Command = "KeepAlive", DeviceID = "100", Value = "Disconnected"};

                        GeneralDataChanged?.Invoke(command, null);
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

            List<string> deviceID = new List<string>();


            Guid usbGUID = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
            //GUID usbGUID = new GUID(0xA5DCBF10L, (short)0x6530, 0x11D2, new byte[] { 0x90, 0x1F, 0x00, 0xC0, 0x4F, 0xB9, 0x51, 0xED });

            //IntPtr h = NativeMethods.SetupDiGetClassDevs(ref usbGUID, null, IntPtr.Zero, (int)DigcfConstants.DIGCF_PRESENT | (int)DigcfConstants.DIGCF_DEVICEINTERFACE);
            IntPtr h = NativeMethods.SetupDiGetClassDevs(IntPtr.Zero, "USB", IntPtr.Zero, (int)DigcfConstants.DIGCF_PRESENT | (int)DigcfConstants.DIGCF_ALLCLASSES);

            SP_DEVICE_INTERFACE_DATA Data = new SP_DEVICE_INTERFACE_DATA();
            Data.cbSize = Marshal.SizeOf(Data);

            if (h.ToInt32() == -1)
            {
                Console.WriteLine("INVALID_HANDLE_VALUE");
            }
            else
            {

                int i = 0;
                SP_DEVINFO_DATA DeviceInfoData = new SP_DEVINFO_DATA();
                for (i = 0; ; i++)
                {
                    DeviceInfoData.cbSize = (uint)Marshal.SizeOf(DeviceInfoData);
                    if (!NativeMethods.SetupDiEnumDeviceInfo(h, i, ref DeviceInfoData))
                        break;

                    uint buffer = BUFFER_SIZE;
                    StringBuilder szDeviceInstanceID = new StringBuilder((int)buffer);
                    //StringBuilder bus = new StringBuilder((int)buffer);
                    byte[] bus = new byte[(int)buffer];

                    int status = NativeMethods.CM_Get_Device_ID(DeviceInfoData.DevInst, szDeviceInstanceID, ((int)buffer), 0);

                    if (status != 0)
                    { continue; }

                    //Console.WriteLine("InstanceID: {0}", szDeviceInstanceID);
                    ulong propType = 0;
                    UInt32 requiredsize;

                    if (NativeMethods.SetupDiGetDevicePropertyW(h, ref DeviceInfoData, ref PropertyKey.PKEY_Device_BusReportedDeviceDesc, out propType, bus, BUFFER_SIZE, out requiredsize, 0))
                    {
                        //Console.WriteLine("bus: {0}", bus);
                        string str = System.Text.Encoding.Default.GetString(bus).Replace("\0", "");
                        if (str.Contains("Filalogger"))
                        {
                            deviceID.Add(szDeviceInstanceID.ToString());
                        }

                    }
                    int error = NativeMethods.GetLastError();
                }
            }


            List<SerialPortClass> ListOfSerialPortClass = new List<SerialPortClass>();


            ManagementObjectCollection managementObjectCollection = null;
            ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("root\\cimv2",
             "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");

            managementObjectCollection = managementObjectSearcher.Get();


            foreach (ManagementObject managementObject in managementObjectCollection)
            {


                foreach (string device in deviceID)
                {
                    try
                    {
                        if (managementObject["DeviceID"].ToString() == device)
                        {
                            string friendlyName = managementObject["Name"].ToString();

                            Match match = new Regex(@"(?!\()COM([0-9]|[0-9][0-9])(?=\))").Match(friendlyName);
                            if (match.Success)
                                ListOfSerialPortClass.Add(new SerialPortClass()
                                {
                                    SerialPort_FriendlyName = "Filalogger",
                                    SerialPort_PortName = match.Value,
                                });
                        }
                    }
                    catch { }
                }
            }


            return ListOfSerialPortClass;
        }

        public List<SerialPortClass> GetAlternateList()
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
            if (serialPort.IsOpen)
            {
                string serialCommand = command.AssembleCommand();

                int checksum = GetCheckSum(serialCommand);

                serialCommand += checksum.ToString();

                Console.WriteLine(serialCommand);

                serialPort.WriteLine(serialCommand);
            }
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

        public void KeepAlive(string[] splitData) //reflection calls this
        {
            processSerialCommand(splitData);
            watchdogTimer = 0;
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

        public void DiameterError(string[] splitData) //reflection calls this
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

static class propertykeyextensions
{

    public static KeyValuePair<string, T>[] ListTypes<T>(this Type classType)
    {
        List<KeyValuePair<string, T>> list = new List<KeyValuePair<string, T>>();
        foreach (FieldInfo fi in classType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            if (fi.FieldType == typeof(T))
                list.Add(new KeyValuePair<string, T>(fi.Name, (T)fi.GetValue(classType)));
        return list.ToArray();
    }
}