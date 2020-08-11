using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prism.Behaviors;
using Prism.Common;
using Prism.Ioc;
using Xamarin.Forms;

namespace Prism.Navigation
{
    /// <summary>
    /// Prism's <see cref="INavigationService"/> for <see cref="Shell"/>
    /// </summary>
    public partial class ShellPrismNavigationService : INavigationService
    {
        private Page _currentPage;
        private Queue<string> _routeSegments;
        private INavigationParameters _currentParameters;
        private INavigationParameters _segmentParameters;
        private IContainerProvider _container { get; }
        private IPageBehaviorFactory _pageBehaviorFactory { get; }

        Page CurrentPage => (CurrentShell?.CurrentItem?.CurrentItem as IShellSectionController)?.PresentedPage;

        Shell CurrentShell => _shell.Value;

        Lazy<Shell> _shell;
        /// <summary>
        /// Creates an instance of <see cref="ShellPrismNavigationService"/>
        /// </summary>
        /// <param name="containerProvider"></param>
        /// <param name="pageBehaviorFactory"></param>
        public ShellPrismNavigationService(
            IContainerProvider containerProvider,
            IPageBehaviorFactory pageBehaviorFactory)
        {
            _container = containerProvider;
            _pageBehaviorFactory = pageBehaviorFactory;

            _shell = new Lazy<Shell>(() =>
            {
                Shell.Current.Navigated += OnNavigated;
                Shell.Current.Navigating += OnNavigating;
                return Shell.Current;
            });
        }


        // you could also create prism versions of all the shell elements
        // <prism:PrismFlyouutItem
        IEnumerable<BaseShellItem> AllTheKingsItems()
        {
            var items = CurrentShell.Items;
            var sections = items.SelectMany(x => x.Items);
            var contents = sections.SelectMany(x => x.Items);

            return 
                items.OfType<BaseShellItem>()
                    .Union(sections)
                    .Union(contents);
        }

        void WireUpTemplates()
        {
            foreach(var item in AllTheKingsItems().OfType<ShellContent>())
            {
                var closure = Routing.GetRoute(item);
                if (string.IsNullOrWhiteSpace(closure))
                    throw new Exception("Everything needs a route");

                if(item.ContentTemplate == null)
                {
                    item.ContentTemplate =
                        new DataTemplate(() =>
                        {
                            return CreatePageFromSegment(closure);
                        });
                }
            }
        }

        void OnNavigating(object sender, ShellNavigatingEventArgs e)
        {
            // ?? should we set _currentPage here so for reliability
            WireUpTemplates();

            // TODO: Validate this is a good way to get the route parameters... we aren't really able to compare to the current route segment
            if (_routeSegments != null && _routeSegments.Any())
            {
                var segment = _routeSegments.Dequeue();
                _segmentParameters = UriParsingHelper.GetSegmentParameters(segment, _currentParameters);
            }
            else if (_currentParameters != null)
            {
                _segmentParameters = UriParsingHelper.GetSegmentParameters("foo?", _currentParameters);
            }
            else
            {
                _segmentParameters = new NavigationParameters();
            }

            if (!PageUtilities.CanNavigate(_currentPage, _segmentParameters)
                //|| !await PageUtilities.CanNavigateAsync(_currentPage, _segmentParameters)
                )
            {
                _segmentParameters = null;
                e.Cancel();
                return;
            }

            // TODO: Determin Navigation Mode which needs to be added to the Navigation Parameters
            _segmentParameters.GetNavigationParametersInternal().Add(KnownInternalParameters.NavigationMode, NavigationMode.Forward);
            // TODO: Support OnInitializedAsync
            // var newPage = ....;
            // PageUtilities.OnInitializedAsync(newPage, _segmentParameters);
        }

        void OnNavigated(object sender, ShellNavigatedEventArgs e)
        {
            PageUtilities.OnNavigatedFrom(_currentPage, _segmentParameters);
            PageUtilities.OnNavigatedTo(CurrentPage, _segmentParameters);
            _currentPage = CurrentPage;
            _segmentParameters = null;
        }

        // TODO: Invoke IDestructible when the Page is Popped from the Navigation Stack & no longer needed so we can free up some memory

        protected virtual Page CreatePage(string segmentName)
        {
            try
            {
                return _container.Resolve<object>(segmentName) as Page;
            }
            catch (Exception ex)
            {
                if (((IContainerRegistry)_container).IsRegistered<object>(segmentName))
                    throw new NavigationException(NavigationException.ErrorCreatingPage, null, ex);

                throw new NavigationException(NavigationException.NoPageIsRegistered, null, ex);
            }
        }

