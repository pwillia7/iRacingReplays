using System;
using System.Windows.Input;

namespace iRacingReplayDirector
{
	public class GenerateCameraPlanCommand : ICommand
	{
		public ReplayDirectorVM ReplayDirectorVM { get; set; }

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public GenerateCameraPlanCommand(ReplayDirectorVM vm)
		{
			ReplayDirectorVM = vm;
		}

		public bool CanExecute(object parameter)
		{
			if (ReplayDirectorVM.AIDirector == null)
				return false;

			if (ReplayDirectorVM.AIDirector.IsBusy)
				return false;

			if (!ReplayDirectorVM.AIDirector.HasScanResult)
				return false;

			return true;
		}

		public async void Execute(object parameter)
		{
			try
			{
				await ReplayDirectorVM.AIDirector.GenerateCameraPlanAsync();
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show(
					$"Error generating camera plan: {ex.Message}",
					"Generation Error",
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Error);
			}
		}
	}
}
