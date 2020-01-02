using Digital_Indicator.Logic.Navigation;
using Digital_Indicator.Logic.SerialCommunications;
using Digital_Indicator.Logic.UI_Intelligence;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Digital_Indicator.Module.Display.ViewModels
{
    public class SerialPortSelectionViewModel : BindableBase
    {
        private ISerialService _serialService;
        private INavigationService _naviService;
        IUI_IntelligenceService _IntelligenceService;

        public ObservableCollection<SerialPortClass> SerialPortList { get; }
        public ObservableCollection<SerialPortClass> AlternateList { get; }

        public DelegateCommand<object> SelectedPort { get; }

        public DelegateCommand NextScreen { get; }

        private SerialPortClass serialPortSelection;
        public SerialPortClass SerialPortSelection
        {
            get { return serialPortSelection; }
            set { serialPortSelection = value; }
        }

        public SerialPortSelectionViewModel(ISerialService serialService, INavigationService naviService, IUI_IntelligenceService intelligenceService)
        {
            _serialService = serialService;
            _naviService = naviService;
            _IntelligenceService = intelligenceService;
            SerialPortList = new ObservableCollection<SerialPortClass>(_serialService.GetSerialPortList());

            AlternateList = new ObservableCollection<SerialPortClass>(_serialService.GetAlternateList());

            if (SerialPortList.Count > 0)
                SerialPortSelection = (SerialPortList.FirstOrDefault(x => x.SerialPort_FriendlyName == "Filalogger"));
            else
                SerialPortList = AlternateList;


            _naviService.NavigationCompleted += _naviService_NavigationCompleted;

            //NextScreen = new DelegateCommand(NextScreen_Click);

            SelectedPort = new DelegateCommand<object>(OnPortSelected);


        }

        private void _naviService_NavigationCompleted(object sender, EventArgs e)
        {

            if (sender.ToString() == "SerialPortSelectionView")
            {
                if (serialPortSelection != null)
                {
                    OnPortSelected(SerialPortSelection);
                }
            }

        }

        private void SetSerialPort()
        {
            if (serialPortSelection != null)
                _serialService.ConnectToSerialPort(serialPortSelection.SerialPort_PortName);
        }

        private void NextScreen_Click()
        {
            if (serialPortSelection == null && _serialService.IsSimulationModeActive)
                SerialPortSelection = new SerialPortClass() { SerialPort_PortName = "", };

            SetSerialPort();

            _naviService.NavigateTo("DiameterView");
        }

        private void OnPortSelected(object obj)
        {
            SerialPortClass port = (SerialPortClass)obj;

            if (port.SerialPort_FriendlyName != string.Empty && port.SerialPort_PortName != string.Empty)
            {
                _serialService.ConnectToSerialPort(port.SerialPort_PortName);

                
                    Dispatcher.CurrentDispatcher.Invoke((Action)(() =>

                 _naviService.NavigateTo("PleaseWaitView")));
                if (_serialService.IsConnected)
                {

                    //_naviService.NavigateTo("PleaseWaitView");

                    _serialService.SendSerialData(new SerialCommand() { Command = "GetFullUpdate", DeviceID = "100" });


                    Dispatcher.CurrentDispatcher.Invoke(() =>
                   {
                       _naviService.NavigateTo("DiameterView");
                   });
                }




            }

        }


    }
}
