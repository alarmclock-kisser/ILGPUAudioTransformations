

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;

namespace ILGPUAudioTransformations
{
	public class GpuKernelHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public GpuMemoryHandling GpuMemoryH;
		private ListBox LogBox;


		private Context Ctx => GpuMemoryH.Ctx;
		private CudaAccelerator? Acc => GpuMemoryH.Acc;
		private CudaDevice? Dev => GpuMemoryH.Dev;

		public bool IsInitialized => Acc != null && Dev != null;
		public int LogInterval => GpuMemoryH.LogInterval;

		private Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers => GpuMemoryH.FloatBuffers;
		private Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> ComplexBuffers => GpuMemoryH.ComplexBuffers;


		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
		public GpuKernelHandling(GpuMemoryHandling gpuMemoryH, ListBox? logBox = null)
		{
			// Set attributes
			this.GpuMemoryH = gpuMemoryH;
			this.LogBox = logBox ?? new ListBox();

			// Register events


		}






		// ----- ----- ----- METHODS ----- ----- ----- //
		// ----- Basic ----- \\
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Kernel>";

			for (int i = 0; i <= layer; i++)
			{
				msg += " - ";
			}

			msg += message;

			if (inner != "")
			{
				msg += "  (" + inner + ")";
			}

			if (update)
			{
				LogBox.Items[LogBox.Items.Count - 1] = msg;
			}
			else
			{
				LogBox.Items.Add(msg);
				LogBox.SelectedIndex = LogBox.Items.Count - 1;
			}
		}

		public void Dispose()
		{
			// Null	all
			return;
		}



		// ----- Kernels ----- \\
		public void Normalize(long firstPointer, float factor)
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				Log("Kernel not initialized", "NormalizeKernel", 1);
				return;
			}

			// Get buffers
			if (!FloatBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<float, Stride1D.Dense>[]? buffers))
			{
				Log("No buffers found", "NormalizeKernel", 1);
				return;
			}

			// Log
			Log("Normalizing buffers", "factor: " + factor, 1);
			Log("");

			try
			{
				// Load kernel once
				var kernel = Acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, float>(NormalizeKernel);

				// For every buffer
				for (int i = 0; i < buffers.Length; i++)
				{
					MemoryBuffer1D<float, Stride1D.Dense> buffer = buffers[i];
					if (buffer == null) continue; // Sicherstellen, dass buffer nicht null ist

					// Call kernel
					kernel((int) buffer.Length, buffer.View, factor);

					// Synchronize accellerator
					Acc.Synchronize();

					// Log
					if (i % LogInterval == 0)
					{
						Log("Normalized buffer " + i + "/" + buffers.Length, "", 2, true);
					}
				}

				// Log
				Log($"Normalized {buffers.Length} buffers", "", 1, true);
			}
			catch (Exception e)
			{
				Log("Error normalizing buffers", e.Message, 1);
			}

			// Update VRAM
			GpuMemoryH.GpuH.UpdateVram();
		}

		private static void NormalizeKernel(Index1D d, ArrayView<float> view, float factor)
		{
			if (d < view.Length)
			{
				view[d] *= factor;
			}
		}

	}
}