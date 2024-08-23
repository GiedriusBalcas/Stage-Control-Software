using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.view_models.system_control;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models
{
    public class SystemControlMainViewModel : ViewModelBase
    {
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        private readonly NavigationStore _navigationStore;
        public ObservableCollection<NavItem> NavigationItems { get; set; }

        private NavItem _selectedNavItem;
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

        public ICommand NavigateToInfoPageCommand { get; set; }
        public ICommand NavigateToConfigPageCommand { get; set; }
        public ICommand NavigateToCompilePageCommand { get; set; }

        public SystemControlViewModel CurrentCompilerViewModel { get; }

        public SystemControlMainViewModel(standa_controller_software.command_manager.CommandManager config, NavigationStore navigationStore,
            Func<SystemPropertiesViewModel> getConfigPageViewModel,
            Func<SystemInformtaionViewModel> getInfoPageViewModel,
            Func<SystemControlViewModel> getCompPageViewModel
            )
        {
            _commandManager = config;
            _navigationStore = navigationStore;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewmodelChanged;

            NavigateToInfoPageCommand = new NavigateCommand(navigationStore, getInfoPageViewModel);
            NavigateToConfigPageCommand = new NavigateCommand(navigationStore, getConfigPageViewModel);
            NavigateToCompilePageCommand = new NavigateCommand(navigationStore, getCompPageViewModel);

            CurrentCompilerViewModel = getCompPageViewModel();

            NavigationItems = new ObservableCollection<NavItem>
            {
                new NavItem(){ Header= "Device Properties", GetViewModel= getConfigPageViewModel},
                new NavItem(){ Header= "Positioner Control", GetViewModel= getInfoPageViewModel},
                new NavItem(){ Header= "Command Window", GetViewModel= getCompPageViewModel},
            };
            _selectedNavItem = NavigationItems.First();
        }

        private void OnCurrentViewmodelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }

    public class NavItem
    {
        public string Header { get; set; }
        public Func<ViewModelBase> GetViewModel { get; set; }
    }
}
