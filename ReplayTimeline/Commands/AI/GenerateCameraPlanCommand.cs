using System;
using System.Windows;
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
				var result = MessageBox.Show(
					"Generate AI camera plan?\n\nThis will send race data to the configured LLM provider and may take a moment.",
					"Generate Camera Plan",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;

				// Show generating message
				ReplayDirectorVM.AIDirector.StatusMessage = "Generating camera plan... (this may take 30-60 seconds)";

				var plan = await ReplayDirectorVM.AIDirector.GenerateCameraPlanAsync();

				if (plan != null && plan.CameraActions.Count > 0)
				{
					MessageBox.Show(
						$"Camera plan generated!\n\n{plan.CameraActions.Count} camera switches created.\n\nClick 'Apply Plan' to add them to the timeline.",
						"Plan Generated",
						MessageBoxButton.OK,
						MessageBoxImage.Information);
				}
				else
				{
					MessageBox.Show(
						"Plan generation completed but no camera actions were created.\n\nTry adjusting settings or re-scanning the replay.",
						"No Actions Generated",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Error generating camera plan: {ex.Message}",
					"Generation Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}
	}
}
