using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore;
using FoenixCore.MemoryLocations;
using FoenixCore.Simulator.Devices;
using FoenixCore.Simulator.FileFormat;


namespace FoenixToolkit.UI
{
    class UploaderWindow : Window
    {
        private FoenixSystem _kernel = null;
        private BoardVersion boardVersion = BoardVersion.RevC;

        public static byte TxLRC = 0;
        public static byte RxLRC = 0;
        public static byte Stat0 = 0;
        public static byte Stat1 = 0;
        public static byte LRC = 0;
        public static string[] ports;
        readonly System.IO.Ports.SerialPort serial = new();
        private readonly Queue<byte> recievedData = new();

#pragma warning disable CS0649  // never assigned
        [GUI] Button btnConnect;
        [GUI] Button btnDisconnect;
        [GUI] Button btnSendBinary;
        [GUI] CheckButton chkReflash;
        [GUI] CheckButton chkDebug;
        [GUI] ComboBoxText cboComPort;
        [GUI] Entry txtLocalAddr;
        [GUI] Entry txtRemoteAddr;
        [GUI] Entry txtTransferSize;
        [GUI] FileChooserButton fcbBrowseFile;
        [GUI] Label lblCountdown;
        [GUI] Label lblFileSize;
        [GUI] Label lblRevMode;
        [GUI] RadioButton rdoBlockFetch;
        [GUI] RadioButton rdoBlockSend;
        [GUI] RadioButton rdoSendFile;
#pragma warning restore CS0649

        public UploaderWindow() : this(new Builder("UploaderWindow.ui"))
        {
            serial.BaudRate = 6000000;
            serial.Handshake = Handshake.None;
            serial.Parity = Parity.None;
            serial.DataBits = 8;
            serial.StopBits = StopBits.One;
            serial.ReadTimeout = 2000;
            serial.WriteTimeout = 2000;

            ports = SerialPort.GetPortNames();  // Save the Ports Name in a String Array

            Console.WriteLine("Available Ports:");

            // Save the Ports Name in the Items list of the ComboBox
            foreach (string s in ports)
            {
                cboComPort.AppendText(s);
                Console.WriteLine("   {0}", s);
            }

            if (ports.Length == 0)
                cboComPort.AppendText("-----");
            else
                cboComPort.ActiveId = "1";
        }

        private UploaderWindow(Builder builder) : base(builder.GetRawOwnedObject("UploaderWindow"))
        {
            builder.Autoconnect(this);
        }

        public void SetKernel(FoenixSystem kernel)
        {
            _kernel = kernel;
        }

        public void SetBoardVersion(BoardVersion version)
        {
            boardVersion = version;

            lblRevMode.Text = boardVersion switch
            {
                BoardVersion.RevB => "Mode: RevB",
                BoardVersion.RevC => "Mode: RevC",
                BoardVersion.RevU => "Mode: RevU",
                BoardVersion.RevUPlus => "Mode: RevU+",
                _ => "Mode: RevC"
            };
        }

        private int GetTransmissionSize()
        {
            int transmissionSize = -1;

            if (rdoSendFile.Active)
            {
                GetFileLength(fcbBrowseFile.Filename);
                transmissionSize = Convert.ToInt32(lblFileSize.Text.Replace("$", "").Replace(":", ""), 16);
            }
            else if (rdoBlockSend.Active)
                transmissionSize = Convert.ToInt32(txtLocalAddr.Text.Replace("$", "").Replace(":", ""), 16);
            else
                transmissionSize = Convert.ToInt32(txtRemoteAddr.Text.Replace("$", "").Replace(":", ""), 16);

            return transmissionSize;
        }

