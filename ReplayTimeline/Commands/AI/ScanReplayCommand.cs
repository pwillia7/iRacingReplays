using System;
using System.Windows;
using System.Windows.Input;

namespace iRacingReplayDirector
{
	public class ScanReplayCommand : ICommand
	{
		public ReplayDirectorVM ReplayDirectorVM { get; set; }

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public ScanReplayCommand(ReplayDirectorVM vm)
		{
			ReplayDirectorVM = vm;
		}

		public bool CanExecute(object parameter)
		{
			try
			{
				if (!ReplayDirectorVM.IsSessionReady())
					return false;

				if (ReplayDirectorVM.AIDirector == null)
					return false;

				if (ReplayDirectorVM.AIDirector.IsBusy)
					return false;

				if (ReplayDirectorVM.FinalFrame <= 0)
					return false;

				return true;
			}
			catch
			{
				return false;
			}
		}

		public async void Execute(object parameter)
		{
			try
			{
				int startFrame = ReplayDirectorVM.CurrentFrame;
				int endFrame = ReplayDirectorVM.FinalFrame;

				if (endFrame <= startFrame)
				{
					MessageBox.Show(
						"Cannot scan: Invalid frame range. Make sure a replay is loaded.",
						"Scan Error",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				var result = MessageBox.Show(
					$"Scan replay from frame {startFrame} to {endFrame}?\n\nThis will take some time and move through the replay.",
					"Confirm Scan",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;

				var scanResult = await ReplayDirectorVM.AIDirector.ScanReplayAsync(startFrame, endFrame);

				if (scanResult == null)
				{
					// Scan failed or was cancelled - show status message
					string errorMsg = ReplayDirectorVM.AIDirector.StatusMessage;
					if (!string.IsNullOrEmpty(errorMsg) && errorMsg.StartsWith("Scan error:"))
					{
						MessageBox.Show(
							errorMsg,
							"Scan Error",
							MessageBoxButton.OK,
							MessageBoxImage.Error);
					}
					return;
				}

				if (ReplayDirectorVM.AIDirector.HasScanResult)
				{
					MessageBox.Show(
						$"Scan complete!\n\nDetected {ReplayDirectorVM.AIDirector.LastScanResult.Events.Count} events.\n\nYou can now generate a camera plan.",
						"Scan Complete",
						MessageBoxButton.OK,
						MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				try
				{
					ReplayDirectorVM.AIDirector?.ClearResults();
				}
				catch { }

				MessageBox.Show(
					$"Error scanning replay: {ex.Message}",
					"Scan Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}
	}
}
