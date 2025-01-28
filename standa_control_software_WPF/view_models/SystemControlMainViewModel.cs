using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.view_models.system_control;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models
{
    /// <summary>
    /// Represents the main view model for system control, managing navigation between different system control pages.
    /// </summary>
    public class SystemControlMainViewModel : ViewModelBase
    {
        private readonly NavigationStore _navigationStore;
        private NavItem _selectedNavItem;

        public ObservableCollection<NavItem> NavigationItems { get; set; }
        public NavItem SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                _selectedNavItem = value;
                _navigationStore.CurrentViewModel = _selectedNavItem.GetViewModel();
            }
        }
        public ViewModelBase CurrentViewModel => _navigationStore.CurrentViewModel;
        public SystemControlViewModel CurrentCompilerViewModel { get; }

        // Initialize navigation commands with corresponding view model retrieval functions
        public ICommand NavigateToInfoPageCommand { get; set; }
        public ICommand NavigateToConfigPageCommand { get; set; }
        public ICommand NavigateToCompilePageCommand { get; set; }

        public SystemControlMainViewModel(NavigationStore navigationStore,
            Func<SystemPropertiesViewModel> getConfigPageViewModel,
            Func<SystemInformationViewModel> getInfoPageViewModel,
            Func<SystemControlViewModel> getCompPageViewModel
            )
        {
            _navigationStore = navigationStore;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewmodelChanged;

            NavigateToInfoPageCommand = new NavigateCommand(navigationStore, getInfoPageViewModel);
            NavigateToConfigPageCommand = new NavigateCommand(navigationStore, getConfigPageViewModel);
            NavigateToCompilePageCommand = new NavigateCommand(navigationStore, getCompPageViewModel);

            CurrentCompilerViewModel = getCompPageViewModel();

            NavigationItems =
            [
                new NavItem(){ Header= "Device Properties", GetViewModel= getConfigPageViewModel},
                new NavItem(){ Header= "Information", GetViewModel= getInfoPageViewModel},
                new NavItem(){ Header= "Command Window", GetViewModel= getCompPageViewModel},
            ];
            _selectedNavItem = NavigationItems.First();
        }

        private void OnCurrentViewmodelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }

    /// <summary>
    /// Represents a navigation item in the system control interface, including its display header and the associated view model.
    /// </summary>
    public class NavItem
    {
        public required string Header { get; set; }
        public required Func<ViewModelBase> GetViewModel { get; set; }
    }
}