        private long GetFileLength(String filename)
        {
            long flen = 0;

            // Display the file length in hex
            if (filename != null && filename.Length > 0)
            {
                if (System.IO.Path.GetExtension(filename).ToUpper().Equals(".BIN"))
                {
                    FileInfo f = new(filename);
                    flen = f.Length;
                }
                else
                {
                    // We're loading a HEX file, so only consider the lines that are record type 00
                    string[] lines = System.IO.File.ReadAllLines(filename);
                    foreach (string l in lines)
                    {
                        if (l.StartsWith(":"))
                        {
                            string mark = l[..1];
                            string reclen = l[1..3];
                            string offset = l[3..7];
                            string rectype = l[7..9];

                            if (rectype.Equals("00"))
                            {
                                flen += Convert.ToInt32(reclen, 16);
                            }
                        }
                    }
                }
            }

            String hexSize = flen.ToString("X6");

            lblFileSize.Text = $"${hexSize[..2]}:{hexSize[2..]}";

            return flen;
        }

        private void HideProgressBarAfter5Seconds(string message)
        {
            //-- UploadProgressBar.Visible = false;

            lblCountdown.Visible = true;
            lblCountdown.Text = message;

            //-- hideLabelTimer.Sensitive = true;

            btnSendBinary.Sensitive = true;
            btnDisconnect.Sensitive = true;
        }

        private byte Checksum(byte[] buffer, int length)
        {
            byte checksum = 0x55;
            for (int i = 1; i < length; ++i)
                checksum ^= buffer[i];

            return checksum;
        }

        private void EraseFlash()
        {
            lblCountdown.Text = "Erasing Flash";

            byte[] commandBuffer = new byte[8];
            commandBuffer[0] = 0x55;   // Header
            commandBuffer[1] = 0x11;   // Reset Flash
            commandBuffer[2] = 0x00;
            commandBuffer[3] = 0x00;
            commandBuffer[4] = 0x00;
            commandBuffer[5] = 0x00;
            commandBuffer[6] = 0x00;
            commandBuffer[7] = Checksum(commandBuffer, 7);

            SendMessage(commandBuffer, null);
        }

        private void ProgramFlash(int address)
        {
            lblCountdown.Text = "Programming Flash";

            byte[] commandBuffer = new byte[8];
            commandBuffer[0] = 0x55;   // Header
            commandBuffer[1] = 0x10;   // Reset Flash
            commandBuffer[2] = (byte)((address & 0xFF_0000) >> 16);
            commandBuffer[3] = (byte)((address & 0x00_FF00) >> 8);
            commandBuffer[4] = (byte)(address & 0x00_00FF);
            commandBuffer[5] = 0x00;
            commandBuffer[6] = 0x00;
            commandBuffer[7] = Checksum(commandBuffer, 7);

            SendMessage(commandBuffer, null, 10000);
        }

        private void SendData(byte[] buffer, int startAddress, int size)
        {
            try
            {
                if (serial.IsOpen)
                {
                    // Now's let's transfer the code
                    if (size <= 2048)
                    {
                        // DataBuffer = The buffer where the loaded Binary File resides
                        // FnxAddressPtr = Pointer where to put the Data in the Fnx
                        // i = Pointer Inside the data buffer
                        // Size_Of_File = Size of the Payload we want to transfer which ought to be smaller than 8192
                        PreparePacket2Write(buffer, startAddress, 0, size);
                        //-- UploadProgressBar.Increment(size);
                    }
                    else
                    {
                        int BufferSize = 2048;
                        int Loop = size / BufferSize;
                        int offset = startAddress;
                        for (int j = 0; j < Loop; ++j)
                        {
                            PreparePacket2Write(buffer, offset, j * BufferSize, BufferSize);
                            offset += BufferSize;   // Advance the Pointer to the next location where to write Data in the Foenix
                            //-- UploadProgressBar.Increment(BufferSize);
                        }

                        BufferSize = (size % BufferSize);
                        if (BufferSize > 0)
                        {
                            PreparePacket2Write(buffer, offset, size - BufferSize, BufferSize);
                            //-- UploadProgressBar.Increment(BufferSize);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                        MessageType.Error, ButtonsType.Ok, ex.Message)) {
                    md.Title = "Send Binary Error";
                    md.Run();
                }
            }
        }

        private bool FetchData(byte[] buffer, int startAddress, int size, bool debugMode)
        {
            bool success = false;
            byte[] partialBuffer;

            try
            {
                if (serial.IsOpen)
                {
                    if (debugMode)
                        GetFnxInDebugMode();

                    if (size < 2048)
                    {
                        partialBuffer = PreparePacket2Read(startAddress, size);
                        Array.Copy(partialBuffer, 0, buffer, 0, size);
                        //-- UploadProgressBar.Increment(size);
                    }
                    else
                    {
                        int BufferSize = 2048;
                        int Loop = size / BufferSize;

                        for (int j = 0; j < Loop; ++j)
                        {
                            partialBuffer = PreparePacket2Read(startAddress, BufferSize);
                            Array.Copy(partialBuffer, 0, buffer, j * BufferSize, BufferSize);
                            partialBuffer = null;
                            startAddress += BufferSize;   // Advance the Pointer to the next location where to write Data in the Foenix
                            //-- UploadProgressBar.Increment(BufferSize);
                        }

                        BufferSize = (size % BufferSize);
                        if (BufferSize > 0)
                        {
                            partialBuffer = PreparePacket2Read(startAddress, BufferSize);
                            Array.Copy(partialBuffer, 0, buffer, size - BufferSize, BufferSize);
                            //-- UploadProgressBar.Increment(BufferSize);
                        }
                    }

                    if (debugMode)
                        ExitFnxDebugMode();

                    success = true;
                }
            }
            catch (Exception ex)
            {
                using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                        MessageType.Error, ButtonsType.Ok, ex.Message)) {
                    md.Title = "Fetch Data Error";
                    md.Run();
                }
            }

            return success;
        }

