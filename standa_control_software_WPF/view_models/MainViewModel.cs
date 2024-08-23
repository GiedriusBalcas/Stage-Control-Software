
using standa_control_software_WPF.view_models.stores;

namespace standa_control_software_WPF.view_models
{
    public class MainViewModel : ViewModelBase
    {
        private readonly NavigationStore _navigationStore;

        public ViewModelBase CurrentViewModel => _navigationStore.CurrentViewModel;

        public MainViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewmodelChanged;
        }

        private void OnCurrentViewmodelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }
}
