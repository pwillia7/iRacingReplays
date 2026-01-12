using iRacingReplayDirector.AI.Models;
using System;
using System.Threading.Tasks;
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

		public void Execute(object parameter)
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

				// Run the scan on a background thread to avoid blocking UI
				Task.Run(async () =>
				{
					try
					{
						ReplayScanResult scanResult = await ReplayDirectorVM.AIDirector.ScanReplayAsync(startFrame, endFrame);

						// Show result on UI thread
						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							try
							{
								if (scanResult == null)
								{
									string errorMsg = ReplayDirectorVM.AIDirector.StatusMessage;
									if (!string.IsNullOrEmpty(errorMsg) && errorMsg.Contains("error"))
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
							catch { }
						});
					}
					catch (Exception ex)
					{
						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							try
							{
								ReplayDirectorVM.AIDirector?.ClearResults();
							}
							catch { }

							try
							{
								MessageBox.Show(
									$"Error scanning replay: {ex.Message}",
									"Scan Error",
									MessageBoxButton.OK,
									MessageBoxImage.Error);
							}
							catch { }
						});
					}
				});
			}
			catch (Exception ex)
			{
				try
				{
					ReplayDirectorVM.AIDirector?.ClearResults();
				}
				catch { }

				try
				{
					MessageBox.Show(
						$"Error starting scan: {ex.Message}",
						"Scan Error",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				catch { }
			}
		}
	}
}