        public void GetFnxInDebugMode()
        {
            byte[] commandBuffer = new byte[8];
            commandBuffer[0] = 0x55;   // Header
            commandBuffer[1] = 0x80;   // GetFNXinDebugMode
            commandBuffer[2] = 0x00;
            commandBuffer[3] = 0x00;
            commandBuffer[4] = 0x00;
            commandBuffer[5] = 0x00;
            commandBuffer[6] = 0x00;
            commandBuffer[7] = Checksum(commandBuffer, 7);

            SendMessage(commandBuffer, null);
        }

        public void ExitFnxDebugMode()
        {
            byte[] commandBuffer = new byte[8];
            commandBuffer[0] = 0x55;   // Header
            commandBuffer[1] = 0x81;   // ExitFNXinDebugMode
            commandBuffer[2] = 0x00;
            commandBuffer[3] = 0x00;
            commandBuffer[4] = 0x00;
            commandBuffer[5] = 0x00;
            commandBuffer[6] = 0x00;
            commandBuffer[7] = Checksum(commandBuffer, 7);

            SendMessage(commandBuffer, null);
        }

        /*
        CMD = 0x00 Read Memory Block
        CMD = 0x01 Write Memory Block
        CMD = 0x0E GetFNXinDebugMode - Stop Processor and put Bus in Tri-State - That needs to be done before any transaction.
        CMD = 0x0F 
         */
        public void PreparePacket2Write(byte[] buffer, int FNXMemPointer, int FilePointer, int Size)
        {
            // Maximum transmission size is 8192
            if (Size > 8192)
                Size = 8192;

            byte[] commandBuffer = new byte[8 + Size];
            commandBuffer[0] = 0x55;   // Header
            commandBuffer[1] = 0x01;   // Write 2 Memory
            commandBuffer[2] = (byte)((FNXMemPointer >> 16) & 0xFF); // (H)24Bit Addy - Where to Store the Data
            commandBuffer[3] = (byte)((FNXMemPointer >> 8) & 0xFF);  // (M)24Bit Addy - Where to Store the Data
            commandBuffer[4] = (byte)(FNXMemPointer & 0xFF);         // (L)24Bit Addy - Where to Store the Data
            commandBuffer[5] = (byte)((Size >> 8) & 0xFF);           // (H)16Bit Size - How many bytes to Store (Max 8Kbytes for now)
            commandBuffer[6] = (byte)(Size & 0xFF);                  // (L)16Bit Size - How many bytes to Store (Max 8Kbytes for now)

            Array.Copy(buffer, FilePointer, commandBuffer, 7, Size);

            TxProcessLRC(commandBuffer);
            Console.WriteLine("Transmit Data LRC:" + TxLRC);

            SendMessage(commandBuffer, null);   // Tx the requested Payload Size (Plus Header and LRC), No Payload to be received aside of the Status.
        }

