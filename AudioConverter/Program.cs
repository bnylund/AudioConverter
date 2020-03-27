using NAudio.Wave;
using NAudio.Lame;
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
    class Program
    {
        public static string pcmDir { get; internal set; }
        public static long HighestDirSize { get; internal set; }
        public static List<string> Converting { get; internal set; }

        /// <summary>
        /// Converts a group of raw PCMs to one, final completed MP3.
        /// </summary>
        /// <param name="args">args[0] = Subdirectory of the current directory containing raw PCM files; args[1] = html recordings/ directory</param>
        static void Main(string[] args)
        {
            string dir = Environment.CurrentDirectory;
            string html = "";

            if (args.Length > 1) {
#if DEBUG
                dir = args[0];
#else
                dir = Environment.CurrentDirectory + "\\" + args[0];
#endif
                html = args[1];
            }
            if (!Directory.Exists(dir))
            {
                Log("PCM directory '" + args[0] + "' invalid!", Environment.CurrentDirectory + "\\log");
                Console.WriteLine("The PCM directory doesn't exist!");
                Console.WriteLine("Correct syntax: convert.exe <PCM Directory> <HTML Directory>");
                return;
            }
            if (!Directory.Exists(html))
            {
                Log("HTML directory '" + args[0] + "' invalid!", Environment.CurrentDirectory + "\\log");
                Console.WriteLine("The HTML directory doesn't exist!");
                Console.WriteLine("Correct syntax: convert.exe <PCM Directory> <HTML Directory>");
                return;
            }
            Converting = new List<string>();

            Console.WriteLine("PCM Path: " + Path.GetFullPath(dir));
            Log("PCM Path: " + Path.GetFullPath(dir), Path.GetFullPath(dir) + "\\log");
            dir = Path.GetFullPath(dir);

            Console.WriteLine("HTML Path: " + Path.GetFullPath(html));
            Log("HTML Path: " + Path.GetFullPath(html), Path.GetFullPath(dir) + "\\log");
            html = Path.GetFullPath(html);

            pcmDir = dir;
            ProcessStartInfo info;
            List<string> files;

            Thread dirWatcher = new Thread(new ThreadStart(() =>
            {
                Log("Thread started.");
                while (true)
                {
                    long curSize = GetDirectorySize();
                    if (curSize > HighestDirSize)
                        HighestDirSize = curSize;
                    Thread.Sleep(100);
                }
            }));
            dirWatcher.Start();

            bool quit = false;
            while (!quit)
            {
                if (DirectorySearcher.IsCompleted(dir))
                {
                    Log("Directory ready!");
                    quit = true;
                    break;
                }


                files = Directory.EnumerateFiles(dir, "*.pcm", SearchOption.TopDirectoryOnly).ToList();
                foreach (string s in files)
                {
                    if (!Converting.Contains(s))
                    {
                        FileChecker fc = new FileChecker(s);
                        if (fc.Finished)
                        {
                            Console.WriteLine(s + " finished! converting...");
                            fc.Convert();
                        }
                    }
                }
                Thread.Sleep(100);
            }


            if (File.Exists(dir + "\\done"))
                File.Delete(dir + "\\done");

            Converting.Clear();

            Log("Deleted done file.");

            // FIND LOWEST TICK
            string first = Directory.EnumerateFiles(dir, "*-*.mp3", SearchOption.TopDirectoryOnly).First();
            double lowest = double.Parse(first.Split('-')[first.Split('-').Length - 1].Split('.')[0]);

            foreach (string file in Directory.EnumerateFiles(dir, "*-*.mp3", SearchOption.TopDirectoryOnly))
                if (double.Parse(file.Split('-')[file.Split('-').Length - 1].Split('.')[0]) < lowest)
                    lowest = double.Parse(file.Split('-')[file.Split('-').Length - 1].Split('.')[0]);

            Log("Lowest tick: " + lowest);

            List<WaveStream> streams = new List<WaveStream>();
            files = Directory.EnumerateFiles(dir, "*-*.mp3", SearchOption.TopDirectoryOnly).ToList();
            streams = new List<WaveStream>();

            TryClear();
            Log("Adding files to stream...");

            for (int i = 0; i < files.Count; i++)
            {
                Console.WriteLine("Adding " + files[i] + "...");
                double ticks = double.Parse(files[0].Split('-')[files[0].Split('-').Length - 1].Split('.')[0]);
                Mp3FileReader reader = new Mp3FileReader(files[0]);
                WaveOffsetStream stream = new WaveOffsetStream(reader, TimeSpan.FromMilliseconds(ticks - lowest), TimeSpan.Zero, reader.TotalTime);
                WaveChannel32 channel = new WaveChannel32(stream);
                channel.Volume = 1.5F;
                channel.PadWithZeroes = false;
                streams.Add(channel);
                files.RemoveAt(0);
                i--;
            }
            WriteLine("Creating final .mp3...");
            Log("Creating final .mp3...");

            using (WaveMixerStream32 mixer = new WaveMixerStream32(streams, true))
            using (Wave32To16Stream stream = new Wave32To16Stream(mixer))
            using (var writer = new LameMP3FileWriter(dir + "\\completed.mp3", stream.WaveFormat, 128))
                stream.CopyTo(writer);


            WriteLine("Cleaning up...");
            Log("Cleaning up...");

            // Dispose of the streams
            foreach (WaveStream stream in streams)
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception ex) { }
            }

            streams.Clear();
            streams = null;

            dirWatcher.Abort();

            // Delete mp3 files
            DirectoryInfo idir = new DirectoryInfo(dir);
            foreach (FileInfo file in idir.GetFiles())
                if (file.Name.EndsWith(".mp3") && !file.Name.Contains("completed.mp3"))
                    file.Delete();

            try
            {
                File.Move(dir + "\\completed.mp3", html + "\\" + lowest + ".mp3");
            }
            catch (Exception ex) { Log("Unable to move completed.mp3! " + ex.Message + "\r\n" + ex.StackTrace); }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            WriteLine("Done!");
            Log("Done!");
            Log("Highest directory size: " + ToMegabytes(HighestDirSize).ToString("N0") + "MB");

        }

        #region Helpers

        public static double ToMegabytes(long bytes) => (bytes / 1024F) / 1024F;

        public static void Log(string message, string file = null) => File.AppendAllText((file == null ? pcmDir + "\\log" : file), "[" + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "] " + message + "\r\n");

        public static void WriteLine(string message)
        {
            try
            {
                Console.Clear();
            } catch(Exception ex) { }
            Console.WriteLine(message);
        }

        public static void TryClear()
        {
            try
            {
                Console.Clear();
            } catch(Exception ex) { }
        }

        public static long GetDirectorySize() => new DirectoryInfo(pcmDir).EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(x => x.Length);

        #endregion
    }
}
