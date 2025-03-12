using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;

namespace ILGPUAudioTransformations
{
	public class GpuMemoryHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public GpuHandling GpuH;

		private ListBox LogBox;


		public Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers = [];
		public Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> ComplexBuffers = [];


		// ----- ----- ----- LAMBDA FUNCTIONS ----- ----- ----- //
		public Context Ctx => GpuH.Ctx;
		public CudaAccelerator? Acc => GpuH.Acc;
		public CudaDevice? Dev => GpuH.Dev;

		public bool IsInitialized => Acc != null && Dev != null;

		public int LogInterval => GpuH.LogInterval;




		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
		public GpuMemoryHandling(GpuHandling gpuHandling, ListBox? logBox = null)
		{
			// Set attributes
			this.GpuH = gpuHandling;
			this.LogBox = logBox ?? new ListBox();
		}






		// ----- ----- ----- METHODS ----- ----- ----- //
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Memory>";

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
			// Dispose all buffers
			foreach (var buffer in FloatBuffers)
			{
				foreach (var buf in buffer.Value)
				{
					buf.Dispose();
				}
			}
			foreach (var buffer in ComplexBuffers)
			{
				foreach (var buf in buffer.Value)
				{
					buf.Dispose();
				}
			}

			// Clear dictionaries
			FloatBuffers.Clear();
			ComplexBuffers.Clear();
		}

		public long PushChunks(List<float[]> chunks, bool silent = false)
		{
			// Abort if not initialized
			if (!IsInitialized || chunks.Count == 0 || Acc == null || Dev == null)
			{
				if (!silent)
				{
					Log("Cannot push chunks: Not initialized or no chunks provided", "", 1);
				}
				return 0;
			}

			// Create buffer-array
			MemoryBuffer1D<float, Stride1D.Dense>[] floatBuffers = new MemoryBuffer1D<float, Stride1D.Dense>[chunks.Count];

			// Log
			Log("Pushing chunks", "count: " + chunks.Count, 1);
			Log("");

			// Push chunks
			for (int i = 0; i < chunks.Count; i++)
			{
				floatBuffers[i] = Acc.Allocate1D<float>(chunks[i].Length);
				floatBuffers[i].CopyFromCPU(chunks[i]);

				// Log
				if (!silent && i % LogInterval == 0)
				{
					Log("Pushed chunk", "index: " + i + " / " + chunks.Count, 2, true);
				}

			}

			// Get first pointer
			long pointer = floatBuffers[0].NativePtr.ToInt64();

			// Add to dictionary
			FloatBuffers.Add(pointer, floatBuffers);

			// Log
			if (!silent)
			{
				Log("Pushed chunks", "count: " + chunks.Count, 1, true);
			}

			// Update Vram
			GpuH.UpdateVram();

			return pointer;
		}

		public List<float[]> PullChunks(long firstPointer)
		{
			// Abort if not initialized
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot pull chunks: Not initialized or no pointer provided", "", 1);
				return [];
			}

			// Get buffers
			if (!FloatBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<float, Stride1D.Dense>[]? floatBuffers))
			{
				Log("Cannot pull chunks: Pointer not found", "", 1);
				return [];
			}

			// Pull chunks
			List<float[]> chunks = [];

			// Log
			Log("Pulling chunks", "count: " + floatBuffers.Length, 1);
			Log("");

			for (int i = 0; i < floatBuffers.Length; i++)
			{
				MemoryBuffer1D<float, Stride1D.Dense>? buffer = floatBuffers[i];
				float[] chunk = new float[buffer.Length];
				buffer.CopyToCPU(chunk);
				chunks.Add(chunk);

				// Log
				if (i % LogInterval == 0)
				{
					Log("Pulled chunk", "index: " + i + " / " + floatBuffers.Length, 2, true);
				}
			}

			// Remove from dictionary
			RemoveFloatPointerGroup(firstPointer);

			// Log
			Log("Pulled chunks", "count: " + chunks.Count, 1, true);

			// Update Vram
			GpuH.UpdateVram();

			return chunks;
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

		public void RemoveComplexPointerGroup(long firstPointer)
		{
			// Abort if not initialized
			if (!IsInitialized || firstPointer == 0 || Acc == null || Dev == null)
			{
				Log("Cannot remove pointer group: Not initialized or no pointer provided", "", 1);
				return;
			}
			
			// Get buffers
			if (!ComplexBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? complexBuffers))
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
	}
}