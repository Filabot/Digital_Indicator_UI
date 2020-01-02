﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Indicator.Logic.Navigation
{
    public interface INavigationService
    {

        void NavigateTo(string screenName);
        void NavigateToRegion(string region, string screenName);
        void ClearRegion(string region);
        event EventHandler RegionCleared;
        event EventHandler NavigationCompleted;
    }
}
