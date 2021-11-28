using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore;
using FoenixCore.Display;
using FoenixCore.Simulator.Devices;
using FoenixCore.Simulator.Devices.SDCard;
using FoenixCore.Simulator.FileFormat;
using FoenixCore.MemoryLocations;
using FoenixToolkit.Display;


namespace FoenixToolkit.UI
{
    class MainWindow : Window
    {
        public static MainWindow Instance = null;
        private FoenixSystem kernel;

        //  Windows
        public Cpu68000Window cpu68000Window;
        public MemoryWindow memoryWindow;
        public UploaderWindow uploaderWindow;
        private readonly WatchWindow watchWindow = new();
        private readonly SDCardWindow sdCardWindow = new();
        private TileEditorWindow tileEditor;
        private CharEditorWindow charEditor;
        public SerialTerminalWindow terminal;
        private readonly JoystickWindow joystickWindow = new();
        private readonly GameGeneratorWindow GGF = new();

        // Local variables and events
        private byte previousGraphicMode;
        private delegate void TileClickEvent(Point tile);
        public delegate void TileLoadedEvent(int layer);

#pragma warning disable CS0649  // never assigned
        private TileClickEvent TileClicked;
#pragma warning restore CS0649

        private readonly ResourceChecker ResChecker = new();
        private delegate void TransmitByteFunction(byte Value);
        private delegate void ShowFormFunction();
        private readonly String defaultKernel = "roms/kernel_A2560U.srec";
        private readonly int jumpStartAddress;
        private readonly bool disabledIRQs = false;
        private readonly bool autoRun = true;
        private BoardVersion version = BoardVersion.A2560U;
        private delegate void WriteCPSFPSFunction(string CPS, string FPS);
        private bool fullScreen = false;
        const double toRadians = 0.017453293;

        private Cairo.Color red = new(Color.Red.R / 255.0, Color.Red.G / 255.0, Color.Red.B / 255.0);
        private Cairo.Color lightGray = new(Color.LightGray.R / 255.0, Color.LightGray.G / 255.0, Color.LightGray.B / 255.0);
        private Cairo.Color darkGray = new(Color.DarkGray.R / 255.0, Color.DarkGray.G / 255.0, Color.DarkGray.B / 255.0);
        private Cairo.Color darkSlateGray = new(Color.DarkSlateGray.R / 255.0, Color.DarkSlateGray.G / 255.0, Color.DarkSlateGray.B / 255.0);
        private Cairo.Color white = new(Color.White.R / 255.0, Color.White.G / 255.0, Color.White.B / 255.0);

#pragma warning disable CS0649  // never assigned
        [GUI] ActionBar mainActionBar;
        [GUI] CheckMenuItem menuSettingsAutorun;
        [GUI] DrawingArea daDipSwitch;
        [GUI] EventBox evtGpu;
        [GUI] Label lblRevision;
        [GUI] Label lblLastKey;
        [GUI] Label lblCpsPerf;
        [GUI] Label lblFpsPerf;
        [GUI] Label lblSDCardPath;
        [GUI] GpuControl ucGpu;
        [GUI] MenuBar mainMenuBar;
        [GUI] MenuItem menuResetRestart;
        [GUI] MenuItem menuResetDebug;
        [GUI] MenuItem menuWindowsMemory;
#pragma warning restore CS0649

        public MainWindow(Dictionary<string, string> context) : this(new Builder("MainWindow.ui"))
        {
            if (context != null)
            {
                if (context.ContainsKey("jumpStartAddress"))
                    jumpStartAddress = int.Parse(context["jumpStartAddress"]);
                if (context.ContainsKey("defaultKernel"))
                    defaultKernel = context["defaultKernel"];
                if (context.ContainsKey("autoRun"))
                    autoRun = "true".Equals(context["autoRun"]);
                if (context.ContainsKey("disabledIRQs"))
                    disabledIRQs = "true".Equals(context["disabledIRQs"]);

                if (context.ContainsKey("version"))
                {
                    if (context["version"] == "A2560U")
                        version = BoardVersion.A2560U;
                    else if (context["version"] == "A2560K")
                        version = BoardVersion.A2560K;
                }
            }

            // If the user didn't specify context switches, read the ini setting
            if (context == null)
            {
                autoRun = false;
                version = BoardVersion.A2560U;
                string versionText = "A2560U";

                //-- autoRun = Simulator.Properties.Settings.Default.Autorun;
                // versionText = Simulator.Properties.Settings.Default.BoardRevision;

                switch (versionText)
                {
                    case "A2560U":
                        version = BoardVersion.A2560U;
                        break;
                    case "A2560K":
                        version = BoardVersion.A2560K;
                        break;
                }
            }

            kernel = new FoenixSystem(version, defaultKernel);

            ucGpu = new() {
                VRAM = kernel.MemMgr.VIDEO,
                RAM = kernel.MemMgr.RAM,
                VICKY = kernel.MemMgr.VICKY
            };
            evtGpu.Add(ucGpu);
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
        }

        private void DisplayBoardVersion()
        {
            //-- string shortVersion = "U";

            if (version == BoardVersion.A2560U)
            {
                lblRevision.Text = "A2560U";
                //-- shortVersion = "U";
            }
            else if (version == BoardVersion.A2560K)
            {
                lblRevision.Text = "A2560K";
                //-- shortVersion = "K";
            }

            // force repaint
            mainActionBar.QueueDraw();

            //-- Simulator.Properties.Settings.Default.BoardRevision = shortVersion;
            // Simulator.Properties.Settings.Default.Save();
        }

