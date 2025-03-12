using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILGPUAudioTransformations
{
	public class GpuHandling
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- //
		private string Repopath;
		private ListBox LogBox;
		private ComboBox DevicesCombo;
		private Label VramLabel;
		private ProgressBar VramPbar;

		public int DeviceId = 0;
		public Context Ctx = Context.CreateDefault();
		public CudaAccelerator? Acc = null;
		public CudaDevice? Dev = null;


		public int LogInterval = 50;


		// ----- ----- ----- OBJECTS ----- ----- ----- //
		public GpuMemoryHandling? GpuMemoryH = null;
		public GpuTransformHandling? GpuTransformH = null;


		// ----- ----- ----- LAMBDA FUNCTIONS ----- ----- ----- //


		// ----- ----- ----- CONSTRUCTORS ----- ----- ----- //
		public GpuHandling(string repopath, ListBox? listBox_log = null, ComboBox? comboBox_cudaDevices = null, Label? label_cudaVram = null, ProgressBar? progressBar_cudaVram = null)
		{
			// Set attributes
			Repopath = repopath;
			this.LogBox = listBox_log ?? new ListBox();
			this.DevicesCombo = comboBox_cudaDevices ?? new ComboBox();
			this.VramLabel = label_cudaVram ?? new Label();
			this.VramPbar = progressBar_cudaVram ?? new ProgressBar();

			// Register events
			DevicesCombo.SelectedIndexChanged += (sender, e) => InitDevice(DevicesCombo.SelectedIndex);

			// Fill devices
			FillDevices();
		}





		// ----- ----- ----- METHODS ----- ----- ----- //
		// ----- Context etc. ----- \\
		public void Log(string message, string inner = "", int layer = 1, bool update = false)
		{
			string msg = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
			msg += "<GPU>";

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

		public void FillDevices(bool silent = false)
		{
			// Clear devices
			DevicesCombo.Items.Clear();

			try
			{
				// Try to get devices where type is Cuda
				foreach (var dev in Ctx.Devices)
				{
					if (dev is CudaDevice cudaDev)
					{
						DevicesCombo.Items.Add(cudaDev.Name);
					}
				}

				// Add no CUDA entry
				DevicesCombo.Items.Add("No CUDA");

				// Select DeviceId
				DevicesCombo.SelectedIndex = Math.Min(DeviceId, DevicesCombo.Items.Count - 1);

				// Log
				if (!silent)
				{
					Log("Devices filled", "count: " + (DevicesCombo.Items.Count - 1), 1);
				}
			}
			catch (Exception e)
			{
				Log("Error filling devices", e.Message, 1);
			}
		}

		public void InitDevice(int index)
		{
			// Dispose previous
			Dispose(true);

			// Set device
			DeviceId = index;

			// If invalid index, return
			if (index < 0 || index >= DevicesCombo.Items.Count - 1)
			{
				return;
			}

			// Try set accelerator
			try
			{
				Acc = Ctx.CreateCudaAccelerator(DeviceId);
				Dev = Acc.Device;

				// Create objects
				GpuMemoryH = new GpuMemoryHandling(this, LogBox);
				GpuTransformH = new GpuTransformHandling(GpuMemoryH, LogBox);

				// Log
				Log("Device initialized", Dev.PCIDeviceId + ":" + Dev.Name, 1);
			}
			catch (Exception e)
			{
				// Log
				Log("Error initializing device", e.Message, 1);
			}

			// Update VRAM
			UpdateVram();
		}

		public void Dispose(bool silent = false)
		{
			// Dispose objects
			GpuMemoryH?.Dispose();
			GpuMemoryH = null;
			GpuTransformH?.Dispose();
			GpuTransformH = null;

			// Dispose context etc.
			Dev = null;

			Acc?.Dispose();
			Acc = null;

			Ctx.Dispose();
			Ctx = Context.CreateDefault();

			if (!silent)
			{
				Log("Context etc. disposed", "", 1);
			}
		}


		// ----- Info ----- \\
		public void UpdateVram()
		{
			// Null if no CUDA
			if (Dev == null || Acc == null)
			{
				VramLabel.Text = "VRAM: 0 / 0 MB";
				VramPbar.Maximum = 100;
				VramPbar.Value = 0;
				return;
			}

			// Get VRAM
			int total = (int) (Dev.MemorySize / 1024 / 1024);
			int free = (int) (Acc.GetFreeMemory() / 1024 / 1024);
			int used = total - free;

			// Update
			VramLabel.Text = "VRAM: " + used + " / " + total + " MB";
			VramPbar.Maximum = total;
			VramPbar.Value = used;
		}


		// ----- Buffers ----- \\








		// ----- ----- ----- EVENTS ----- ----- ----- //

	}
}
