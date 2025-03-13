using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;
using System.Diagnostics;
using System.Numerics;
using System;

namespace ILGPUAudioTransformations
{
	public class GpuTransformHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public GpuMemoryHandling GpuMemoryH;

		private ListBox LogBox;



		// ----- ----- ----- LAMBDA ----- ----- ----- //
		public Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers => GpuMemoryH.FloatBuffers;
		public Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> Float2Buffers => GpuMemoryH.Float2Buffers;
		public Dictionary<long, MemoryBuffer1D<Complex, Stride1D.Dense>[]> ComplexBuffers => GpuMemoryH.ComplexBuffers;
		public Dictionary<long, MemoryBuffer1D<double, Stride1D.Dense>[]> DoubleBuffers => GpuMemoryH.DoubleBuffers;


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
		// ----- Logging ----- \\
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Transform>";

			for (int i = 0; i <= layer; i++)
			{
				msg += " - ";
			}

			msg += message;

			if (!string.IsNullOrEmpty(inner))
			{
				msg += "  (" + inner + ")";
			}

			// Sicherstellen, dass wir auf dem UI-Thread sind
			if (LogBox.InvokeRequired)
			{
				LogBox.BeginInvoke(new Action(() => UpdateLogBox(msg, update)));
			}
			else
			{
				UpdateLogBox(msg, update);
			}
		}

		private void UpdateLogBox(string msg, bool update)
		{
			if (update && LogBox.Items.Count > 0)
			{
				LogBox.Items[LogBox.Items.Count - 1] = msg;
			}
			else
			{
				LogBox.Items.Add(msg);
				LogBox.SelectedIndex = LogBox.Items.Count - 1;
			}
		}


		// ----- Basic ----- \\
		public void Dispose()
		{
			// Dispose all
			return;
		}


		// ----- (I)FFT ----- \\

		public async Task<long> PerformFFTAsync(long pointer, bool silent = false)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				Log("Cannot perform FFT: GPU not initialized", "PerformFFT");
				return 0;
			}

			// Abort if pointer not found
			if (!FloatBuffers.TryGetValue(pointer, out var buffers))
			{
				Log("Cannot perform FFT: Pointer not found", "PerformFFT");
				return pointer;
			}

			// Create complex buffers
			var complexBuffers = new MemoryBuffer1D<Float2, Stride1D.Dense>[buffers.Length];
			var cufft = new CuFFT();
			int total = buffers.Length;
			int processed = 0;

			// Log & stopwatch
			if (!silent)
			{
				Log($"Performing FFT on {buffers.Length} buffers", "PerformFFT");
				Log("");
			}
			Stopwatch sw = Stopwatch.StartNew();

			await Task.Run(() => Parallel.For(0, total, i =>
			{
				try
				{
					// Get buffers
					var buffer = buffers[i];
					var complexBuffer = Acc.Allocate1D<Float2>(buffer.Length);

					// Create FFT plan
					var result = cufft.Plan1D(out var plan, (int) buffer.Length, CuFFTType.CUFFT_R2C, 1);
					if (result != CuFFTResult.CUFFT_SUCCESS || plan == null)
					{
						if (!silent)
						{
							Log($"Failed to create FFT plan: {result}", "PerformFFT");
						}
						return;
					}

					// Execute FFT
					plan.ExecR2C(buffer.View, complexBuffer.View);
					complexBuffers[i] = complexBuffer;
					plan.Dispose();

					// Increment progress counter
					int done = Interlocked.Increment(ref processed);

					// Log if not silenzt & at log interval
					if (done % LogInterval == 0 && !silent)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformFFT", 2, true);
					}
				}
				catch (Exception e)
				{
					if (!silent)
					{
						Log($"Error in FFT: {e.Message}", e.InnerException?.Message ?? "", 1);
					}
				}
			}));

			// Stop stopwatch & log
			sw.Stop();
			if (!silent)
			{
				Log("Performed FFT on " + complexBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1, true);
			}

			// Remove float pointer group & add complex pointer group
			GpuMemoryH.RemoveFloatPointerGroup(pointer);
			long firstPointer = complexBuffers[0].NativePtr.ToInt64();
			Float2Buffers[firstPointer] = complexBuffers;

			return firstPointer;
		}

		public async Task<long> PerformIFFTAsync(long pointer, bool silent = false)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				if (!silent)
				{
					Log("Cannot perform IFFT: GPU not initialized", "PerformIFFT");
				}
				return 0;
			}

			// Abort if pointer not found
			if (!Float2Buffers.TryGetValue(pointer, out var buffers))
			{
				if (!silent)
				{
					Log("Cannot perform IFFT: Pointer not found", "PerformIFFT");
				}
				return pointer;
			}

			// Create float buffers
			var floatBuffers = new MemoryBuffer1D<float, Stride1D.Dense>[buffers.Length];
			var cufft = new CuFFT();
			int total = buffers.Length;
			int processed = 0;

			// Log & stopwatch
			if (!silent)
			{
				Log($"Performing IFFT on {buffers.Length} buffers", "PerformIFFT");
				Log("");
			}
			Stopwatch sw = Stopwatch.StartNew();

			await Task.Run(() => Parallel.For(0, total, i =>
			{
				try
				{
					// Get buffers
					var buffer = buffers[i];
					var floatBuffer = Acc.Allocate1D<float>(buffer.Length);

					// Create IFFT plan
					var result = cufft.Plan1D(out var plan, (int) buffer.Length, CuFFTType.CUFFT_C2R, 1);
					if (result != CuFFTResult.CUFFT_SUCCESS || plan == null)
					{
						Log($"Failed to create IFFT plan: {result}", "PerformIFFT");
						return;
					}

					// Execute IFFT & dispose plan afterwards
					plan.ExecC2R(buffer.View, floatBuffer.View);
					floatBuffers[i] = floatBuffer;
					plan.Dispose();

					// Increment progress counter
					int done = Interlocked.Increment(ref processed);

					// Only log if not silent and at log interval
					if (done % LogInterval == 0 && !silent)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformIFFT", 2, true);
					}
				}
				catch (Exception e)
				{
					Log($"Error in IFFT: {e.Message}", e.InnerException?.Message ?? "", 1);
				}
			}));

			// Stop stopwatch & log
			sw.Stop();
			if (!silent)
			{
				Log("Performed IFFT on " + floatBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1, true);
			}

			// Remove complex pointer group & add float pointer group
			GpuMemoryH.RemoveFloat2PointerGroup(pointer);
			long firstPointer = floatBuffers[0].NativePtr.ToInt64();
			FloatBuffers[firstPointer] = floatBuffers;

			return firstPointer;
		}


		// ----- (I)FFTW ASYNC ----- \\

		public async Task<long> PerformFFTWAsync(long pointer, bool silent = false)
		{
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				if (!silent) Log("Cannot perform FFTW: GPU not initialized", "PerformFFTW");
				return 0;
			}

			if (!FloatBuffers.TryGetValue(pointer, out var buffers))
			{
				if (!silent) Log("Cannot perform FFTW: Pointer not found", "PerformFFTW");
				return pointer;
			}

			if ((buffers[0].Length & (buffers[0].Length - 1)) != 0)
			{
				Log($"FFT Length {buffers[0].Length} is not a power of 2!", "PerformFFTW");
				return pointer;
			}

			if (!silent) Log($"Performing FFTW on {buffers.Length} buffers", "PerformFFTW");
			Stopwatch sw = Stopwatch.StartNew();

			var complexBuffers = new MemoryBuffer1D<Float2, Stride1D.Dense>[buffers.Length];
			var cufft = new CuFFTW();
			int total = buffers.Length;
			int processed = 0;

			await Task.Run(() => Parallel.For(0, total, i =>
			{
				try
				{
					var buffer = buffers[i];
					var float2Buffer = Acc.Allocate1D<Float2>(buffer.Length);

					var bufferSpan = new Span<float>(buffer.AsArrayView<float>(0, buffer.Length).GetAsArray());
					var float2bufferSpan = new Span<Float2>(float2Buffer.AsArrayView<Float2>(0, float2Buffer.Length).GetAsArray());

					var plan = cufft.Plan1D_R2C((int) buffer.Length, bufferSpan, float2bufferSpan, 0);
					if (plan == null)
					{
						if (!silent)
						{
							Log("Failed to create FFTW plan", "PerformFFTW");
						}
						return;
					}

					Acc.Synchronize();
					plan.Execute_R2C(bufferSpan, float2bufferSpan);
					complexBuffers[i] = float2Buffer;
					plan.Dispose();
					Acc.Synchronize();

					int done = Interlocked.Increment(ref processed);
					if (!silent && done % LogInterval == 0)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformFFTW", 2, true);
					}
				}
				catch (Exception e)
				{
					Log($"Error in FFTW: {e.Message}", e.InnerException?.Message ?? "", 1);
				}
			}));

			sw.Stop();
			if (!silent) Log("Performed FFTW on " + complexBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1);

			GpuMemoryH.RemoveFloatPointerGroup(pointer);
			long firstPointer = complexBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;
			if (firstPointer != 0) Float2Buffers[firstPointer] = complexBuffers;

			return firstPointer;
		}

		public async Task<long> PerformIFFTWAsync(long pointer, bool silent = false)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				if (!silent)
				{
					Log("Cannot perform IFFTW: GPU not initialized", "PerformIFFTW");
				}
				return 0;
			}

			// Abort if pointer not found
			if (!Float2Buffers.TryGetValue(pointer, out var buffers))
			{
				if (!silent)
				{
					Log("Cannot perform IFFTW: Pointer not found", "PerformIFFTW");
				}
				return pointer;
			}

			// Log & stopwatch
			if (!silent)
			{
				Log($"Performing IFFTW on {buffers.Length} buffers", "PerformIFFTW");
			}
			Stopwatch sw = Stopwatch.StartNew();

			// Create float buffers
			var floatBuffers = new MemoryBuffer1D<float, Stride1D.Dense>[buffers.Length];

			// Create FFTW API
			var cufft = new CuFFTW();
			int total = buffers.Length; // Declare total outside Parallel.For
			int processed = 0; // Initialize processed counter

			await Task.Run(() => Parallel.For(0, total, i =>
			{
				try
				{
					// Get buffers
					var buffer = buffers[i];
					var floatBuffer = Acc.Allocate1D<float>(buffer.Length);

					// Get spans
					var bufferSpan = new Span<Float2>(buffer.AsArrayView<Float2>(0, buffer.Length).GetAsArray());
					var floatBufferSpan = new Span<float>(floatBuffer.AsArrayView<float>(0, floatBuffer.Length).GetAsArray());

					// Create FFTW plan
					var plan = cufft.Plan1D_C2R((int) buffer.Length, bufferSpan, floatBufferSpan, 0);
					if (plan == null)
					{
						if (!silent)
						{
							Log("Failed to create IFFTW plan", "PerformIFFTW");
						}
						return;
					}

					// Synchronize
					Acc.Synchronize();

					// Execute FFTW
					plan.Execute_C2R(bufferSpan, floatBufferSpan);

					// Add to float buffers
					floatBuffers[i] = floatBuffer;

					// Dispose plan
					plan.Dispose();

					// Sync
					Acc.Synchronize();

					// Log if not silent & at log interval
					int done = Interlocked.Increment(ref processed); // Increment processed counter
					if (!silent && done % LogInterval == 0)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformIFFTW", 2, true);
					}
				}
				catch (Exception e)
				{
					// Log
					Log("Error in IFFTW: " + e.Message, e.InnerException?.Message ?? "", 1);
				}
			}));

			// Stop stopwatch & log
			sw.Stop();
			if (!silent)
			{
				Log("Performed IFFTW on " + floatBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1);
			}

			// Remove complex pointer group & add float pointer group
			GpuMemoryH.RemoveFloat2PointerGroup(pointer);
			long firstPointer = floatBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;

			// Add to dictionary if not 0
			if (firstPointer != 0)
			{
				FloatBuffers[firstPointer] = floatBuffers;
			}

			return firstPointer;
		}


		// ----- (I)FFTW) ----- \\

		public long PerformFFTW(long pointer)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				Log("Cannot perform FFTW: GPU not initialized", "PerformFFTW");
				return 0;
			}
			
			// Abort if pointer not found
			if (!FloatBuffers.TryGetValue(pointer, out var buffers))
			{
				Log("Cannot perform FFTW: Pointer not found", "PerformFFTW");
				return pointer;
			}
			
			// Log & stopwatch
			Log($"Performing FFTW on {buffers.Length} buffers", "PerformFFTW");
			Stopwatch sw = Stopwatch.StartNew();
			
			// Create complex buffers
			var doublePtr = GpuMemoryH.ConvertFloatToDouble(pointer);

			// Abort if no pointer found
			if (!DoubleBuffers.TryGetValue(doublePtr, out var doubleBuffers))
			{
				Log("Cannot perform FFTW: Double pointer not found", "PerformFFTW");
				return pointer;
			}

			// Get api & declare counters
			var cufft = new CuFFTW();
			int total = doubleBuffers.Length;
			int processed = 0;
			var complexBuffers = new MemoryBuffer1D<Complex, Stride1D.Dense>[total];

			// Loop through buffers
			for (int i = 0; i < total; i++)
			{
				try
				{
					// Get buffers
					var doubleBuffer = doubleBuffers[i];
					var complexBuffer = Acc.Allocate1D<Complex>(doubleBuffer.Length);

					// Get spans
					var doubleBufferSpan = new Span<double>(doubleBuffer.AsArrayView<double>(0, doubleBuffer.Length).GetAsArray());
					var complexBufferSpan = new Span<Complex>(complexBuffer.AsArrayView<Complex>(0, complexBuffer.Length).GetAsArray());

					// Create FFTW plan
					var plan = cufft.Plan1D_R2C((int) doubleBuffer.Length, doubleBufferSpan, complexBufferSpan, 0);
					if (plan == null)
					{
						Log("Failed to create FFTW plan", "PerformFFTW");
						continue;
					}
					
					// Execute FFTW
					plan.Execute_R2C(doubleBufferSpan, complexBufferSpan);
					complexBuffers[i] = complexBuffer;
					plan.Dispose();
					
					// Increment progress counter
					int done = Interlocked.Increment(ref processed);
					
					// Log if at log interval
					if (done % LogInterval == 0)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformFFTW", 2, true);
					}
				}
				catch (Exception e)
				{
					Log($"Error in FFTW: {e.Message}", e.InnerException?.Message ?? "", 1);
				}
			}
			
			// Stop stopwatch & log
			sw.Stop();
			Log("Performed FFTW on " + complexBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1);

			// Remove float pointer group & add complex pointer group
			GpuMemoryH.RemoveFloatPointerGroup(pointer);
			long firstPointer = complexBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;
			if (firstPointer != 0)
			{
				ComplexBuffers[firstPointer] = complexBuffers;
			}
			
			return firstPointer;
		}

		public long PerformIFFTW(long pointer)
		{
			// Abort if not initialized
			if (!GpuMemoryH.IsInitialized || Acc == null || Dev == null)
			{
				Log("Cannot perform IFFTW: GPU not initialized", "PerformFFTW");
				return 0;
			}

			// Abort if pointer not found
			if (!FloatBuffers.TryGetValue(pointer, out var buffers))
			{
				Log("Cannot perform IFFTW: Pointer not found", "PerformFFTW");
				return pointer;
			}

			// Log & stopwatch
			Log($"Performing FFTW on {buffers.Length} buffers", "PerformFFTW");
			Stopwatch sw = Stopwatch.StartNew();

			// Create complex buffers
			var complexPtr = GpuMemoryH.ConvertFloatToDouble(pointer);

			// Abort if no pointer found
			if (!ComplexBuffers.TryGetValue(complexPtr, out var complexBuffers))
			{
				Log("Cannot perform IFFTW: Double pointer not found", "PerformFFTW");
				return pointer;
			}

			// Get api & declare counters
			var cufft = new CuFFTW();
			int total = complexBuffers.Length;
			int processed = 0;
			var doubleBuffers = new MemoryBuffer1D<double, Stride1D.Dense>[total];

			// Loop through buffers
			for (int i = 0; i < total; i++)
			{
				try
				{
					// Get buffers
					var complexBuffer = complexBuffers[i];
					var doubleBuffer = Acc.Allocate1D<double>(complexBuffer.Length);

					// Get spans
					var complexBufferSpan = new Span<Complex>(complexBuffer.AsArrayView<Complex>(0, complexBuffer.Length).GetAsArray());
					var doubleBufferSpan = new Span<double>(doubleBuffer.AsArrayView<double>(0, doubleBuffer.Length).GetAsArray());

					// Create IFFTW plan
					var plan = cufft.Plan1D_C2R((int) doubleBuffer.Length, complexBufferSpan, doubleBufferSpan, 0);
					if (plan == null)
					{
						Log("Failed to create FFTW plan", "PerformFFTW");
						continue;
					}

					// Execute IFFTW
					plan.Execute_C2R(complexBufferSpan, doubleBufferSpan);
					doubleBuffers[i] = doubleBuffer;
					plan.Dispose();

					// Increment progress counter
					int done = Interlocked.Increment(ref processed);

					// Log if at log interval
					if (done % LogInterval == 0)
					{
						Log($"Progress: {done} / {total} ({(done * 100) / total}%)", "PerformFFTW", 2, true);
					}
				}
				catch (Exception e)
				{
					Log($"Error in FFTW: {e.Message}", e.InnerException?.Message ?? "", 1);
				}
			}

			// Stop stopwatch & log
			sw.Stop();
			Log("Performed FFTW on " + doubleBuffers.Length + " buffers", "time: " + sw.ElapsedMilliseconds + "ms", 1);

			// Remove float pointer group & add complex pointer group
			GpuMemoryH.RemoveFloatPointerGroup(pointer);
			long firstPointer = doubleBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;
			if (firstPointer != 0)
			{
				DoubleBuffers[firstPointer] = doubleBuffers;
			}

			return firstPointer;
		}
	}
}