        private void LoadSrecFile(string Filename)
        {
            if (cpu68000Window != null)
                cpu68000Window.Pause();

            kernel.SetVersion(version);

            if (kernel.ResetCPU(Filename))
            {
                ucGpu.QueueDraw();

                if (kernel.lstFile != null)
                {
                    ShowCpu68000Window();
                    ShowMemoryWindow();
                }

                ResetSDCard();

                if (cpu68000Window != null)
                    cpu68000Window.ClearTrace();
            }
        }

        private void LoadHexFile(string Filename)
        {
            if (cpu68000Window != null)
                cpu68000Window.Pause();

            kernel.SetVersion(version);

            if (kernel.ResetCPU(Filename))
            {
                ucGpu.QueueDraw();

                if (kernel.lstFile != null)
                {
                    ShowCpu68000Window();
                    ShowMemoryWindow();
                }

                ResetSDCard();

                if (cpu68000Window != null)
                    cpu68000Window.ClearTrace();
            }
        }

        private void ResetSDCard()
        {
            string path = sdCardWindow.GetPath();
            int capacity = sdCardWindow.GetCapacity();
            int clusterSize = sdCardWindow.GetClusterSize();
            string fsType = sdCardWindow.GetFSType();
            bool ISOMode = sdCardWindow.GetISOMode();

            kernel.MemMgr.SDCARD.SDCardPath = path;

            byte sdCardStat = 0;

            if (path == null || path.Length == 0)
            {
                lblSDCardPath.Text = "SD Card Disabled";
                kernel.MemMgr.SDCARD.isPresent = false;
            }
            else
            {
                lblSDCardPath.Text = $"SDC: {path}";
                kernel.MemMgr.SDCARD.isPresent = true;
                kernel.MemMgr.SDCARD.IsoMode = ISOMode;
                sdCardStat = 1;

                kernel.MemMgr.SDCARD.Capacity = capacity;
                kernel.MemMgr.SDCARD.ClusterSize = clusterSize;

                if ("FAT12".Equals(fsType))
                    kernel.MemMgr.SDCARD.FileSystemType = FSType.FAT12;
                else if ("FAT16".Equals(fsType))
                    kernel.MemMgr.SDCARD.FileSystemType = FSType.FAT16;
                else if ("FAT32".Equals(fsType))
                    kernel.MemMgr.SDCARD.FileSystemType = FSType.FAT32;

                kernel.MemMgr.SDCARD.ResetMbrBootSector();
            }

            if (typeof(CH376SRegister) == kernel.MemMgr.SDCARD.GetType())
                kernel.MemMgr.WriteByte(MemoryMap.SDCARD_STAT, sdCardStat);
        }

        private void ShowCpu68000Window()
        {
            if (cpu68000Window == null || !cpu68000Window.IsRealized)
            {
                kernel.m68000Cpu.DebugPause = true;

                cpu68000Window = new Cpu68000Window();
                cpu68000Window.SetKernel(kernel);
            }

            cpu68000Window.Show();
        }

        void ShowUploaderWindow()
        {
            if (uploaderWindow == null || !uploaderWindow.IsRealized)
            {
                uploaderWindow = new UploaderWindow();
                uploaderWindow.SetKernel(kernel);
                uploaderWindow.SetBoardVersion(version);
            }

            uploaderWindow.Show();
        }

        private void ShowMemoryWindow()
        {
            menuWindowsMemory.Sensitive = true;

            if (memoryWindow == null || !memoryWindow.IsRealized)
            {
                memoryWindow = new MemoryWindow
                {
                    Memory = kernel.MemMgr,
                };
            }

            memoryWindow.Show();

            memoryWindow.UpdateMCRButtons();
            memoryWindow.SetGamma += UpdateGamma;
            memoryWindow.SetHiRes += UpdateHiRes;
        }

        private void SetDipSwitchMemory()
        {
            // if kernel memory is available, set the memory
            byte bootMode = (byte)((switches[0] ? 0 : 1) + (switches[1] ? 0 : 2));
            byte userMode = (byte)((switches[2] ? 0 : 1) + (switches[3] ? 0 : 2) + (switches[4] ? 0 : 4));

            if (kernel.MemMgr != null)
            {
                kernel.MemMgr.WriteByte(MemoryMap.DIP_BOOT_MODE, bootMode);
                kernel.MemMgr.WriteByte(MemoryMap.DIP_USER_MODE, userMode);

                // switch 5 - high-res mode
                byte hiRes = kernel.MemMgr.ReadByte(MemoryMap.VICKY_BASE_ADDR + 1);
                if (switches[4])
                    hiRes |= 1;
                else
                    hiRes &= 0xFE;

                kernel.MemMgr.WriteByte(MemoryMap.VICKY_BASE_ADDR + 1, hiRes);

                // switch 6 - Gamma
                byte MCR = kernel.MemMgr.ReadByte(MemoryMap.VICKY_BASE_ADDR);
                if (switches[6])
                    MCR |= 0x40;
                else
                    MCR &= 0b1011_1111;

                kernel.MemMgr.WriteByte(MemoryMap.VICKY_BASE_ADDR, MCR);
            }
        }

        private void Write_CPS_FPS_Safe(string CPS, string FPS)
        {
            lblCpsPerf.Text = CPS;
            lblFpsPerf.Text = FPS;
        }