        protected virtual Page CreatePageFromSegment(string segment)
        {
            string segmentName = null;
            try
            {
                segmentName = UriParsingHelper.GetSegmentName(segment);
                var page = CreatePage(segmentName);
                if (page == null)
                {
                    var innerException = new NullReferenceException(string.Format("{0} could not be created. Please make sure you have registered {0} for navigation.", segmentName));
                    throw new NavigationException(NavigationException.NoPageIsRegistered, null, innerException);
                }

                PageUtilities.SetAutowireViewModelOnPage(page);
                _pageBehaviorFactory.ApplyPageBehaviors(page);

                // Not Relavent for Shell since we only work with Content Page and not Tabbed or Carousel Pages
                //ConfigurePages(page, segment);

                return page;
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine(e);
                System.Diagnostics.Debugger.Break();
#endif
                throw;
            }
        }

        Task<INavigationResult> INavigationService.GoBackAsync()
        {
            throw new NotImplementedException();
        }

        Task<INavigationResult> INavigationService.GoBackAsync(INavigationParameters parameters)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Initiates navigation to the target specified by the <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The Uri to navigate to</param>
        /// <example>
        /// Navigate(new Uri("MainPage?id=3&amp;name=dan", UriKind.RelativeSource), parameters);
        /// </example>
        /// <returns>The <see cref="INavigationResult" /> which will provide a Success == <c>true</c> if the Navigation was successful.</returns>
        public virtual Task<INavigationResult> NavigateAsync(Uri uri) =>
            NavigateAsync(uri, null);

        /// <summary>
        /// Initiates navigation to the target specified by the <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The Uri to navigate to</param>
        /// <param name="parameters">The navigation parameters</param>
        /// <remarks>Navigation parameters can be provided in the Uri and by using the <paramref name="parameters"/>.</remarks>
        /// <example>
        /// Navigate(new Uri("MainPage?id=3&amp;name=dan", UriKind.RelativeSource), parameters);
        /// </example>
        /// <returns>The <see cref="INavigationResult" /> which will provide a Success == <c>true</c> if the Navigation was successful.</returns>
        public virtual async Task<INavigationResult> NavigateAsync(Uri uri, INavigationParameters parameters)
        {
            try
            {
                WireUpTemplates();
                _currentParameters = parameters ?? new NavigationParameters();
                var navigationSegments = UriParsingHelper.GetUriSegments(uri);
                _routeSegments = UriParsingHelper.GetUriSegments(uri);
                var navUri = string.Join("/", 
                    navigationSegments
                        .Select(x=> UriParsingHelper.GetSegmentName(x))
                        .ToArray()
                    );

                // just see if the first route is somewhere 
                // in the shell
                foreach (var ns in navigationSegments.Take(1))
                {
                    string segName = UriParsingHelper.GetSegmentName(ns);
                    foreach (var item in AllTheKingsItems())
                    {
                        if (Routing.GetRoute(item) == segName)
                        {
                            navigationSegments.Dequeue();
                            break;
                        }
                    }
                }

                foreach (var ns in navigationSegments.ToList())
                {
                    Routing.RegisterRoute(UriParsingHelper.GetSegmentName(ns), new ShellPrismTypeFactory(_container, ns));
                }

                var pantalooons = UriParsingHelper.GetUriSegments(uri).ToList();

                // this means a shell item matched
                if (navigationSegments.Count != pantalooons.Count)
                    navUri = $"//{navUri}";

                await CurrentShell.GoToAsync(navUri);
                return new NavigationResult()
                {
                    Success = true
                };
            }
            catch (NavigationException navEx)
            {
                return new NavigationResult
                {
                    Success = false,
                    Exception = navEx
                };
            }
            catch (Exception ex)
            {
                return new NavigationResult
                {
                    Success = false,
                    Exception = new NavigationException(NavigationException.UnknownException, null, ex)
                };
            }
            finally
            {
                _currentParameters = null;
                _routeSegments = null;
                _segmentParameters = null;
            }
        }

        /// <summary>
        /// Initiates navigation to the target specified by the <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the target to navigate to.</param>
        /// <returns>The <see cref="INavigationResult" /> which will provide a Success == <c>true</c> if the Navigation was successful.</returns>
        public virtual Task<INavigationResult> NavigateAsync(string name) =>
            NavigateAsync(name, null);

        /// <summary>
        /// Initiates navigation to the target specified by the <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the target to navigate to.</param>
        /// <param name="parameters">The navigation parameters</param>
        /// <returns>The <see cref="INavigationResult" /> which will provide a Success == <c>true</c> if the Navigation was successful.</returns>
        public virtual Task<INavigationResult> NavigateAsync(string name, INavigationParameters parameters) =>
            NavigateAsync(UriParsingHelper.Parse(name), parameters);
    }
}
