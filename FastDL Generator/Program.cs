﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using ICSharpCode.SharpZipLib.BZip2;

namespace FastDL_Generator
{
    class Program
    {

        public static int Main(string[] args)
        {
            Console.Title = "FastDL Generator";
            string[] SearchFor = new string[] {
                "materials\\*.vmt",
                "materials\\*.vtf",
                "materials\\*.png",
                "materials\\*.gif",
                "materials\\*.jpg",
                "sound\\*.wav",
                "sound\\*.mp3",
                "sound\\*.ogg",
                "maps\\*.bsp",
                "maps\\graphs\\*.ain",
                "models\\*.mdl",
                "models\\*.vtx",
                "models\\*.dx80.vtx",
                "models\\*.dx90.vtf",
                "models\\*.xbox.vtx",
                "models\\*.sw.vtx",
                "models\\*.vvd",
                "models\\*.phy",
                "resource\\*.ttf",
                "particles\\*.pcf"
            };
            string Path = "";
            string PathOutput = "";
            int Threads = Math.Max(Environment.ProcessorCount - 2, 2);
            int RunningThreads = 0;

            // Parsing arguments
            if(args.Length == 0)
            {
                Console.WriteLine("Usage: ");
                Console.WriteLine("fastdlgen.exe <path to addon dir> (output path) (Threads)");
                Console.WriteLine("If no output path is given it will be put into '<path to addon dir>/upload_dir'");
                Console.WriteLine($"If no amount of Threads are given it will default to {Threads} threads.");
                Console.WriteLine("The Output directory will always be cleaned.");

                return 1;
            } else
            {
                // Input path
                if (Directory.Exists(args[0]))
                {
                    Path = args[0];
                    Console.WriteLine($"Using {Path} as source.");
                    PathOutput = $"{Path}/upload_dir";
                } else
                {
                    Console.WriteLine($"{Path} was not found!");
                    return 1;
                }


                // Output path
                if (args.Length >= 2)
                {
                    try
                    {
                        PathOutput = args[1];
                    } catch(Exception) { }
                }
                // Clean it up before use.
                if (Directory.Exists(PathOutput))
                {
                    try
                    {
                        Directory.Delete(PathOutput, true);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Error creating {PathOutput}: Could not delete existing directory for cleanup.");
                        return 1;
                    }
                }

                try
                {
                    Directory.CreateDirectory(PathOutput);
                } catch(Exception e)
                {
                    Console.WriteLine($"Error creating {PathOutput}: {e.Message}");
                    return 1;
                }
                Console.WriteLine($"Using {PathOutput} as output.");

                // Threads
                if (args.Length >= 3)
                {
                    try
                    {
                        Threads = Int32.Parse(args[2]);
                    }
                    catch (Exception) { }
                }
                Console.WriteLine($"Using {Threads} Threads.");
            }

            // Define first 2 lines for fastdl.lua
            string FileData = "// fastdl.lua generated by FastDL Generator.\n" +
                                "if (SERVER) then\n";

            // Indexing Files for copy
            var CopyQueue = new ConcurrentQueue<string>();
            var CompressQueue = new ConcurrentQueue<string>();

            foreach (var Type in SearchFor)
            {
                string[] Data = TreeScan(Path, Type);
                foreach (var item in Data)
                {
                    string path = item.Substring(Path.Length + 1).Replace('\\', '/');
                    CopyQueue.Enqueue(path);
                    CompressQueue.Enqueue(path);
                    FileData = FileData + " resource.AddFile(\"" + path + "\")\n";
                }
            }

            Console.WriteLine($"Found {CopyQueue.Count} files to copy.");
            FileData = FileData + "end"; // Done i guess
            File.WriteAllText(PathOutput + "/fastdl.lua", FileData);


            // Copy files
            for (int i = 0; i < Threads; i++)
            {
                Thread temp = new Thread(new ThreadStart(ThreadedCopy));
                temp.Start();
                RunningThreads++;
                Console.WriteLine("Thread #{0} Started", temp.ManagedThreadId);
            }

            // Lazy way of waiting for threads i guess but it works
            while (RunningThreads > 0) {
                Thread.Sleep(10);
            }

            Console.WriteLine("Compressing files...");

            // Compress files
            for (int i = 0; i < Threads; i++)
            {
                Thread temp = new Thread(new ThreadStart(ThreadedCompressing));
                temp.Start();
                RunningThreads++;
                Console.WriteLine("Thread #{0} Started", temp.ManagedThreadId);
            }

            // Lazy way of waiting for threads i guess but it works
            while (RunningThreads > 0)
            {
                Thread.Sleep(100);
            }

            void ThreadedCopy()
            {
                string currentFile;
                while (CopyQueue.TryDequeue(out currentFile))
                {
                    if (!Directory.Exists(PathOutput + "/" + currentFile))
                    {
                        try
                        {
                            Directory.CreateDirectory(PathOutput + "/" + currentFile);
                            Directory.Delete(PathOutput + "/" + currentFile); // hacky way
                        }
                        catch (Exception) { } // Ignore that
                    }

                    // Copy file into new directory
                    try
                    {
                        File.Copy($"{Path}/{currentFile}", $"{PathOutput}/{currentFile}", true);
                        Console.WriteLine($"#{Thread.CurrentThread.ManagedThreadId} Copied: {Path}/{currentFile}");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Error at Copy:\n{Path}/{currentFile} >>> {PathOutput}/{currentFile}");
                    }
                    Console.Title = $"FastDL Generator ({CopyQueue.Count} items in queue, {RunningThreads}/{Threads} Threads active)";
                }
                RunningThreads--;
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} Stopped");
            }

            void ThreadedCompressing()
            {
                string currentFile;
                while(CompressQueue.TryDequeue(out currentFile))
                {
                    string filePath = PathOutput + "/" + currentFile;

                    try
                    {
                        if (File.Exists(filePath))
                        {
                            Console.WriteLine($"#{Thread.CurrentThread.ManagedThreadId} Compressing: {currentFile}");
                            BzipFile(filePath);
                        }
                    } catch(Exception e)
                    {
                        Console.WriteLine($"#{Thread.CurrentThread.ManagedThreadId} Error: {e.Message}");
                        CompressQueue.Enqueue(currentFile);
                        if(File.Exists(filePath + ".bzip"))
                        {
                            File.Delete(filePath + ".bzip"); // At least dont download broken files
                        }
                    }
                    Console.Title = $"FastDL Generator ({CompressQueue.Count} items in queue, {RunningThreads}/{Threads} Threads active)";
                }
                RunningThreads--;
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} Stopped");
            }

            Console.WriteLine("Done");
            return 0;
        }
        
        
        private static string[] TreeScan(string mainDir, string search)
        {
            try
            {
              return Directory.GetFiles(mainDir, search, SearchOption.AllDirectories);
            }
            catch (Exception)
            {
              return new string[] { };
            }

        }

        private static bool BzipFile(string Path)
        {
            if(!File.Exists(Path))
            {
                return false;
            }
            FileInfo fileToBeZipped = new FileInfo(Path);
            FileInfo zipFileName = new FileInfo(string.Concat(fileToBeZipped.FullName, ".bz2"));
            using (FileStream fileToBeZippedAsStream = fileToBeZipped.OpenRead())
            {
                using (FileStream zipTargetAsStream = zipFileName.Create())
                {
                    try
                    {
                        BZip2.Compress(fileToBeZippedAsStream, zipTargetAsStream, true, 4096);
                        File.Delete(Path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                }
            }
            return true;
        }
    }
} 