using System;
using System.Windows.Input;

namespace iRacingReplayDirector
{
	public class ApplyAIPlanCommand : ICommand
	{
		public ReplayDirectorVM ReplayDirectorVM { get; set; }

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public ApplyAIPlanCommand(ReplayDirectorVM vm)
		{
			ReplayDirectorVM = vm;
		}

		public bool CanExecute(object parameter)
		{
			if (ReplayDirectorVM.AIDirector == null)
				return false;

			if (ReplayDirectorVM.AIDirector.IsBusy)
				return false;

			if (!ReplayDirectorVM.AIDirector.HasGeneratedPlan)
				return false;

			return true;
		}

		public void Execute(object parameter)
		{
			try
			{
				var result = System.Windows.MessageBox.Show(
					"This will clear existing nodes and apply the AI-generated camera plan. Continue?",
					"Apply AI Plan",
					System.Windows.MessageBoxButton.YesNo,
					System.Windows.MessageBoxImage.Question);

				if (result == System.Windows.MessageBoxResult.Yes)
				{
					int nodesCreated = ReplayDirectorVM.AIDirector.ApplyPlanToNodeCollection(clearExisting: true);

					System.Windows.MessageBox.Show(
						$"Successfully created {nodesCreated} camera nodes.",
						"Plan Applied",
						System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show(
					$"Error applying plan: {ex.Message}",
					"Apply Error",
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Error);
			}
		}
	}
}
