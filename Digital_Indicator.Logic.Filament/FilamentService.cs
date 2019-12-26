using Digital_Indicator.Infrastructure;
using Digital_Indicator.Logic.FileOperations;
using Digital_Indicator.Logic.Helpers;
using Digital_Indicator.Logic.SerialCommunications;
using Digital_Indicator.WindowForms.ZedGraphUserControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Indicator.Logic.Filament
{
    public class FilamentService : IFilamentService
    {
        public event EventHandler DiameterChanged;

        public event EventHandler PropertyChanged;

        public event EventHandler StopWatchedTimeChanged;

        public Dictionary<string, string> FilamentServiceVariables { get; private set; }

        public Stopwatch DurationClock { get; private set; }

        public String RemainingTime { get; private set; }

        private IXmlService _xmlService;
        private ICsvService _csvService;
        private ISerialService _serialService;

        private bool captureStarted;
        public bool CaptureStarted
        {
            get { return captureStarted; }
            set
            {
                captureStarted = value;

                if (captureStarted)
                {
                    FilamentServiceVariables["HighestValue"] = FilamentServiceVariables["ActualDiameter"];
                    FilamentServiceVariables["LowestValue"] = FilamentServiceVariables["ActualDiameter"];
                    if (FilamentServiceVariables["SpoolNumber"] == string.Empty) { FilamentServiceVariables["SpoolNumber"] = "0"; }
                    //FilamentServiceVariables["HighestValue"] = nominalDiameter;
                    //FilamentServiceVariables["LowestValue"] = nominalDiameter;


                    FilamentServiceVariables["SpoolNumber"] = (FilamentServiceVariables["SpoolNumber"].GetInteger() + 1).ToString();
                    SetupPlots();
                    SetupStopwatch();
                    SaveXmlData();
                }
                else
                {
                    SaveHistoricalData(ZedGraphPlotModel.GetPlot("HistoricalModel").GetDataPoints());
                    DurationClock.Stop();
                }
                SerialCommand command = new SerialCommand() { Command = "FilamentCapture", DeviceID = "100", Value = captureStarted ? "1" : "0" };
                _serialService.SendSerialData(command);
            }
        }

        public bool IsSimulationModeActive
        {
            get { return _serialService.IsSimulationModeActive; }
            set { _serialService.IsSimulationModeActive = value; }
        }

        public FilamentService(ISerialService serialService, IXmlService xmlService, ICsvService csvService)
        {
            DurationClock = new Stopwatch();

            _serialService = serialService;
            _serialService.DiameterChanged += SerialService_DiameterChanged;
            //_serialService.TraverseDataChanged += _serialService_TraverseDataChanged;
            _serialService.GeneralDataChanged += _serialService_GeneralDataChanged;

            _xmlService = xmlService;
            _csvService = csvService;

            FilamentServiceVariables = new Dictionary<string, string>();
            FilamentServiceVariables.Add("Description", "");
            FilamentServiceVariables.Add("ActualDiameter", "");
            FilamentServiceVariables.Add("HighestValue", "");
            FilamentServiceVariables.Add("LowestValue", "");
            FilamentServiceVariables.Add("NominalDiameter", "");
            FilamentServiceVariables.Add("UpperLimit", "");
            FilamentServiceVariables.Add("LowerLimit", "");
            FilamentServiceVariables.Add("Tolerance", "");
            FilamentServiceVariables.Add("Duration", "");
            FilamentServiceVariables.Add("SpoolNumber", "");
            FilamentServiceVariables.Add("SpoolRPM", "");
            FilamentServiceVariables.Add("SpecificGravity", "");
            FilamentServiceVariables.Add("SpoolWeightLimit", "");
            FilamentServiceVariables.Add("PullerRPM", "");
            FilamentServiceVariables.Add("FilamentLength", "");
            FilamentServiceVariables.Add("Feedrate", "");
            FilamentServiceVariables.Add("SpoolWeight", "");
            FilamentServiceVariables.Add("OutputRate", "");
            FilamentServiceVariables.Add("RemainingTime", "");

            BuildXmlData();
            SetupPlots();
        }

        private void _serialService_GeneralDataChanged(object sender, EventArgs e)
        {
            SerialCommand command = (SerialCommand)sender;

            FilamentServiceVariables[command.Command] = command.Value;

            PropertyChanged?.Invoke(command, new PropertyChangedEventArgs(command.Command));

            if (command.Command == "FilamentCapture" && command.Value == "1" && !captureStarted)
            {
                captureStarted = true;
            }

            ////Expression set (faster than reflection)
            //Type type = this.GetType();
            //PropertyInfo propertyInfo = type.GetProperty(command.Command);
            //ParameterExpression instanceParam = Expression.Parameter(type);
            //ParameterExpression argumentParam = Expression.Parameter(typeof(Object));

            //if (propertyInfo != null)
            //{
            //    Action<FilamentService, Object> expression = Expression.Lambda<Action<FilamentService, Object>>(
            //       Expression.Call(instanceParam, propertyInfo.GetSetMethod(), Expression.Convert(argumentParam, propertyInfo.PropertyType)),
            //       instanceParam, argumentParam
            //     ).Compile();
            //    expression(this, command.Value);
            //}
        }

        private void SetupPlots()
        {
            ZedGraphPlotModel.CreatePlots(FilamentServiceVariables["UpperLimit"], FilamentServiceVariables["NominalDiameter"], FilamentServiceVariables["LowerLimit"]);
            ZedGraphPlotModel.GetPlot("HistoricalModel").ZedGraph.GraphPane.XAxis.Scale.Min = new ZedGraph.XDate(DateTime.Now.AddMilliseconds(-100));

            //ZedGraphPlotModel.CreatePlots(UpperLimit, NominalDiameter, LowerLimit);
            //ZedGraphPlotModel.GetPlot("HistoricalModel").ZedGraph.GraphPane.XAxis.Scale.Min = new ZedGraph.XDate(DateTime.Now.AddMilliseconds(-100));

        }

        private void SerialService_DiameterChanged(object sender, EventArgs e)
        {
            //ActualDiameter = sender.ToString();

            double newDiameter = (double)Convert.ChangeType(sender, typeof(double));

            FilamentServiceVariables["ActualDiameter"] = sender.ToString();

            if (captureStarted)
                ZedGraphPlotModel.GetPlots().Select(x => { x.AddDataPoint(FilamentServiceVariables["ActualDiameter"]); return x; }).ToList();

            DiameterChanged?.Invoke(sender, e);

            if (captureStarted)
                UpdateHighsAndLows();
        }

        private void UpdateHighsAndLows()
        {
            string actualDiameter = FilamentServiceVariables["ActualDiameter"];
            string highestValue = FilamentServiceVariables["HighestValue"];
            string lowestValue = FilamentServiceVariables["LowestValue"];
            FilamentServiceVariables["HighestValue"] = highestValue == null ? actualDiameter : highestValue.GetDouble() < actualDiameter.GetDouble() ? actualDiameter : highestValue;
            FilamentServiceVariables["LowestValue"] = lowestValue == null ? actualDiameter : lowestValue.GetDouble() > actualDiameter.GetDouble() ? actualDiameter : lowestValue;
            FilamentServiceVariables["Tolerance"] = ((highestValue.GetDouble() - lowestValue.GetDouble()) / 2).ToString("0.000");
        }

        private void UpdatePlots()
        {
            var zedGraphPlotModel = ZedGraphPlotModel.GetPlots();

            if (zedGraphPlotModel != null)
                zedGraphPlotModel.Select(x =>
                {
                    x.UpperLimitDiameter = FilamentServiceVariables["UpperLimit"];
                    x.NominalDiameter = FilamentServiceVariables["NominalDiameter"];
                    x.LowerLimitDiameter = FilamentServiceVariables["LowerLimit"];
                    return x;
                }).ToList();
        }

        private void BuildXmlData()
        {

            //FilamentServiceVariables["NominalDiameter"] = _xmlService.XmlSettings["filamentData.nominalDiameter"];
            //FilamentServiceVariables["UpperLimit"] = _xmlService.XmlSettings["filamentData.upperLimit"];
            //FilamentServiceVariables["LowerLimit"] = _xmlService.XmlSettings["filamentData.lowerLimit"];
            //FilamentServiceVariables["SpoolNumber"] = _xmlService.XmlSettings["filamentData.spoolNumber"];
            //FilamentServiceVariables["Description"] = _xmlService.XmlSettings["filamentData.materialDescription"];




            foreach (KeyValuePair<string, string> kvp in FilamentServiceVariables.ToList())
            {


                if (!_xmlService.XmlSettings.ContainsKey("filamentData." + kvp.Key))
                {
                    _xmlService.XmlSettings.Add("filamentData." + kvp.Key.ToString(), string.Empty);
                }

                //if (_xmlService.XmlSettings.ContainsKey("filamentData." + kvp.Key))
                //{
                //    _xmlService.XmlSettings.Add("filamentData." + kvp.Key.ToString(), string.Empty);
                //}

                FilamentServiceVariables[kvp.Key] = _xmlService.XmlSettings["filamentData." + kvp.Key];
            }
            SaveXmlData();

            //nominalDiameter = _xmlService.XmlSettings["filamentData.nominalDiameter"];
            //upperLimit = _xmlService.XmlSettings["filamentData.upperLimit"];
            //lowerLimit = _xmlService.XmlSettings["filamentData.lowerLimit"];
            //spoolNumber = _xmlService.XmlSettings["filamentData.spoolNumber"];
            //description = _xmlService.XmlSettings["filamentData.materialDescription"];


            //SetFilamentVariables();
        }

        public void SaveXmlData()
        {
            //_xmlService.XmlSettings["filamentData.nominalDiameter"] = FilamentServiceVariables["NominalDiameter"];
            //_xmlService.XmlSettings["filamentData.upperLimit"] = FilamentServiceVariables["UpperLimit"];
            //_xmlService.XmlSettings["filamentData.lowerLimit"] = FilamentServiceVariables["LowerLimit"];
            //_xmlService.XmlSettings["filamentData.spoolNumber"] = FilamentServiceVariables["SpoolNumber"];
            //_xmlService.XmlSettings["filamentData.materialDescription"] = FilamentServiceVariables["Description"];

            foreach (KeyValuePair<string, string> kvp in FilamentServiceVariables)
            {
                _xmlService.XmlSettings["filamentData." + kvp.Key] = kvp.Value;
            }
            UpdatePlots();


            //_xmlService.XmlSettings["filamentData.nominalDiameter"] = nominalDiameter;
            //_xmlService.XmlSettings["filamentData.upperLimit"] = upperLimit;
            //_xmlService.XmlSettings["filamentData.lowerLimit"] = lowerLimit;
            //_xmlService.XmlSettings["filamentData.spoolNumber"] = spoolNumber;
            //_xmlService.XmlSettings["filamentData.materialDescription"] = description;


            _xmlService.SaveSettings();
        }

        private void SetFilamentVariables()
        {
            //FilamentServiceVariables["UpperLimit"] = upperLimit.ToString();
            //FilamentServiceVariables["LowerLimit"] = lowerLimit.ToString();
            //FilamentServiceVariables["NominalDiameter"] = nominalDiameter.ToString();

        }

        private void SetupStopwatch()
        {
            DurationClock.Reset();
            DurationClock.Start();


            Task.Factory.StartNew(() =>
            {
                while (DurationClock.IsRunning)
                {
                    StopWatchedTimeChanged?.Invoke(DurationClock, new EventArgs());


                    FilamentServiceVariables["Duration"] = DurationClock.Elapsed.Hours.ToString("0") + ":" +
                                                          DurationClock.Elapsed.Minutes.ToString("0#") + ":" +
                                                          DurationClock.Elapsed.Seconds.ToString("0#");


                    double rate = 0.0;
                    double.TryParse(FilamentServiceVariables["OutputRate"], out rate);
                    double spoolWeightLimit = 0;
                    double.TryParse(FilamentServiceVariables["SpoolWeightLimit"], out spoolWeightLimit);

                    rate = rate * 1000;
                    double perMinuteRate = (60 / rate) * 1000;
                    double totalMilliseconds = (perMinuteRate * 60) * spoolWeightLimit;
                    double remainingTime = Math.Abs(DurationClock.ElapsedMilliseconds - totalMilliseconds);

                    if (remainingTime != double.NaN && DurationClock.ElapsedMilliseconds >= 15000)
                    {
                        TimeSpan time = TimeSpan.FromMilliseconds(remainingTime);

                        FilamentServiceVariables["RemainingTime"] = time.Hours.ToString("0") + ":" +
                                                              time.Minutes.ToString("0#") + ":" +
                                                              time.Seconds.ToString("0#");
                    }
                    if (DurationClock.ElapsedMilliseconds < 15000)
                    {
                        FilamentServiceVariables["RemainingTime"] = "Stabilizing";
                    }

                    System.Threading.Thread.Sleep(500);
                }

            });
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SaveHistoricalData(HashSet<DataListXY> dataPoints)
        {
            _csvService.SaveSettings(dataPoints, FilamentServiceVariables["SpoolNumber"], FilamentServiceVariables["Description"]);
        }
    }
}
