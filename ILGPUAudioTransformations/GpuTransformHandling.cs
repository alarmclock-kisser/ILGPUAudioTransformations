using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;

namespace ILGPUAudioTransformations
{
	public class GpuTransformHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public GpuMemoryHandling GpuMemoryH;

		private ListBox LogBox;



		// ----- ----- ----- LAMBDA ----- ----- ----- //
		public Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers => GpuMemoryH.FloatBuffers;
		public Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> ComplexBuffers => GpuMemoryH.ComplexBuffers;

		public Context Ctx => GpuMemoryH.Ctx;
		public CudaAccelerator? Acc => GpuMemoryH.Acc;
		public CudaDevice? Dev => GpuMemoryH.Dev;

		public int LogInterval => GpuMemoryH.LogInterval;


		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
		public GpuTransformHandling(GpuMemoryHandling gpuMemoryH, ListBox? logBox = null)
		{
			// Set attributes
			this.GpuMemoryH = gpuMemoryH;
			this.LogBox = logBox ?? new ListBox();
		}





		// ----- ----- ----- METHODS ----- ----- ----- //
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Transform>";

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
			// Dispose all
			return;
		}

		public long PerformFFT(long pointer, bool silent = false)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				Log("Cannot perform FFT: GPU not initialized", "PerformFFT", 1);
				return 0;
			}

			// Get buffers
			if (!FloatBuffers.TryGetValue(pointer, out MemoryBuffer1D<float, Stride1D.Dense>[]? buffers))
			{
				Log("Cannot perform FFT: Pointer not found", "PerformFFT", 1);
				return pointer;
			}

			// Create complex buffers array
			var complexBuffers = new MemoryBuffer1D<Float2, Stride1D.Dense>[buffers.Length];

			// Get cufft api
			var cufft = new CuFFT();

			// Log
			Log("Performing FFT", "buffers: " + buffers.Length, 1, true);
			Log("");

			// Try to perform FFT
			try
			{
				// On each buffer
				for (int i = 0; i < buffers.Length; i++)
				{
					// Get buffer
					MemoryBuffer1D<float, Stride1D.Dense>? buffer = buffers[i];

					// Create complex buffer
					var complexBuffer = Acc.Allocate1D<Float2>(buffer.Length);

					// Create plan
					CuFFTResult result = cufft.Plan1D(out CuFFTPlan? plan, (int) buffer.Length, CuFFTType.CUFFT_R2C, 1);
					if (result != CuFFTResult.CUFFT_SUCCESS || plan == null)
					{
						Log($"Failed to create FFT plan: {result}", "PerformFFT", 1);
						return 0;
					}

					// Execute plan
					plan.ExecR2C(buffer.View, complexBuffer.View);

					// Store complex buffer
					complexBuffers[i] = complexBuffer;

					// Dispose plan
					plan.Dispose();

					// Log
					if (i % LogInterval == 0)
					{
						Log("Performed FFT", "buffer: " + i + " / " + buffers.Length, 2, true);
					}
				}

				// Remove float buffers pointer
				GpuMemoryH.RemoveFloatPointerGroup(pointer);

				// Get first pointer
				long firstPointer = complexBuffers[0].NativePtr.ToInt64();

				// Store complex buffers
				ComplexBuffers.Add(firstPointer, complexBuffers);

				return firstPointer;
			}
			catch (Exception e)
			{
				Log("Failed to perform FFT: " + e.Message, "PerformFFT", 1);
				return 0;
			}
		}

		public long PerformIFFT(long pointer)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				Log("Cannot perform FFT: GPU not initialized", "PerformIFFT", 1);
				return 0;
			}

			// Get buffers
			if (!ComplexBuffers.TryGetValue(pointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? buffers))
			{
				Log("Cannot perform FFT: Pointer not found", "PerformIFFT", 1);
				return pointer;
			}

			// Create float buffers array
			var floatBuffers = new MemoryBuffer1D<float, Stride1D.Dense>[buffers.Length];

			// Get cufft api
			var cufft = new CuFFT();

			// Log
			Log("Performing IFFT", "buffers: " + buffers.Length, 1, true);
			Log("");

			// Try to perform FFT
			try
			{
				// On each buffer
				for (int i = 0; i < buffers.Length; i++)
				{
					// Get buffer
					MemoryBuffer1D<Float2, Stride1D.Dense>? buffer = buffers[i];

					// Create float buffer
					var floatBuffer = Acc.Allocate1D<float>(buffer.Length);

					// Create plan
					CuFFTResult result = cufft.Plan1D(out CuFFTPlan? plan, (int) buffer.Length, CuFFTType.CUFFT_C2R, 1);
					if (result != CuFFTResult.CUFFT_SUCCESS || plan == null)
					{
						Log($"Failed to create FFT plan: {result}", "PerformFFT", 1);
						return 0;
					}

					// Execute plan
					plan.ExecC2R(buffer.View, floatBuffer.View);

					// Store complex buffer
					floatBuffers[i] = floatBuffer;

					// Dispose plan
					plan.Dispose();

					// Log
					if (i % LogInterval == 0)
					{
						Log("Performed IFFT", "buffer: " + i + " / " + buffers.Length, 2, true);
					}
				}

				// Remove float buffers pointer
				GpuMemoryH.RemoveComplexPointerGroup(pointer);

				// Get first pointer
				long firstPointer = floatBuffers[0].NativePtr.ToInt64();

				// Store complex buffers
				FloatBuffers.Add(firstPointer, floatBuffers);

				return firstPointer;
			}
			catch (Exception e)
			{
				Log("Failed to perform FFT: " + e.Message, "PerformFFT", 1);
				return 0;
			}
		}

		public long PerformFFTW(long pointer)
		{
			return 0;
		}

		public long PerformIFFTW(long pointer)
		{
			return 0;
		}
	}
}