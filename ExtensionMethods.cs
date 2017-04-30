using Xamarin.Forms;

namespace OpenNETCF.MVVM
{
    public static class ExtensionMethods
    {
        public static IViewModel GetRegisteredViewModel(this Page view)
        {
            var viewType = view.GetType();
            return NavigationService.GetViewModelForView(viewType);
        }

        public static Page GetRegisteredView(this IViewModel viewModel)
        {
            var viewModelType = viewModel.GetType();
            return NavigationService.GetViewForViewModel(viewModelType);
        }
    }
}
