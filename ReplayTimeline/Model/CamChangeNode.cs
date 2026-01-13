using iRacingSimulator;


namespace iRacingReplayDirector
{
	public class CamChangeNode : Node
	{
		public override string NodeType { get => "Camera Change"; }

		private Driver _driver;
		public Driver Driver
		{
			get { return _driver; }
			set { _driver = value; UpdateLabel(); OnPropertyChanged("Driver"); }
		}

		private Camera _camera;
		public Camera Camera
		{
			get { return _camera; }
			set { _camera = value; UpdateLabel(); OnPropertyChanged("Camera"); }
		}

		/// <summary>
		/// Whether to use "Most Exciting" mode (iRacing automatically picks the driver)
		/// </summary>
		public bool UseMostExciting => Driver != null && Driver.NumberRaw == -1;

		public CamChangeNode(bool enabled, int frame, Driver driver, Camera camera)
		{
			Enabled = enabled;
			Frame = frame;
			Driver = driver;
			Camera = camera;

			UpdateLabel();
		}

		protected override void UpdateLabel()
		{
			if (Driver == null || Camera == null) return;

			NodeDetails = Driver.TeamName;
			NodeDetailsAdditional = Camera.GroupName;
		}

		public override void ApplyNode()
		{
			bool playbackEnabled = Sim.Instance.Telemetry.ReplayPlaySpeed.Value != 0;

			// If replay is playing back AND node is disabled, skip it...
			if (playbackEnabled && !Enabled)
				return;

			// Switch camera
			if (UseMostExciting)
			{
				// Use raw SDK broadcast for "Most Exciting" mode
				// BroadcastMessageTypes.CamSwitchNum = 1
				// Parameters: carNumber (-1 = most exciting), cameraGroup, cameraNumber (0 = auto)
				// Pack cameraGroup and cameraNumber into second parameter: (group << 16) | camera
				int packedCameraInfo = (Camera.GroupNum << 16) | 0;
				Sim.Instance.Sdk.Sdk.BroadcastMessage(
					iRSDKSharp.BroadcastMessageTypes.CamSwitchNum,
					-1,  // -1 = Most Exciting
					packedCameraInfo);
			}
			else
			{
				// Use standard method for specific driver
				Sim.Instance.Sdk.Camera.SwitchToCar(Driver.NumberRaw, Camera.GroupNum);
			}

			// If playback is disabled, skip to the frame
			if (!playbackEnabled)
				Sim.Instance.Sdk.Replay.SetPosition(Frame);
		}
	}
}
