using iRacingSimulator;


namespace iRacingReplayDirector
{
	public class CaptureMode_Iracing : CaptureModeBase
	{
		public CaptureMode_Iracing() : base()
		{
			Name = "In-Sim Capture";
		}

		public override bool IsAvailable()
		{
			CaptureModeAvailable = Sim.Instance.Sdk.GetTelemetryValue<bool>("VidCapEnabled").Value;;

			CaptureAvailabilityMessage = CaptureModeAvailable ? "" : "Enable In-Sim capture under iRacing's Options (Misc) and restart iRacing.";

			return CaptureModeAvailable;
		}

		public override bool IsReadyToRecord()
		{
			return IsAvailable();
		}

		public override void StartRecording()
		{
			// VideoCapture = 14 in iRacing SDK
			Sim.Instance.Sdk.Sdk.BroadcastMessage((iRSDKSharp.BroadcastMessageTypes)14, 1, 0);
		}

		public override void StopRecording()
		{
			// VideoCapture = 14 in iRacing SDK
			Sim.Instance.Sdk.Sdk.BroadcastMessage((iRSDKSharp.BroadcastMessageTypes)14, 2, 0);
		}
	}
}
