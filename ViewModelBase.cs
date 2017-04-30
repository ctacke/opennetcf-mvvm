using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenNETCF.MVVM
{
    public abstract class ViewModelBase : IViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
