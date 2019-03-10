using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compressor
{
    //Flags to add
    //-p exe name/dpath
    //-o output directory
    //-r root directoyr to parse
    //-e file extensions comma separated
    //-x exclude directory
    /*1/8/2018
    [x bytes exe..]
    [4 bytes Table length]
    [4 bytes *Number of file entries* ]
    [..]
    [4 bytes File Entry location length]
    [x bytes file entry location]
    [4 bytes file entry offset relative to table]
    [4 bytes file entry size]
    [...]
    [..files..]
    [4 bytes of EXE size]
    [4 bytes signature]
    */

    class Utils
    {
        public static void PrintAndExit(string str, bool error)
        {
            if (error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.WriteLine(str);
            Console.ReadKey();
            Environment.Exit(0);
        }
        public static byte[] Combine(byte[] a, string b)
        {
            byte[] ret = Combine(a, b.Length);
            ret = Combine(ret, ASCIIEncoding.ASCII.GetBytes(b));
            return ret;
        }
        public static byte[] Combine(byte[] a, Int32 b)
        {
            return Combine(a, BitConverter.GetBytes(b));
        }
        public static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] b2 = new byte[a.Length + b.Length];
            System.Buffer.BlockCopy(a, 0, b2, 0, a.Length);
            System.Buffer.BlockCopy(b, 0, b2, a.Length, b.Length);
            return b2;
        }
    }
    class FileEntry
    {
        public string strFullPath;
        public string strTableRelativePath;
        public Int32 iFileOffset; //Right after file table.
        public Int32 iFileSize; // Size of file bytes
    }
    class FileTable
    {
        public long GetTotalFileLengthBytes()
        {
            long len = 0;
            foreach (FileEntry fe in Files) { len += fe.iFileSize;  }
            return len;
        }
        public List<FileEntry> Files = new List<FileEntry>();
        private byte[] SerializeTable() {
            Console.Write("Packing Table..");
            Int32 iSize = 0;
            byte[] ret = new byte[0];
            foreach (FileEntry fe in Files)
            {
                ret = Utils.Combine(ret, fe.strTableRelativePath);
                iSize += 4;
                iSize += ASCIIEncoding.ASCII.GetBytes(fe.strTableRelativePath).Length;
                ret = Utils.Combine(ret, fe.iFileOffset);
                iSize += 4;
                ret = Utils.Combine(ret, fe.iFileSize);
                iSize += 4;
            }
            iSize += 8; // for the count + size bytes
            //4 bytes for file entry count
            ret = Utils.Combine(BitConverter.GetBytes(Files.Count), ret);
            //4 bytes of file table size at front of table.
            ret = Utils.Combine(BitConverter.GetBytes(iSize), ret);
            //so [4][4][x..]
            Console.Write("..Done "+ iSize + " bytes\r\n");

            return ret;
        }
        private byte[] SerializeFiles()
        {
            Console.Write("Packing Files..");
            long totalLen = GetTotalFileLengthBytes();
            byte[] ret = new byte[totalLen];
            int iOff = 0;
            for (int iEntry=0; iEntry<Files.Count; ++iEntry)
            {
                FileEntry fe = Files[iEntry];
                byte[] fileBytes = System.IO.File.ReadAllBytes(fe.strFullPath);
                System.Buffer.BlockCopy(fileBytes, 0, ret, iOff, fileBytes.Length);
                iOff += fileBytes.Length;

                //ret = Utils.Combine(ret, b);
                Console.Write("("+iEntry+"/"+Files.Count+")");
            }
            Console.Write("..Done\r\n");

            return ret;
        }
        public byte[] Serialize()
        {
            byte[] ret = Utils.Combine(SerializeTable(), SerializeFiles());
            return ret;
        }
        private bool ExtMatch(string fileName, List<string> lstExts)
        {
            //Also remove the dot from the ext because we have no dots in ours.
            string ext = System.IO.Path.GetExtension(fileName).ToLower().Replace(".","");
            return lstExts.Where(x => ext == x.ToLower()).ToList().Count > 0;
        }
        private bool IsExcludeDir(string dir, CompressArgs objArgs)
        {
            string full = System.IO.Path.GetFullPath(dir);
            foreach (string path in objArgs.ExcludeDirs)
            {
                string full2 = System.IO.Path.GetFullPath(path);
                if (full.Equals(full2))
                {
                    return true;
                }
            }
            return false;
        }
        public void Build(string strParentDir, CompressArgs objArgs)
        {
            string[] files = System.IO.Directory.GetFiles(strParentDir);
            foreach (string file in files)
            {
                if (ExtMatch(file, objArgs.Extensions))
                {
                    FileEntry fe = new FileEntry();
                    fe.strFullPath = file;
                    fe.strTableRelativePath = fe.strFullPath; //TODO: fix this ** make it relative to tabe.
                    fe.iFileSize = (Int32)new System.IO.FileInfo(file).Length;
                    fe.iFileOffset = Files.Count > 0 ? Files[Files.Count - 1].iFileOffset + Files[Files.Count - 1].iFileSize : 0;

                    Files.Add(fe);
                }
            }
            string[] dirs = System.IO.Directory.GetDirectories(strParentDir);
            foreach (string dir in dirs)
            {
                if (!IsExcludeDir(dir, objArgs))
                {
                    Build(dir, objArgs);
                }
            }
        }
        public void Print()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Packed Files:");
            foreach (FileEntry fe in Files)
            {
                Console.WriteLine(" " + fe.strTableRelativePath);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
    class CompressArgs
    {
        public string ExeName { get; set; }
        public string OutputDir { get; set; }
        public string RootDir { get; set; }
        public List<string> Extensions { get; set; } = new List<string>();
        public List<string> ExcludeDirs { get; set; } = new List<string>();
        public void Parse(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.PrintAndExit("Failure, please supply EXE name and compress root Direcotry", true);
            }
            if (args.Length % 2 != 0)
            {
                Utils.PrintAndExit("Invalid number of arguments to switches.", true);
            }
            for (int i = 0; i < args.Length; i += 2)
            {
                if (args[i + 0].ToLower() == "-p")
                {
                    ExeName = args[i + 1].Replace("\"", "");
                }
                else if (args[i + 0].ToLower() == "-o")
                {
                    OutputDir = args[i + 1].Replace("\"", "");
                }
                else if (args[i + 0].ToLower() == "-r")
                {
                    RootDir = args[i + 1].Replace("\"", "");
                }
                else if (args[i + 0].ToLower() == "-e")
                {
                    Extensions = args[i + 1].Replace("\"", "").Split(',').ToList();
                }
                else if (args[i + 0].ToLower() == "-x")
                {
                    ExcludeDirs.Add(args[i + 1].Replace("\"", ""));
                }
                else
                { 
                    Utils.PrintAndExit("Unrecognized argument '" + args[i + 0] + "'", true);
                }
            }

            if (ExeName == "")
            {
                Utils.PrintAndExit("NO executable (-p) specified", true);
            }
            if (OutputDir  == "")
            {
                Utils.PrintAndExit("No output directory (-o) specified", true);
            }
            if (Extensions.Count == 0)
            {
                Console.WriteLine("Warning: No file extensions given, EVERYTHING gets packed!");
            }

        }
    }
    class Program
    {
        static string strVersion = "0.01";

        static void Main(string[] args)
        {
            CompressArgs objArgs = new CompressArgs();
            objArgs.Parse(args);

            Console.WriteLine("**************************************************");
            Console.WriteLine("* Compressor v" + strVersion);
            Console.WriteLine("* Usage: compressor.exe -p [exe path] -o [output dir] -r [root dir] -e [csv of file exts'] -x [dir to exclude]");
            
            Console.WriteLine("* Derek page  1/6/18");
            Console.WriteLine("**************************************************");
            if (!System.IO.Directory.Exists(objArgs.RootDir))
            {
                Utils.PrintAndExit("Directory '" + objArgs.RootDir + "' does not exist.", true);
            }
            else if (!System.IO.File.Exists(objArgs.ExeName))
            {
                Utils.PrintAndExit("Executable file '" + objArgs.ExeName + "' does not exist.", true);
            }
            else
            {
                try
                {
                    Console.WriteLine("Building..");
                    //build file table.
                    FileTable ft = new FileTable();
                    ft.Build(objArgs.RootDir, objArgs);

                    Console.WriteLine("Located " + ft.Files.Count + " files, " + (float)ft.GetTotalFileLengthBytes() / (float)(1000*1000) + " Mbytes.");

                    byte[] exeBytes = System.IO.File.ReadAllBytes(objArgs.ExeName);
                    byte[] _final = Utils.Combine(exeBytes, ft.Serialize());

                    //Pack on 4 bytes at the end of the EXE size so we can find the table.
                    _final = Utils.Combine(_final, (Int32)exeBytes.Length);
                    //Pack an additional signature
                    _final = Utils.Combine(_final, ASCIIEncoding.ASCII.GetBytes("asdf"));

                    //Backup? Maybe.
                    //System.IO.File.Move(strExe, strExe + "-" + DateTime.Now.ToString("YYYYMMDDHHMMSS"));
                    string strOutfull = System.IO.Path.GetFullPath(objArgs.OutputDir);
                    Console.WriteLine("Saving to '"+ strOutfull + "'..");
                    if (!System.IO.Directory.Exists(strOutfull))
                    {
                        Console.WriteLine("Creating directory '"+ strOutfull + "'");
                        System.IO.Directory.CreateDirectory(strOutfull);
                    }
                    string strOutFile = System.IO.Path.Combine(strOutfull, objArgs.ExeName);

                    Console.WriteLine("Writing '" + strOutFile +"'");
                    System.IO.File.WriteAllBytes(strOutFile, _final);
                            
                    ft.Print();

                    Console.WriteLine("Packed " + exeBytes.Length + " bytes to " + _final.Length + " bytes.");
                    Console.WriteLine("Added " + (_final.Length - exeBytes.Length) + " bytes.");
                    //   Utils.PrintAndExit("Press any key to exit", false);
                }
                catch (Exception ex)
                {
                    Utils.PrintAndExit("There was an error while building the file table:\r\n" + ex.ToString(), true);
                }

            }
        }
    }
    
}
