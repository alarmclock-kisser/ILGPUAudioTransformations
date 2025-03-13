using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;
using System.Diagnostics;
using System.Numerics;

namespace ILGPUAudioTransformations
{
	public class GpuMemoryHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public GpuHandling GpuH;

		private ListBox LogBox;


		public Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers = [];
		public Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> Float2Buffers = [];
		public Dictionary<long, MemoryBuffer1D<Complex, Stride1D.Dense>[]> ComplexBuffers = [];
		public Dictionary<long, MemoryBuffer1D<double, Stride1D.Dense>[]> DoubleBuffers = [];


		// ----- ----- ----- LAMBDA FUNCTIONS ----- ----- ----- //
		public Context Ctx => GpuH.Ctx;
		public CudaAccelerator? Acc => GpuH.Acc;
		public CudaDevice? Dev => GpuH.Dev;

		public bool IsInitialized => Acc != null && Dev != null;

		public int LogInterval => GpuH.LogInterval;


		private readonly SemaphoreSlim _semaphorePush = new(1, 1);
		private readonly SemaphoreSlim _semaphorePull = new(1, 1);


		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
		public GpuMemoryHandling(GpuHandling gpuHandling, ListBox? logBox = null)
		{
			// Set attributes
			this.GpuH = gpuHandling;
			this.LogBox = logBox ?? new ListBox();
		}






		// ----- ----- ----- METHODS ----- ----- ----- //
		// ----- Logging ----- \\
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Memory>";

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
			// Dispose all buffers
			foreach (var buffer in FloatBuffers.Values.SelectMany(b => b)) buffer.Dispose();
			foreach (var buffer in Float2Buffers.Values.SelectMany(b => b)) buffer.Dispose();

