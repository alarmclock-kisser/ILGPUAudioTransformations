namespace ILGPUAudioTransformations
{
    partial class MainView
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

		#region Windows Form Designer generated code

		/// <summary>
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			listBox_tracks = new ListBox();
			listBox_log = new ListBox();
			pictureBox_waveform = new PictureBox();
			groupBox_controls = new GroupBox();
			button_levelVolume = new Button();
			numericUpDown_normalize = new NumericUpDown();
			label_loggingFreq = new Label();
			numericUpDown_loggingFreq = new NumericUpDown();
			button_colorBack = new Button();
			button_colorGraph = new Button();
			button_exportWav = new Button();
			button_normalize = new Button();
			textBox_timestamp = new TextBox();
			button_playback = new Button();
			hScrollBar_offset = new HScrollBar();
			comboBox_cudaDevices = new ComboBox();
			label_cudaVram = new Label();
			progressBar_cudaVram = new ProgressBar();
			label_trackMeta = new Label();
			groupBox_move = new GroupBox();
			label_chunkSize = new Label();
			numericUpDown_chunkSize = new NumericUpDown();
			button_move = new Button();
			groupBox_transform = new GroupBox();
			comboBox_cudaTransformations = new ComboBox();
			button_transform = new Button();
			groupBox_kernels = new GroupBox();
			button_bpmFit = new Button();
			numericUpDown_goalBpm = new NumericUpDown();
			comboBox_kernels = new ComboBox();
			label_param1 = new Label();
			numericUpDown_param1 = new NumericUpDown();
			button_run = new Button();
			label_bpm = new Label();
			((System.ComponentModel.ISupportInitialize) pictureBox_waveform).BeginInit();
			groupBox_controls.SuspendLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_normalize).BeginInit();
			((System.ComponentModel.ISupportInitialize) numericUpDown_loggingFreq).BeginInit();
			groupBox_move.SuspendLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_chunkSize).BeginInit();
			groupBox_transform.SuspendLayout();
			groupBox_kernels.SuspendLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_goalBpm).BeginInit();
			((System.ComponentModel.ISupportInitialize) numericUpDown_param1).BeginInit();
			SuspendLayout();
			// 
			// listBox_tracks
			// 
			listBox_tracks.FormattingEnabled = true;
			listBox_tracks.ItemHeight = 15;
			listBox_tracks.Location = new Point(1472, 750);
			listBox_tracks.Name = "listBox_tracks";
			listBox_tracks.Size = new Size(300, 199);
			listBox_tracks.TabIndex = 0;
			// 
			// listBox_log
			// 
			listBox_log.FormattingEnabled = true;
			listBox_log.ItemHeight = 15;
			listBox_log.Location = new Point(12, 750);
			listBox_log.Name = "listBox_log";
			listBox_log.Size = new Size(1000, 199);
			listBox_log.TabIndex = 1;
			// 
			// pictureBox_waveform
			// 
			pictureBox_waveform.Location = new Point(12, 527);
			pictureBox_waveform.Name = "pictureBox_waveform";
			pictureBox_waveform.Size = new Size(1000, 200);
			pictureBox_waveform.TabIndex = 2;
			pictureBox_waveform.TabStop = false;
			// 
			// groupBox_controls
			// 
			groupBox_controls.Controls.Add(button_levelVolume);
			groupBox_controls.Controls.Add(numericUpDown_normalize);
			groupBox_controls.Controls.Add(label_loggingFreq);
			groupBox_controls.Controls.Add(numericUpDown_loggingFreq);
			groupBox_controls.Controls.Add(button_colorBack);
			groupBox_controls.Controls.Add(button_colorGraph);
			groupBox_controls.Controls.Add(button_exportWav);
			groupBox_controls.Controls.Add(button_normalize);
			groupBox_controls.Controls.Add(textBox_timestamp);
			groupBox_controls.Controls.Add(button_playback);
			groupBox_controls.Location = new Point(1472, 607);
			groupBox_controls.Name = "groupBox_controls";
			groupBox_controls.Size = new Size(300, 120);
			groupBox_controls.TabIndex = 3;
			groupBox_controls.TabStop = false;
			groupBox_controls.Text = "Controls";
			// 
			// button_levelVolume
			// 
			button_levelVolume.Location = new Point(6, 51);
			button_levelVolume.Name = "button_levelVolume";
			button_levelVolume.Size = new Size(75, 23);
			button_levelVolume.TabIndex = 12;
			button_levelVolume.Text = "Level vol.";
			button_levelVolume.UseVisualStyleBackColor = true;
			button_levelVolume.Click += button_levelVolume_Click;
			// 
			// numericUpDown_normalize
			// 
			numericUpDown_normalize.DecimalPlaces = 3;
			numericUpDown_normalize.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
			numericUpDown_normalize.Location = new Point(87, 22);
			numericUpDown_normalize.Maximum = new decimal(new int[] { 4, 0, 0, 0 });
			numericUpDown_normalize.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
			numericUpDown_normalize.Name = "numericUpDown_normalize";
			numericUpDown_normalize.Size = new Size(67, 23);
			numericUpDown_normalize.TabIndex = 11;
			numericUpDown_normalize.Value = new decimal(new int[] { 1, 0, 0, 0 });
			// 
			// label_loggingFreq
			// 
			label_loggingFreq.AutoSize = true;
			label_loggingFreq.Location = new Point(214, 44);
			label_loggingFreq.Name = "label_loggingFreq";
			label_loggingFreq.Size = new Size(78, 15);
			label_loggingFreq.TabIndex = 11;
			label_loggingFreq.Text = "Logging freq.";
			// 
			// numericUpDown_loggingFreq
			// 
			numericUpDown_loggingFreq.Increment = new decimal(new int[] { 100, 0, 0, 0 });
			numericUpDown_loggingFreq.Location = new Point(214, 62);
			numericUpDown_loggingFreq.Maximum = new decimal(new int[] { 25000, 0, 0, 0 });
			numericUpDown_loggingFreq.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
			numericUpDown_loggingFreq.Name = "numericUpDown_loggingFreq";
			numericUpDown_loggingFreq.Size = new Size(80, 23);
			numericUpDown_loggingFreq.TabIndex = 10;
			numericUpDown_loggingFreq.Value = new decimal(new int[] { 100, 0, 0, 0 });
			numericUpDown_loggingFreq.ValueChanged += numericUpDown_loggingFreq_ValueChanged;
			// 
			// button_colorBack
			// 
			button_colorBack.Location = new Point(271, 22);
			button_colorBack.Name = "button_colorBack";
			button_colorBack.Size = new Size(23, 23);
			button_colorBack.TabIndex = 9;
			button_colorBack.Text = "B";
			button_colorBack.UseVisualStyleBackColor = true;
			// 
			// button_colorGraph
			// 
			button_colorGraph.BackColor = SystemColors.HotTrack;
			button_colorGraph.ForeColor = Color.White;
			button_colorGraph.Location = new Point(242, 22);
			button_colorGraph.Name = "button_colorGraph";
			button_colorGraph.Size = new Size(23, 23);
			button_colorGraph.TabIndex = 8;
			button_colorGraph.Text = "G";
			button_colorGraph.UseVisualStyleBackColor = false;
			// 
			// button_exportWav
			// 
			button_exportWav.Location = new Point(214, 91);
			button_exportWav.Name = "button_exportWav";
			button_exportWav.Size = new Size(80, 23);
			button_exportWav.TabIndex = 5;
			button_exportWav.Text = "Export WAV\r\n";
			button_exportWav.UseVisualStyleBackColor = true;
			// 
			// button_normalize
			// 
			button_normalize.Location = new Point(6, 22);
			button_normalize.Name = "button_normalize";
			button_normalize.Size = new Size(75, 23);
			button_normalize.TabIndex = 7;
			button_normalize.Text = "Normalize";
			button_normalize.UseVisualStyleBackColor = true;
			button_normalize.Click += button_normalize_Click;
			// 
			// textBox_timestamp
			// 
			textBox_timestamp.Location = new Point(35, 91);
			textBox_timestamp.Name = "textBox_timestamp";
			textBox_timestamp.ReadOnly = true;
			textBox_timestamp.Size = new Size(80, 23);
			textBox_timestamp.TabIndex = 6;
			// 
			// button_playback
			// 
			button_playback.Location = new Point(6, 91);
			button_playback.Name = "button_playback";
			button_playback.Size = new Size(23, 23);
			button_playback.TabIndex = 5;
			button_playback.Text = ">";
			button_playback.UseVisualStyleBackColor = true;
			// 
			// hScrollBar_offset
			// 
			hScrollBar_offset.Location = new Point(12, 730);
			hScrollBar_offset.Name = "hScrollBar_offset";
			hScrollBar_offset.Size = new Size(1000, 17);
			hScrollBar_offset.TabIndex = 4;
			// 
			// comboBox_cudaDevices
			// 
			comboBox_cudaDevices.FormattingEnabled = true;
			comboBox_cudaDevices.Location = new Point(12, 12);
			comboBox_cudaDevices.Name = "comboBox_cudaDevices";
			comboBox_cudaDevices.Size = new Size(300, 23);
			comboBox_cudaDevices.TabIndex = 5;
			// 
			// label_cudaVram
			// 
			label_cudaVram.AutoSize = true;
			label_cudaVram.Location = new Point(12, 38);
			label_cudaVram.Name = "label_cudaVram";
			label_cudaVram.Size = new Size(90, 15);
			label_cudaVram.TabIndex = 6;
			label_cudaVram.Text = "VRAM: 0 / 0 MB";
			// 
			// progressBar_cudaVram
			// 
			progressBar_cudaVram.Location = new Point(12, 56);
			progressBar_cudaVram.Name = "progressBar_cudaVram";
			progressBar_cudaVram.Size = new Size(300, 12);
			progressBar_cudaVram.TabIndex = 7;
			// 
			// label_trackMeta
			// 
			label_trackMeta.AutoSize = true;
			label_trackMeta.Location = new Point(1472, 732);
			label_trackMeta.Name = "label_trackMeta";
			label_trackMeta.Size = new Size(91, 15);
			label_trackMeta.TabIndex = 8;
			label_trackMeta.Text = "Track meta data";
			// 
			// groupBox_move
			// 
			groupBox_move.Controls.Add(label_chunkSize);
			groupBox_move.Controls.Add(numericUpDown_chunkSize);
			groupBox_move.Controls.Add(button_move);
			groupBox_move.Location = new Point(1472, 527);
			groupBox_move.Name = "groupBox_move";
			groupBox_move.Size = new Size(300, 74);
			groupBox_move.TabIndex = 9;
			groupBox_move.TabStop = false;
			groupBox_move.Text = "CUDA move";
			// 
			// label_chunkSize
			// 
			label_chunkSize.AutoSize = true;
			label_chunkSize.Location = new Point(87, 27);
			label_chunkSize.Name = "label_chunkSize";
			label_chunkSize.Size = new Size(67, 15);
			label_chunkSize.TabIndex = 2;
			label_chunkSize.Text = "Chunk size:\r\n";
			// 
			// numericUpDown_chunkSize
			// 
			numericUpDown_chunkSize.Location = new Point(87, 45);
			numericUpDown_chunkSize.Maximum = new decimal(new int[] { 16777216, 0, 0, 0 });
			numericUpDown_chunkSize.Minimum = new decimal(new int[] { 1024, 0, 0, 0 });
			numericUpDown_chunkSize.Name = "numericUpDown_chunkSize";
			numericUpDown_chunkSize.Size = new Size(120, 23);
			numericUpDown_chunkSize.TabIndex = 1;
			numericUpDown_chunkSize.Value = new decimal(new int[] { 16384, 0, 0, 0 });
			numericUpDown_chunkSize.ValueChanged += numericUpDown_chunkSize_ValueChanged;
			// 
			// button_move
			// 
			button_move.Location = new Point(6, 45);
			button_move.Name = "button_move";
			button_move.Size = new Size(75, 23);
			button_move.TabIndex = 0;
			button_move.Text = "Move";
			button_move.UseVisualStyleBackColor = true;
			button_move.Click += button_move_Click;
			// 
			// groupBox_transform
			// 
			groupBox_transform.Controls.Add(comboBox_cudaTransformations);
			groupBox_transform.Controls.Add(button_transform);
			groupBox_transform.Location = new Point(1472, 447);
			groupBox_transform.Name = "groupBox_transform";
			groupBox_transform.Size = new Size(300, 74);
			groupBox_transform.TabIndex = 10;
			groupBox_transform.TabStop = false;
			groupBox_transform.Text = "CUDA transformations";
			// 
			// comboBox_cudaTransformations
			// 
			comboBox_cudaTransformations.FormattingEnabled = true;
			comboBox_cudaTransformations.Items.AddRange(new object[] { "FFT", "IFFT", "FFTW", "IFFTW" });
			comboBox_cudaTransformations.Location = new Point(87, 45);
			comboBox_cudaTransformations.Name = "comboBox_cudaTransformations";
			comboBox_cudaTransformations.Size = new Size(207, 23);
			comboBox_cudaTransformations.TabIndex = 11;
			comboBox_cudaTransformations.SelectedIndexChanged += comboBox_cudaTransformations_SelectedIndexChanged;
			// 
			// button_transform
			// 
			button_transform.Location = new Point(6, 45);
			button_transform.Name = "button_transform";
			button_transform.Size = new Size(75, 23);
			button_transform.TabIndex = 11;
			button_transform.Text = "Transform\r\n";
			button_transform.UseVisualStyleBackColor = true;
			button_transform.Click += button_transform_Click;
			// 
			// groupBox_kernels
			// 
			groupBox_kernels.Controls.Add(button_bpmFit);
			groupBox_kernels.Controls.Add(numericUpDown_goalBpm);
			groupBox_kernels.Controls.Add(comboBox_kernels);
			groupBox_kernels.Controls.Add(label_param1);
			groupBox_kernels.Controls.Add(numericUpDown_param1);
			groupBox_kernels.Controls.Add(button_run);
			groupBox_kernels.Location = new Point(1472, 321);
			groupBox_kernels.Name = "groupBox_kernels";
			groupBox_kernels.Size = new Size(300, 120);
			groupBox_kernels.TabIndex = 11;
			groupBox_kernels.TabStop = false;
			groupBox_kernels.Text = "CUDA kernels";
			// 
			// button_bpmFit
			// 
			button_bpmFit.Location = new Point(219, 51);
			button_bpmFit.Name = "button_bpmFit";
			button_bpmFit.Size = new Size(75, 23);
			button_bpmFit.TabIndex = 13;
			button_bpmFit.Text = "BPM fit";
			button_bpmFit.UseVisualStyleBackColor = true;
			button_bpmFit.Click += button_bpmFit_Click;
			// 
			// numericUpDown_goalBpm
			// 
			numericUpDown_goalBpm.DecimalPlaces = 2;
			numericUpDown_goalBpm.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
			numericUpDown_goalBpm.Location = new Point(140, 51);
			numericUpDown_goalBpm.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
			numericUpDown_goalBpm.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
			numericUpDown_goalBpm.Name = "numericUpDown_goalBpm";
			numericUpDown_goalBpm.Size = new Size(73, 23);
			numericUpDown_goalBpm.TabIndex = 13;
			numericUpDown_goalBpm.Value = new decimal(new int[] { 180, 0, 0, 0 });
			// 
			// comboBox_kernels
			// 
			comboBox_kernels.FormattingEnabled = true;
			comboBox_kernels.Items.AddRange(new object[] { "F: Normalize", "C: TimeStretch" });
			comboBox_kernels.Location = new Point(6, 22);
			comboBox_kernels.Name = "comboBox_kernels";
			comboBox_kernels.Size = new Size(288, 23);
			comboBox_kernels.TabIndex = 12;
			comboBox_kernels.SelectedIndexChanged += comboBox_kernels_SelectedIndexChanged;
			// 
			// label_param1
			// 
			label_param1.AutoSize = true;
			label_param1.Location = new Point(6, 73);
			label_param1.Name = "label_param1";
			label_param1.Size = new Size(80, 15);
			label_param1.TabIndex = 2;
			label_param1.Text = "Parameter #1:";
			// 
			// numericUpDown_param1
			// 
			numericUpDown_param1.DecimalPlaces = 12;
			numericUpDown_param1.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
			numericUpDown_param1.Location = new Point(6, 91);
			numericUpDown_param1.Maximum = new decimal(new int[] { 9999, 0, 0, 196608 });
			numericUpDown_param1.Name = "numericUpDown_param1";
			numericUpDown_param1.Size = new Size(120, 23);
			numericUpDown_param1.TabIndex = 1;
			numericUpDown_param1.Value = new decimal(new int[] { 1, 0, 0, 0 });
			// 
			// button_run
			// 
			button_run.Location = new Point(219, 91);
			button_run.Name = "button_run";
			button_run.Size = new Size(75, 23);
			button_run.TabIndex = 0;
			button_run.Text = "Run kernel";
			button_run.UseVisualStyleBackColor = true;
			button_run.Click += button_run_Click;
			// 
			// label_bpm
			// 
			label_bpm.AutoSize = true;
			label_bpm.Location = new Point(1686, 732);
			label_bpm.Name = "label_bpm";
			label_bpm.Size = new Size(43, 15);
			label_bpm.TabIndex = 12;
			label_bpm.Text = "BPM: -";
			// 
			// MainView
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1784, 961);
			Controls.Add(label_bpm);
			Controls.Add(groupBox_kernels);
			Controls.Add(groupBox_transform);
			Controls.Add(groupBox_move);
			Controls.Add(label_trackMeta);
			Controls.Add(progressBar_cudaVram);
			Controls.Add(label_cudaVram);
			Controls.Add(comboBox_cudaDevices);
			Controls.Add(hScrollBar_offset);
			Controls.Add(groupBox_controls);
			Controls.Add(pictureBox_waveform);
			Controls.Add(listBox_log);
			Controls.Add(listBox_tracks);
			MaximizeBox = false;
			MaximumSize = new Size(1800, 1000);
			MinimumSize = new Size(1800, 1000);
			Name = "MainView";
			Text = "ILGPU Audio Tranformations (WinForms)";
			((System.ComponentModel.ISupportInitialize) pictureBox_waveform).EndInit();
			groupBox_controls.ResumeLayout(false);
			groupBox_controls.PerformLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_normalize).EndInit();
			((System.ComponentModel.ISupportInitialize) numericUpDown_loggingFreq).EndInit();
			groupBox_move.ResumeLayout(false);
			groupBox_move.PerformLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_chunkSize).EndInit();
			groupBox_transform.ResumeLayout(false);
			groupBox_kernels.ResumeLayout(false);
			groupBox_kernels.PerformLayout();
			((System.ComponentModel.ISupportInitialize) numericUpDown_goalBpm).EndInit();
			((System.ComponentModel.ISupportInitialize) numericUpDown_param1).EndInit();
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private ListBox listBox_tracks;
		private ListBox listBox_log;
		private PictureBox pictureBox_waveform;
		private GroupBox groupBox_controls;
		private Button button_playback;
		private HScrollBar hScrollBar_offset;
		private Button button_exportWav;
		private Button button_normalize;
		private TextBox textBox_timestamp;
		private ComboBox comboBox_cudaDevices;
		private Label label_cudaVram;
		private ProgressBar progressBar_cudaVram;
		private Button button_colorGraph;
		private Button button_colorBack;
		private Label label_trackMeta;
		private GroupBox groupBox_move;
		private Label label_chunkSize;
		private NumericUpDown numericUpDown_chunkSize;
		private Button button_move;
		private GroupBox groupBox_transform;
		private ComboBox comboBox_cudaTransformations;
		private Button button_transform;
		private Label label_loggingFreq;
		private NumericUpDown numericUpDown_loggingFreq;
		private NumericUpDown numericUpDown_normalize;
		private GroupBox groupBox_kernels;
		private ComboBox comboBox_kernels;
		private Label label_param1;
		private NumericUpDown numericUpDown_param1;
		private Button button_run;
		private Button button_levelVolume;
		private Label label_bpm;
		private Button button_bpmFit;
		private NumericUpDown numericUpDown_goalBpm;
	}
}
