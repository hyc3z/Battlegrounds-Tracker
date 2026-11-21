namespace Hearthstone_Deck_Tracker.HsReplay
{
	/// <summary>
	/// No-op replacement for the upload token history file of the upstream project. Log uploading has been
	/// removed from this fork; the uploader code paths are dormant and this keeps them compiling.
	/// </summary>
	internal static class UploadTokenHistory
	{
		public static void Write(string message) { }
	}
}