        public byte[] PreparePacket2Read(int address, int size)
        {
            if (size > 0)
            {
                byte[] commandBuffer = new byte[8];
                commandBuffer[0] = 0x55;   // Header
                commandBuffer[1] = 0x00;   // Command READ Memory
                commandBuffer[2] = (byte)(address >> 16); // Address Hi
                commandBuffer[3] = (byte)(address >> 8); // Address Med
                commandBuffer[4] = (byte)(address & 0xFF); //Address Lo
                commandBuffer[5] = (byte)(size >> 8); //Size HI
                commandBuffer[6] = (byte)(size & 0xFF); //Size LO
                commandBuffer[7] = Checksum(commandBuffer, 7);

                byte[] partialBuffer = new byte[size];

                SendMessage(commandBuffer, partialBuffer);

                return partialBuffer;
            }

            return null;
        }

        public void SendMessage(byte[] command, byte[] data, int delay = 0)
        {
            byte byte_buffer;
            Stopwatch stopWatch = new();
            serial.Write(command, 0, command.Length);

            Stat0 = 0;
            Stat1 = 0;
            LRC = 0;

            if (delay > 2000)
                serial.ReadTimeout = delay;

            if (delay > 0)
            {
                long StartTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                int roundTime = delay / 1000;
                string label = lblCountdown.Text;

                do
                {
                    lblCountdown.Text = $"{label} - {roundTime}s";
                    Thread.Sleep(1000);
                    roundTime--;
                }
                while (System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - StartTime < delay);

                lblCountdown.Text = $"{label} - Done!";
            }

            stopWatch.Start();

            do
                byte_buffer = (byte)serial.ReadByte();
            while (byte_buffer != 0xAA);

            stopWatch.Stop();
            TimeSpan tsReady = stopWatch.Elapsed;

            if (delay > 2000)
                serial.ReadTimeout = 2000;

            // reset the stop watch
            stopWatch.Reset();
            stopWatch.Start();

            if (byte_buffer == 0xAA)
            {
                Stat0 = (byte)serial.ReadByte();
                Stat1 = (byte)serial.ReadByte();

                if (data != null)
                    serial.Read(data, 0, data.Length);

                LRC = (byte)serial.ReadByte();
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            Console.WriteLine($"Ready: {tsReady.Milliseconds}, Receive Data LRC: {RxLRC}, Time: {ts.Milliseconds}ms");

            RxProcessLRC(data);
        }

        public int TxProcessLRC(byte[] buffer)
        {
            TxLRC = 0;

            for (int i = 0; i < buffer.Length; ++i)
                TxLRC = (byte)(TxLRC ^ buffer[i]);

            return TxLRC;
        }

        public int RxProcessLRC(byte[] data)
        {
            RxLRC = 0xAA;
            RxLRC = (byte)(RxLRC ^ Stat0);
            RxLRC = (byte)(RxLRC ^ Stat1);

            if (data != null)
                for (int i = 0; i < data.Length; ++i)
                    RxLRC = (byte)(RxLRC ^ data[i]);

            RxLRC = (byte)(RxLRC ^ LRC);

            return RxLRC;
        }

        private void on_UploaderWindow_unrealize(object sender, EventArgs e)
        {
            btnDisconnect.Activate();
        }

        private void on_UploaderWindow_key_press_event(object sender, KeyPressEventArgs e)
        {
            if (e.Event.Key == Gdk.Key.Escape)
                Close();
        }

        private void on_btnConnect_clicked(object sender, EventArgs e)
        {
            try
            {
                serial.PortName = cboComPort.ActiveText;
                serial.Open();

                // Enable all the button if the serial Port turns out to be the good one.
                fcbBrowseFile.Sensitive = rdoSendFile.Active;

                btnSendBinary.Sensitive = GetTransmissionSize() > 0;
                cboComPort.Sensitive = false;

                btnConnect.Visible = false;
                btnDisconnect.Visible = true;

                Console.WriteLine($"Serial Port Connected: {cboComPort.ActiveText}");
            }
            catch (Exception ex)
            {
                using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                        MessageType.Error, ButtonsType.Ok, ex.Message)) {
                    md.Title = "Serial Connection Error";
                    md.Run();
                }
            }
        }

