using System.ComponentModel;

namespace SekiroFpsUnlockAndMore
{
    /// <summary>
    /// For Status bar display
    /// </summary>
    class StatusViewModel : INotifyPropertyChanged
    {
        private int _deaths = 0;
        public int Deaths
        {
            get { return _deaths; }
            set
            {
                _deaths = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Deaths"));
            }
        }

        private int _kills = 0;
        public int Kills
        {
            get { return _kills; }
            set
            {
                _kills = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Kills"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