        public void SerialTransmitByte(byte Value)
        {
            terminal.AppendContent(Convert.ToChar(Value));
        }

        public void SDCardInterrupt(CH376SInterrupt irq)
        {
            // Check if the SD Card interrupt is allowed
            byte mask = kernel.MemMgr.ReadByte(MemoryMap.INT_MASK_REG1);
            if (!kernel.m68000Cpu.DebugPause && (~mask & (byte)Register1.FNX1_INT07_SDCARD) == (byte)Register1.FNX1_INT07_SDCARD)
            {
                // Set the SD Card Interrupt
                byte IRQ1 = kernel.MemMgr.ReadByte(MemoryMap.INT_PENDING_REG1);
                IRQ1 |= (byte)Register1.FNX1_INT07_SDCARD;
                kernel.MemMgr.INTERRUPT.WriteFromGabe(1, IRQ1);
                kernel.m68000Cpu.Pins.IRQ = true;

                // Write the interrupt result
                kernel.MemMgr.SDCARD.WriteByte(0, (byte)irq);
            }
        }

        private void EnableMenuItems()
        {
            menuResetRestart.Sensitive = true;
            menuResetDebug.Sensitive = true;
        }

        private void FullScreenToggle()
        {
            if (fullScreen == false)
            {
                fullScreen = true;

                mainMenuBar.Visible = false;
                mainActionBar.Visible = false;

                if (cpu68000Window != null)
                    cpu68000Window.Visible = false;

                if (memoryWindow != null)
                    memoryWindow.Visible = false;

                fullScreen = true;
            }
            else
            {
                fullScreen = false;

                mainMenuBar.Visible = true;
                mainActionBar.Visible = true;

                if (cpu68000Window != null)
                {
                    cpu68000Window.Visible = true;
                    cpu68000Window.Show();
                    cpu68000Window.QueueDraw();
                }

                if (memoryWindow != null)
                {
                    memoryWindow.Visible = true;
                    memoryWindow.Show();
                    memoryWindow.QueueDraw();
                }

                GrabFocus();
                QueueDraw();
            }
        }

        public void UpdateGamma(bool gamma)
        {
            switches[6] = gamma;
            daDipSwitch.QueueDraw();
        }

        public void UpdateHiRes(bool hires)
        {
            switches[4] = hires;
            daDipSwitch.QueueDraw();
        }

        private void on_evtRevision_button_press_event(object sender, ButtonPressEventArgs e)
        {
            if (version == BoardVersion.A2560U)
                version = BoardVersion.A2560K;
            else if (version == BoardVersion.A2560K)
                version = BoardVersion.A2560U;

            kernel.SetVersion(version);
            if (uploaderWindow != null)
                uploaderWindow.SetBoardVersion(version);

            DisplayBoardVersion();

            // Reset the memory, keyboard, GABE and reload the program?
            if (cpu68000Window != null)
                cpu68000Window.Pause();

            QueueDraw();
            //-- MainWindow_Load(null, null);   // TODO: bad implementation
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            ucGpu.StartOfFrame = null;
            ucGpu.StartOfLine = null;

            if (cpu68000Window != null)
            {
                cpu68000Window.Close();
                cpu68000Window.Dispose();
                cpu68000Window.Destroy();
            }

            if (memoryWindow != null)
            {
                memoryWindow.Close();
                memoryWindow.Dispose();
                memoryWindow.Destroy();
            }

            if (GGF != null)
            {
                GGF.Close();
                GGF.Dispose();
                GGF.Destroy();
            }

            Close();
            Dispose();

            Application.Quit();
        }

        // File Menu
        private void on_menuFileExit_Activate(object sender, EventArgs e)
        {
            Application.Quit();
        }

        private void on_menuFileLoadSREC_activate(object sender, EventArgs e)
        {
            FileChooserDialog filechooser =
                new("Select a SREC File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

            FileFilter ff = new();
            ff.Name = "SREC Files";
            ff.AddPattern("*.srec");
            filechooser.AddFilter(ff);

            if (filechooser.Run() == (int)ResponseType.Accept) 
                LoadSrecFile(filechooser.Filename);

            ff.Dispose();
            filechooser.Destroy();
        }

        private void on_menuFileLoadHex_Activate(object sender, EventArgs e)
        {
            FileChooserDialog filechooser =
                new("Select a Hex File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

            FileFilter ff = new();
            ff.Name = "Hex Files";
            ff.AddPattern("*.hex");
            filechooser.AddFilter(ff);

            if (filechooser.Run() == (int)ResponseType.Accept) 
                LoadHexFile(filechooser.Filename);

            ff.Dispose();
            filechooser.Destroy();
        }

        /*
         * Read a Foenix XML file
         */
        private void on_menuFileLoadProject_Activate(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Load Project File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "Foenix Project File";
                    ff.AddPattern("*.fnxml");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        // TODO - this code is so coupled - we need to set the version in the XML file too.
                        kernel.Resources = ResChecker;
                        if (kernel.ResetCPU(filechooser.Filename))
                        {
                            ucGpu.QueueDraw();

                            cpu68000Window.Pause();

                            SetDipSwitchMemory();

                            ShowCpu68000Window();
                            ShowMemoryWindow();

                            EnableMenuItems();
                        }
                    }
                }
            }
        }