        private void on_btnDisconnect_clicked(object sender, EventArgs e)
        {
            serial.Close();

            btnDisconnect.Visible = false;
            btnConnect.Visible = true;

            cboComPort.Sensitive = true;
            btnSendBinary.Sensitive = false;
        }

        /*
         * Let the user select a file from the file system and display it in a text box.
         */
        private void on_fcbBrowseFile_file_set(object sender, EventArgs e)
        {
            //-- OpenFileDialog openFileDlg = new()
            // {
            //     DefaultExt = ".hex",
            //     Filter = "Hex documents|*.hex|Binary documents|*.bin",
            //     Title = "Upload to the A2560 Foenix"
            // };

            // Load content of file in a TextBlock
            string extension = System.IO.Path.GetExtension(fcbBrowseFile.Filename);

            txtRemoteAddr.Sensitive = extension.ToUpper().Equals(".BIN");
            chkReflash.Sensitive = extension.ToUpper().Equals(".BIN");

            // Display the file length
            long flen = GetFileLength(fcbBrowseFile.Filename);

            btnSendBinary.Sensitive = (flen != -1) && !btnConnect.Visible;
        }

        /**
         * This method fires whenever the radio buttons are changed.
         */
        private void on_rdoSendFile_toggled(object sender, EventArgs e)
        {
            fcbBrowseFile.Sensitive = rdoSendFile.Active;

            int transmissionSize = GetTransmissionSize();
            txtTransferSize.Sensitive = rdoBlockSend.Active;
            txtLocalAddr.Sensitive = rdoBlockSend.Active;

            if (fcbBrowseFile.Filename == null || fcbBrowseFile.Filename.Length == 0 || rdoBlockSend.Active)
                txtRemoteAddr.Sensitive = (transmissionSize > 0 || rdoBlockSend.Active);
            else
            {
                string extension = System.IO.Path.GetExtension(fcbBrowseFile.Filename).ToUpper();
                txtRemoteAddr.Sensitive = (transmissionSize > 0 || rdoBlockSend.Active) && (extension.Equals(".BIN") || chkReflash.Active);
            }

            txtTransferSize.Sensitive = rdoBlockFetch.Active;

            btnSendBinary.Sensitive = (transmissionSize > 0) && !btnConnect.Visible;
            btnSendBinary.Label = rdoBlockFetch.Active ? "Fetch from C256" : "Send Binary";
        }

        private void on_rdoBlockFetch_toggled(object sender, EventArgs e)
        {}

        private void on_rdoBlockSend_toggled(object sender, EventArgs e)
        {}

