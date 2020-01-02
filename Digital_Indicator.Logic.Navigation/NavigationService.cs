using Microsoft.Practices.Unity;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Indicator.Logic.Navigation
{
    public class NavigationService : BindableBase, INavigationService
    {
        private readonly IUnityContainer _container;
        private readonly IRegionManager _regionManager;

        public event EventHandler RegionCleared;
        public event EventHandler NavigationCompleted;

        public NavigationService(IUnityContainer container, IRegionManager regionManager)
        {
            _container = container;
            _regionManager = regionManager;
        }

        public void NavigateTo(string screenName)
        {
            _regionManager.RequestNavigate("ContentRegion", screenName, NavigateCallback);

            
        }

        private void NavigateCallback(NavigationResult obj)
        {
            NavigationCompleted?.Invoke(obj.Context.Uri.ToString(), null);
        }

        public void NavigateToRegion(string region, string screenName)
        {
            _regionManager.RequestNavigate(region, screenName, NavigateCallback);

        }

        public void ClearRegion(string region)
        {
            _regionManager.Regions[region].RemoveAll();

            RegionCleared?.Invoke(region, null);
        }

        
    }
}
