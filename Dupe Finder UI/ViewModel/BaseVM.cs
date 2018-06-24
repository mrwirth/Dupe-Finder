using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_UI.ViewModel
{
    public class BaseVM : INotifyPropertyChanged
    {
        #region Data
        public BaseVM Parent { get; }
        public ObservableCollection<BaseVM> Children { get; } = new ObservableCollection<BaseVM>();
        #endregion Data

        #region Constructors
        public BaseVM(BaseVM parent)
        {
            Parent = parent;
        }

        public BaseVM() : this(null) { }
        #endregion Constructors

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyPropertyChanged Members
    }
}