        /*
         * Export all memory content to an XML file.
         */
        private void on_menuFileSaveProject_Activate(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Save Project File", this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "Foenix Project File";
                    ff.AddPattern("*.fnxml");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        FoenixXmlFile fnxml = new(kernel, ResChecker);
                        fnxml.Write(filechooser.Filename, true);
                    }
                }
            }
        }

        private void on_menuFileLoadWatch_Activate(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Load Watch List File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "Foenix Watch List File";
                    ff.AddPattern("*.wlxml");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        FoenixXmlFile xmlFile = new(kernel, null);
                        xmlFile.ReadWatches(filechooser.Filename);

                        watchWindow.SetKernel(kernel);
                        if (!watchWindow.Visible)
                            watchWindow.Show();
                    }
                }
            }
        }

        private void on_menuFileSaveWatch_Activate(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Save Watch List File", this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "Foenix Watch List File";
                    ff.AddPattern("*.wlxml");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        FoenixXmlFile xmlFile = new(kernel, null);
                        xmlFile.WriteWatches(filechooser.Filename);
                    }
                }
            }
        }

        // Tools Menu
        private void on_menuToolsUploader_Activate(object sender, EventArgs e)
        {
            ShowUploaderWindow();
        }

        /*
         * Loading image into memory requires the user to specify what kind of image (tile, bitmap, sprite).
         * What address location in video RAM.
         */
        private void on_menuToolsAssetLoader_Activate(object sender, EventArgs e)
        {
            AssetLoaderWindow dialog = new()
            {
                MemMgrRef = kernel.MemMgr,
                ResChecker = ResChecker
            };

            dialog.Show();
        }

        private void on_menuToolsSDC_Activate(object sender, EventArgs e)
        {
            if (kernel.MemMgr != null)
            {
                sdCardWindow.SetPath(kernel.MemMgr.SDCARD.SDCardPath);
                sdCardWindow.SetCapacity(kernel.MemMgr.SDCARD.Capacity);
                sdCardWindow.SetClusterSize(kernel.MemMgr.SDCARD.ClusterSize);
                sdCardWindow.SetFSType(kernel.MemMgr.SDCARD.FileSystemType.ToString());

                sdCardWindow.Show();

                ResetSDCard();
            }
        }

        private void on_menuToolsJoystickSim_Activate(object sender, EventArgs e)
        {
            JoystickWindow dialog = new();
            dialog.Show();
        }

        private void on_menuToolsTileEditor_Activate(object sender, EventArgs e)
        {
            TileEditorWindow dialog = new();
            dialog.Show();

            if (tileEditor == null)
            {
                tileEditor = new TileEditorWindow();
                tileEditor.SetMemory(kernel.MemMgr);

                ucGpu.TileEditorMode = true;

                // Set Vicky into Tile mode
                previousGraphicMode = kernel.MemMgr.VICKY.ReadByte(0);
                kernel.MemMgr.VICKY.WriteByte(0, 0x10);

                // Enable borders
                kernel.MemMgr.VICKY.WriteByte(4, 1);
                tileEditor.Show();
                tileEditor.UnmapEvent += EditorWindowClosed;

                // coordinate between the tile editor window and the GPU canvas
                //-- TileClicked += new TileClickEvent(tileEditor.TileClicked_Click);
            }
        }

        // When the editor window is closed, exit the TileEditorMode
        private void EditorWindowClosed(object sender, EventArgs e)
        {
            ucGpu.TileEditorMode = false;

            // Restore the previous graphics mode
            kernel.MemMgr.VICKY.WriteByte(0, previousGraphicMode);

            tileEditor.Dispose();
            tileEditor = null;
        }

        private void on_menuToolsFontEditor_Activate(object sender, EventArgs e)
        {
            if (charEditor == null)
                charEditor = new();

            charEditor.Show();
        }

        private void on_menuToolsGameEditor_Activate(object sender, EventArgs e)
        {
            GameGeneratorWindow dialog = new();
            dialog.Show();
        }

        // Convert a Hex file to PGX
        // Header is PGX,1,4 byte jump address
        private void on_menuToolsConvertHexToPGX_Activate(object sender, EventArgs e)
        {
            FileChooserDialog filechooser =
                new("Select a Hex File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

            FileFilter ff = new();
            ff.Name = "Hex Files";
            ff.AddPattern("*.hex");
            filechooser.AddFilter(ff);

            if (filechooser.Run() == (int)ResponseType.Accept)
            {
                MemoryRAM temporaryRAM = new(0, 4 * 1024 * 1024);

                HexFile.Load(temporaryRAM, filechooser.Filename, 0, out int DataStartAddress, out int DataLength);

                // write the file
                string outputFileName = System.IO.Path.ChangeExtension(filechooser.Filename, "PGX");

                byte[] buffer = new byte[DataLength];
                temporaryRAM.CopyIntoBuffer(DataStartAddress, DataLength, buffer);

                using (BinaryWriter writer = new(File.Open(outputFileName, FileMode.Create)))
                {
                    // 8 byte header
                    writer.Write((byte)'P');
                    writer.Write((byte)'G');
                    writer.Write((byte)'X');
                    writer.Write((byte)1);
                    writer.Write(DataStartAddress);
                    writer.Write(buffer);
                }
            }

            ff.Dispose();
            filechooser.Destroy();
        }

        private void on_menuToolsConvertBinToPGX_Activate(object sender, EventArgs e)
        {
            FileChooserDialog filechooser =
                new("Select a Bin File", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

            FileFilter ff = new();
            ff.AddPattern("*.bin");
            filechooser.AddFilter(ff);

            if (filechooser.Run() == (int)ResponseType.Accept)
            {
                // Ask the user what address to write in the header
                string StrAddress = Microsoft.VisualBasic.Interaction.InputBox("Enter the PGX Start Address (Hexadecimal)", "PGX Start Address", "0");

                if (!StrAddress.Equals("0"))
                {
                    byte[] buffer = File.ReadAllBytes(filechooser.Filename);

                    // write the file
                    int DataStartAddress = Convert.ToInt32(StrAddress, 16);
                    string outputFileName = System.IO.Path.ChangeExtension(filechooser.Filename, "PGX");

                    using (BinaryWriter writer = new(File.Open(outputFileName, FileMode.Create)))
                    {
                        // 8 byte header
                        writer.Write((byte)'P');
                        writer.Write((byte)'G');
                        writer.Write((byte)'X');
                        writer.Write((byte)1);
                        writer.Write(DataStartAddress);
                        writer.Write(buffer);
                    }
                }
                else
                {
                    using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                            MessageType.Error, ButtonsType.Ok, "Invalid Start Address")) {
                        md.Title = "PGX File Error";
                        md.Run();
                    }
                }
            }

            ff.Dispose();
            filechooser.Destroy();
        }

        // Settings Menu
        private void on_menuSettingsAutorun_Activate(object sender, EventArgs e)
        {
            throw new NotImplementedException();

            //-- Simulator.Properties.Settings.Default.Autorun = autorunEmulatorToolStripMenuItem.Checked;
            // Simulator.Properties.Settings.Default.Save();
        }

        // Windows Menu
        private void on_menuWindowsTerminal_Activate(object sender, EventArgs e)
        {
            SerialTerminalWindow dialog = new();
            dialog.Show();
        }

        private void on_menuWindowsCPUModule_Activate(object sender, EventArgs e)
        {
            ShowCpu68000Window();
        }

        private void on_menuWindowsMemory_Activate(object sender, EventArgs e)
        {
            ShowMemoryWindow();
        }

        private void on_menuWindowsWatchList_Activate(object sender, EventArgs e)
        {
            WatchWindow dialog = new();
            dialog.Show();
        }

        private void on_menuWindowsAssetList_Activate(object sender, EventArgs e)
        {
            AssetWindow dialog = new();
            dialog.SetKernel(kernel);
            dialog.Show();
        }

        // Reset Menu
        /**
         * Restart the CPU
         */
        private void on_menuResetRestart_Activate(object sender, EventArgs e)
        {
            previousCounter = 0;
            cpu68000Window.Pause();

            if (kernel.ResetCPU(null))
            {
                ucGpu.QueueDraw();

                cpu68000Window.SetKernel(kernel);
                cpu68000Window.ClearTrace();

                SetDipSwitchMemory();

                memoryWindow.Memory = kernel.MemMgr;
                memoryWindow.UpdateMCRButtons();

                // Restart the CPU
                cpu68000Window.Run();
            }
        }

        /** 
         * Reset the system and go to step mode.
         */
        private void on_menuResetDebug_Activate(object sender, EventArgs e)
        {
            previousCounter = 0;

            cpu68000Window.Pause();

            if (kernel.ResetCPU(null))
            {
                cpu68000Window.SetKernel(kernel);
                cpu68000Window.ClearTrace();

                SetDipSwitchMemory();

                memoryWindow.Memory = kernel.MemMgr;
                memoryWindow.UpdateMCRButtons();

                cpu68000Window.QueueDraw();
            }
        }

        // Help Menu
        /**
         * Call the GitHub REST API with / repos / Trinity-11 / FoenixIDE / releases.
         * From the returned JSON, check which one is the latest and if it matches ours.
         */
        private void on_menuHelpUpdateApp_Activate(object sender, EventArgs e)
        {
            string URL = "https://api.github.com/repos/Trinity-11/FoenixIDE/releases";
            HttpClient client = new HttpClient();

            string version = AboutWindowProcess.AppVersion();
            int appVersion = MainProcess.VersionValue(version);

            // Add an Accept header for JSON format
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("user-agent", "Foenix Toolkit");
            bool done = false;

            // List data response.
            HttpResponseMessage response = client.GetAsync(URL).Result;  // Blocking call!
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body.
                string value = response.Content.ReadAsStringAsync().Result;
                MatchCollection matches = Regex.Matches(value, "\"tag_name\":\"(.*?)\"");

                foreach (Match match in matches)
                {
                    string fullRelease = match.Groups[1].Value;
                    string release = fullRelease.Replace("release-", "");

                    int releaseVersion = MainProcess.VersionValue(release);
                    if (releaseVersion > appVersion)
                    {
                        string message = string.Format("A new version is available.\n\n" +
                            "The latest release is {0}.\nYou are running version {1}.", release, version);
                        using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                                MessageType.Info, ButtonsType.Ok, message)) {
                            md.Title = "Version Check";
                            md.Run();
                        }

                        done = true;
                        break;
                    }
                }                
            }
            else
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);

            if (!done)
            {
                string message = "You are running the latest version.";
                using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                        MessageType.Info, ButtonsType.Ok, message)) {
                    md.Title = "Version Check";
                    md.Run();
                }
            }

            client.Dispose();
       }

        private void on_menuHelpAbout_Activate(object sender, EventArgs e)
        {
            AboutWindow dialog = new();
            dialog.Show();
        }

        private void on_MainWindow_map(object sender, EventArgs e)
        {
        }

        private void on_MainWindow_unmap(object sender, EventArgs e)
        {
            ucGpu.StartOfFrame = null;
            ucGpu.StartOfLine = null;

            //-- ModeText.Text = "Shutting down CPU thread";

            if (kernel.m68000Cpu != null)
            {
                kernel.m68000Cpu.DebugPause = true;
                kernel.m68000Cpu.CPUThread?.Join(1000);
            }
        }

        private void on_MainWindow_realize(object sender, EventArgs e)
        {
            terminal = new SerialTerminalWindow();

            ShowCpu68000Window();
            ShowMemoryWindow();

            // Now that the kernel is initialized, allocate variables to the GPU
            ucGpu.StartOfFrame += on_Gpu_SOF;
            ucGpu.StartOfLine += on_Gpu_SOL;
            ucGpu.GpuUpdated += on_Gpu_Updated;

            SetDipSwitchMemory();

            joystickWindow.gabe = kernel.MemMgr.GABE;

            // This fontset is loaded just in case the kernel doesn't provide one.
            ucGpu.LoadFontSet("Foenix", @"Resources/Bm437_PhoenixEGA_8x8.bin", 0, CharacterSet.CharTypeCodes.ASCII_PET, CharacterSet.SizeCodes.Size8x8);

            if (disabledIRQs)
                cpu68000Window.DisableIRQs(true);

            int Width = this.AllocatedWidth;
            int Height = this.AllocatedHeight;

            if (Width > 1200)
                Width = 1200;

            Height = Convert.ToInt32(Width * 0.75);
            Resize(Width, Height);
            QueueResize();

            // Code is tightly coupled with memory manager
            kernel.MemMgr.UART1.TransmitByte += SerialTransmitByte;
            kernel.MemMgr.UART2.TransmitByte += SerialTransmitByte;
            kernel.MemMgr.SDCARD.sdCardIRQMethod += SDCardInterrupt;
            kernel.Resources = ResChecker;

            watchWindow.SetKernel(kernel);

            DisplayBoardVersion();
            EnableMenuItems();
            ResetSDCard();

            if (autoRun)
            {
                cpu68000Window.Run();
            }

            menuSettingsAutorun.Active = autoRun;

            evtGpu.AddEvents((int)Gdk.EventMask.PointerMotionMask);
        }

        private void on_MainWindow_hide(object sender, EventArgs e)
        {

        }

        private void on_MainWindow_key_press_event(object sender, KeyPressEventArgs e)
        {
            //-- Console.WriteLine($"key_press_event: {e.Event.Key} {e.Event.KeyValue}");

            // we take over Shift+F11 and Shift+F5
            if ((e.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask)
            {
                switch (e.Event.Key)
                {
                    case Gdk.Key.F11:
                        FullScreenToggle();
                        break;

                    case Gdk.Key.F5:
                        if (cpu68000Window != null)
                            cpu68000Window.Run();

                        break;
                }
            }

            ScanCode scanCode = ScanCodes.GetScanCode(e.Event.Key);
            if (scanCode != ScanCode.sc_null)
            {
                e.RetVal = true;

                lblLastKey.Text = "$" + ((byte)scanCode).ToString("X2");

                if (kernel.MemMgr != null && !kernel.m68000Cpu.DebugPause)
                    kernel.MemMgr.KEYBOARD.WriteKey(scanCode);
            }
            else
                lblLastKey.Text = "";
        }

        private void on_MainWindow_key_release_event(object sender, KeyReleaseEventArgs e)
        {
            Console.WriteLine($"key_release_event: {e.Event.Key}");

            ScanCode scanCode = ScanCodes.GetScanCode(e.Event.Key);
            if (scanCode != ScanCode.sc_null)
            {
                e.RetVal = true;

                scanCode += 0x80;
                lblLastKey.Text = "$" + ((byte)scanCode).ToString("X2");

                if (kernel.MemMgr != null && !kernel.m68000Cpu.DebugPause)
                    kernel.MemMgr.KEYBOARD.WriteKey(scanCode);
            }
            else
                lblLastKey.Text = "";
        }

        private void on_MainWindow_drag_begin(object sender, DragBeginArgs e)
        {
            // Allow if the file is Hex
            //-- string[] obj = (string[])e.Context.Data. Data.GetData("FileName");
            // if (obj != null && obj.Length > 0)
            // {
            //     FileInfo info = new(obj[0]);
            //     if (info.Extension.ToUpper().Equals(".HEX"))
            //     {
            //         e.Context.Actions = Gdk.DragAction.Copy;
            //         return;
            //     }
            // }
            // e.Effect = DragDropEffects.None;
        }

        private void on_MainWindow_drag_drop(object sender, DragDropArgs e)
        {
            //-- string[] obj = (string[])e.Data.GetData("FileName");
            // if (obj != null && obj.Length > 0)
            // {
            //     FileInfo info = new(obj[0]);
            //     if (info.Extension.ToUpper().Equals(".HEX"))
            //         LoadHexFile(obj[0]);
            // }
        }

        /*
         * DIP SWITCH Definition:
            DIP1 - BOOT MODE0 - b0 : $AF:E80E
            DIP2 - BOOT MODE1 - b1 : $AF:E80E
            DIP3 - USER DEFINED2
            DIP4 - USER DEFINED1
            DIP5 - USER DEFINED0
            DIP6 - HIGH-RES @ Boot (800 x 600) (when it is instantiated in Vicky II)
            DIP7 - GAMMA Correction ON/OFF
            DIP8- HDD INSTALLED
        */
        private readonly bool[] switches = new bool[8];

        private void on_daDipSwitch_draw(object sender, DrawnArgs e)
        {
            var cr = e.Cr;

            cr.SelectFontFace("Consolas", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            cr.SetFontSize(9);

            if (version != BoardVersion.A2560K)
            {
                int width = 128;
                int height = 23;
                int textOffset = 24;
                int offset = 2;

                int bankHeight = height;
                int switchWidth = (width - textOffset) / 8;
                int dipHeight = (bankHeight - 6) / 2;

                cr.SetSourceColor(red);
                cr.Rectangle(0, 0, width, height);
                cr.Fill();

                cr.SetSourceColor(white); 
                cr.MoveTo(8, 9);
                cr.ShowText("ON");

                cr.SetSourceColor(white); 
                cr.MoveTo(4, 20);
                cr.ShowText("OFF");

                for (int i = 0; i < 8; ++i)
                {
                    // Draw the switch slide
                    cr.SetSourceColor(lightGray);
                    cr.Rectangle(textOffset + (i * switchWidth), offset, switchWidth - offset, bankHeight - offset * 2);
                    cr.Fill();
                    
                    int top = (switches[i]) ? offset : offset + dipHeight;

                    cr.SetSourceColor(darkSlateGray);
                    cr.MoveTo(textOffset + (i * switchWidth) + switchWidth / 2, top + switchWidth / 2);
                    cr.Arc(textOffset + (i * switchWidth) + switchWidth / 2, top + switchWidth / 2,
                         4, 0.0, 360 * toRadians);
                    cr.Fill();
                }
            }
        }

        private void on_evtDipSwitch_button_press_event(object sender, ButtonPressEventArgs e)
        {
            int width = 128;
            int textOffset = 24;
            float switchWidth = (width - textOffset) / 8f; // 13.125

            // DEBUG: lblSDCardPath.Text = $"X: {e.Event.X}, Y:{e.Event.Y}";

            int switchID = (int)Math.Floor((e.Event.X - 24.0) / switchWidth);
            if (switchID >= 0 && switchID < 8)
            {
                // get current status and toggle it
                switches[switchID] = !switches[switchID];
                SetDipSwitchMemory();

                daDipSwitch.QueueDraw();
            }
        }

        DateTime pSof;
        private void on_Gpu_SOF()
        {
            // Check if the interrupt is enabled
            DateTime currentDT = DateTime.Now;
            TimeSpan ts = currentDT - pSof;
            pSof = currentDT;

            byte mask = kernel.MemMgr.ReadByte(MemoryMap.INT_MASK_REG0);
            if (!kernel.m68000Cpu.DebugPause)
            {
                // Set the SOF Interrupt
                byte IRQ0 = kernel.MemMgr.ReadByte(MemoryMap.INT_PENDING_REG0);
                IRQ0 |= (byte)Register0.FNX0_INT00_SOF;

                kernel.MemMgr.INTERRUPT.WriteFromGabe(0, IRQ0);

                if ((~mask & (byte)Register0.FNX0_INT00_SOF) == (byte)Register0.FNX0_INT00_SOF)
                    kernel.m68000Cpu.Pins.IRQ = true;
            }
        }

        private void on_Gpu_SOL()
        {
            // Check if the interrupt is enabled
            byte mask = kernel.MemMgr.ReadByte(MemoryMap.INT_MASK_REG0);

            if (!kernel.m68000Cpu.DebugPause && ((~mask & (byte)Register0.FNX0_INT01_SOL) == (byte)Register0.FNX0_INT01_SOL))
            {
                // Set the SOL Interrupt
                byte IRQ0 = kernel.MemMgr.ReadByte(MemoryMap.INT_PENDING_REG0);
                IRQ0 |= (byte)Register0.FNX0_INT01_SOL;

                kernel.MemMgr.INTERRUPT.WriteFromGabe(0, IRQ0);

                kernel.m68000Cpu.Pins.IRQ = true;
            }
        }

        int previousCounter = 0;
        int previousFrame = 0;
        DateTime previousTime = DateTime.Now;

        private void on_Gpu_Updated()
        {
            if (kernel != null  && kernel.m68000Cpu != null)
            {
                DateTime currentTime = DateTime.Now;

                if (!kernel.m68000Cpu.DebugPause)
                {
                    TimeSpan s = currentTime - previousTime;
                    int currentCounter = kernel.m68000Cpu.CycleCounter;
                    int currentFrame = ucGpu.paintCycle;
                    double cps = (currentCounter - previousCounter) / s.TotalSeconds;
                    double fps = (currentFrame - previousFrame) / s.TotalSeconds;

                    previousCounter = currentCounter;
                    previousTime = currentTime;
                    previousFrame = currentFrame;
                    Write_CPS_FPS_Safe("CPS: " + cps.ToString("N0"), "FPS: " + fps.ToString("N0"));
                }

                // write the time to memory - values are BCD
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_SEC - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Second));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_MIN - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Minute));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_HRS - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Hour));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_DAY - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Day));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_MONTH - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Month));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_YEAR - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Year % 100));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_CENTURY - kernel.MemMgr.VICKY.StartAddress, MainProcess.BCD(currentTime.Year / 100));
                kernel.MemMgr.VICKY.WriteByte(MemoryMap.RTC_DOW - kernel.MemMgr.VICKY.StartAddress, (byte)(currentTime.DayOfWeek + 1));
            }
            else
            {
                lblCpsPerf.Text = "CPS: 0";
                lblFpsPerf.Text = "FPS: 0";
            }
        }

        private void on_evtGpu_motion_notify_event(object sender, MotionNotifyEventArgs e)
        {
            if (ucGpu.TileEditorMode)
            {
                Point size = ucGpu.GetScreenSize();
                double ratioW = ucGpu.AllocatedWidth / (double)size.X;
                double ratioH = ucGpu.AllocatedHeight / (double)size.Y;

                if ((e.Event.X / ratioW > 32 && e.Event.X / ratioW < size.X - 32) && (e.Event.Y / ratioH > 32 && e.Event.Y / ratioH < size.Y - 32))
                {
                    Window.Cursor = new Gdk.Cursor(Gdk.CursorType.Hand1);
                    if (e.Event.State == Gdk.ModifierType.Button1Mask)
                        TileClicked?.Invoke(new Point((int)(e.Event.X / ratioW / 16), (int)(e.Event.Y / ratioH / 16)));
                }
                else
                    Window.Cursor = new Gdk.Cursor(Gdk.CursorType.BlankCursor);
            }

            GenerateMouseInterrupt(e);
        }

        private void on_Gpu_button_press_event(object sender, ButtonPressEventArgs e)
        {
            Point size = ucGpu.GetScreenSize();
            double ratioW = ucGpu.AllocatedWidth / (double)size.X;
            double ratioH = ucGpu.AllocatedHeight / (double)size.Y;

            if (ucGpu.TileEditorMode && Window.Cursor.CursorType != Gdk.CursorType.BlankCursor)
                TileClicked?.Invoke(new Point((int)(e.Event.X / ratioW / 16), (int)(e.Event.Y / ratioH / 16)));
            else
                GenerateMouseInterrupt(e);
        }

        private void on_Gpu_button_release_event(object sender, ButtonReleaseEventArgs e)
        {
            GenerateMouseInterrupt(e);
        }

        private void GenerateMouseInterrupt(EventArgs e)
        {
            var mouseEvtPress = e as ButtonPressEventArgs;
            var mouseEvtRelease = e as ButtonReleaseEventArgs;
            var mouseEvtMotion = e as MotionNotifyEventArgs;

            Point size = ucGpu.GetScreenSize();
            Console.WriteLine($"@size: {size.X} {size.Y}");
            double ratioW = ucGpu.AllocatedWidth / (double)size.X;
            double ratioH = ucGpu.AllocatedHeight / (double)size.Y;
            Console.WriteLine($"@ratio: {ratioW} {ratioH}");

            var evtX = 
                mouseEvtPress != null ? mouseEvtPress.Event.X : 
                mouseEvtRelease != null ? mouseEvtRelease.Event.X :
                mouseEvtMotion != null ? mouseEvtMotion.Event.X : 0.0;
            var evtY =
                mouseEvtPress != null ? mouseEvtPress.Event.Y : 
                mouseEvtRelease != null ? mouseEvtRelease.Event.Y :
                mouseEvtMotion != null ? mouseEvtMotion.Event.Y : 0.0;
            Console.WriteLine($"@raw: {evtX} {evtY}");

            int X = (int)(evtX  / ratioW);
            int Y = (int)(evtY  / ratioH);
            Console.WriteLine($"@mouse: {X} {Y}");

            var evtState = 
                mouseEvtPress != null ? mouseEvtPress.Event.State : 
                mouseEvtRelease != null ? mouseEvtRelease.Event.State :
                mouseEvtMotion != null ? mouseEvtMotion.Event.State : Gdk.ModifierType.None;

            bool middle = evtState == Gdk.ModifierType.Button3Mask;
            bool left = evtState == Gdk.ModifierType.Button1Mask;
            bool right = evtState == Gdk.ModifierType.Button2Mask;
            Console.WriteLine($"@state: {left} {middle} {right}");

            if (kernel.MemMgr == null)
                return;

            kernel.MemMgr.VICKY.WriteWord(0x702, (int)evtX);
            kernel.MemMgr.VICKY.WriteWord(0x704, (int)evtY);

            // Generate three interrupts - to emulate how the PS/2 controller works
            byte mask = kernel.MemMgr.ReadByte(MemoryMap.INT_MASK_REG0);

            // The PS/2 packet is byte0, xm, ym
            if ((~mask & (byte)Register0.FNX0_INT07_MOUSE) == (byte)Register0.FNX0_INT07_MOUSE)
            {
                kernel.MemMgr.KEYBOARD.MousePackets((byte)(8 + (middle ? 4 : 0) + (right ? 2 : 0) + (left ? 1 : 0)), (byte)(X & 0xFF), (byte)(Y & 0xFF));
            }
        }

        private void on_Gpu_leave_notify_event(object sender, EventArgs e)
        {
            if (ucGpu.IsMousePointerVisible() || ucGpu.TileEditorMode)
                Window.Cursor = new Gdk.Cursor(Gdk.CursorType.Arrow);
        }

        private void on_Gpu_enter_notify_event(object sender, EventArgs e)
        {
            if (ucGpu.IsMousePointerVisible() && !ucGpu.TileEditorMode)
                Window.Cursor = new Gdk.Cursor(Gdk.CursorType.BlankCursor);
        }
    }
}
