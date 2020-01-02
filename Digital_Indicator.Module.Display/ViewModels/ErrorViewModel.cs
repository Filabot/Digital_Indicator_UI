using Digital_Indicator.Infrastructure.UI.Controls;
using Digital_Indicator.Logic.Filament;
using Digital_Indicator.Logic.Navigation;
using Digital_Indicator.Logic.UI_Intelligence;
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
    public class ErrorViewModel : BindableBase
    {

        IUI_IntelligenceService _iui_IntelligenceService;
        IFilamentService _filamentService;
        INavigationService _navigationService;

        private DelegateCommand closeErrorView;


        public ObservableCollection<ViewModelBase> Errors
        {
            get { return (ObservableCollection<ViewModelBase>)_iui_IntelligenceService.GetErrors(); }
        }

        public DelegateCommand CloseErrorView
        {
            get { return closeErrorView; }
            set { SetProperty(ref closeErrorView, value); }
        }



        public ErrorViewModel(IFilamentService filamentService, IUI_IntelligenceService iui_IntelligenceService, INavigationService navigationService)
        {
            _iui_IntelligenceService = iui_IntelligenceService;
            _filamentService = filamentService;
            _navigationService = navigationService;

            _filamentService.PropertyChanged += _filamentService_PropertyChanged;

            CloseErrorView = new DelegateCommand(CloseView_Click);
        }

        private void CloseView_Click()
        {

        }


        private void _filamentService_PropertyChanged(object sender, EventArgs e)
        {
            

        }

        public void CloseSettings()
        {
            _navigationService.ClearRegion("SettingsRegion");
            _iui_IntelligenceService.GetErrors().Clear();
        }


    }
}
