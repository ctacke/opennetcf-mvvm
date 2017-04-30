using System;

namespace OpenNETCF.MVVM
{
    public class ViewTypeNotRegisteredException : Exception
    {
        public ViewTypeNotRegisteredException(Type viewType)
            : base(string.Format("View type '{0}' not registered", viewType.Name))
        {
        }
    }
}