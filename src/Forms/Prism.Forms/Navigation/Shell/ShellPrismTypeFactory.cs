using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Prism.Common;
using Prism.Ioc;
using Prism.Behaviors;
using System.Linq;

namespace Prism.Navigation
{
    public class ShellPrismTypeFactory : Xamarin.Forms.RouteFactory
    {
        IContainerProvider _container;
        string _segmentName;

        public ShellPrismTypeFactory(IContainerProvider containerProvider, string segmentName)
        {
            _container = containerProvider;
            _segmentName = segmentName;
        }

        public override Element GetOrCreate()
        {
            try
            {
                return _container.Resolve<object>(_segmentName) as Page;
            }
            catch (Exception ex)
            {
                if (((IContainerRegistry)_container).IsRegistered<object>(_segmentName))
                    throw new NavigationException(NavigationException.ErrorCreatingPage, null, ex);

                throw new NavigationException(NavigationException.NoPageIsRegistered, null, ex);
            }
        }
    }
}
