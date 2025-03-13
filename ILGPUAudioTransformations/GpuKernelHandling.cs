

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Algorithms;
using System.Reflection;
using System.Linq;

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
		public int KernelCountTimestretch => typeof(GpuKernelHandling).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.Count(method => method.IsStatic &&
			method.ReturnType == typeof(void) &&
			method.Name.StartsWith("TimeStretchKernel", StringComparison.Ordinal));
		public int KernelCountPitchshift => typeof(GpuKernelHandling).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.Count(method => method.IsStatic &&
			method.ReturnType == typeof(void) &&
			method.Name.StartsWith("PitchShiftKernel", StringComparison.Ordinal));

		private Dictionary<long, MemoryBuffer1D<float, Stride1D.Dense>[]> FloatBuffers => GpuMemoryH.FloatBuffers;
		private Dictionary<long, MemoryBuffer1D<Float2, Stride1D.Dense>[]> ComplexBuffers => GpuMemoryH.Float2Buffers;


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
		private void Log(string message, string inner = "", int layer = 1, bool update = false)
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
				Log("Context not initialized", "NormalizeKernel", 1);
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
				var kernel = Acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, float>(NormalizeKernel1);

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

		private static void NormalizeKernel1(Index1D d, ArrayView<float> view, float factor)
		{
			if (d < view.Length)
			{
				view[d] *= factor;
			}
		}

		public void TimeStretch(long firstPointer, float factor, int version = 1)
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				Log("Context not initialized", "TimeSTretchKernel", 1);
				return;
			}

			// Get buffers
			if (!ComplexBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? buffers))
			{
				Log("No buffers found", "TimeStretchKernel", 1);
				return;
			}

			// Clamp version
			if (version < 1 || version > KernelCountTimestretch)
			{
				version = 1;
			}

			// Load kernel depending on version (1-x dynamical)
			var methodInfo = typeof(GpuKernelHandling).GetMethod("TimeStretchKernel" + version, BindingFlags.Static | BindingFlags.NonPublic);

			// Abort if method not found
			if (methodInfo == null)
			{
				Log("Method not found", "TimeStretchKernel", 1);
				return;
			}

			// Get kernel
			Action<Index1D, ArrayView<Float2>, float> kernel = Acc.LoadAutoGroupedStreamKernel((Action<Index1D, ArrayView<Float2>, float>) Delegate.CreateDelegate(typeof(Action<Index1D, ArrayView<Float2>, float>), methodInfo));

			// Log
			Log("Timestretching buffers", methodInfo.Name, 1);
			Log("");

			try
			{
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
				Log("Error timestretching buffers: " + e.Message, e.InnerException?.Message ?? "", 1);
				GpuMemoryH.GpuH.UpdateVram();
				return;
			}

			// Update VRAM
			GpuMemoryH.GpuH.UpdateVram();
		}

		private static void TimeStretchKernel1(Index1D d, ArrayView<Float2> view, float factor)
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

		private static void TimeStretchKernel3(Index1D d, ArrayView<Float2> view, float factor)
		{
			int size = (int) view.Length;
			int overlap = size / 2; // Fester Overlap von 50%

			int i = d;
			if (i >= size) // Bounds Check, falls Kernelaufruf grösser ist als Array (optional, aber sicherheitshalber)
				return;

			// Hann-Window für sanftes Overlapping
			float window = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * (float) i / (size - 1));

			// **Korrektur der Phasenverschiebung für Zeitstreckung (ohne Pitch-Shift)**
			// Die Idee ist, die Phase langsamer ablaufen zu lassen, um das Signal zu strecken.
			// Wir skalieren den Index 'i' im Phasenoffset, um die Zeitachse zu dehnen.
			float phaseOffset = 2 * MathF.PI * (float) i * (1.0f / factor - 1.0f) / (float) size;

			// Lese Originalwert
			Float2 value = view[i];

			// Phasenverschiebung anwenden (Rotation in der komplexen Ebene)
			float cosPhase = MathF.Cos(phaseOffset);
			float sinPhase = MathF.Sin(phaseOffset);

			float newReal = value.X * cosPhase - value.Y * sinPhase;
			float newImag = value.X * sinPhase + value.Y * sinPhase;

			// Windowing anwenden
			newReal *= window;
			newImag *= window;

			// Schreiben des gefensterten und phasenverschobenen Wertes
			view[i] = new Float2(newReal, newImag);


			// **Overlapping-Add (OLA) korrekt implementieren**
			// Überlappende Region hinzufügen, um Clipping und Artefakte zu vermeiden.
			// Wichtig: Nur innerhalb des Overlap-Bereichs arbeiten und korrekte Gewichtung verwenden.
			if (i < overlap)
			{
				int overlapIndex = i + overlap;
				if (overlapIndex < size) // Sicherstellen, dass overlapIndex nicht Array-Grenzen überschreitet
				{
					// Gewichtung für sanften Übergang im Overlap-Bereich (Hann-Window-Form)
					float overlapWeight = 0.5f + 0.5f * MathF.Cos(MathF.PI * (float) i / (float) overlap);

					// Überlappende Werte additiv mischen mit Gewichtung
					Float2 overlapValue = view[overlapIndex];
					view[overlapIndex] = new Float2(
						overlapValue.X * (1.0f - overlapWeight) + newReal * overlapWeight,
						overlapValue.Y * (1.0f - overlapWeight) + newImag * overlapWeight
					);
				}
			}
		}

		private static void TimeStretchKernel4(Index1D d, ArrayView<Float2> view, float factor)
		{
			int size = (int) view.Length;
			int overlap = size / 2; // Fester Overlap von 50%
			int i = d;

			if (i >= size) // Bounds Check
				return;

			// Hann-Window
			float window = 0.5f - 0.5f * XMath.Cos(2 * XMath.PI * (float) i / (size - 1));

			// **1. Konvertiere zu Magnitude und Phase (Polarkoordinaten)**
			Float2 complexValue = view[i];
			float magnitude = XMath.Sqrt(complexValue.X * complexValue.X + complexValue.Y * complexValue.Y); // Magnitude berechnen
			float phase = XMath.Atan2(complexValue.Y, complexValue.X); // Phase berechnen (im Bogenmass)

			// **2. Vereinfachte Phase-Skalierung für Zeitstreckung**
			// **DIREKTE SKALIERUNG der Phase mit dem Faktor**
			phase *= (1.0f / factor); // Phase skalieren:  Streckung -> Phase langsamer, Kompression -> Phase schneller

			// **3. Konvertiere zurück zu Real- und Imaginärteil (Kartesische Koordinaten)**
			float newReal = magnitude * XMath.Cos(phase);
			float newImag = magnitude * XMath.Sin(phase);

			// Windowing anwenden
			newReal *= window;
			newImag *= window;

			// Schreibe den gefensterten und phasenverschobenen Wert
			view[i] = new Float2(newReal, newImag);


			// Overlapping-Add (OLA)
			if (i < overlap)
			{
				int overlapIndex = i + overlap;
				if (overlapIndex < size) // Bounds Check
				{
					float overlapWeight = 0.5f + 0.5f * XMath.Cos(XMath.PI * (float) i / (float) overlap);

					Float2 overlapValue = view[overlapIndex];
					view[overlapIndex] = new Float2(
						overlapValue.X * (1.0f - overlapWeight) + newReal * overlapWeight,
						overlapValue.Y * (1.0f - overlapWeight) + newImag * overlapWeight
					);
				}
			}
		}

		public void PitchShift(long firstPointer, float factor, int version = 1)
		{
			// Abort if not initialized
			if (!IsInitialized || Acc == null || Dev == null)
			{
				Log("Context not initialized", "PitchShiftKernel", 1);
				return;
			}

			// Get buffers
			if (!ComplexBuffers.TryGetValue(firstPointer, out MemoryBuffer1D<Float2, Stride1D.Dense>[]? buffers))
			{
				Log("No buffers found", "PitchShiftKernel", 1);
				return;
			}

			// Clamp version
			if (version < 1 || version > KernelCountTimestretch)
			{
				version = 1;
			}

			// Load kernel depending on version (1-x dynamical)
			var methodInfo = typeof(GpuKernelHandling).GetMethod("PitchShiftKernel" + version, BindingFlags.Static | BindingFlags.NonPublic);

			// Abort if method not found
			if (methodInfo == null)
			{
				Log("Method not found", "PitchShiftKernel", 1);
				return;
			}

			// Get kernel
			Action<Index1D, ArrayView<Float2>, float> kernel = Acc.LoadAutoGroupedStreamKernel((Action<Index1D, ArrayView<Float2>, float>) Delegate.CreateDelegate(typeof(Action<Index1D, ArrayView<Float2>, float>), methodInfo));

			// Log
			Log("Timestretching buffers", methodInfo.Name, 1);
			Log("");

			try
			{
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
						Log("Pitchshifted buffer " + i + "/" + buffers.Length, "", 2, true);
					}
				}

				// Log
				Log($"Pitchshifted {buffers.Length} buffers", "", 1, true);
			}
			catch (Exception e)
			{
				Log("Error pitchshifting buffers: " + e.Message, e.InnerException?.Message ?? "", 1);
				GpuMemoryH.GpuH.UpdateVram();
				return;
			}

			// Update VRAM
			GpuMemoryH.GpuH.UpdateVram();
		}

		private static void PitchShiftKernel1(Index1D d, ArrayView<Float2> view, float factor)
		{
			int size = (int) view.Length;
			int i = d;

			if (i >= size) // Bounds Check
				return;

			// 1. Berechne den Shift in Frequenzbins basierend auf dem Faktor.
			//    Ein Faktor von 2.0 (doppelte Frequenz) sollte etwa um die Hälfte der FFT-Grösse verschieben (Nyquist-Frequenz).
			//    Der Shift wird hier als *Integer* Bins berechnet.
			float shiftFloat = (factor - 1.0f) * size / 2.0f; // Proportional zum Faktor und der halben Grösse (ungefähre Skalierung)
			int shiftBins = (int) MathF.Round(shiftFloat); // Auf nächste ganze Zahl runden für Bin-Index


			// 2. Berechne den neuen Index nach dem Pitch-Shift.
			int newIndex = i - shiftBins; //  Negativer Shift für Pitch-Up (Frequenzen nach rechts/höher), positiver Shift für Pitch-Down

			// 3. Bounds Check für den neuen Index.
			//    Wenn newIndex ausserhalb des gültigen Bereichs liegt (0 bis size-1), setze auf 0.
			if (newIndex >= 0 && newIndex < size)
			{
				// 4. Kopiere den Wert vom originalen Index zum neuen Index (In-Place Shift).
				view[i] = view[newIndex]; // Wert von 'newIndex' nach 'i' verschieben (In-Place)
			}
			else
			{
				// 5. Wenn newIndex ausserhalb der Grenzen liegt, setze den Wert am aktuellen Index auf 0.
				//    Das "verwirft" Frequenzen, die ausserhalb des Bereichs verschoben wurden.
				view[i] = new Float2(0, 0); // Auf 0 setzen, wenn ausserhalb der Grenzen
			}
		}

		private static void PitchShiftKernel2(Index1D d, ArrayView<Float2> view, float factor)
		{
			int size = (int) view.Length;
			int overlap = size / 2; // Fester Overlap von 50%
			int i = d;

			if (i >= size) // Bounds Check
				return;

			// 1. Berechne ein LINEARES Frequenz-Ratio basierend auf dem Faktor (MILDERE Abstimmung).
			//    Wir verwenden jetzt eine lineare Skalierung, um den Pitch-Shift-Effekt zu VERMINDERN.
			//    Der Faktor wird DIREKT als lineare Verschiebung des Frequenzverhältnisses interpretiert,
			//    zentriert um 1.0 (keine Änderung).
			//    Beispiel: factor = 0.1  -> frequencyRatio = 1.1 (leichte Erhöhung)
			//             factor = -0.1 -> frequencyRatio = 0.9 (leichte Senkung)
			float frequencyRatio = 1.0f + factor; // Lineare Skalierung des Frequenzverhältnisses


			// 2. Berechne den neuen Index im Frequenz-Array basierend auf dem Frequenz-Ratio.
			//    Multipliziere den aktuellen Index 'i' mit dem Frequenz-Ratio, um den neuen Index zu erhalten.
			float newIndexFloat = (float) i * frequencyRatio;
			int newIndex = (int) MathF.Round(newIndexFloat); // Auf nächsten Integer-Index runden.

			Float2 shiftedValue;

			// 3. Bounds Check für den neuen Index.
			//    Stelle sicher, dass newIndex innerhalb des gültigen Bereichs (0 bis size-1) bleibt.
			if (newIndex >= 0 && newIndex < size)
			{
				// 4. Kopiere den Wert vom neuen Index zum aktuellen Index (Pitch-Shift durch Bin-Verschiebung).
				shiftedValue = view[newIndex]; // Wert von 'newIndex' holen
			}
			else
			{
				// 5. Wenn newIndex ausserhalb der Grenzen liegt, setze den Wert auf 0.
				//    Frequenzen, die ausserhalb des Bereichs verschoben wurden, werden verworfen (auf Stille gesetzt).
				shiftedValue = new Float2(0, 0); // Auf 0 setzen für Indices ausserhalb der Grenzen
			}

			// 6. Hann-Window für sanfte Übergänge (angewendet auf den *verschobenen* Wert)
			float window = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * (float) i / (size - 1));
			shiftedValue = new Float2(shiftedValue.X * window, shiftedValue.Y * window); // Windowing anwenden

			// 7. Schreiben des gefensterten und (pitch-)verschobenen Wertes zurück
			view[i] = shiftedValue; // In-Place zurückschreiben

			// **Hinweis:** Ein klassisches Overlap-Add (OLA) ist in dieser In-Place Frequenz-Bin-Verschiebungs-Implementierung NICHT direkt implementiert.
			//            Der Hann-Window dient hier primär der sanfteren Ausblendung der Chunk-Grenzen in der Frequenzdomain.
			//            Für ein vollständiges OLA müsste man die Verarbeitung Chunk-basiert mit Überlappung implementieren und die überlappenden Bereiche additiv mischen.
		}


	}
}