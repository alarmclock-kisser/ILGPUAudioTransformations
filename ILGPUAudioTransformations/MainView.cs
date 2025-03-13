using System.Reflection;
using System.Linq;

namespace ILGPUAudioTransformations
{
	public partial class MainView : Form
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		public string Repopath;

		public AudioHandling AudioH;
		public GpuHandling GpuH;



		private int _oldChunkSize = 65536;

		public float CurrentBpm = 0;



		// ----- ----- ----- LAMBDA FUNCTIONS ----- ----- ----- //
		public AudioObject? SelectedTrack => AudioH.CurrentTrack;

		public bool DataOnHost => AudioH.CurrentTrack?.Floats.Length > 0 && AudioH.CurrentTrack?.Pointer == 0;
		public bool DataOnCuda => AudioH.CurrentTrack?.Pointer != 0 && AudioH.CurrentTrack?.Floats.Length == 0;
		public bool DataTransformed => GpuH.GpuMemoryH != null && AudioH.CurrentTrack?.Floats.Length == 0 && AudioH.CurrentTrack?.Pointer != 0 && GpuH.GpuMemoryH.Float2Buffers.ContainsKey(AudioH.CurrentTrack?.Pointer ?? 0);

		public string TransformMode => DataOnCuda ? comboBox_cudaTransformations.SelectedItem?.ToString()?.Split(" ").FirstOrDefault() ?? "None" : "None";
		public string KernelName => SelectedTrack != null ? comboBox_kernels.SelectedItem?.ToString()?.Replace("F: ", "").Replace("C: ", "") ?? "None" : "None";


		public MainView()
		{
			InitializeComponent();
			Repopath = GetRepopath(true);

			// Window position
			this.StartPosition = FormStartPosition.Manual;
			this.Location = new Point(0, 0);

			// Init. classes
			AudioH = new AudioHandling(Repopath, listBox_tracks, pictureBox_waveform, button_playback, textBox_timestamp, button_colorGraph, button_colorBack, label_trackMeta, hScrollBar_offset);
			GpuH = new(Repopath, listBox_log, comboBox_cudaDevices, label_cudaVram, progressBar_cudaVram);

			// Register events
			listBox_tracks.SelectedIndexChanged += (sender, e) => ToggleUI();
			button_exportWav.Click += (sender, e) => ExportWav();
			button_normalize.MouseEnter += (sender, e) => numericUpDown_duration.Enabled = false;
			button_normalize.MouseLeave += (sender, e) => numericUpDown_duration.Enabled = true;
			numericUpDown_goalBpm.ValueChanged += (sender, e) => button_bpmFit_Click(sender, e);
			listBox_log.MouseDown += (sender, e) => ExportLogLineToClip(listBox_log.SelectedItem?.ToString() ?? "");

			// Set picturebox to double buffered
			SetDoubleBuffered(pictureBox_waveform);
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

			// Init. audio files
			ImportResourcesAudios();

			// Update UI
			ToggleUI();
			FillKernelsCombo();
		}

		









		// ----- ----- ----- METHODS ----- ----- ----- //
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<Main>";

			for (int i = 0; i <= layer; i++)
			{
				msg += " - ";
			}

			msg += message;

			if (inner != "")
			{
				msg += "  (" + inner + ")";
			}

