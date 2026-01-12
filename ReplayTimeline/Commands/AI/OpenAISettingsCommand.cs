using System;
using System.Windows.Input;

namespace iRacingReplayDirector
{
	public class OpenAISettingsCommand : ICommand
	{
		public ReplayDirectorVM ReplayDirectorVM { get; set; }

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public OpenAISettingsCommand(ReplayDirectorVM vm)
		{
			ReplayDirectorVM = vm;
		}

		public bool CanExecute(object parameter)
		{
			return ReplayDirectorVM.AIDirector != null;
		}

		public void Execute(object parameter)
		{
			var settingsWindow = new AIDirectorSettingsWindow(ReplayDirectorVM.AIDirector);
			settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
			settingsWindow.ShowDialog();
		}
	}
}
