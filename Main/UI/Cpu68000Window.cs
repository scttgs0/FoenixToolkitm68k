using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Timers;
using System.Text;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore;
using FoenixCore.Processor.Generic;
using FoenixCore.Processor.m68000;
using FoenixCore.Simulator.FileFormat;
using FoenixCore.Simulator.Devices;


namespace FoenixToolkit.UI
{
    class Cpu68000Window : Window
    {
        public static Cpu68000Window Instance = null;
        private FoenixSystem _kernel = null;
        bool isRunning = false;

        System.Timers.Timer traceTimer = null;

        private int StepCounter = 0;
        private bool isStepOver = false;
        private const int LABEL_WIDTH = 100;

        private Breakpoints knl_breakpoints;
        private List<DebugLine> codeList = null;

        private readonly int[] ActiveLine = { 0, 0, 0 };  // PC, startofline, width - the point of this is to underline the ADDRESS name

        const int ROW_HEIGHT = 14;
        const int BASELINE_OFFSET = ROW_HEIGHT - 2;
        private int IRQPC = 0; // we only keep track of a single interrupt
        private int TopLineIndex = 0; // this is to help us track which line is the current one being executed

        Point position = new();
        private int MemoryLimit = 0;
        const double toRadians = 0.017453293;

        private Cairo.Color red = new(Color.Red.R / 255.0, Color.Red.G / 255.0, Color.Red.B / 255.0);
        private Cairo.Color darkRed = new(Color.DarkRed.R / 255.0, Color.DarkRed.G / 255.0, Color.DarkRed.B / 255.0);
        private Cairo.Color blue = new(Color.Blue.R / 255.0, Color.Blue.G / 255.0, Color.Blue.B / 255.0);
        private Cairo.Color lightBlue = new(Color.LightBlue.R / 255.0, Color.LightBlue.G / 255.0, Color.LightBlue.B / 255.0);
        private Cairo.Color orange = new(Color.Orange.R / 255.0, Color.Orange.G / 255.0, Color.Orange.B / 255.0);
        private Cairo.Color yellow = new(Color.Yellow.R / 255.0, Color.Yellow.G / 255.0, Color.Yellow.B / 255.0);
        private Cairo.Color black = new(0.2, 0.2, 0.2);
        private Cairo.Color white = new(Color.White.R / 255.0, Color.White.G / 255.0, Color.White.B / 255.0);

#pragma warning disable CS0649  // never assigned
        //[GUI] Box boxUpperLeft;
        //[GUI] Button btnClear;
        //[GUI] Button btnJump;
        //[GUI] Button btnMinus;
        //[GUI] Button btnPlus;
        //[GUI] Button btnReset;
        [GUI] Button btnRun;
        [GUI] Button btnStep;
        [GUI] Button btnStepOver;
        //[GUI] Button btnWatch;
        [GUI] CheckButton chkBreakOnIRQ;
        [GUI] CheckButton chkIrq0_Mouse;
        [GUI] CheckButton chkIrq0_FDC;
        [GUI] CheckButton chkIrq0_RTC;
        [GUI] CheckButton chkIrq0_TMR2;
        [GUI] CheckButton chkIrq0_TMR1;
        [GUI] CheckButton chkIrq0_TMR0;
        [GUI] CheckButton chkIrq0_SOL;
        [GUI] CheckButton chkIrq0_SOF;
        [GUI] CheckButton chkIrq1_SDC;
        [GUI] CheckButton chkIrq1_MPU401;
        [GUI] CheckButton chkIrq1_COM1;
        [GUI] CheckButton chkIrq1_COM2;
        [GUI] CheckButton chkIrq1_KBD;
        [GUI] CheckButton chkIrq2_OPL2L;
        [GUI] CheckButton chkIrq2_OPL2R;
        //[GUI] ComboBoxText cboBreakpoint;
        [GUI] Entry txtAddress;
        [GUI] Entry txtLastLine;
        [GUI] Label lblBreakpoint;
        [GUI] Fixed pnlRegisters;
        [GUI] TextView tvwDisassembly;
        //[GUI] TextView tvwHeader;
        [GUI] TextView tvwStack;
        [GUI] Cpu68000RegisterDisplayControl ucRegisterDisplay;
#pragma warning restore CS0649

        public Cpu68000Window() : this(new Builder("Cpu68000Window.ui"))
        {
            ucRegisterDisplay = new();
            pnlRegisters.Add(ucRegisterDisplay);
        }

        private Cpu68000Window(Builder builder) : base(builder.GetRawOwnedObject("Cpu68000Window"))
        {
            builder.Autoconnect(this);
            HideOnDelete();

            Instance = this;
            DisableIRQs(true);
        }