			FloatBuffers.Clear();
			Float2Buffers.Clear();
		}


		// ----- Chunk memory ----- \\

		public async Task<long> PushChunksAsync(List<float[]> chunks, bool silent = false)
		{
			if (!IsInitialized || chunks.Count == 0 || Acc == null || Dev == null)
			{
				if (!silent) Log("Cannot push chunks: Not initialized or no chunks provided", "", 1);
				return 0;
			}

			Stopwatch sw = Stopwatch.StartNew();
			await _semaphorePush.WaitAsync();
			try
			{
				var floatBuffers = new MemoryBuffer1D<float, Stride1D.Dense>[chunks.Count];

				Log("Pushing chunks", $"count: {chunks.Count}", 1);
				Log("");

				await Task.Run(() =>
				{
					for (int i = 0; i < chunks.Count; i++)
					{
						floatBuffers[i] = Acc.Allocate1D<float>(chunks[i].Length);
						floatBuffers[i].CopyFromCPU(chunks[i]);

						if (!silent && i % LogInterval == 0)
						{
							Log("Pushed chunk", $"index: {i} / {chunks.Count}", 2, true);
						}
					}
				});

				long pointer = floatBuffers[0].NativePtr.ToInt64();
				FloatBuffers.TryAdd(pointer, floatBuffers);

				sw.Stop();
				long delta = sw.ElapsedMilliseconds;

				if (!silent)
				{
					Log("Pushed " + chunks.Count + " chunks", "time: " + delta + "ms", 1, true);
				}

				GpuH.UpdateVram();
				return pointer;
			}
			finally
			{
				_semaphorePush.Release();
			}
		}

		public async Task<List<float[]>> PullChunksAsync(long firstPointer, bool silent = false)
		{
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot pull chunks: Not initialized or no pointer provided", "", 1);
				return [];
			}

			Stopwatch sw = Stopwatch.StartNew();
			await _semaphorePull.WaitAsync();
			try
			{
				if (!FloatBuffers.TryGetValue(firstPointer, out var floatBuffers))
				{
					if (!silent)
					{
						Log("Cannot pull chunks: Pointer not found", "", 1);
					}
					return [];
				}

				List<float[]> chunks = [];
				Log("Pulling chunks", $"count: {floatBuffers.Length}", 1);
				Log("");

				await Task.Run(() =>
				{
					for (int i = 0; i < floatBuffers.Length; i++)
					{
						float[] chunk = new float[floatBuffers[i].Length];
						floatBuffers[i].CopyToCPU(chunk);
						chunks.Add(chunk);

						if (i % LogInterval == 0 && !silent)
						{
							Log("Pulled chunk", $"index: {i} / {floatBuffers.Length}", 2, true);
						}
					}
				});

				RemoveFloatPointerGroup(firstPointer);

				sw.Stop();
				long delta = sw.ElapsedMilliseconds;

				if (!silent)
				{
					Log("Pushed " + chunks.Count + " chunks", "time: " + delta + "ms", 1, true);
				}

				GpuH.UpdateVram();
				return chunks;
			}
			finally
			{
				_semaphorePull.Release();
			}
		}

		public void RemoveFloatPointerGroup(long firstPointer)
		{
			// Abort if not initialized
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot remove pointer group: Not initialized or no pointer provided", "", 1);
				return;
			}

			// Get buffers
			if (!FloatBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<float, Stride1D.Dense>[]? floatBuffers))
			{
				Log("Cannot remove pointer group: Pointer not found", "", 1);
				return;
			}

			// Dispose buffers
			foreach (var buffer in floatBuffers)
			{
				buffer.Dispose();
			}

			// Remove from dictionary
			FloatBuffers.Remove(firstPointer);

			// Log
			Log("Removed pointer group", "pointer: " + firstPointer, 1);

			// Update Vram
			GpuH.UpdateVram();
		}

		public void RemoveFloat2PointerGroup(long firstPointer)
		{
			// Abort if not initialized
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot remove pointer group: Not initialized or no pointer provided", "", 1);
				return;
			}

			// Get buffers
			if (!Float2Buffers.TryGetValue(firstPointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? complexBuffers))
			{
				Log("Cannot remove pointer group: Pointer not found", "", 1);
				return;
			}

			// Dispose buffers
			foreach (var buffer in complexBuffers)
			{
				buffer.Dispose();
			}

			// Remove from dictionary
			Float2Buffers.Remove(firstPointer);

			// Log
			Log("Removed pointer group", "pointer: " + firstPointer, 1);

			// Update Vram
			GpuH.UpdateVram();
		}

		public void RemoveComplexPointerGroup(long firstPointer)
		{
			// Abort if not initialized
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot remove pointer group: Not initialized or no pointer provided", "", 1);
				return;
			}

			// Get buffers
			if (!ComplexBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<Complex, Stride1D.Dense>[]? complexBuffers))
			{
				Log("Cannot remove pointer group: Pointer not found", "", 1);
				return;
			}

			// Dispose buffers
			foreach (var buffer in complexBuffers)
			{
				buffer.Dispose();
			}

			// Remove from dictionary
			ComplexBuffers.Remove(firstPointer);

			// Log
			Log("Removed pointer group", "pointer: " + firstPointer, 1);

			// Update Vram
			GpuH.UpdateVram();
		}


		// ----- Conversion ----- \\
		public long ConvertFloat2ToComplex(long pointer, bool silent = false)
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				if (!silent)
				{
					Log("Cannot convert Float2 to Complex: GPU not initialized", "ConvertFloat2ToComplex");
				}
				return 0;
			}

			// Abort if pointer not found
			if (!Float2Buffers.TryGetValue(pointer, out var buffers))
			{
				if (!silent)
				{
					Log("Cannot convert Float2 to Complex: Pointer not found", "ConvertFloat2ToComplex");
				}
				return pointer;
			}

			// Log & stopwatch
			if (!silent)
			{
				Log($"Converting Float2 to Complex on {buffers.Length} buffers", "ConvertFloat2ToComplex");
			}
			Stopwatch sw = Stopwatch.StartNew();

			List<MemoryBuffer1D<Complex, Stride1D.Dense>> complexBuffers = [];

			// Convert each buffer values Float2(float, float) -> Complex(double, double)
			for (int i = 0; i < buffers.Length; i++)
			{
				// Get buffers
				var buffer = buffers[i];
				var complexBuffer = Acc.Allocate1D<Complex>(buffer.Length);

				// Convert (parallel)
				for (int j = 0; j < buffer.Length; j++)
				{
					complexBuffer.View[j] = new Complex(buffer.View[j].X, buffer.View[j].Y);
				}

				// Add to list
				complexBuffers.Add(complexBuffer);
			}

			// Remove Float2 buffers
			RemoveFloat2PointerGroup(pointer);

			// Get first pointer
			long firstPointer = complexBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;

			// Add to dictionary
			if (firstPointer != 0)
			{
				ComplexBuffers.TryAdd(firstPointer, complexBuffers.ToArray());
			}

			// Log & update VRAM
			sw.Stop();
			if (!silent)
			{
				Log($"Converted Float2 to Complex on {complexBuffers.Count} buffers", $"time: {sw.ElapsedMilliseconds}ms", 1);
			}
			GpuH.UpdateVram();

			return firstPointer;
		}

		public long ConvertFloatToDouble(long pointer, bool silent = false) 
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				if (!silent)
				{
					Log("Cannot convert Float to Double: GPU not initialized", "ConvertFloatToDouble");
				}
				return 0;
			}
			
			// Abort if pointer not found
			if (!FloatBuffers.TryGetValue(pointer, out var buffers))
			{
				if (!silent)
				{
					Log("Cannot convert Float to Double: Pointer not found", "ConvertFloatToDouble");
				}
				return pointer;
			}
			
			// Log & stopwatch
			if (!silent)
			{
				Log($"Converting Float to Double on {buffers.Length} buffers", "ConvertFloatToDouble");
			}
			Stopwatch sw = Stopwatch.StartNew();
			
			List<MemoryBuffer1D<double, Stride1D.Dense>> doubleBuffers = [];
			
			// Convert each buffer values Float -> Double
			for (int i = 0; i < buffers.Length; i++)
			{
				// Get buffers
				var buffer = buffers[i];
				var doubleBuffer = Acc.Allocate1D<double>(buffer.Length);
				// Convert (parallel)
				for (int j = 0; j < buffer.Length; j++)
				{
					doubleBuffer.View[j] = buffer.View[j];
				}
				// Add to list
				doubleBuffers.Add(doubleBuffer);
			}

			// Remove Float buffers
			RemoveFloatPointerGroup(pointer);

			// Get first pointer
			long firstPointer = doubleBuffers.FirstOrDefault()?.NativePtr.ToInt64() ?? 0;

			// Add to dictionary
			if (firstPointer != 0)
			{
				DoubleBuffers.TryAdd(firstPointer, doubleBuffers.ToArray());
			}

			// Log & update VRAM
			sw.Stop();
			if (!silent)
			{
				Log($"Converted Float to Double on {doubleBuffers.Count} buffers", $"time: {sw.ElapsedMilliseconds}ms", 1);
			}

			GpuH.UpdateVram();

			return firstPointer;
		}
	}
}