			// Thread-sichere Aktualisierung der listBox_log mit Invoke
			if (listBox_log.InvokeRequired)
			{
				listBox_log.Invoke(new Action(() =>
				{
					if (update)
					{
						if (listBox_log.Items.Count > 0) // Check ob Items vorhanden sind
						{
							listBox_log.Items[listBox_log.Items.Count - 1] = msg;
						}
					}
					else
					{
						listBox_log.Items.Add(msg);
						listBox_log.SelectedIndex = listBox_log.Items.Count - 1;
					}
				}));
			}
			else
			{
				// Direkt im UI-Thread (kein Invoke nötig)
				if (update)
				{
					if (listBox_log.Items.Count > 0) // Check ob Items vorhanden sind
					{
						listBox_log.Items[listBox_log.Items.Count - 1] = msg;
					}
				}
				else
				{
					listBox_log.Items.Add(msg);
					listBox_log.SelectedIndex = listBox_log.Items.Count - 1;
				}
			}
		}

		private string GetRepopath(bool root)
		{
			string repo = AppDomain.CurrentDomain.BaseDirectory;

			if (root)
			{
				repo += @"..\..\..\";
			}

			repo = Path.GetFullPath(repo);
			return repo;
		}

		public void ImportResourcesAudios()
		{
			// Get each wav mp3 flac file in Resources/Audios
			string[] files = Directory.GetFiles(Repopath + @"Resources\Audio\", "*.*", SearchOption.AllDirectories);

			// Add each file with AudioH
			foreach (string file in files)
			{
				AudioH.AddTrack(file);
			}

			// Update UI
			ToggleUI();
		}

		public void ToggleUI()
		{
			// Update PictureBox
			if (DataOnHost)
			{
				SelectedTrack?.DrawWaveformSmoothOld(pictureBox_waveform, 0, 1024, button_colorGraph.BackColor, button_colorBack.BackColor);
			}
			else if (DataOnCuda)
			{
				// Null image
				pictureBox_waveform.Image = null;
			}

			// BPM tag
			CurrentBpm = AudioHandling.UpdateId3Tag(SelectedTrack?.Filepath ?? "", label_bpm);

			// Playback button
			button_playback.Enabled = SelectedTrack != null;

			// Move button
			button_move.Enabled = SelectedTrack != null && (DataOnHost || DataOnCuda);
			button_move.Text = DataOnHost ? "-> CUDA" : "Host <-";

			// Transform button
			button_transform.Enabled = SelectedTrack != null && DataOnCuda;
			button_transform.Text = DataTransformed ? "Inv-Trans" : "Transform";

			// Normalize button
			button_normalize.Enabled = SelectedTrack != null && (DataOnHost || DataOnCuda);

			// Run button
			button_run.Enabled = SelectedTrack != null && DataOnCuda && KernelName != "None";

			// NumUD kernel version only at TimeStretch
			numericUpDown_kernelVersion.Enabled = KernelName != "None";
			numericUpDown_kernelVersion.Minimum = 1;
			numericUpDown_kernelVersion.Maximum = KernelName == "TimeStretch" ? GpuH.GpuKernelH?.KernelCountTimestretch ?? 0 : KernelName == "PitchShift" ? GpuH.GpuKernelH?.KernelCountPitchshift ?? 0 : 0;
		}

		public void ExportWav()
		{
			// OFD at MyMusic
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
			sfd.Filter = "WAV files (*.wav)|*.wav";
			sfd.FileName = SelectedTrack?.Name ?? "track";

			// Export
			if (sfd.ShowDialog() == DialogResult.OK)
			{
				SelectedTrack?.ExportAudioWav(sfd.FileName);
			}

			// MsgBox
			MessageBox.Show("Exported to " + sfd.FileName);
		}

		private void ExportLogLineToClip(string line)
		{
			// Only if Right mouse down & NOT Left mouse down
			if (line == null || line == "" || string.IsNullOrEmpty(line) || MouseButtons != MouseButtons.Right)
			{
				return;
			}

			// Abort if no line
			if (line == null || line == "" || string.IsNullOrEmpty(line))
			{
				Log("No log line to copy", "", 1);
				return;
			}

			// Copy to clipboard
			Clipboard.SetText(line);
		}

		public void FillKernelsCombo()
		{
			// Clear combobox
			comboBox_kernels.Items.Clear();

			// Get function names in GpuKernelH via Reflection
			if (GpuH.GpuKernelH != null)
			{
				// Get all methods in GpuKernelH which are public, non-static and return type void, 3 parameters
				MethodInfo[] methods = GpuH.GpuKernelH.GetType().GetMethods(BindingFlags.Public !| BindingFlags.Static! | BindingFlags.Instance).Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 3).ToArray();

				// Add each method name to combobox
				foreach (MethodInfo method in methods)
				{
					comboBox_kernels.Items.Add(method.Name);
				}
			}



		}

		private void SetDoubleBuffered(Control control)
		{
			typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(control, true, null);
		}



		// ----- ----- ----- EVENTS ----- ----- ----- //
		// ----- Value changed ----- //
		private void numericUpDown_chunkSize_ValueChanged(object sender, EventArgs e)
		{
			// Double or half the chunk size if increasing or decreasing
			if (numericUpDown_chunkSize.Value > _oldChunkSize)
			{
				numericUpDown_chunkSize.Value = Math.Min(numericUpDown_chunkSize.Maximum, _oldChunkSize * 2);
			}
			else if (numericUpDown_chunkSize.Value < _oldChunkSize)
			{
				numericUpDown_chunkSize.Value = Math.Max(numericUpDown_chunkSize.Minimum, _oldChunkSize / 2);
			}

			// Update chunk size
			_oldChunkSize = (int) numericUpDown_chunkSize.Value;
		}

		private void numericUpDown_loggingFreq_ValueChanged(object sender, EventArgs e)
		{
			// Update logging interval
			GpuH.LogInterval = (int) numericUpDown_loggingFreq.Value;
		}

		private void comboBox_kernels_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Toggle UI
			ToggleUI();
		}

		private void comboBox_cudaTransformations_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Toggle UI
			ToggleUI();
		}



		// ----- Clicked ----- //
		private void button_normalize_Click(object sender, EventArgs e)
		{
			float factor = (float) numericUpDown_normalize.Value;

			if (DataOnHost)
			{
				SelectedTrack?.Normalize(factor);
			}
			else if (DataOnCuda)
			{
				GpuH.GpuKernelH?.Normalize(SelectedTrack?.Pointer ?? 0, factor);
			}

			// Log
			Log("Normalized track", "factor: " + factor, 1);

			// Update UI
			ToggleUI();
		}

		private void button_levelVolume_Click(object sender, EventArgs e)
		{
			// Abort if no track selected
			if (SelectedTrack == null)
			{
				return;
			}

			SelectedTrack.NormalizeVolume((float) numericUpDown_normalize.Value, (int) numericUpDown_duration.Value);

			// Log
			Log("Normalized volume", "factor: " + numericUpDown_normalize.Value + ", duration: " + numericUpDown_duration.Value, 1);

			// Update UI
			ToggleUI();
		}

		private void button_bpmFit_Click(object? sender, EventArgs e)
		{
			// Abort if no track selected
			if (SelectedTrack == null || numericUpDown_goalBpm.Value == 0 || CurrentBpm < 10 || CurrentBpm > 240)
			{
				return;
			}

			// Get factor & percentage increase
			float factor = CurrentBpm / (float) numericUpDown_goalBpm.Value;
			float increase = (factor - 1) * 100 * -1;

			// Set factor to param1 & label bpm increase
			numericUpDown_param1.Value = (decimal) factor;
			label_bpmPercent.Text = increase >= 0 ? "+" + increase + "%" : increase + "%";

			// Toggle UI
			ToggleUI();
		}


		// ----- Clicked GpuH ----- //
		private async void button_move_Click(object sender, EventArgs e)
		{
			// Abort if no track
			if (SelectedTrack == null || GpuH.GpuMemoryH == null)
			{
				return;
			}

			// Move data
			if (DataOnHost)
			{
				// Host -> CUDA
				SelectedTrack.Pointer = await GpuH.GpuMemoryH.PushChunksAsync(SelectedTrack.MakeChunks((int) numericUpDown_chunkSize.Value, (int) (numericUpDown_chunkSize.Value / 2)));
				SelectedTrack.Floats = [];

				// Select FFT (index 0) in transform combobox
				comboBox_cudaTransformations.SelectedIndex = 0;
			}
			else if (DataOnCuda)
			{
				// CUDA -> Host
				SelectedTrack.AggregateChunks(await GpuH.GpuMemoryH.PullChunksAsync(SelectedTrack.Pointer));
				SelectedTrack.Pointer = 0;

				// Select None in transform combobox
				comboBox_cudaTransformations.SelectedIndex = -1;
			}

			// Update UI
			ToggleUI();
		}

		private async void button_transform_Click(object sender, EventArgs e)
		{
			// Abort if no track
			if (SelectedTrack == null || GpuH.GpuMemoryH == null || GpuH.GpuTransformH == null || !DataOnCuda)
			{
				return;
			}

			// Transform switch case
			switch (TransformMode)
			{
				case "None":
					// No transformation
					break;
				case "FFT":
					// FFT transformation
					SelectedTrack.Pointer = await GpuH.GpuTransformH.PerformFFTAsync(SelectedTrack.Pointer);
					comboBox_cudaTransformations.SelectedIndex = 1;
					comboBox_kernels.SelectedIndex = 1;
					break;
				case "IFFT":
					// IFFT transformation
					SelectedTrack.Pointer = await GpuH.GpuTransformH.PerformIFFTAsync(SelectedTrack.Pointer);
					comboBox_cudaTransformations.SelectedIndex = -1;
					comboBox_kernels.SelectedIndex = -1;
					break;
				case "FFTW":
					// FFTW transformation
					SelectedTrack.Pointer = GpuH.GpuTransformH.PerformFFTW(SelectedTrack.Pointer);
					break;
				case "IFFTW":
					// IFFTW transformation
					SelectedTrack.Pointer = GpuH.GpuTransformH.PerformIFFTW(SelectedTrack.Pointer);
					break;
			}
		}

		private void button_run_Click(object sender, EventArgs e)
		{
			// Abort if no track
			if (SelectedTrack == null || GpuH.GpuKernelH == null || GpuH.GpuMemoryH == null || GpuH.GpuKernelH == null)
			{
				return;
			}

			// Run KernelH function switch case
			switch (KernelName)
			{
				case "None":
					// No kernel
					break;
				case "Normalize":
					// Normalize kernel
					GpuH.GpuKernelH.Normalize(SelectedTrack.Pointer, (float) numericUpDown_param1.Value);
					break;
				case "TimeStretch":
					// TimeStretch kernel
					GpuH.GpuKernelH.TimeStretch(SelectedTrack.Pointer, (float) numericUpDown_param1.Value, (int) numericUpDown_kernelVersion.Value);
					break;
				case "PitchShift":
					// PitchShift kernel
					GpuH.GpuKernelH.PitchShift(SelectedTrack.Pointer, (float) numericUpDown_param1.Value, (int) numericUpDown_kernelVersion.Value);
					break;
			}

			// Update UI
			ToggleUI();
		}

		
	}
}
