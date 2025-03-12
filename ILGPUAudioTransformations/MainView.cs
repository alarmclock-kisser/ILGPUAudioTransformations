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
		public bool DataTransformed => GpuH.GpuMemoryH != null && AudioH.CurrentTrack?.Floats.Length == 0 && AudioH.CurrentTrack?.Pointer != 0 && GpuH.GpuMemoryH.ComplexBuffers.ContainsKey(AudioH.CurrentTrack?.Pointer ?? 0);

		public string TransformMode => DataOnCuda ? comboBox_cudaTransformations.SelectedItem?.ToString() ?? "None" : "None";
		public string KernelName => SelectedTrack != null ? comboBox_kernels.SelectedItem?.ToString()?.Replace("F: ", "").Replace("C: ", "") ?? "None" : "None";


		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
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


			// Init. audio files
			ImportResourcesAudios();
		}







		// ----- ----- ----- METHODS ----- ----- ----- //
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
			SelectedTrack?.DrawWaveformSmooth(pictureBox_waveform, 0, 1024, button_colorGraph.BackColor, button_colorBack.BackColor);

			// BPM tag
			CurrentBpm = AudioHandling.UpdateId3Tag(SelectedTrack?.Filepath ?? "", label_bpm);

			// Playback button
			button_playback.Enabled = SelectedTrack != null;

			// Move button
			button_move.Enabled = SelectedTrack != null && (DataOnHost || DataOnCuda);
			button_move.Text = DataOnHost ? "-> CUDA" : "Host <-";

			// Transform button
			button_transform.Enabled = SelectedTrack != null && DataOnCuda;
			button_transform.Text = DataTransformed ? "Re-Transform" : "Transform";

			// Normalize button
			button_normalize.Enabled = SelectedTrack != null && (DataOnHost || DataOnCuda);

			// Run button
			button_run.Enabled = SelectedTrack != null && DataOnCuda && KernelName != "None";
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







		// ----- ----- ----- EVENTS ----- ----- ----- //
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
			// Update UI
			ToggleUI();
		}

		private void comboBox_cudaTransformations_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Toggle UI
			ToggleUI();
		}

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

			// Update UI
			ToggleUI();
		}

		private void button_move_Click(object sender, EventArgs e)
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
				SelectedTrack.Pointer = GpuH.GpuMemoryH.PushChunks(SelectedTrack.MakeChunks((int) (numericUpDown_chunkSize.Value), (int) (numericUpDown_chunkSize.Value / 2)));
				SelectedTrack.Floats = [];
			}
			else if (DataOnCuda)
			{
				// CUDA -> Host
				SelectedTrack.AggregateChunks(GpuH.GpuMemoryH.PullChunks(SelectedTrack.Pointer));
				SelectedTrack.Pointer = 0;
			}

			// Update UI
			ToggleUI();
		}

		private void button_transform_Click(object sender, EventArgs e)
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
					SelectedTrack.Pointer = GpuH.GpuTransformH.PerformFFT(SelectedTrack.Pointer);
					break;
				case "IFFT":
					// IFFT transformation
					SelectedTrack.Pointer = GpuH.GpuTransformH.PerformIFFT(SelectedTrack.Pointer);
					break;
				case "FFTW":
					// TFT transformation
					SelectedTrack.Pointer = GpuH.GpuTransformH.PerformFFTW(SelectedTrack.Pointer);
					break;
				case "IFFTW":
					// ISTFT transformation
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
					GpuH.GpuKernelH.TimeStretch(SelectedTrack.Pointer, (float) numericUpDown_param1.Value);
					break;
			}

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

			SelectedTrack.NormalizeVolume((float) numericUpDown_normalize.Value);

			// Update UI
			ToggleUI();
		}
	}
}
