using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioConverter
{
    public class FileChecker
    {
        public string File { get; private set; }
        public bool Finished { 
            get
            {
                if ((DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(File)).TotalSeconds > 3)
                    return true;
                return false;
            }
        }

        internal FileChecker(string filename)
        {
            File = filename;
        }

        internal void Convert()
        {
            if(Path.GetExtension(File) == ".pcm")
            {
                Program.Converting.Add(File);
                Console.WriteLine("Converting " + File + "...");

                new Thread(new ThreadStart(() =>
                {
                    ProcessStartInfo info = new ProcessStartInfo()
                    {
                        FileName = "ffmpeg.exe",
                        Arguments = "-y -f s16le -ar 48000 -ac 2 -i \"" + File + "\" \"" + Path.ChangeExtension(File, "mp3") + "\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(info).WaitForExit();
                    System.IO.File.Delete(File);
                })).Start();
            }
        }
    }

    public class DirectorySearcher
    {

        public static bool IsCompleted(string dir)
        {
            foreach (string s in Directory.EnumerateFiles(dir, "*.mp3", SearchOption.TopDirectoryOnly))
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(s)).TotalSeconds < 4)
                    return false;
            if (Directory.EnumerateFiles(dir, "*.pcm", SearchOption.TopDirectoryOnly).Count() > 0)
                return false;
            if (!File.Exists(dir + "\\done"))
                return false;
            return true;
        }
    }
}
