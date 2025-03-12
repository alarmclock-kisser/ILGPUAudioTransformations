

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;
using System.Numerics;

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

		public void TimeStretch(long firstPointer, float factor)
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				Log("Kernel not initialized", "TimeSTretchKernel", 1);
				return;
			}

			// Get buffers
			if (!ComplexBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? buffers))
			{
				Log("No buffers found", "TimeStretchKernel", 1);
				return;
			}

			// Log
			Log("Timestretching buffers", "factor: " + factor, 1);
			Log("");

			try
			{
				// Load kernel once
				var kernel = Acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Float2>, float>(TimeStretchKernel2);

				// For every buffer
				for (int i = 0; i < buffers.Length; i++)
				{
					MemoryBuffer1D<Float2, Stride1D.Dense> buffer = buffers[i];
					if (buffer == null) continue; // Sicherstellen, dass buffer nicht null ist

					// Call kernel
					kernel((int) buffer.Length, buffer.View, factor);

					// Synchronize accellerator
					Acc.Synchronize();

					// Log
					if (i % LogInterval == 0)
					{
						Log("Timestretched buffer " + i + "/" + buffers.Length, "", 2, true);
					}
				}

				// Log
				Log($"Timestretched {buffers.Length} buffers", "", 1, true);
			}
			catch (Exception e)
			{
				Log("Error timestretching buffers", e.Message, 1);
			}

			// Update VRAM
			GpuMemoryH.GpuH.UpdateVram();
		}

		private static void TimeStretchKernel(Index1D d, ArrayView<Float2> view, float factor)
		{
			// Get size & overlap 50%
			int size = (int) view.Length;
			int overlap = size / 2;

			// Get index
			int i = d;

			// Get factor
			float stretchFactor = factor;

			// Get window
			float window = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / size);

			// Get phase
			float phase = 2 * MathF.PI * i * stretchFactor / size;

			// Get real & imaginary
			float real = view[i].X;
			float imag = view[i].Y;

			// Get phase
			float newPhase = phase + window;
			float newReal = real + phase;
			float newImag = imag + phase;

			// Set new values
			view[i] = new Float2(newReal, newImag);

			// Set overlap
			if (i < overlap)
			{
				view[i + overlap] = new Float2(newReal, newImag);
			}

			// Set overlap
			if (i >= size - overlap)
			{
				view[i - overlap] = new Float2(newReal, newImag);
			}

		}

		private static void TimeStretchKernel2(Index1D d, ArrayView<Float2> view, float factor)
		{
			int size = (int) view.Length;
			int overlap = size / 2;
			int i = d;

			// Hann-Window für sanftes Overlapping
			float window = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (size - 1));

			// Frequenzbereich normalisieren und Phasenkorrektur berechnen
			float phaseOffset = 2 * MathF.PI * i * (1.0f - factor) / size;

			// Lese Originalwerte
			Float2 value = view[i];

			// Phasenverschiebung anwenden (Rotation in der komplexen Ebene)
			float cosPhase = MathF.Cos(phaseOffset);
			float sinPhase = MathF.Sin(phaseOffset);

			float newReal = value.X * cosPhase - value.Y * sinPhase;
			float newImag = value.X * sinPhase + value.Y * cosPhase;

			// Windowing anwenden
			newReal *= window;
			newImag *= window;

			// Schreiben
			view[i] = new Float2(newReal, newImag);

			// Overlapping korrekt berechnen, um Clipping zu vermeiden
			if (i < overlap)
			{
				int overlapIndex = i + overlap;
				float overlapWeight = 0.5f + 0.5f * MathF.Cos(MathF.PI * i / overlap); // Sanfter Übergang
				view[overlapIndex] = new Float2(
					view[overlapIndex].X * (1 - overlapWeight) + newReal * overlapWeight,
					view[overlapIndex].Y * (1 - overlapWeight) + newImag * overlapWeight
				);
			}
		}

	}
}