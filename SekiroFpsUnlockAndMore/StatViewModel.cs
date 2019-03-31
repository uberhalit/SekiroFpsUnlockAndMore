using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SekiroFpsUnlockAndMore
{
	/// <summary>
	/// For Status bar display
	/// </summary>
	class StatViewModel : INotifyPropertyChanged
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
