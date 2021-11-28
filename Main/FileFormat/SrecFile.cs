using System;
using System.IO;

using Gtk;

using FoenixCore.MemoryLocations;


namespace FoenixCore.Simulator.FileFormat
{
    public class SrecFile
    {
        static public string Load(MemoryRAM ram, string Filename, int gabeAddressBank, out int startAddress, out int length)
        {
            string processedFileName = Filename;

            startAddress = -1;
            length = -1;

            if (!File.Exists(Filename))
            {
                processedFileName = promptForFile();
                if (processedFileName == null)
                    return null;
            }

            string[] lines = File.ReadAllLines(processedFileName);

            foreach (string ln in lines)
            {
                string mark = ln[..1];
                if (!mark.Equals("S"))
                {
                    using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                            MessageType.Error, ButtonsType.Ok, "This doesn't appear to be an Motorola SREC file.")) {
                        md.Title = "Error Loading SREC File";
                        md.Run();
                    }
                    break;
                }

                string rectype = ln[1..2];
                string reclen = ln[2..4];
                string offset = rectype switch
                {
                    "2" or "6" or "8" => ln[4..10],     // 24-bit address
                    "3" or "7" => ln[4..12],            // 32-bit address
                    _ => ln[4..8]                       // 16-bit address
                };

                int dataIndex = 4 + offset.Length;
                string data = ln[dataIndex..^2];
                string checksum = ln[^2..];

                // process data record for record type 1, 2, or 3
                if ("123".IndexOf(rectype) != -1)
                {
                    int address = GetByte(offset, 0, offset.Length / 2);

                    for (int i = 0; i < data.Length; i += 2)
                    {
                        byte b = (byte)GetByte(data, i, 1);
                        ram.WriteByte(address, b);

                        // Copy bank $38 or $18 to page 0
                        //-- if (bank == gabeAddressBank)
                        //     ram.WriteByte(address, b);

                        address++;
                    }
                }
            }

            return processedFileName;
        }

        static private string promptForFile()
        {
            using (FileChooserDialog filechooser =
                new("Select a kernel file", null,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept))
            {
                using (FileFilter ff1 = new())
                {
                    ff1.Name = "SREC Files";
                    ff1.AddPattern("*.srec");
                    filechooser.AddFilter(ff1);
                }
                using (FileFilter ff2 = new())
                {
                    ff2.Name = "All Files";
                    ff2.AddPattern("*.*");
                    filechooser.AddFilter(ff2);
                }

                if (filechooser.Run() != (int)ResponseType.Accept) 
                    return null;

                return filechooser.Filename;
            }
        }

        // Read a two-character hex string into a byte
        static public int GetByte(string data, int startPos, int bytes)
        {
            return Convert.ToInt32(data.Substring(startPos, bytes * 2), 16);
        }
    }
}
