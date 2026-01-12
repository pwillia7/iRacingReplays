using System;
using System.Windows.Input;

namespace iRacingReplayDirector
{
	public class ClearAIResultsCommand : ICommand
	{
		public ReplayDirectorVM ReplayDirectorVM { get; set; }

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public ClearAIResultsCommand(ReplayDirectorVM vm)
		{
			ReplayDirectorVM = vm;
		}

		public bool CanExecute(object parameter)
		{
			if (ReplayDirectorVM.AIDirector == null)
				return false;

			return ReplayDirectorVM.AIDirector.HasScanResult || ReplayDirectorVM.AIDirector.HasGeneratedPlan;
		}

		public void Execute(object parameter)
		{
			ReplayDirectorVM.AIDirector.ClearResults();
		}
	}
}
