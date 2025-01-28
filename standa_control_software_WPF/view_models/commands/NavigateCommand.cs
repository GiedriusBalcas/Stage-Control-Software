
using standa_control_software_WPF.view_models;
using standa_control_software_WPF.view_models.stores;


namespace standa_control_software_WPF.view_models.commands
{
    public class NavigateCommand : CommandBase
    {
        private readonly NavigationStore _navigateStore;
        private readonly Func<ViewModelBase> _getViewModel;

        public NavigateCommand(NavigationStore navigateStore, Func<ViewModelBase> getViewModel)
        {
            _navigateStore = navigateStore;
            _getViewModel = getViewModel;
        }

        public override void Execute(object? parameter = null)
        {
            _navigateStore.CurrentViewModel = _getViewModel();
        }


    }
}