        private void on_btnSendBinary_clicked(object sender, EventArgs e)
        {
            btnSendBinary.Sensitive = false;
            btnDisconnect.Sensitive = false;

            //-- HideLabelTimer_Tick(null, null);

            int transmissionSize = GetTransmissionSize();

            //-- UploadProgressBar.Maximum = transmissionSize;
            // UploadProgressBar.Value = 0;
            // UploadProgressBar.Visible = true;

            int BaseBankAddress = 0x38_0000;

            if (boardVersion == BoardVersion.RevB)
                BaseBankAddress = 0x18_0000;

            if (rdoSendFile.Active)
            {
                if (serial.IsOpen)
                {
                    // Get into Debug mode (Reset the CPU and keep it in that state and Gavin will take control of the bus)
                    if (chkDebug.Active)
                        GetFnxInDebugMode();

                    if (System.IO.Path.GetExtension(fcbBrowseFile.Filename).ToUpper().Equals(".BIN"))
                    {
                        // Read the bytes and put them in the buffer
                        byte[] DataBuffer = System.IO.File.ReadAllBytes(fcbBrowseFile.Filename);
                        int FnxAddressPtr = int.Parse(txtRemoteAddr.Text.Replace(":", ""), System.Globalization.NumberStyles.AllowHexSpecifier);
                        Console.WriteLine("Starting Address: " + FnxAddressPtr);
                        Console.WriteLine("File Size: " + transmissionSize);
                        SendData(DataBuffer, FnxAddressPtr, transmissionSize);

                        // Update the Reset Vectors from the Binary Files Considering that the Files Keeps the Vector @ $00:FF00
                        if (FnxAddressPtr < 0xFF00 && (FnxAddressPtr + DataBuffer.Length) > 0xFFFF || (FnxAddressPtr == BaseBankAddress && DataBuffer.Length > 0xFFFF))
                            PreparePacket2Write(DataBuffer, 0x00FF00, 0x00FF00, 256);
                    }
                    else
                    {
                        bool resetVector = false;

                        // Page FF is used to store IRQ vectors - this is only used when the program modifies the
                        // values between BaseBank + FF00 to BaseBank + FFFF
                        // BaseBank on RevB is $18
                        // BaseBank on RevC is $38
                        byte[] pageFF = PreparePacket2Read(0xFF00, 0x100);

                        // If send HEX files, each time we encounter a "bank" change - record 04 - send a new data block
                        string[] lines = System.IO.File.ReadAllLines(fcbBrowseFile.Filename);
                        int bank = 0;
                        int address = 0;

                        foreach (string l in lines)
                        {
                            if (l.StartsWith(":"))
                            {
                                string mark = l.Substring(0, 1);
                                string reclen = l.Substring(1, 2);
                                string offset = l.Substring(3, 4);
                                string rectype = l.Substring(7, 2);
                                string data = l[9..^11];
                                string checksum = l[^2..];

                                switch (rectype)
                                {
                                    case "00":
                                        int length = Convert.ToInt32(reclen, 16);
                                        byte[] DataBuffer = new byte[length];
                                        address = HexFile.GetByte(offset, 0, 2);

                                        for (int i = 0; i < data.Length; i += 2)
                                            DataBuffer[i / 2] = (byte)HexFile.GetByte(data, i, 1);

                                        PreparePacket2Write(DataBuffer, bank + address, 0, length);

                                        // TODO - make this backward compatible
                                        if (bank + address >= (BaseBankAddress + 0xFF00) && (bank + address) < (BaseBankAddress + 0xFFFF))
                                        {
                                            int pageFFLen = length - ((bank + address + length) - (BaseBankAddress + 0x1_0000));
                                            if (pageFFLen > length)
                                                pageFFLen = length;

                                            Array.Copy(DataBuffer, 0, pageFF, bank + address - (BaseBankAddress + 0xFF00), length);
                                            resetVector = true;
                                        }

                                        //-- UploadProgressBar.Increment(length);
                                        break;

                                    case "01":
                                        // Don't do anything... this is the end of file record.
                                        break;

                                    case "02":
                                        bank = HexFile.GetByte(data, 0, 2) * 16;
                                        break;

                                    case "04":
                                        bank = HexFile.GetByte(data, 0, 2) << 16;
                                        break;

                                    default:
                                        Console.WriteLine("Unsupport HEX record type:" + rectype);
                                        break;
                                }
                            }
                        }

                        if (chkDebug.Active)
                        {
                            // Update the Reset Vectors from the Binary Files Considering that the Files Keeps the Vector @ $00:FF00
                            if (resetVector)
                                PreparePacket2Write(pageFF, 0x00FF00, 0, 256);
                        }
                    }

                    if (chkReflash.Active)
                    {
                        //-- using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                        //         MessageType.Warning, ButtonsType.YesNo,
                        //         string.Format(
                        //         "Are you sure you want to reflash your A2560 System?",
                        //         res.Name, res.StartAddress, res.StartAddress + res.Length))) {
                        //     md.Title = "Reflash";
                        //     if (md.Run() == (int)ResponseType.Yes)
                        //     {
                        //         lblCountdown.Visible = true;

                        //         EraseFlash();

                        //         int SrcFlashAddress = Convert.ToInt32(txtRemoteAddr.Text.Replace(":", ""), 16);
                        //         ProgramFlash(SrcFlashAddress);

                        //         lblCountdown.Visible = false;
                        //     }
                        // }
                    }

                    if (chkDebug.Active)
                    {
                        // The Loading of the File is Done, Reset the FNX and Get out of Debug Mode
                        ExitFnxDebugMode();
                    }

                    HideProgressBarAfter5Seconds("Transfer Done! System Reset!");
                }
            }
            else if (rdoBlockSend.Active && _kernel.m68000Cpu != null)
            {
                // Get into Debug mode (Reset the CPU and keep it in that state and Gavin will take control of the bus)
                if (chkDebug.Active)
                    GetFnxInDebugMode();

                int blockAddress = Convert.ToInt32(txtLocalAddr.Text.Replace(":", ""), 16);

                // Read the data directly from emulator memory
                int offset = 0;
                int FnxAddressPtr = int.Parse(txtRemoteAddr.Text.Replace(":", ""), System.Globalization.NumberStyles.AllowHexSpecifier);
                byte[] DataBuffer = new byte[transmissionSize];  // Maximum 2 MB, example from $0 to $1F:FFFF.

                for (int start = blockAddress; start < blockAddress + transmissionSize; start++)
                    DataBuffer[offset++] = _kernel.m68000Cpu.MemMgr.ReadByte(start);

                SendData(DataBuffer, FnxAddressPtr, transmissionSize);

                // Update the Reset Vectors from the Binary Files Considering that the Files Keeps the Vector @ $00:FF00
                if (FnxAddressPtr < 0xFF00 && (FnxAddressPtr + DataBuffer.Length) > 0xFFFF || (FnxAddressPtr == BaseBankAddress && DataBuffer.Length > 0xFFFF))
                    PreparePacket2Write(DataBuffer, 0x00FF00, 0x00FF00, 256);

                if (chkDebug.Active)
                {
                    // The Loading of the File is Done, Reset the FNX and Get out of Debug Mode
                    ExitFnxDebugMode();
                }

                HideProgressBarAfter5Seconds("Transfer Done! System Reset!");
            }
            else
            {
                int blockAddress = Convert.ToInt32(txtRemoteAddr.Text.Replace(":", ""), 16);
                byte[] DataBuffer = new byte[transmissionSize];  // Maximum 2 MB, example from $0 to $1F:FFFF.

                if (FetchData(DataBuffer, blockAddress, transmissionSize, chkDebug.Active))
                {
                    MemoryRAM mem = new(blockAddress, transmissionSize);
                    mem.Load(DataBuffer, 0, 0, transmissionSize);

                    string from = blockAddress.ToString("X6");
                    string to   = (blockAddress + transmissionSize - 1).ToString("X6");

                    MemoryWindow tempMem = new()
                    {
                        Memory = mem,
                        Title = $"A2560 Memory from {from} to {to}"
                    };

                    tempMem.GotoAddress(blockAddress);
                    tempMem.AllowSave();
                    tempMem.Show();
                }

                btnSendBinary.Sensitive = true;
                btnDisconnect.Sensitive = true;
            }
        }

        private void AddressTextBox_TextChanged(object sender, EventArgs e)
        {
            int uploadSize = GetTransmissionSize();
            btnSendBinary.Sensitive = uploadSize > 0 && !btnConnect.Visible;
        }

        private void BlockAddressTextBox_Leave(object sender, EventArgs e)
        {
            //-- TextBox tb = (TextBox)sender;

            // string item = tb.Text.Replace(":", "");
            // if (item.Length > 0)
            // {
            //     int n = Convert.ToInt32(item, 16);
            //     String value = n.ToString("X6");
            //     tb.Text = value[..2] + ":" + value[2..];
            // }
        }

        //-- private void HideLabelTimer_tick(object sender, ElapsedEventArgs e)
        // {
        //     //-- hideLabelTimer.Sensitive = false;

        //     lblCountdown.Visible = false;
        //     lblCountdown.Text = "";
        // }
    }
}
