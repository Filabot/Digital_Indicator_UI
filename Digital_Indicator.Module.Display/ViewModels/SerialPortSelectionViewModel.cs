using Digital_Indicator.Logic.Navigation;
using Digital_Indicator.Logic.SerialCommunications;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Indicator.Module.Display.ViewModels
{
    public class SerialPortSelectionViewModel : BindableBase
    {
        private ISerialService _serialService;
        private INavigationService _naviService;

        public ObservableCollection<SerialPortClass> SerialPortList { get; }

        public DelegateCommand<object> SelectedPort { get; }

        public DelegateCommand NextScreen { get; }

        private SerialPortClass serialPortSelection;
        public SerialPortClass SerialPortSelection
        {
            get { return serialPortSelection; }
            set { serialPortSelection = value; }
        }

        public SerialPortSelectionViewModel(ISerialService serialService, INavigationService naviService)
        {
            _serialService = serialService;
            _naviService = naviService;
            SerialPortList = new ObservableCollection<SerialPortClass>(_serialService.GetSerialPortList());

            NextScreen = new DelegateCommand(NextScreen_Click);

            SelectedPort = new DelegateCommand<object>(OnPortSelected);
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

                _naviService.NavigateTo("PleaseWaitView");

                _serialService.SendSerialData(new SerialCommand() { Command = "GetFullUpdate", DeviceID = "100" });

                _naviService.NavigateTo("DiameterView");

            }

        }
    }
}
