using Microsoft.VisualBasic.Devices;
using Microsoft.VisualBasic.Logging;
using NAudio.Wave;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using System.Transactions;

namespace ILGPUAudioTransformations
{
	public class AudioHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public string Repopath;

		public List<AudioObject> Tracks = [];

		public int FPS = 45;
		public int SamplesPerPixel => CurrentTrack?.Position == 0 ? CurrentTrack.GetFitResolution(WaveformPbox.Width) : _samplesPerPixel;

		private int _samplesPerPixel = 128;

		// ----- ----- ----- OBJECTS ----- ----- ----- //
		public ListBox TracksList;
		public PictureBox WaveformPbox;
		public Button PlayButton;
		public TextBox TimeText;
		public Button ColorGraphButton;
		public Button ColorBackgroundButton;
		public Label TrackMetaLabel;
		public HScrollBar Scrollbar;

		private System.Windows.Forms.Timer waveformTimer;




		// ----- ----- ----- LAMBDAS ----- ----- ----- //
		private AudioObject? _currentTrack;
		private bool _isDrawing = false; // Verhindert doppelte Zeichenaufrufe

		public AudioObject? CurrentTrack
		{
			get => _currentTrack;
			set
			{
				if (_currentTrack != value)
				{
					// Altes Track-Objekt freigeben (falls nötig)
					if (_currentTrack != null)
					{
						_currentTrack.Dispose(); // Falls IDisposable
						_currentTrack = null;
					}

					_currentTrack = value;
					_ = DrawWaveformSmoothAsync(); // Asynchron neu zeichnen
				}
			}
		}

		public long Offset => Scrollbar.Value * 10;

		// ----- ----- ----- CONSTRUCTOR ----- ----- ----- //
		public AudioHandling(string repopath, ListBox? tracksListbox = null, PictureBox? waveformPbox = null, Button? playButton = null, TextBox? timeText = null, Button? colorGraphButton = null, Button? colorBackgroundButton = null, Label? trackMetaLabel = null, HScrollBar? scrollbar = null)
		{
			// Set attributes
			this.Repopath = repopath;
			this.TracksList = tracksListbox ?? new ListBox();
			this.WaveformPbox = waveformPbox ?? new PictureBox();
			this.PlayButton = playButton ?? new Button();
			this.TimeText = timeText ?? new TextBox();
			this.ColorGraphButton = colorGraphButton ?? new Button();
			this.ColorBackgroundButton = colorBackgroundButton ?? new Button();
			this.TrackMetaLabel = trackMetaLabel ?? new Label();
			this.Scrollbar = scrollbar ?? new HScrollBar();

			// Register events
			TracksList.SelectedIndexChanged += async (sender, e) =>
			{
				CurrentTrack = TracksList.SelectedIndex >= 0 && TracksList.SelectedIndex < Tracks.Count
					? Tracks[TracksList.SelectedIndex]
					: null;
				TrackMetaLabel.Text = CurrentTrack != null
					? $"{CurrentTrack.Samplerate / 1000}kHz {CurrentTrack.Bitdepth}b {CurrentTrack.Channels}Ch {CurrentTrack.Duration:0}s {(CurrentTrack.Floats.LongLength / 1000 / 1000)}M"
					: "No track selected";
				Scrollbar.Maximum = CurrentTrack != null ? (int) CurrentTrack.Floats.LongLength / 10 / 4 : 0;
				await DrawWaveformSmoothAsync();
				Scrollbar.Value = 0;
			};
			Scrollbar.Scroll += async (sender, e) => await DrawWaveformSmoothAsync();
			TracksList.Click += (sender, e) => RemoveTrack();

			this.PlayButton.Click += (sender, e) => CurrentTrack?.PlayStop(this.PlayButton);
			this.WaveformPbox.DoubleClick += (sender, e) => ImportTracks();
			this.WaveformPbox.MouseWheel += (sender, e) =>
			{
				if (e.Delta < 0)
				{
					_samplesPerPixel = Math.Min(SamplesPerPixel * 2, 65536);
				}
				else
				{
					_samplesPerPixel = Math.Max(SamplesPerPixel / 2, 1);
				}
				_ = DrawWaveformSmoothAsync();
			};

			// **Optimiertes Paint-Event**
			this.WaveformPbox.Paint += async (sender, e) =>
			{
				await DrawWaveformSmoothAsync();
			};

			// Init. waveform timer (Async-Update)
			this.waveformTimer = new System.Windows.Forms.Timer();
			this.waveformTimer.Interval = 1000 / FPS;
			this.waveformTimer.Tick += async (sender, e) =>
			{
				if (CurrentTrack?.Player.PlaybackState == PlaybackState.Playing)
				{
					await DrawWaveformSmoothAsync();
				}
			};
			this.waveformTimer.Start();


		}

