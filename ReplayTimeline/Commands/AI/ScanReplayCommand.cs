using iRacingReplayDirector.AI.Models;
using iRacingSimulator;
using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
					$"Scan replay from frame {startFrame} to {endFrame}?\n\nThis will take some time and the UI may be unresponsive.",
					"Confirm Scan",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;

				// Run scan synchronously on UI thread (like other commands in this app)
				var scanResult = RunScanSynchronously(startFrame, endFrame);

				if (scanResult == null)
				{
					string errorMsg = ReplayDirectorVM.AIDirector?.StatusMessage ?? "Scan failed";
					if (errorMsg.Contains("error") || errorMsg.Contains("Error"))
					{
						MessageBox.Show(
							errorMsg,
							"Scan Error",
							MessageBoxButton.OK,
							MessageBoxImage.Error);
					}
					return;
				}

				// Play completion chime
				SystemSounds.Asterisk.Play();

				MessageBox.Show(
					$"Scan complete!\n\nDetected {scanResult.Events.Count} events.\n\nYou can now generate a camera plan.",
					"Scan Complete",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				try { ReplayDirectorVM.AIDirector?.ClearResults(); } catch { }

				MessageBox.Show(
					$"Error scanning replay: {ex.Message}",
					"Scan Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

		private ReplayScanResult RunScanSynchronously(int startFrame, int endFrame)
		{
			var aiDirector = ReplayDirectorVM.AIDirector;
			if (aiDirector == null) return null;

			try
			{
				// Set state
				aiDirector.SetScanning();

				var scanResult = new ReplayScanResult
				{
					StartFrame = startFrame,
					EndFrame = endFrame,
					TrackName = "Unknown Track",
					SessionType = "Race"
				};

				// Get track info
				try
				{
					var weekendInfo = Sim.Instance.SessionInfo["WeekendInfo"];
					scanResult.TrackName = weekendInfo?["TrackName"]?.GetValue("Unknown Track") ?? "Unknown Track";

					var sessionNum = Sim.Instance.Telemetry?.SessionNum?.Value ?? 0;
					var sessionInfo = Sim.Instance.SessionInfo["SessionInfo"]?["Sessions"]?["SessionNum", sessionNum];
					scanResult.SessionType = sessionInfo?["SessionType"]?.GetValue("Race") ?? "Race";
				}
				catch { }

				var snapshots = new List<TelemetrySnapshot>();
				int totalFrames = endFrame - startFrame;
				int frameStep = aiDirector.Settings.ScanIntervalFrames;
				int originalFrame = ReplayDirectorVM.CurrentFrame;

				// Scan loop
				for (int frame = startFrame; frame <= endFrame; frame += frameStep)
				{
					try
					{
						// Jump to frame
						Sim.Instance.Sdk.Replay.SetPosition(frame);

						// Let UI update and telemetry refresh
						DoEvents();
						System.Threading.Thread.Sleep(50);
						DoEvents();

						// Capture snapshot
						var snapshot = CaptureSnapshot(frame);
						if (snapshot != null)
						{
							snapshots.Add(snapshot);
						}

						// Update progress
						int progress = (int)(((frame - startFrame) / (float)totalFrames) * 100);
						aiDirector.UpdateProgress(progress, $"Scanning: {progress}% ({frame}/{endFrame})");
					}
					catch
					{
						// Skip this frame on error
						continue;
					}
				}

				scanResult.Snapshots = snapshots;
				scanResult.DurationSeconds = totalFrames / 60.0;

				// Run detectors
				aiDirector.UpdateProgress(100, "Analyzing events...");
				DoEvents();

				aiDirector.RunDetectors(scanResult, snapshots);

				// Return to original position
				try { Sim.Instance.Sdk.Replay.SetPosition(originalFrame); } catch { }

				aiDirector.SetScanComplete(scanResult);
				return scanResult;
			}
			catch (Exception ex)
			{
				aiDirector.SetError($"Scan error: {ex.Message}");
				return null;
			}
		}

		private TelemetrySnapshot CaptureSnapshot(int frame)
		{
			try
			{
				if (ReplayDirectorVM?.Drivers == null || ReplayDirectorVM.Drivers.Count == 0)
					return null;

				var snapshot = new TelemetrySnapshot
				{
					Frame = frame,
					SessionTime = ReplayDirectorVM.SessionTime,
					DriverStates = new List<DriverSnapshot>()
				};

				foreach (var driver in ReplayDirectorVM.Drivers)
				{
					if (driver == null) continue;

					try
					{
						snapshot.DriverStates.Add(new DriverSnapshot
						{
							Id = driver.Id,
							NumberRaw = driver.NumberRaw,
							TeamName = driver.TeamName ?? string.Empty,
							Position = driver.Position,
							Lap = driver.Lap,
							LapDistance = driver.LapDistance,
							TrackSurface = driver.TrackSurface
						});
					}
					catch { }
				}

				return snapshot;
			}
			catch
			{
				return null;
			}
		}

		private void DoEvents()
		{
			try
			{
				Application.Current?.Dispatcher?.Invoke(
					DispatcherPriority.Background,
					new Action(delegate { }));
			}
			catch { }
		}
	}
}
