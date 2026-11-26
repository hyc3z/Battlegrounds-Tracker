using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Captures the Hearthstone client window to a JPEG file on a background thread.
	/// Only captures while Hearthstone is in the foreground and not minimized so unrelated
	/// desktop windows are never captured.
	/// </summary>
	internal static class ScreenCaptureHelper
	{
		private static readonly ImageCodecInfo? JpegCodec =
			ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

		internal static Task<bool> CaptureHearthstoneAsync(string outputPath, long jpegQuality = 85)
		{
			if(!User32.IsHearthstoneInForeground() || User32.GetHearthstoneWindowState() == WindowState.Minimized)
				return Task.FromResult(false);

			if(!BattlegroundsBoardCoordinates.TryGetClientRect(out var rect))
				return Task.FromResult(false);

			return Task.Run(() => CaptureRectToJpeg(rect, outputPath, jpegQuality));
		}

		private static bool CaptureRectToJpeg(Rectangle rect, string outputPath, long jpegQuality)
		{
			try
			{
				var dir = Path.GetDirectoryName(outputPath);
				if(!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);

				using var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
				using(var g = Graphics.FromImage(bitmap))
				{
					g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
				}

				if(JpegCodec != null)
				{
					using var encoderParams = new EncoderParameters(1);
					encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
					bitmap.Save(outputPath, JpegCodec, encoderParams);
				}
				else
				{
					bitmap.Save(outputPath, ImageFormat.Jpeg);
				}

				return true;
			}
			catch(Exception ex)
			{
				Log.Warn($"ScreenCaptureHelper: failed to capture screenshot to {outputPath}: {ex.Message}");
				return false;
			}
		}
	}
}
