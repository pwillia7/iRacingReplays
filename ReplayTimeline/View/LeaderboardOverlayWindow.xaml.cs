using iRacingReplayDirector.AI.Director;
using iRacingSdkWrapper.Bitfields;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace iRacingReplayDirector
{
	/// <summary>
	/// Represents a single entry in the leaderboard display
	/// </summary>
	public class LeaderboardEntry
	{
		public string Position { get; set; }
		public string Code { get; set; }
		public string Gap { get; set; }
		public Brush CodeColor { get; set; }
	}

	public partial class LeaderboardOverlayWindow : Window
	{
		private readonly ReplayDirectorVM _viewModel;
		private readonly AIDirectorSettings _settings;
		private int _updateCounter = 0;
		private const int UPDATE_INTERVAL = 15; // Update every 15 frames (~4 times per second at 60fps)

		public LeaderboardOverlayWindow(ReplayDirectorVM viewModel, AIDirectorSettings settings)
		{
			InitializeComponent();
			_viewModel = viewModel;
			_settings = settings;

			// Subscribe to property changes
			_viewModel.PropertyChanged += ViewModel_PropertyChanged;

			// Set initial position
			PositionWindow();

			// Initial update
			UpdateLeaderboard();
		}

		private void PositionWindow()
		{
			var screenWidth = SystemParameters.PrimaryScreenWidth;
			var screenHeight = SystemParameters.PrimaryScreenHeight;

			// Position on left side by default, with some margin
			Left = 20;

			if (_settings.OverlayPosition == "Top")
			{
				Top = _settings.OverlayOffset;
			}
			else
			{
				// For bottom positioning, we need to account for dynamic height
				// Position from top with offset
				Top = _settings.OverlayOffset;
			}
		}

		private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Update on playback state changes or periodically during playback
			if (e.PropertyName == "PlaybackEnabled" || e.PropertyName == "CurrentFrame")
			{
				_updateCounter++;
				if (_updateCounter >= UPDATE_INTERVAL || e.PropertyName == "PlaybackEnabled")
				{
					_updateCounter = 0;
					UpdateLeaderboard();
				}
			}
		}

		private void UpdateLeaderboard()
		{
			if (_viewModel.Drivers == null || !_viewModel.Drivers.Any())
			{
				LeaderboardItems.ItemsSource = null;
				return;
			}

			// Get drivers sorted by position, excluding pace car and NotInWorld
			var sortedDrivers = _viewModel.Drivers
				.Where(d => d != null &&
				            d.Position > 0 &&
				            d.Position < 999 &&
				            d.TrackSurface != TrackSurfaces.NotInWorld)
				.OrderBy(d => d.Position)
				.ToList();

			if (!sortedDrivers.Any())
			{
				LeaderboardItems.ItemsSource = null;
				return;
			}

			var leader = sortedDrivers.FirstOrDefault();
			var entries = new List<LeaderboardEntry>();
			var currentDriver = _viewModel.OverlayDriver;

			foreach (var driver in sortedDrivers)
			{
				var entry = new LeaderboardEntry
				{
					Position = driver.Position.ToString(),
					Code = GetDriverCode(driver.Name),
					Gap = driver == leader ? "" : CalculateGapDisplay(leader, driver),
					CodeColor = driver == currentDriver ?
						new SolidColorBrush(Color.FromRgb(255, 215, 0)) : // Gold for current driver
						new SolidColorBrush(Colors.White)
				};
				entries.Add(entry);
			}

			LeaderboardItems.ItemsSource = entries;
		}

		/// <summary>
		/// Extract 3-letter code from driver name (last name, first 3 chars uppercase)
		/// </summary>
		private string GetDriverCode(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return "---";

			var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			// Use last name if available, otherwise use full name
			var lastName = parts.Length > 1 ? parts[parts.Length - 1] : parts[0];

			// Take first 3 characters, uppercase
			var code = lastName.Length >= 3
				? lastName.Substring(0, 3).ToUpper()
				: lastName.ToUpper().PadRight(3, '-');

			return code;
		}

		/// <summary>
		/// Calculate and format the gap display between leader and a driver
		/// </summary>
		private string CalculateGapDisplay(Driver leader, Driver driver)
		{
			if (leader == null || driver == null)
				return "";

			int lapDiff = leader.Lap - driver.Lap;

			if (lapDiff > 0)
			{
				// Driver is lapped
				return lapDiff == 1 ? "+1 LAP" : $"+{lapDiff} LAPS";
			}

			// Same lap - calculate distance gap as percentage
			float gap = leader.LapDistance - driver.LapDistance;

			// Handle wrap-around (leader just crossed start/finish)
			if (gap < 0)
				gap += 1.0f;

			// Format as percentage with 1 decimal place
			return $"+{gap * 100:F1}%";
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_viewModel.PropertyChanged -= ViewModel_PropertyChanged;
			base.OnClosing(e);
		}
	}
}