		// ----- ----- ----- Asynchrones Zeichnen ----- ----- ----- //
		private async Task DrawWaveformSmoothAsync()
		{
			if (_isDrawing || CurrentTrack == null)
			{
				return;
			}
			_isDrawing = true;

			long offset = Offset;
			if (offset == 0)
			{
				offset = -1;
			}

			Bitmap? newBitmap = null; // Bitmap nullable machen

			try
			{
				newBitmap = await Task.Run(() =>
					CurrentTrack.DrawWaveformSmooth(WaveformPbox, offset, SamplesPerPixel, ColorGraphButton.BackColor, ColorBackgroundButton.BackColor)
				);
				TimeText.Text = $"{CurrentTrack?.Position ?? 0 / CurrentTrack?.Samplerate ?? 1 * CurrentTrack?.Channels ?? 2 / 20.0:0.00} s";


				if (WaveformPbox.InvokeRequired)
				{
					WaveformPbox.Invoke(new Action(() =>
					{
						// **Vergleiche mit der aktuellen Bitmap (falls vorhanden)**
						if (WaveformPbox.Image == null || !WaveformBitmapsAreEqual(WaveformPbox.Image as Bitmap, newBitmap))
						{
							WaveformPbox.Image?.Dispose(); // Dispose vorherige Bitmap
							WaveformPbox.Image = newBitmap; // Weise *neue* Bitmap nur zu, wenn sie sich unterscheidet
						}
						else
						{
							newBitmap?.Dispose(); // Dispose die *neue* Bitmap, da sie nicht zugewiesen wird
						}
					}));
				}
				else
				{
					// **Vergleiche mit der aktuellen Bitmap (falls vorhanden)**
					if (WaveformPbox.Image == null || !WaveformBitmapsAreEqual(WaveformPbox.Image as Bitmap, newBitmap))
					{
						WaveformPbox.Image?.Dispose(); // Dispose vorherige Bitmap
						WaveformPbox.Image = newBitmap; // Weise *neue* Bitmap nur zu, wenn sie sich unterscheidet
					}
					else
					{
						newBitmap?.Dispose(); // Dispose die *neue* Bitmap, da sie nicht zugewiesen wird
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Waveform-Fehler: {ex.Message}");
				newBitmap?.Dispose(); // Dispose Bitmap auch im Fehlerfall
			}
			finally
			{
				_isDrawing = false;
			}
		}

		// Hilfsmethode zum Bitmap-Vergleich (Pixel für Pixel)
		private bool WaveformBitmapsAreEqual(Bitmap? bmp1, Bitmap? bmp2)
		{
			if (bmp1 == null || bmp2 == null) return false;
			if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height) return false;

			for (int x = 0; x < bmp1.Width; x++)
			{
				for (int y = 0; y < bmp1.Height; y++)
				{
					if (bmp1.GetPixel(x, y) != bmp2.GetPixel(x, y))
					{
						return false; // Pixel unterscheiden sich
					}
				}
			}
			return true; // Bitmaps sind identisch
		}







		// ----- ----- ----- METHODS ----- ----- ----- //
		public void InitTracksBinding()
		{
			this.TracksList.DataSource = null;
			this.TracksList.DataSource = Tracks;
			this.TracksList.DisplayMember = "Name";
		}

		public void AddTrack(string path)
		{
			this.Tracks.Add(new AudioObject(path));

			// Init. Listbox binding
			this.InitTracksBinding();
		}

		public void ImportTracks()
		{
			// OFD multiselect audio files (wav, mp3, flac) at MyMusic
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Title = "Import audio file(s)";
			ofd.Filter = "Audio Files|*.wav;*.mp3;*.flac";
			ofd.Multiselect = true;
			ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

			// Ofd show -> add tracks
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				foreach (string path in ofd.FileNames)
				{
					this.AddTrack(path);
				}
			}

			// Init. Listbox binding
			this.InitTracksBinding();

			// Draw waveform
			this.WaveformPbox.Invalidate();

		}

