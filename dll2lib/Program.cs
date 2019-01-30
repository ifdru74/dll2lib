/* Copyright (c) 2015, Peter Nelson (peter@peterdn.com)
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, 
 *    this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace dll2lib
{
    static class ExtensionMethods
    {
        public static string ReadRequiredLine(this StreamReader reader)
        {
            var line = reader.ReadLine();
            if (line == null)
                throw new InvalidDataException("Unexpected end of file");
            return line;
        }
    }

    class Program
    {
        static bool bx64Ddumper = false;    // will try to use x64 dumper if true
        static bool bVerbose = false;       // will produce some additional ouput
        private static readonly string[] PRIVATES = 
        {
            "DllCanUnloadNow",
            "DllGetClassObject",
            "DllGetClassFactoryFromClassString",
            "DllGetDocumentation",
            "DllInitialize",
            "DllInstall",
            "DllRegisterServer",
            "DllRegisterServerEx",
            "DllRegisterServerExW",
            "DllUnload",
            "DllUnregisterServer",
            "RasCustomDeleteEntryNotify",
            "RasCustomDial",
            "RasCustomDialDlg",
            "RasCustomEntryDlg"
        };

        static int Main(string[] args) 
        {
            if (args.Length < 1) 
                return Usage();

            bx64Ddumper = (IntPtr.Size == 8);   // set from runtime mode
            bVerbose = false;
            var dllpath = "";
            var cleanfiles = true;
            foreach (var arg in args)
            {
                switch(arg.ToLower())
                {
                    case "/noclean":
                        cleanfiles = false;
                        break;
                    case "/x64":
                        bx64Ddumper = true;
                        break;
                    case "/verbose":
                        bVerbose = true;
                        break;
                    case "/help":
                    case "-help":
                    case "--help":
                        return Usage();
                    default:
                        if (arg[0] == '/')
                        {
                            Console.WriteLine(string.Format("Unknown command line argument: {0}", arg));
                            return Usage();
                        }
                        dllpath = arg;
                        break;
                }
            }
            if (!File.Exists(dllpath))
                return Usage(string.Format("Could not find input file {0}", dllpath));
            if (bVerbose)
            {
                Console.WriteLine(string.Format("Use '{0}' dumpbin.exe", (bx64Ddumper?"x64":"x86")));
                Console.WriteLine(string.Format("File to process: '{0}'", dllpath));
            }
            var index = dllpath.LastIndexOf('.');
            var dllname = index >= 0 ? dllpath.Substring(0, index) : dllpath;

            var dmppath = dllname + ".dmp";
            var defpath = dllname + ".def";
            var libpath = dllname + ".lib";

            try
            {
                RunDumpbin(dllpath, dmppath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RunDumpbin: " + ex.Message);
                return -1;
            }

            try
            {
                Dump2Def(dmppath, defpath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Dump2Def: " + ex.Message);
                return -1;
            }
            finally
            {
                if (cleanfiles && File.Exists(dmppath))
                    File.Delete(dmppath);
            }

            try
            {

                RunLib(defpath, libpath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RunLib: " + ex.Message);
                return -1;
            }
            finally
            {
                if (cleanfiles && File.Exists(defpath))
                    File.Delete(defpath);
            }
            if(bVerbose)
                Console.WriteLine(string.Format("File '{0}' processed successfully", dllpath));
            return 0;
        }
        /**
         * tried to find executable name in %PATH% or at lical drives
         */
        private static string getExecutablePath(string executableName)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            // %PATH% lookup
            string[] pElements = path.Split(System.IO.Path.PathSeparator);
            foreach(String pe in pElements)
            {
                if (Directory.Exists(pe))
                {
                    if(File.Exists(pe + Path.DirectorySeparatorChar + executableName + ".exe"))
                    {
                        if (bVerbose)
                            Console.WriteLine(string.Format("{0} found in PATH at: '{1}'", executableName, pe));
                        return executableName;  // found in path
                    }
                }
            }
            if (bVerbose)
                Console.WriteLine(string.Format("No {0} present in PATH - try to acquire it from '\\Program Files (x86)'", executableName));
            // local HDD lookup
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach(DriveInfo d in drives)
                {
                    if (d.DriveType != DriveType.Fixed)
                        continue;
                    DirectoryInfo dir = new DirectoryInfo(d.ToString() + "Program Files (x86)\\Microsoft Visual Studio");
                    FileInfo[] files = dir.GetFiles(executableName, SearchOption.AllDirectories);
                    if(files.Count()>0)
                    {
                        string pattern = Path.DirectorySeparatorChar + "x86";
                        if (bx64Ddumper)
                            pattern = Path.DirectorySeparatorChar + "x64";
                        foreach (FileInfo fi in files)
                        {
                            if(fi.DirectoryName.IndexOf(pattern)>0)
                            {
                                if (bVerbose)
                                    Console.WriteLine(string.Format("{0} detected at: '{1}'", executableName, fi.DirectoryName));
                                executableName = fi.FullName;
                                return executableName;  // found at local HDD
                            }
                        }
                    }
                }

            }   catch(Exception e)
            {
                Console.WriteLine(string.Format("Unable to find {0} at local HDDs Error:'{1}'", executableName, e.ToString()));
            }
            return executableName;
        }

        private static void RunDumpbin(string dllpath, string dmppath)
        {
            var procinfo = new ProcessStartInfo(getExecutablePath("dumpbin.exe"), string.Format("/out:\"{0}\" /exports \"{1}\"", dmppath, dllpath));
            procinfo.UseShellExecute = false;
            var dumpbin = Process.Start(procinfo);
            dumpbin.WaitForExit();
            if (dumpbin.ExitCode != 0)
                throw new ApplicationException(string.Format("dumpbin failed with exit code {0}", dumpbin.ExitCode));
        }

        private static void RunLib(string defpath, string libpath)
        {
            var procinfo = new ProcessStartInfo(getExecutablePath("lib.exe"), string.Format("/machine:arm /def:\"{0}\" /out:\"{1}\"", defpath, libpath));
            procinfo.UseShellExecute = false;
            var lib = Process.Start(procinfo);
            lib.WaitForExit();
            if (lib.ExitCode != 0)
                throw new ApplicationException(string.Format("lib failed with exit code {0}", lib.ExitCode));
        }

        private static void Dump2Def(string dmppath, string defpath)
        {
            using (var dmpfile = new StreamReader(File.OpenRead(dmppath)))
            {
                using (var deffile = new StreamWriter(File.OpenWrite(defpath)))
                {
                    // skip header
                    for (int i = 0; i < 3; ++i)
                        dmpfile.ReadRequiredLine();

                    // check input file type
                    var next = dmpfile.ReadRequiredLine().Trim();
                    if (next != "File Type: DLL")
                    {
                        throw new InvalidDataException(String.Format("Unexpected file type: {0}", next));
                    }

                    // skip info lines
                    for (int i = 0; i < 10; ++i)
                        dmpfile.ReadRequiredLine();

                    // assert next 2 lines are what we expect
                    if (!dmpfile.ReadRequiredLine().TrimStart().StartsWith("ordinal"))
                        throw new InvalidDataException("Unexpected input; expected 'ordinal'");

                    if (dmpfile.ReadRequiredLine().Trim().Length != 0)
                        throw new InvalidDataException("Unexpected input; expected empty line");

                    // begin exports
                    deffile.WriteLine("EXPORTS");
                    while (true)
                    {
                        var line = dmpfile.ReadRequiredLine();

                        if (line.Length == 0)
                            break;

                        var words = line.Split(' ');
                        var index = words.Length - 1;
                        var hasforward = words[index].EndsWith(")");
                        if (hasforward)
                            index -= 3;

                        var proc = words[index];
                        if (proc != "[NONAME]")
                        {
                            deffile.Write(proc);
                            if (PRIVATES.Contains(proc))
                                deffile.Write(" PRIVATE");
                            deffile.WriteLine();
                        }
                    }

                    // assert begin of summary
                    if (!dmpfile.ReadRequiredLine().Trim().StartsWith("Summary"))
                        throw new InvalidDataException();
                }
            }
        }

        private static int Usage(string message = "")
        {
            if (message.Length > 0)
                Console.WriteLine(message);
            else
            {
                Console.WriteLine("Usage: dll2lib.exe <options> <dll>");
                Console.WriteLine();
                Console.WriteLine("  options:");
                Console.WriteLine();
                Console.WriteLine("    /noclean        don't delete intermediate files");
                Console.WriteLine("    /x64            use x64 version of dumpbin.exe");
                Console.WriteLine("    /verbose        produce some additional output");
            }
            return -1;
        }
    }
}