        public void Pause()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            _kernel.m68000Cpu.DebugPause = true;

            if (traceTimer != null)
                traceTimer.Enabled = false;

            _kernel.m68000Cpu.Halt();

            RefreshStatus();

            btnRun.Label = "Run (F5)";
            isRunning = false;

            ucRegisterDisplay.AutoUpdate = false;
        }

        public void Run()
        {
            btnRun.Activate();
        }

        public void DisableIRQs(bool value)
        {
            chkBreakOnIRQ.Active = !value;
        }

        public void ClearTrace()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            StepCounter = 0;
            IRQPC = 0;

            //-- _kernel.m68000Cpu.Stack.Reset();

            tvwStack.Buffer.Text = "";
            txtLastLine.Text = "";

            tvwDisassembly.QueueDraw();
        }

        public void SetKernel(FoenixSystem kernel)
        {
            if ((_kernel = kernel) == null)
                return;

            MemoryLimit = _kernel.MemMgr.RAM.Length;

            ucRegisterDisplay.CPU = _kernel.m68000Cpu;

            knl_breakpoints = _kernel.Breakpoints;

            if (knl_breakpoints.Count > 0)
            {
                lblBreakpoint.Text = knl_breakpoints.Count.ToString() + " BP";

                //-- // Update the combo
                // foreach (KeyValuePair<int, string> kvp in knl_breakpoints)
                // {
                //     cboBreakpoint.Items.Add(kvp.Value);
                //     UpdateDebugLines(true);
                // }
            }
            else
                lblBreakpoint.Text = "Breakpoint";

            UpdateQueue();

            int pc = _kernel.m68000Cpu.PC;
            DebugLine line = GetExecutionInstruction(pc);
            if (line == null)
                GenerateNextInstruction(pc);

            tvwDisassembly.QueueDraw();
        }

        private void UpdateQueue()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            if (_kernel.lstFile != null && _kernel.lstFile.Lines.Count > 0)
            {
                codeList = new List<DebugLine>(_kernel.lstFile.Lines.Count);
                foreach (DebugLine line in _kernel.lstFile.Lines.Values)
                {
                    codeList.Add(line);
                }
            }
            else
            {
                codeList = new List<DebugLine>(tvwDisassembly.AllocatedHeight / ROW_HEIGHT);
                GenerateNextInstruction(_kernel.m68000Cpu.PC);
            }
        }

        private void ThreadProc()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            while (!_kernel.m68000Cpu.DebugPause)
                ExecuteStep();
        }

        private void RefreshAddress()
        {
            if (string.IsNullOrEmpty(txtAddress.Text) || string.IsNullOrWhiteSpace(txtAddress.Text))
                txtAddress.Text = "00:0800";

            int jumpAddress = Convert.ToInt32(txtAddress.Text.Replace(":", ""), 16);
            string address = jumpAddress.ToString("X6");

            txtAddress.Text = $"{address[..2]}:{address[2..]}";
        }

        private void RefreshIrqDisplay()
        {
            bool visible = chkBreakOnIRQ.Active;

            chkIrq0_SOF.Visible = visible;
            chkIrq0_SOL.Visible = visible;
            chkIrq0_TMR0.Visible = visible;
            chkIrq0_TMR1.Visible = visible;
            chkIrq0_TMR2.Visible = visible;
            chkIrq0_RTC.Visible = visible;
            chkIrq0_FDC.Visible = visible;
            chkIrq0_Mouse.Visible = visible;

            chkIrq1_KBD.Visible = visible;
            chkIrq1_COM2.Visible = visible;
            chkIrq1_COM1.Visible = visible;
            chkIrq1_MPU401.Visible = visible;
            chkIrq1_SDC.Visible = visible;

            chkIrq2_OPL2L.Visible = visible;
            chkIrq2_OPL2R.Visible = visible;
        }

        private void RefreshStatus()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            Title = "Debug: " + StepCounter.ToString();

            if (_kernel.m68000Cpu.DebugPause)
            {
                tvwDisassembly.QueueDraw();
                UpdateStackDisplay();
            }

            ucRegisterDisplay.UpdateRegisters();
        }

        public void UpdateStackDisplay()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            StringBuilder c = new();
            //-- c.AppendLine("Top: $" + _kernel.m68000Cpu.Stack.TopOfStack.ToString("X4"));
            // c.AppendLine("SP : $" + _kernel.m68000Cpu.Stack.Value.ToString("X4"));
            // c.AppendLine("N  : " + (_kernel.m68000Cpu.Stack.TopOfStack - _kernel.m68000Cpu.Stack.Value).ToString().PadLeft(4));
            // c.AppendLine("───────────");

            // // Display all values on the stack
            // if (_kernel.m68000Cpu.Stack.Value != _kernel.m68000Cpu.Stack.TopOfStack)
            // {
            //     int i = _kernel.m68000Cpu.Stack.TopOfStack - _kernel.m68000Cpu.Stack.Value;
            //     if (i > 28)
            //         i = 28;

            //     while (i > 0)
            //     {
            //         int address = _kernel.m68000Cpu.Stack.Value + i;
            //         c.AppendLine(address.ToString("X4") + " " + _kernel.m68000Cpu.MemMgr[address].ToString("X2"));
            //         --i;
            //     }
            // }

            tvwStack.Buffer.Text = c.ToString();
        }

        /// <summary>
        /// Determine if the objects in IRQ Registers match on of the checkboxes.
        /// </summary>
        /// <returns></returns>
        private bool InterruptMatchesCheckboxes()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            // Read Interrupt Register 0
            byte reg0 = _kernel.MemMgr.INTERRUPT.ReadByte(0);

            if (chkIrq0_SOF.Active && (reg0 & (byte)Register0.FNX0_INT00_SOF) != 0)
                return true;

            if (chkIrq0_SOL.Active && (reg0 & (byte)Register0.FNX0_INT01_SOL) != 0)
                return true;

            if (chkIrq0_TMR0.Active && (reg0 & (byte)Register0.FNX0_INT02_TMR0) != 0)
                return true;

            if (chkIrq0_TMR1.Active && (reg0 & (byte)Register0.FNX0_INT03_TMR1) != 0)
                return true;

            if (chkIrq0_TMR2.Active && (reg0 & (byte)Register0.FNX0_INT04_TMR2) != 0)
                return true;

            if (chkIrq0_Mouse.Active && (reg0 & (byte)Register0.FNX0_INT07_MOUSE) != 0)
                return true;

            // Read Interrupt Register 1
            byte reg1 = _kernel.MemMgr.INTERRUPT.ReadByte(1);

            if (chkIrq1_SDC.Active && (reg1 & (byte)Register1.FNX1_INT07_SDCARD) != 0)
                return true;

            if (chkIrq1_KBD.Active && (reg1 & (byte)Register1.FNX1_INT00_KBD) != 0)
                return true;

            //Read Interrupt Register 2
            _ = _kernel.MemMgr.INTERRUPT.ReadByte(2);

            //Read Interrupt Register 3
            _ = _kernel.MemMgr.INTERRUPT.ReadByte(3);

            return false;
        }

        private delegate void breakpointSetter(int pc);

        private void BreakpointReached(int pc)
        {
            btnRun.Label = "Run (F5)";
            isRunning = false;

            if (isStepOver)
            {
                isStepOver = false;
                knl_breakpoints.Remove(pc.ToString("X"));
            }
            else
            {
                //-- cboBreakpoint.Text = knl_breakpoints.GetHex(pc);
            }

            RefreshStatus();

            btnRun.Sensitive = true;
        }

        private delegate void nullParamMethod();

        /// <summary>
        /// Executes next step of 65C816 code, logs dubeugging data
        /// if debugging check box is set on CPU Window
        /// </summary>
        public void ExecuteStep()
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            StepCounter++;

            DebugLine line = null;

            int previousPC = _kernel.m68000Cpu.PC;

            if (!_kernel.m68000Cpu.ExecuteNext())
            {
                int nextPC = _kernel.m68000Cpu.PC;
                if (nextPC > MemoryLimit)
                {
                    if (traceTimer != null)
                        traceTimer.Enabled = false;

                    _kernel.m68000Cpu.DebugPause = true;

                    string errorMessage = "PC exceeds memory limit.  Calling instruction at address: $" + previousPC.ToString("X6");

                    //-- if (txtLastLine.InvokeRequired)
                    // {
                    //     txtLastLine.Invoke(new lastLineDelegate(ShowLastLine), new object[] { errorMessage });
                    // }
                    // else
                    // {
                    //     txtLastLine.Text = errorMessage;
                    // }

                    return;
                }

                if (knl_breakpoints.ContainsKey(nextPC) ||
                     _kernel.m68000Cpu.CurrentOpcode.Value == 0 ||
                     (chkBreakOnIRQ.Active && (_kernel.m68000Cpu.Pins.InterruptPinActive && InterruptMatchesCheckboxes()))
                   )
                {
                    if (_kernel.m68000Cpu.CurrentOpcode.Value == 0)
                    {
                        //-- if (txtLastLine.InvokeRequired)
                        // {
                        //     txtLastLine.Invoke((MethodInvoker)delegate
                        //    {
                        //        txtLastLine.Text = "BRK OpCode read";
                        //    });
                        // }
                        // else
                        // {
                        //     txtLastLine.Text = "BRK OpCode read";
                        // }
                    }

                    if (/*(traceTimer != null && traceTimer.Enabled) ||*/ _kernel.m68000Cpu.CurrentOpcode.Value == 0)
                    {
                        if (traceTimer != null)
                           traceTimer.Enabled = false;

                        _kernel.m68000Cpu.DebugPause = true;

                        //queue.Clear();
                    }

                    if (_kernel.m68000Cpu.Pins.InterruptPinActive /*-- && !_kernel.m68000Cpu.Flags.IrqDisable*/)
                    {
                        IRQPC = _kernel.m68000Cpu.PC;
                    }

                    if (line == null)
                    {
                        line = GetExecutionInstruction(nextPC);
                        if (line == null)
                            GenerateNextInstruction(nextPC);
                    }

                    //-- Invoke(new breakpointSetter(BreakpointReached), new object[] { nextPC });
                }
            }

            // // Print the next instruction on lastLine
            // if (!UpdateTraceTimer.Enabled && line == null)
            // {
            //     int pc = _kernel.m68000Cpu.PC;

            //     line = GetExecutionInstruction(pc);
            //     if (line == null)
            //     {
            //         GenerateNextInstruction(pc);
            //     }
            // }
        }

        private delegate void lastLineDelegate(string line);

        private void ShowLastLine(string line)
        {
            txtLastLine.Text = line;
        }

        private DebugLine GetExecutionInstruction(int PC)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            if (_kernel.lstFile != null)
            {
                DebugLine dl = codeList.Find(x => x.PC == PC);
                return dl;
            }
            else
                return null;
        }

        private void GenerateNextInstruction(int pc)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            OpCode oc = _kernel.m68000Cpu.PreFetch();
            int ocLength = oc.Length;
            byte[] command = new byte[ocLength];

            for (int i = 0; i < ocLength; ++i)
                command[i] = _kernel.MemMgr.RAM.ReadByte(pc + i);

            string opcodes = oc.ToString(_kernel.m68000Cpu.ReadSignature(ocLength, pc));

            //string status = "";

            DebugLine line = new(pc);
            line.SetOpcodes(command);
            line.SetMnemonic(opcodes);

            //-- if (!txtLastLine.InvokeRequired)
            //     txtLastLine.Text = line.ToString();
            // else
            // {
            //     try
            //     {
            //         txtLastLine.Invoke(new lastLineDelegate(ShowLastLine), new object[] { line.ToString() });
            //     }
            //     finally
            //     { }
            // }

            // find the proper place to insert the line, based on the PC
            int index = 0;
            for (index = 0; index < codeList.Count; index++)
            {
                DebugLine l = codeList[index];
                if (l.PC > pc)
                    break;
            }

            codeList.Add(line);
        }

        private void UpdateDebugLines(bool state)
        {
            //-- cboBreakpoint.BeginUpdate();
            // cboBreakpoint.Items.Clear();

            // foreach (KeyValuePair<int, string> bp in knl_breakpoints)
            //     cboBreakpoint.Items.Add(bp.Value);

            // cboBreakpoint.EndUpdate();
            tvwDisassembly.QueueDraw();
        }

        private void on_Cpu68000Window_map(object sender, EventArgs e)
        {
            traceTimer = new()
            {
               Interval = 1000,
               Enabled = false,
               AutoReset = true
            };

            traceTimer.Elapsed += on_traceTimer_tick;

            ClearTrace();
            RefreshStatus();
            RefreshIrqDisplay();

            //-- Tooltip.SetToolTip(AddBPOverlayButton, "Add Breakpoint");
            // Tooltip.SetToolTip(DeleteBPOverlayButton, "Remove Breakpoint");
            // Tooltip.SetToolTip(InspectOverlayButton, "Browse Memory");
            // Tooltip.SetToolTip(StepOverOverlayButton, "Step Over");
        }

        private void on_Cpu68000Window_unmap(object sender, EventArgs e)
        {
            if (traceTimer != null)
            {
               traceTimer.Stop();
               traceTimer.Dispose();
               traceTimer = null;
            }

            // Kill the thread
            if (_kernel != null)
            {
                _kernel.m68000Cpu.DebugPause = true;
                _kernel.m68000Cpu.CPUThread?.Join();
            }
        }

        private void on_Cpu68000Window_key_release_event(object sender, KeyReleaseEventArgs e)
        {
            switch (e.Event.Key)
            {
                case Gdk.Key.F5:
                    btnRun.GrabFocus();
                    btnRun.Activate();
                    break;

                case Gdk.Key.F6:
                    btnStep.GrabFocus();
                    btnStep.Activate();
                    break;

                case Gdk.Key.F7:
                    btnStepOver.GrabFocus();
                    btnStepOver.Activate();
                    break;
            }
        }

        private void on_tvwDisassembly_draw(object sender, DrawnArgs e)
        {
            var cr = e.Cr;

            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            if (!(_kernel.m68000Cpu.DebugPause && codeList != null))
            {
                cr.SetSourceColor(lightBlue);
                var rec = tvwDisassembly.VisibleRect;
                cr.Rectangle(rec.X, rec.Y, rec.Width, rec.Height);
                cr.Fill();

                cr.SetSourceColor(black); 
                cr.SelectFontFace("Consolas", Cairo.FontSlant.Normal, Cairo.FontWeight.Bold);
                cr.SetFontSize(14);
                cr.MoveTo(20, tvwDisassembly.AllocatedHeight / 2);
                cr.ShowText("Running... Press 'Pause' to debug.");

                return;
            }

            cr.SelectFontFace("Consolas", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            cr.SetFontSize(14);

            int currentPC = _kernel.m68000Cpu.PC;
            bool paint = false;
            int painted = 0;
            int index = 0;

            // Draw the position box
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                int col = 12;

                cr.SetSourceColor(lightBlue);
                cr.Rectangle(col, row * ROW_HEIGHT, 7 * 6, 14);
                cr.Fill();
            }

            bool offsetPrinted = false;

            foreach (DebugLine line in codeList)
            {
                if (line == null)
                {
                    index++;
                    continue;
                }

                if (line.PC == currentPC)
                {
                    paint = true;
                    TopLineIndex = index;

                    if (!offsetPrinted)
                    {
                        if (index > 4)
                        {
                            TopLineIndex -= 5;

                            for (int c = 5; c > 0; --c)
                            {
                                DebugLine q0 = codeList[index - c];

                                // Draw the label
                                if (q0.label != null)
                                {
                                    cr.SetSourceColor(blue);
                                    cr.Rectangle(1, painted * ROW_HEIGHT, LABEL_WIDTH + 2, ROW_HEIGHT + 2);
                                    cr.Fill();

                                    cr.SetSourceColor(yellow);
                                    cr.MoveTo(2, painted * ROW_HEIGHT + BASELINE_OFFSET);
                                    cr.ShowText(q0.label);
                                }

                                if (q0.PC == IRQPC)
                                {
                                    cr.SetSourceColor(orange);
                                    cr.Rectangle(0, painted * ROW_HEIGHT, tvwDisassembly.AllocatedWidth, ROW_HEIGHT);
                                    cr.Fill();
                                }

                                if (knl_breakpoints.ContainsKey(q0.PC))
                                {
                                    cr.SetSourceColor(white);
                                    cr.Arc(LABEL_WIDTH - ROW_HEIGHT + ROW_HEIGHT / 2 - 1, painted * ROW_HEIGHT + ROW_HEIGHT / 2,
                                        (ROW_HEIGHT + 1) / 2, 0.0, 360.0 * toRadians);

                                    cr.SetSourceColor(darkRed);
                                    cr.Arc(LABEL_WIDTH - ROW_HEIGHT + ROW_HEIGHT / 2, painted * ROW_HEIGHT + ROW_HEIGHT / 2 + 1,
                                        ROW_HEIGHT / 2, 0.0, 360.0 * toRadians);
                                    cr.Fill();
                                }

                                // Check if the memory still matches the opcodes
                                if (!q0.CheckOpcodes(_kernel.MemMgr.RAM))
                                {
                                    cr.SetSourceColor(red);
                                    cr.Rectangle(LABEL_WIDTH + 3, painted * ROW_HEIGHT, tvwDisassembly.AllocatedWidth, ROW_HEIGHT);
                                    cr.Fill();
                                }

                                cr.SetSourceColor(black);
                                cr.MoveTo(LABEL_WIDTH + 2, painted * ROW_HEIGHT + BASELINE_OFFSET);
                                cr.ShowText(q0.ToString());

                                if (q0.PC == ActiveLine[0])
                                {
                                    cr.MoveTo(LABEL_WIDTH + ActiveLine[1], (painted + 1) * ROW_HEIGHT);
                                    cr.LineTo(LABEL_WIDTH + ActiveLine[1] + ActiveLine[2], (painted + 1) * ROW_HEIGHT);
                                }

                                painted++;
                            }
                        }

                        offsetPrinted = true;
                    }

                    if (line.PC == IRQPC)
                        cr.SetSourceColor(orange);
                    else
                        cr.SetSourceColor(lightBlue);
                    cr.Rectangle(LABEL_WIDTH + 1, painted * ROW_HEIGHT, tvwDisassembly.AllocatedWidth, ROW_HEIGHT);
                    cr.Fill();
                }

                if (painted > 28)
                {
                    paint = false;
                    break;
                }

                if (paint)
                {
                    if (line.label != null)
                    {
                        cr.SetSourceColor(blue);
                        cr.Rectangle(1, painted * ROW_HEIGHT, LABEL_WIDTH + 2, ROW_HEIGHT + 2);
                        cr.Fill();

                        cr.SetSourceColor(yellow);
                        cr.MoveTo(2, painted * ROW_HEIGHT + BASELINE_OFFSET);
                        cr.ShowText(line.label);
                    }

                    if (knl_breakpoints.ContainsKey(line.PC))
                    {
                        cr.SetSourceColor(white);
                        cr.Arc(LABEL_WIDTH - ROW_HEIGHT + ROW_HEIGHT / 2 - 1, painted * ROW_HEIGHT + ROW_HEIGHT / 2,
                            (ROW_HEIGHT + 1) / 2, 0.0, 360.0 * toRadians);

                        cr.SetSourceColor(darkRed);
                        cr.Arc(LABEL_WIDTH - ROW_HEIGHT + ROW_HEIGHT / 2, painted * ROW_HEIGHT + ROW_HEIGHT / 2 + 1,
                            ROW_HEIGHT / 2, 0.0, 360.0 * toRadians);
                        cr.Fill();
                    }

                    if (line.PC == IRQPC)
                    {
                        cr.SetSourceColor(orange);
                        cr.Rectangle(0, painted * ROW_HEIGHT, tvwDisassembly.AllocatedWidth, ROW_HEIGHT);
                        cr.Fill();
                    }

                    // Check if the memory still matches the opcodes
                    if (!line.CheckOpcodes(_kernel.MemMgr.RAM))
                    {
                        cr.SetSourceColor(red);
                        cr.Rectangle(0, painted * ROW_HEIGHT, tvwDisassembly.AllocatedWidth, ROW_HEIGHT);
                        cr.Fill();
                    }

                    cr.SetSourceColor(black);
                    cr.MoveTo(102, painted * ROW_HEIGHT + BASELINE_OFFSET);
                    cr.ShowText(line.ToString());

                    if (line.PC == ActiveLine[0])
                    {
                        cr.MoveTo(LABEL_WIDTH + ActiveLine[1], (painted + 1) * ROW_HEIGHT);
                        cr.LineTo(LABEL_WIDTH + ActiveLine[1] + ActiveLine[2], (painted + 1) * ROW_HEIGHT);
                    }

                    painted++;
                }

                index++;
            }
        }

        /// <summary>
        /// When checked, we receive interrupts.  When unchecked, all interrupt boxes are hidden and disabled.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void on_chkBreakOnIRQ_toggled(object sender, EventArgs e)
        {
            RefreshIrqDisplay();
        }

        private void on_txtAddress_activate(object sender, EventArgs e)
        {
            RefreshAddress();
        }

        private void on_txtAddress_focus_out_event(object sender, FocusOutEventArgs e)
        {
            RefreshAddress();
        }

        private void on_btnRun_clicked(object sender, EventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            //-- tvwDisassembly_Leave(sender, e);

            if (!isRunning)
            {
                // Clear the interrupt
                IRQPC = -1;

                _kernel.m68000Cpu.DebugPause = false;
                txtLastLine.Text = "";

                _kernel.m68000Cpu.CPUThread = new Thread(new ThreadStart(ThreadProc));

                if (traceTimer != null)
                   traceTimer.Enabled = true;

                _kernel.m68000Cpu.CPUThread.Start();

                btnRun.Label = "Pause (F5)";
                isRunning = true;

                ucRegisterDisplay.AutoUpdate = true;

                tvwDisassembly.QueueDraw();
            }
            else
                Pause();
        }

        private void on_btnStep_clicked(object sender, EventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            //-- tvwDisassembly_Leave(sender, e);

            _kernel.m68000Cpu.DebugPause = true;
            _kernel.m68000Cpu.CPUThread?.Join();

            isRunning = false;
            btnRun.Label = "Run (F5)";
            btnRun.Sensitive = true;

            if (traceTimer != null)
               traceTimer.Enabled = false;

            ExecuteStep();
            RefreshStatus();

            _kernel.m68000Cpu.DebugPause = true;
        }

        private void on_btnStepOver_clicked(object sender, EventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            int pc = _kernel.m68000Cpu.PC;
            if (pc > MemoryLimit)
            {
                string errorMessage = "PC exceeds memory limit.";
                txtLastLine.Text = errorMessage;

                //-- ucRegisterDisplay.PC.BackColor = Color.Red;

                return;
            }

            DebugLine line = GetExecutionInstruction(pc);

            if (line != null && line.StepOver)
            {
                // Set a breakpoint to the next address
                int nextAddress = pc + line.commandLength;
                int newValue = knl_breakpoints.Add(nextAddress.ToString("X"));

                if (newValue != -1)
                {
                    // Run the CPU until the breakpoint is reached
                    btnRun.Activate();

                    // Ensure the breakpoint is removed
                    isStepOver = true;

                    btnRun.Label = "Run (F5)";
                    isRunning = false;
                }
            }
            else
            {
                ExecuteStep();
                RefreshStatus();
            }
        }

        private void on_btnReset_clicked(object sender, EventArgs e)
        {
            //-- MainWindow.Instance.RestartMenuItemClick(sender, e);
        }

        private void on_btnWatch_clicked(object sender, EventArgs e)
        {
            //-- MainWindow.Instance.WatchListToolStripMenuItem_Click(sender, e);
        }

        private void on_btnClear_clicked(object sender, EventArgs e)
        {
            ClearTrace();
        }

        private void on_btnJump_clicked(object sender, EventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            int pc = Breakpoints.GetIntFromHex(txtAddress.Text);
            _kernel.m68000Cpu.PC = pc;
            ClearTrace();

            DebugLine line = GetExecutionInstruction(pc);
            if (line == null)
                GenerateNextInstruction(pc);

            QueueDraw();
        }

        private void on_btnPlus_clicked(object sender, EventArgs e)
        {
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                if (codeList.Count > TopLineIndex + row)
                {
                    DebugLine line = codeList[TopLineIndex + row];
                    string value = line.PC.ToString("X6");
                    //-- cboBreakpoint.Text = "$" + value.Substring(0, 2) + ":" + value[2..];
                    // AddBPButton_Click(null, null);
                }
            }
        }

        private void on_btnMinus_clicked(object sender, EventArgs e)
        {
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                if (codeList.Count > TopLineIndex + row)
                {
                    DebugLine line = codeList[TopLineIndex + row];
                    string value = line.PC.ToString("X6");
                    //-- cboBreakpoint.Text = "$" + value.Substring(0, 2) + ":" + value[2..];
                    // DeleteBPButton_Click(null, null);
                }
            }
        }

        // Don't try to display the CPU information too often
        private void on_traceTimer_tick(object sender, ElapsedEventArgs e)
        {
           RefreshStatus();
        }