		public void RemoveTrack()
		{
			// Abort if not CTRL down
			if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
			{
				return;
			}

			if (TracksList.SelectedIndex >= 0 && TracksList.SelectedIndex < Tracks.Count)
			{
				Tracks.RemoveAt(TracksList.SelectedIndex);
				InitTracksBinding();
				WaveformPbox.Refresh();
			}
		}

		public static float UpdateId3Tag(string filePath, Label? bpmLabel = null)
		{
			// Verify file
			if (!File.Exists(filePath) || (Path.GetExtension(filePath).ToLower() != ".mp3" &&
										   Path.GetExtension(filePath).ToLower() != ".flac" &&
										   Path.GetExtension(filePath).ToLower() != ".wav"))
			{
				return 0.0f;
			}

			// Get TagLib file
			var file = TagLib.File.Create(filePath);

			// Versuch 1: Standard BPM-Tag (Ganzzahl)
			float bpm = file.Tag.BeatsPerMinute;

			// Versuch 2: Benutzerdefiniertes ID3-Tag (MixMeister speichert BPM oft als String)
			if (bpm == 0 && file.Tag is TagLib.Id3v2.Tag id3v2Tag)
			{
				var bpmFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TBPM", false);
				if (bpmFrame != null && float.TryParse(bpmFrame.Text[0], out float parsedBpm))
				{
					bpm = parsedBpm;
				}
			}

			// Update Label mit 2 Nachkommastellen
			if (bpmLabel != null)
			{
				bpmLabel.Text = $"BPM: {(bpm / 100):F2}";
			}

			// Return BPM
			return bpm / 100;
		}



	}


	public class AudioObject
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public string Filepath;
		public string Name { get; private set; }

		public WaveOutEvent Player = new();
		public bool Playing => Player?.PlaybackState == PlaybackState.Playing;
		public long Position
		{
			get
			{
				if (Player?.PlaybackState == PlaybackState.Playing)
				{
					return Player.GetPosition();
				}
				return 0;
			}
		}

		public int Samplerate = 44100;
		public int Bitdepth = 16;
		public int Channels = 2;
		public long Length = 0;
		public double Duration = 0.0;

		public float[] Floats = [];
		public long Pointer = 0;
		public int Overlap = 0;


		private readonly object _waveformLock = new object(); // Lock object for thread safety


		// ----- ----- ----- OBJECTS ----- ----- ----- //
		public Stopwatch TimerPlayback = new();




		// ----- ----- ----- CONSTRUCTOR ----- ----- ----- //
		public AudioObject(string path)
		{
			// Set filepath & name
			this.Filepath = path;
			this.Name = Path.GetFileNameWithoutExtension(path);

			// Load audio file
			this.LoadAudio();
		}





		// ----- ----- ----- METHODS ----- ----- ----- //
		// ----- IO ----- \\
		public void LoadAudio()
		{
			// Get audiofilereader
			AudioFileReader reader = new(Filepath);

			// Set attributes
			Samplerate = reader.WaveFormat.SampleRate;
			Bitdepth = reader.WaveFormat.BitsPerSample;
			Channels = reader.WaveFormat.Channels;

			// Read audio
			Length = reader.Length;
			Duration = reader.TotalTime.TotalSeconds;
			Floats = new float[Length];

			reader.Read(Floats, 0, Floats.Length);

			// Close reader
			reader.Close();
			reader.Dispose();
		}

		public void Dispose()
		{
			Player.Dispose();
			TimerPlayback.Stop();
			TimerPlayback.Reset();
		}

		public byte[] GetBytes()
		{
			int bytesPerSample = Bitdepth / 8;
			byte[] bytes = new byte[Floats.Length * bytesPerSample];

			for (int i = 0; i < Floats.Length; i++)
			{
				byte[] byteArray;
				float sample = Floats[i];

				switch (Bitdepth)
				{
					case 16:
						short shortSample = (short) (sample * short.MaxValue);
						byteArray = BitConverter.GetBytes(shortSample);
						break;
					case 24:
						int intSample24 = (int) (sample * (1 << 23));
						byteArray = new byte[3];
						byteArray[0] = (byte) (intSample24 & 0xFF);
						byteArray[1] = (byte) ((intSample24 >> 8) & 0xFF);
						byteArray[2] = (byte) ((intSample24 >> 16) & 0xFF);
						break;
					case 32:
						int intSample32 = (int) (sample * int.MaxValue);
						byteArray = BitConverter.GetBytes(intSample32);
						break;
					default:
						throw new ArgumentException("Unsupported bit depth");
				}

				Buffer.BlockCopy(byteArray, 0, bytes, i * bytesPerSample, bytesPerSample);
			}

			return bytes;
		}

		public void ExportAudioWav(string filepath)
		{
			int sampleRate = Samplerate;
			int bitDepth = Bitdepth;
			int channels = Channels;
			float[] audioData = Floats;

			// Berechne die tatsächliche Länge der Audiodaten
			int actualLength = audioData.Length / (bitDepth / 8) / channels;

			using (var fileStream = new FileStream(filepath, FileMode.Create))
			using (var writer = new BinaryWriter(fileStream))
			{
				// RIFF header
				writer.Write(Encoding.ASCII.GetBytes("RIFF"));
				writer.Write(36 + actualLength * channels * (bitDepth / 8)); // File size
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));

				// fmt subchunk
				writer.Write(Encoding.ASCII.GetBytes("fmt "));
				writer.Write(16); // Subchunk1Size (16 for PCM)
				writer.Write((short) 1); // AudioFormat (1 for PCM)
				writer.Write((short) channels); // NumChannels
				writer.Write(sampleRate); // SampleRate
				writer.Write(sampleRate * channels * (bitDepth / 8)); // ByteRate
				writer.Write((short) (channels * (bitDepth / 8))); // BlockAlign
				writer.Write((short) bitDepth); // BitsPerSample

				// data subchunk
				writer.Write(Encoding.ASCII.GetBytes("data"));
				writer.Write(actualLength * channels * (bitDepth / 8)); // Subchunk2Size

				// Convert float array to the appropriate bit depth and write to file
				for (int i = 0; i < actualLength * channels; i++)
				{
					float sample = audioData[i];
					switch (bitDepth)
					{
						case 16:
							var shortSample = (short) (sample * short.MaxValue);
							writer.Write(shortSample);
							break;
						case 24:
							var intSample24 = (int) (sample * (1 << 23));
							writer.Write((byte) (intSample24 & 0xFF));
							writer.Write((byte) ((intSample24 >> 8) & 0xFF));
							writer.Write((byte) ((intSample24 >> 16) & 0xFF));
							break;
						case 32:
							var intSample32 = (int) (sample * int.MaxValue);
							writer.Write(intSample32);
							break;
						default:
							throw new ArgumentException("Unsupported bit depth");
					}
				}
			}
		}


		// ----- View ----- \\
		public Bitmap DrawWaveformSmoothOld(PictureBox wavebox, long offset = -1, int samplesPerPixel = 1, Color? graph = null, Color? background = null)
		{
			// **Offset berechnen**
			if (offset == -1)
			{
				if (Player?.PlaybackState == PlaybackState.Playing)
				{
					// **1. GetPosition() in Sekunden umrechnen**
					long positionBytes = Player.GetPosition();
					int bytesPerSample = (Bitdepth / 8) * Channels;

					double elapsedTime = 0;
					if (bytesPerSample > 0)
					{
						elapsedTime = (double) positionBytes / (Samplerate * bytesPerSample);
					}

					// **2. Falls Timer läuft, zusätzliche Zeit addieren**
					if (TimerPlayback.IsRunning)
					{
						elapsedTime += TimerPlayback.Elapsed.TotalSeconds;
					}

					// **3. Umrechnung in Samples**
					offset = (long) (elapsedTime * Samplerate);
				}
				else
				{
					offset = 0;
				}
			}

			// **Check: Gültige Werte**
			if (Floats.Length == 0 || wavebox.Width <= 0 || wavebox.Height <= 0)
			{
				return new Bitmap(1, 1);
			}

			// **Farben festlegen**
			Color waveformColor = graph ?? Color.FromName("HotTrack");
			Color backgroundColor = background ?? (waveformColor.GetBrightness() > 0.5f ? Color.White : Color.Black);

			// **Bitmap & Graphics**
			Bitmap bmp = new(wavebox.Width, wavebox.Height);
			using Graphics gfx = Graphics.FromImage(bmp);
			using Pen pen = new(waveformColor);
			gfx.SmoothingMode = SmoothingMode.AntiAlias;
			gfx.Clear(backgroundColor);

			// **Achsenwerte**
			float centerY = wavebox.Height / 2f;
			float yScale = wavebox.Height / 2f;

			// **Wellenform zeichnen**
			lock (_waveformLock) // **Lock around Floats access**
			{
				for (int x = 0; x < wavebox.Width; x++)
				{
					long sampleIndex = offset + (long) x * samplesPerPixel;

					if (sampleIndex >= Floats.Length) break;

					float maxValue = float.MinValue;
					float minValue = float.MaxValue;

					for (int i = 0; i < samplesPerPixel; i++)
					{
						if (sampleIndex + i < Floats.Length)
						{
							if (Floats.Length == 0)
							{
								break;
							}
							try
							{
								maxValue = Math.Max(maxValue, Floats[sampleIndex + i]);
								minValue = Math.Min(minValue, Floats[Math.Min(sampleIndex + i, Floats.Length - 1)]);
							}
							catch
							{
								Console.WriteLine("Error drawing waveform.");
							}
						}
					}

					float yMax = centerY - maxValue * yScale;
					float yMin = centerY - minValue * yScale;

					// **Begrenzungen checken**
					yMax = Math.Max(0, Math.Min(yMax, wavebox.Height));
					yMin = Math.Max(0, Math.Min(yMin, wavebox.Height));

					// **Linie oder Punkt zeichnen**
					if (Math.Abs(yMax - yMin) > 0.01f)
					{
						gfx.DrawLine(pen, x, yMax, x, yMin);
					}
					else if (samplesPerPixel == 1)
					{
						gfx.DrawLine(pen, x, centerY, x, centerY - Floats[sampleIndex] * yScale);
					}
				}
			}


			return bmp;
		}

		public Bitmap DrawWaveformSmooth(PictureBox wavebox, long offset = -1, int samplesPerPixel = 1, Color? graph = null, Color? background = null)
		{
			// **Offset berechnen**
			if (offset == -1)
			{
				if (Player?.PlaybackState == PlaybackState.Playing)
				{
					// Get offset in samples (divide by channels)
					offset = Player.GetPosition() / (Bitdepth / 8);
				}
				else
				{
					offset = 0;
				}
			}

			// **Check: Gültige Werte**
			if (Floats.Length == 0 || wavebox.Width <= 0 || wavebox.Height <= 0)
			{
				return new Bitmap(1, 1);
			}

			// **Farben festlegen**
			Color waveformColor = graph ?? Color.FromName("HotTrack");
			Color backgroundColor = background ?? (waveformColor.GetBrightness() > 0.5f ? Color.White : Color.Black);

			// **Bitmap & Graphics**
			Bitmap bmp = new(wavebox.Width, wavebox.Height);
			using Graphics gfx = Graphics.FromImage(bmp);
			using Pen pen = new(waveformColor);
			gfx.SmoothingMode = SmoothingMode.AntiAlias;
			gfx.Clear(backgroundColor);

			// **Achsenwerte**
			float centerY = wavebox.Height / 2f;
			float yScale = wavebox.Height / 2f;

			float[] yMaxValues = new float[wavebox.Width]; // Arrays to store calculated yMax and yMin values
			float[] yMinValues = new float[wavebox.Width];

			// **Parallele Berechnung der Wellenform-Daten**
			Parallel.For(0, wavebox.Width, x =>
			{
				long sampleIndex = offset + (long) x * samplesPerPixel;
				if (sampleIndex >= Floats.Length) return; // Exit parallel loop early if out of bounds

				float maxValue = float.MinValue;
				float minValue = float.MaxValue;

				for (int i = 0; i < samplesPerPixel; i++)
				{
					if (sampleIndex + i < Floats.Length)
					{
						if (Floats.Length == 0)
						{
							break;
						}
						try
						{
							lock (_waveformLock) // Lock only for Floats access
							{
								maxValue = Math.Max(maxValue, Floats[sampleIndex + i]);
								minValue = Math.Min(minValue, Floats[Math.Min(sampleIndex + i, Floats.Length - 1)]);
							}
						}
						catch
						{
							Console.WriteLine("Error calculating waveform data.");
						}
					}
				}
				yMaxValues[x] = centerY - maxValue * yScale;
				yMinValues[x] = centerY - minValue * yScale;
			});


			// **Sequentielles Zeichnen der Wellenform basierend auf den paralle berechneten Daten**
			for (int x = 0; x < wavebox.Width; x++)
			{
				float yMax = yMaxValues[x];
				float yMin = yMinValues[x];

				// **Begrenzungen checken**
				yMax = Math.Max(0, Math.Min(yMax, wavebox.Height));
				yMin = Math.Max(0, Math.Min(yMin, wavebox.Height));

				// **Linie oder Punkt zeichnen**
				if (Math.Abs(yMax - yMin) > 0.01f)
				{
					gfx.DrawLine(pen, x, yMax, x, yMin);
				}
				else if (samplesPerPixel == 1)
				{
					lock (_waveformLock) // Lock again for Floats access for single sample drawing if needed
					{
						long sampleIndex = offset + (long) x * samplesPerPixel; // Recalculate sampleIndex if needed for single sample drawing
						if (sampleIndex < Floats.Length && Floats.Length > 0) // Add bounds check
						{
							gfx.DrawLine(pen, x, centerY, x, centerY - Floats[sampleIndex] * yScale);
						}
					}
				}
			}

			return bmp;
		}

		public int GetFitResolution(int width)
		{
			// Gets pixels per sample for a given width to fit the whole waveform
			int samplesPerPixel = (int) Math.Ceiling((double) Floats.Length / width) / 4;
			return samplesPerPixel;
		}


		// ----- Chunks ----- \\

		public List<float[]> MakeChunks(int chunkSize, int overlap)
		{
			// Sicherstellen, dass Overlap gültig ist
			Overlap = Math.Max(0, Math.Min(overlap, chunkSize / 2));

			// Berechnung der Anzahl an Chunks
			int stepSize = chunkSize - Overlap;
			int chunkCount = (int) Math.Ceiling((double) Floats.Length / stepSize);

			List<float[]> chunks = new(chunkCount);
			int index = 0;

			while (index < Floats.Length)
			{
				// Standardgröße setzen
				int length = Math.Min(chunkSize, Floats.Length - index);
				float[] chunk = new float[chunkSize]; // Immer volle Größe

				// Werte kopieren, Rest bleibt 0
				Array.Copy(Floats, index, chunk, 0, length);
				chunks.Add(chunk);

				// Index für nächstes Chunk anpassen
				index += stepSize;
			}

			return chunks;
		}

		public void AggregateChunks(List<float[]> chunks)
		{
			if (chunks == null || chunks.Count == 0)
			{
				Floats = [];
				return;
			}

			// Berechnung der finalen Länge
			int stepSize = chunks[0].Length - Overlap;
			int totalLength = stepSize * (chunks.Count - 1) + chunks[^1].Length;
			float[] aggregated = new float[totalLength];

			int index = 0;
			foreach (float[] chunk in chunks)
			{
				// Anzahl der zu kopierenden Samples (letztes Chunk kann kürzer sein)
				int copyLength = Math.Min(chunk.Length, aggregated.Length - index);

				Array.Copy(chunk, 0, aggregated, index, copyLength);
				index += stepSize;
			}

			// Set Floats & Length
			Floats = aggregated;
			Length = aggregated.LongLength;
		}



		// ----- Playback ----- \\
		public void PlayStop(Button? playbackButton = null)
		{
			if (Player.PlaybackState == PlaybackState.Playing)
			{
				if (playbackButton != null)
				{
					playbackButton.Text = "⏵";
				}
				TimerPlayback.Stop();
				Player.Stop();
			}
			else
			{
				byte[] bytes = GetBytes();

				MemoryStream ms = new(bytes);
				RawSourceWaveStream raw = new(ms, new WaveFormat(Samplerate, Bitdepth, Channels));

				Player.Init(raw);

				if (playbackButton != null)
				{
					playbackButton.Text = "⏹";
				}
				TimerPlayback.Restart();
				Player.Play();

				while (Player.PlaybackState == PlaybackState.Playing)
				{
					Application.DoEvents();
					Thread.Sleep(100);
				}

				if (playbackButton != null)
				{
					playbackButton.Text = "⏵";
				}
			}
		}

		public void Stop(Button? playbackButton = null)
		{
			if (Player.PlaybackState == PlaybackState.Playing)
			{
				if (playbackButton != null)
				{
					playbackButton.Text = "⏵";
				}
				TimerPlayback.Stop();
				Player.Stop();
			}
		}



		// ----- Simple Transformations ----- \\
		public void Normalize(float maxAmplitude = 1.0f)
		{
			// Get length
			long length = Floats.Length;

			// Normalize to max amplitude
			float max = length > 0 ? Floats.Max() : 1.0f;
			for (int i = 0; i < length; i++)
			{
				Floats[i] *= maxAmplitude / max;
			}
		}

		public void NormalizeVolume(float targetRMS = 0.1f, int windowMs = 100)
		{
			// Vars
			float[] audio = Floats;
			int sampleRate = Samplerate;


			int windowSize = (sampleRate * windowMs) / 1000; // Fenstergröße in Samples
			float epsilon = 1e-6f; // Vermeidung von Division durch 0

			for (int i = 0; i < audio.Length; i += windowSize)
			{
				// 1. RMS im Fenster berechnen
				float sum = 0;
				int count = 0;
				for (int j = 0; j < windowSize && (i + j) < audio.Length; j++)
				{
					sum += audio[i + j] * audio[i + j];
					count++;
				}

				float rms = (float) Math.Sqrt(sum / (count + epsilon));

				// 2. Verstärkungsfaktor berechnen
				float gain = targetRMS / (rms + epsilon);

				// 3. Begrenzung des Gains, um extreme Verstärkungen zu verhindern
				gain = Math.Min(gain, 4.0f); // Maximal 4-fache Verstärkung

				// 4. Lautstärke anpassen
				for (int j = 0; j < windowSize && (i + j) < audio.Length; j++)
				{
					audio[i + j] *= gain;
				}
			}
		}

		public int GetBestRmsWindowSize()
		{
			// Prüfe, ob Audio geladen ist
			if (Floats.Length == 0) return 1024; // Standardwert

			int sampleRate = Samplerate;

			// Dynamische Anpassung der Fenstergröße
			int minSize = sampleRate / 1000 * 20; // Minimum: 20 ms Fenster
			int maxSize = sampleRate / 10;        // Maximum: 100 ms Fenster

			float avgAmplitude = Floats.Take(10000).Average(Math.Abs); // Analyse der ersten 10k Samples

			// Größeres Fenster bei leisen Signalen, kleineres bei lauten
			float scaleFactor = Math.Max(0.2f, Math.Min(1.0f, avgAmplitude * 10));
			int windowSize = (int) (minSize + (maxSize - minSize) * (1 - scaleFactor));

			// Sicherstellen, dass windowSize eine Potenz von 2 ist (wichtig für FFT)
			int bestWindowSize = 1;
			while (bestWindowSize < windowSize) bestWindowSize *= 2;

			return bestWindowSize;
		}

		public float GetBestRmsGain()
		{
			// Prüfe, ob Audio geladen ist
			if (Floats.Length == 0) return 1.0f; // Kein Audio = Keine Verstärkung

			int windowSize = GetBestRmsWindowSize();

			// Berechnung des RMS-Werts über das erste relevante Fenster
			float sum = 0;
			for (int j = 0; j < windowSize && j < Floats.Length; j++)
			{
				sum += Floats[j] * Floats[j];
			}
			float rms = (float) Math.Sqrt(sum / windowSize);

			// Zielwert für einen "normalisierten" RMS
			float targetRms = 0.05f; // Beispiel: 5% der vollen Amplitude

			// Verstärkungsfaktor berechnen (Skalierung zu targetRms)
			float gain = targetRms / (rms + 1e-6f);

			// Begrenzung der Verstärkung (um Clipping zu vermeiden)
			gain = Math.Clamp(gain, 0.5f, 2.0f); // Verstärkung nur 0.5x - 2.0x

			return gain;
		}

		public float[][] SplitChannels(bool keepLeft = false)
		{
			if (Floats.Length == 0)
			{
				return [];
			}

			// Split audio into channels
			float[][] channels = new float[Channels][];
			for (int i = 0; i < Channels; i++)
			{
				channels[i] = new float[Floats.Length / Channels];
			}

			for (int i = 0; i < Floats.Length; i++)
			{
				channels[i % Channels][i / Channels] = Floats[i];
			}

			if (keepLeft)
			{
				Floats = channels[0];
			}
			else
			{
				if (Channels > 1)
				{
					Floats = channels[1];
					Samplerate /= 2;
				}
				else if (Channels == 1)
				{
					Floats = channels[0];
					Samplerate /= 2;
				}
			}

			return channels;

		}

	}
}