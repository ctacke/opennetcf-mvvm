using OpenNETCF.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace OpenNETCF.MVVM
{
    public static class NavigationService
    {
        // this is a viewType : viewModelType lookup table
        private static SafeDictionary<Type, Type> m_index = new SafeDictionary<Type, Type>();
        private static Page m_homeView;
        private static Page m_loginView;
        private static bool m_navigating;
        private static List<Type> m_multipagesBeingWatched = new List<Type>();
        private static object m_mainViewSyncRoot = new object();
        private static object m_creationSyncRoot = new object();

        public static AnalyticsSettings Analytics { get; private set; } = new AnalyticsSettings();

        public static Page CurrentView
        {
            get
            {
                if (m_homeView == null) return null;
                if (m_homeView.Navigation.NavigationStack.Count == 0) return null;

                var page = m_homeView.Navigation.NavigationStack.Last();
                if (page is CarouselPage)
                {
                    return (page as CarouselPage).CurrentPage;
                }
                return page;
            }
        }

        public static INavigation Navigation
        {
            get
            {
                if (m_homeView == null) return null;
                return m_homeView.Navigation;
            }
        }

        [Obsolete("Method is obsolete. Use 'SetHomeView' instead", false)]
        public static void SetMainView<TView>(bool wrapInNavigationPage)
            where TView : Page, new()
        {
            SetHomeView<TView>(wrapInNavigationPage);
        }

        /// <summary>
        /// Registers and shows the page to act as the application's primary Home view
        /// </summary>
        /// <typeparam name="TView"></typeparam>
        /// <param name="wrapInNavigationPage">Set to <b>true</b> if the view is not already a NavigationPage.</param>
        /// <param name="showImmediately">Set to <b>true</b> to have this call to show the view.  <b>False</b> to have it only created and registered.</param>
        public static void SetHomeView<TView>(bool wrapInNavigationPage, bool showImmediately = true)
            where TView : Page, new()
        {
            var fromPageName = Analytics.GetPageName(CurrentView);

            lock (m_mainViewSyncRoot)
            {
                var view = CreateViewAndViewModel<TView>();

                if (wrapInNavigationPage)
                {
                    m_homeView = new NavigationPage(view)
                    {

                        Title = view.Title ?? "[Title not set]"
                    };

                }
                else
                {
                    m_homeView = view;
                }

                if (showImmediately)
                {
                    Application.Current.MainPage = m_homeView;
                }
            }

            var toPageName = Analytics.GetPageName(m_homeView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        /// <summary>
        /// Registers a Page to be used as the LoginView
        /// </summary>
        /// <typeparam name="TView">Type of the page that is your Login page</typeparam>
        /// <param name="showImmediately">Set to <b>true</b> to have this call to show the view.  <b>False</b> to have it only created and registered.</param>
        public static void SetLoginView<TView>(bool showImmediately = false)
            where TView : Page, new()
        {
            var fromPageName = Analytics.GetPageName(CurrentView);

            lock (m_mainViewSyncRoot)
            {
                var view = CreateViewAndViewModel<TView>();
                m_loginView = view;

                if (showImmediately)
                {
                    Application.Current.MainPage = m_loginView;
                }
            }

            var toPageName = Analytics.GetPageName(m_homeView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        public static void HideNavigationBar()
        {
            if (m_homeView != null)
            {
                NavigationPage.SetHasNavigationBar(m_homeView, false);
            }
        }

        public static void ShowNavigationBar()
        {
            if (m_homeView != null)
            {
                NavigationPage.SetHasNavigationBar(m_homeView, true);
            }
        }

        public static void Register<TView, TViewModel>()
            where TView : Page
            where TViewModel : class, IViewModel, new()
        {
            lock (m_index)
            {
                var viewType = typeof(TView);
                var viewModelType = typeof(TViewModel);
                if (!m_index.ContainsKey(viewType))
                {
                    m_index.Add(viewType, viewModelType);
                }
            }
        }

        public async static Task ShowLogin(bool animate = false)
        {
            var fromPageName = Analytics.GetPageName(CurrentView);

            Validate.Begin()
                .IsNotNull(m_loginView, "SetLoginView has not been called")
                .Check();

            // if we have no main page yet (first opening app), this becomes the main page
            if(Application.Current.MainPage == null)
            {
                Application.Current.MainPage = m_loginView;
            }
            // otherwise (user session timeout, etc), we're just a modal on top of whatever is there
            else
            {
                await ShowView(m_loginView, animate, false);
            }
            var toPageName = Analytics.GetPageName(CurrentView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        public async static Task ShowHome()
        {
            Validate.Begin()
                .IsNotNull(m_homeView, "SetHomeView has not been called")
                .Check();

            var fromPageName = Analytics.GetPageName(CurrentView);

            // if the current page is the login, just show the home page
            if (Application.Current.MainPage == m_loginView)
            {
                Application.Current.MainPage = m_homeView;
            }
            // otherwise pop everything off the nav stack back to home
            else
            {
                await m_homeView.Navigation.PopToRootAsync(true);
            }

            var toPageName = Analytics.GetPageName(CurrentView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        public async static Task NavigateForward<TView>()
            where TView : Page, new()
        {
            await NavigateForward<TView>(false);
        }

        public async static Task NavigateForward<TView>(bool animated)
            where TView : Page, new()
        {
            await ShowView<TView>(animated, false);
        }

        public async static Task ShowModal<TView>(bool animated)
            where TView : Page, new()
        {
            Validate.Begin()
                .IsNotNull(m_homeView, "SetHomeView has not been called")
                .Check();

            await ShowView<TView>(animated, true);
        }

        public async static Task HideModal(bool animated)
        {
            Validate.Begin()
                .IsTrue(Navigation.ModalStack.Count > 0, "No modal found")
                .Check();

            var fromPageName = Analytics.GetPageName(CurrentView);

            await m_homeView.Navigation.PopModalAsync(animated);

            var toPageName = Analytics.GetPageName(CurrentView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        public async static Task NavigateBack(bool animated)
        {
            Validate.Begin()
                .IsTrue(Navigation.NavigationStack.Count > 1, "Already at the start of the navigation stack")
                .Check();

            var fromPageName = Analytics.GetPageName(CurrentView);

            if (m_navigating) return;
            try
            {
                await m_homeView.Navigation.PopAsync(animated);
            }
            finally
            {
                m_navigating = false;
            }

            var toPageName = Analytics.GetPageName(CurrentView);
            Analytics.LogPageNavigation(fromPageName, toPageName);
        }

        private async static Task ShowView<TView>(bool animated, bool modal)
            where TView : Page, new()
        {
            if (m_navigating) return;
            try
            {
                m_navigating = true;
                var view = CreateViewAndViewModel<TView>();

                // now show it
                await ShowView(view, animated, modal);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                m_navigating = false;
            }
        }

        private static async Task ShowView(Page view, bool animated, bool modal)
        {
            Validate.Begin()
                .IsNotNull(m_homeView, "SetHomeView has not been called")
                .Check();

            var fromViewName = Analytics.GetPageName(CurrentView);
            var toViewName = Analytics.GetPageName(view);

            if (view.Parent != null)
            {
                view.Parent = null;
            }

            if (modal)
            {
                await m_homeView.Navigation.PushModalAsync(view, animated);
            }
            else
            {
                await m_homeView.Navigation.PushAsync(view, animated);
            }

            Analytics.LogPageNavigation(fromViewName, toViewName);
        }

        private static TView CreateViewAndViewModel<TView>()
            where TView : Page, new()
        {
            var viewType = typeof(TView);
            return (TView)CreateViewAndViewModel(viewType);
        }

        private static object CreateViewAndViewModel(Type viewType)
        {
            Validate.Begin()
                .ParameterIsNotNull(viewType, "viewType")
                .Check();

            Type viewModelType = null;

            lock (m_index)
            {
                if (!m_index.ContainsKey(viewType))
                {
                    throw new ViewTypeNotRegisteredException(viewType);
                }
                viewModelType = m_index[viewType];
            }

            Page view;

            lock (m_creationSyncRoot)
            {
                // do we have a view already created?
                view = RootWorkItem.Services.Get(viewType) as Page;

                if (view == null)
                {
                    try
                    {
                        // create the view
                        view = Activator.CreateInstance(viewType) as Page;

                        // check to see if it's now registered
                        // if the View calls something like GetRegisteredViewModel, we'll get re-entered and it will already be registered
                        var existing = RootWorkItem.Services.Get(viewType);
                        if (existing == null)
                        {
                            RootWorkItem.Services.Add(viewType, view);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to create View: " + viewType.Name, ex);
                    }
                }
            }

            if (view is MultiPage<ContentPage>)
            {
                var t = typeof(View);
                if (!m_multipagesBeingWatched.Contains(t))
                {
                    (view as MultiPage<ContentPage>).CurrentPageChanged += OnMultiPageChanged;
                    m_multipagesBeingWatched.Add(t);
                }
            }

            IViewModel viewModel;

            lock (m_creationSyncRoot)
            {
                // do we have a viewmodel already created?
                viewModel = RootWorkItem.Services.Get(viewModelType) as IViewModel;

                if (viewModel == null)
                {
                    try
                    {
                        viewModel = RootWorkItem.Services.AddNew(viewModelType) as IViewModel;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to create ViewModel: " + viewModelType.Name, ex);
                    }
                }
            }

            // make sure the binding is set
            view.BindingContext = viewModel;

            return view;
        }

        private static void OnMultiPageChanged(object sender, EventArgs e)
        {
            var source = sender as CarouselPage;
            var toPage = Analytics.GetPageName(source.CurrentPage);
            Analytics.LogPageNavigation(null, toPage);
        }

        public static TViewModel GetViewModel<TViewModel>()
            where TViewModel : class, IViewModel
        {
            var viewModelType = typeof(TViewModel);
            var viewModel = RootWorkItem.Services.Get(viewModelType) as TViewModel;

            if (viewModel == null)
            {
                try
                {
                    viewModel = RootWorkItem.Services.AddNew(viewModelType) as TViewModel;
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to create ViewModel: " + viewModelType.Name, ex);
                }
            }

            return viewModel;
        }

        public static IViewModel GetViewModelForView<TView>()
            where TView : Page, new()
        {
            var viewType = typeof(TView);
            return GetViewModelForView(viewType);
        }

        internal static IViewModel GetViewModelForView(Type viewType)
        {
            Validate.Begin()
                .ParameterIsNotNull(viewType, "viewType")
                .Check();

            Type viewModelType = null;

            lock (m_index)
            {
                if (!m_index.ContainsKey(viewType))
                {
                    throw new ViewTypeNotRegisteredException(viewType);
                }
                viewModelType = m_index[viewType];
            }
            var existing = RootWorkItem.Services.Get(viewModelType) as IViewModel;

            if (existing == null)
            {
                CreateViewAndViewModel(viewType);
            }

            return RootWorkItem.Services.Get(viewModelType) as IViewModel;
        }

        public static Page GetViewForViewModel<TViewModel>()
            where TViewModel : IViewModel
        {
            var viewModelType = typeof(TViewModel);
            return GetViewForViewModel(viewModelType);
        }

        internal static Page GetViewForViewModel(Type viewModelType)
        {
            Validate.Begin()
                .ParameterIsNotNull(viewModelType, "viewModelType")
                .Check();

            lock (m_index)
            {
                var tuple = m_index.FirstOrDefault(v => v.Value == viewModelType);

                if (tuple.Equals(default(KeyValuePair<Type, Type>))) return null;  // not registered - not sure this is a valid compare.  Maybe should check the items in the tuple?

                return RootWorkItem.Services.Get(tuple.Key) as Page;
            }
        }

        public static Page GetRegisteredView<TViewModel>(this TViewModel viewModel)
            where TViewModel : class, IViewModel
        {
            Validate.Begin()
                .ParameterIsNotNull(viewModel, "viewModel")
                .Check();

            var viewModelType = typeof(TViewModel);

            lock (m_index)
            {
                var tuple = m_index.FirstOrDefault(v => v.Value == viewModelType);

                if (tuple.Equals(default(KeyValuePair<Type, Type>))) return null;  // not registered - not sure this is a valid compare.  Maybe should check the items in the tuple?

                return RootWorkItem.Services.Get(tuple.Key) as Page;
            }
        }

        public static IViewModel GetRegisteredViewModel<TView>(this TView view)
            where TView : Page, new()
        {
            Validate.Begin()
                .ParameterIsNotNull(view, "view")
                .Check();

            var viewType = typeof(TView);
            Type viewModelType = null;

            // if the view hasn't been registered with the DI container, add it now
            if (!RootWorkItem.Services.Contains<TView>())
            {
                RootWorkItem.Services.Add(view);
            }

            lock (m_index)
            {
                if (!m_index.ContainsKey(viewType))
                {
                    throw new ViewTypeNotRegisteredException(viewType);
                }
                viewModelType = m_index[viewType];
            }
            var existing = RootWorkItem.Services.Get(viewModelType) as IViewModel;

            if (existing == null)
            {
                CreateViewAndViewModel<TView>();
            }

            return RootWorkItem.Services.Get(viewModelType) as IViewModel;
        }

        public static TView GetView<TView>()
            where TView : Page, new()
        {
            return CreateViewAndViewModel<TView>();
        }

        public static async Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
        {
            var tcs = new TaskCompletionSource<bool>();

            Device.BeginInvokeOnMainThread(async () =>
            {
                var sourceView = CurrentView == null ? m_homeView : CurrentView;

                var result = await CurrentView.DisplayAlert(title, message, accept, cancel);
                tcs.SetResult(result);
            });

            return await tcs.Task;
        }

        public static async Task DisplayAlert(string title, string message, string cancel)
        {
            var tcs = new TaskCompletionSource<bool>();

            Device.BeginInvokeOnMainThread(async () =>
            {
                var sourceView = CurrentView == null ? m_homeView : CurrentView;

                await CurrentView.DisplayAlert(title, message, cancel);
                tcs.SetResult(true);
            });

            await tcs.Task;
        }
    }
}