/*--
        private void tvwDisassembly_MouseMove(object sender, MouseEventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            if (_kernel.m68000Cpu.DebugPause)
            {
                if (e.X > 2 && e.X < 2 + LABEL_WIDTH)
                {
                    int top = e.Y / ROW_HEIGHT * ROW_HEIGHT;

                    ActiveLine[0] = 0;

                    tvwDisassembly.Cursor = Cursors.Default;

                    if ((e.Y / ROW_HEIGHT != position.Y / ROW_HEIGHT || position.Y == -1) && e.Y / ROW_HEIGHT < 28)
                    {
                        position.X = e.X;
                        position.Y = e.Y;

                        AddBPOverlayButton.Top = tvwDisassembly.Top + top - 1;
                        DeleteBPOverlayButton.Top = tvwDisassembly.Top + top - 1;
                        InspectOverlayButton.Top = tvwDisassembly.Top + top - 1;
                        StepOverOverlayButton.Top = tvwDisassembly.Top + top - 1;
                        LabelOverlayButton.Top = tvwDisassembly.Top + top - 1;

                        AddBPOverlayButton.Left = 3;
                        DeleteBPOverlayButton.Left = AddBPOverlayButton.Left + AddBPOverlayButton.Width;
                        InspectOverlayButton.Left = DeleteBPOverlayButton.Left + DeleteBPOverlayButton.Width;
                        LabelOverlayButton.Left = InspectOverlayButton.Left + InspectOverlayButton.Width;
                        StepOverOverlayButton.Left = LabelOverlayButton.Left + LabelOverlayButton.Width;

                        AddBPOverlayButton.Visible = true;
                        DeleteBPOverlayButton.Visible = true;
                        InspectOverlayButton.Visible = true;
                        LabelOverlayButton.Visible = true;

                        int row = position.Y / ROW_HEIGHT;

                        // Only show the Step Over button for Jump and Branch commands
                        if (codeList != null && codeList.Count > TopLineIndex + row)
                        {
                            DebugLine line = codeList[TopLineIndex + row];
                            StepOverOverlayButton.Visible = line.StepOver;
                        }
                    }
                }
                else
                {
                    position.X = -1;
                    position.Y = -1;

                    AddBPOverlayButton.Visible = false;
                    DeleteBPOverlayButton.Visible = false;
                    InspectOverlayButton.Visible = false;
                    StepOverOverlayButton.Visible = false;
                    LabelOverlayButton.Visible = false;

                    ActiveLine[0] = 0;

                    int row = e.Y / ROW_HEIGHT;

                    if (codeList != null && codeList.Count > TopLineIndex + row)
                    {
                        DebugLine line = codeList[TopLineIndex + row];

                        // try to highlight the word we are over 
                        if (line.HasAddress())
                        {
                            ActiveLine[0] = line.PC;
                            ActiveLine[1] = 174;
                            ActiveLine[2] = line.GetAddressName().Length * 7;
                            tvwDisassembly.Cursor = Cursors.Hand;
                        }
                    }

                    if (ActiveLine[0] == 0)
                        tvwDisassembly.Cursor = Cursors.Default;
                }

                tvwDisassembly.QueueDraw();
            }
        }

        private void tvwDisassembly_Leave(object sender, EventArgs e)
        {
            position.X = -1;
            position.Y = -1;

            AddBPOverlayButton.Visible = false;
            DeleteBPOverlayButton.Visible = false;
            InspectOverlayButton.Visible = false;
            StepOverOverlayButton.Visible = false;
            LabelOverlayButton.Visible = false;

            tvwDisassembly.QueueDraw();
        }

        private void LabelOverlayButton_Click(object sender, EventArgs e)
        {
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                if (codeList.Count > TopLineIndex + row)
                {
                    DebugLine line = codeList[TopLineIndex + row];
                    string oldValue = line.label;
                    string value = Interaction.InputBox("Enter Label for Address: $" + line.PC.ToString("X6").Insert(2, ":"), "Label Dialog", oldValue, Left + LabelOverlayButton.Left + LabelOverlayButton.Width, Top + LabelOverlayButton.Top);
                    line.label = value;
                    tvwDisassembly.QueueDraw();
                }
            }
        }

        private void AddBPButton_Click(object sender, EventArgs e)
        {
            if (cboBreakpoint.Text.Trim() != "")
            {
                int newValue = knl_breakpoints.Add(cboBreakpoint.Text.Trim().Replace(">", ""));
                if (newValue > -1)
                {
                    cboBreakpoint.Text = knl_breakpoints.Format(newValue.ToString("X"));
                    UpdateDebugLines(true);
                    lblBreakpoint.Text = knl_breakpoints.Count.ToString() + " BP";
                }
            }
        }

        private void DeleteBPButton_Click(object sender, EventArgs e)
        {
            if (cboBreakpoint.Text != "")
            {
                knl_breakpoints.Remove(cboBreakpoint.Text);
                UpdateDebugLines(false);
                cboBreakpoint.Items.Remove(cboBreakpoint.Text);
            }

            if (knl_breakpoints.Count == 0)
                cboBreakpoint.Text = "";
            else
                cboBreakpoint.Text = knl_breakpoints.Values[0];

            lblBreakpoint.Text = knl_breakpoints.Count.ToString() + " BP";
        }

        private void InspectButton_Click(object sender, EventArgs e)
        {
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                DebugLine line = codeList[TopLineIndex + row];
                MemoryWindow.Instance.GotoAddress(line.PC & 0xFF_FF00);
                MemoryWindow.Instance.BringToFront();
            }
        }

        private void StepOverOverlayButton_Click(object sender, EventArgs e)
        {
            if (position.X > 0 && position.Y > 0)
            {
                int row = position.Y / ROW_HEIGHT;
                DebugLine line = codeList[TopLineIndex + row];
                // Set a breakpoint to the next address
                int nextAddress = line.PC + line.commandLength;
                int newValue = knl_breakpoints.Add(nextAddress.ToString("X"));

                if (newValue != -1)
                {
                    // Run the CPU until the breakpoint is reached
                    btnRun.Activate();

                    // Ensure the breakpoint is removed
                    isStepOver = true;
                }
            }
        }

        private void StepsInput_Enter(object sender, EventArgs e)
        {
            if (!(sender is Entry tb))
                return;

            tb.SelectAll();
        }

        private void tvwDisassembly_MouseClick(object sender, MouseEventArgs e)
        {
            if (_kernel == null)
                throw new InvalidOperationException("Kernel is undefined");

            if (ActiveLine[0] != 0 && _kernel.lstFile.Lines.ContainsKey(ActiveLine[0]))
            {
                DebugLine line = _kernel.lstFile.Lines[ActiveLine[0]];
                if (line != null)
                {
                    string name = line.GetAddressName();
                    int address = line.GetAddress();
                    WatchedMemory mem = new(name, address, 0, 0);

                    if (_kernel.WatchList.ContainsKey(address))
                    {
                        _kernel.WatchList.Remove(address);
                    }

                    _kernel.WatchList.Add(address, mem);
                    MainWindow.Instance.WatchListToolStripMenuItem_Click(sender, e);
                }
            }
        }
*/
    }
}
