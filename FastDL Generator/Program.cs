using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.BZip2;

namespace FastDL_Generator
{
    class Program
    {

        public static void Main(string[] args)
        {
            Console.Title = "FastDL Generator";
            string[] FolderWhitelist = new string[] { "maps","cache", "gamemodes", "maps", "materials", "models", "particles", "resource", "sound" };
            string[] ZuSuchen = new string[] { "*.mdl", "*.vmt", "*.vtf", "*.wav", "*.mp3", "*.bsp", "*.ain", "*.dx80.vtx", "*.dx90.vtf", "*.phy", "*.sw.vtx", "*.vvd", "*.xbox.vtx", "*.png", "*.ttf", "*.pcf", "*.gif", "*.jpg"} ;
            string MainPath;
                try
                {
                    MainPath = args[0]; 
                }
                catch (Exception)
                {
                    Console.WriteLine("Bitte einen pfad angeben");
                    return;
                }
            string copyPath = MainPath + "/../FastDL_Upload/";
            if(Directory.Exists(copyPath))
            {
                Directory.Delete(copyPath, true);
            }

            if(!Directory.Exists(MainPath))
            {
                Console.WriteLine("der Angegebene Pfad existiert nicht: {0}",MainPath);
                return;
            }


            List<string> IndexedFiles = new List<string>();

            for (int SearchIndex = 0; SearchIndex < ZuSuchen.Length; SearchIndex++)
            {
                for (int WhitelistCount = 0; WhitelistCount < FolderWhitelist.Length; WhitelistCount++)
                {
                    string[] TestData = TreeScan(MainPath, FolderWhitelist[WhitelistCount], ZuSuchen[SearchIndex]);

                    for (int FileIndex = 0; FileIndex < TestData.Length; FileIndex++)
                    {
                        string EndFile = TestData[FileIndex].Substring(MainPath.Length+1).Replace('\\','/');
                        IndexedFiles.Add(EndFile);

                    }
                }
            }

            string FileData = "// fastdl.lua Erstellt mit dem FastDL generator von JustPlayerDE.\n" +
                "if (SERVER) then\n";
            foreach (var item in IndexedFiles)
            {
                Console.WriteLine("Kopiere > "+item);
                FileData = FileData + " resource.AddFile(\""+item+"\")\n";
                CopyFile(item, MainPath, copyPath);
            }

            FileData = FileData + "end";
            File.WriteAllText(copyPath + "/fastdl.lua", FileData);
            int temp = 0;
            Console.Title = "Komprimiere Dateien...";
            foreach (var item in IndexedFiles)
            {
                temp++;
                Console.WriteLine("Komprimiere > "+item);
                Console.Title = "Komprimiere Datei "+temp+" / " + IndexedFiles.Count+"...";

                BzipFile(copyPath + "/" + item);
            }


            Console.Title = "FERTIG";
            Console.WriteLine("Operation abgeschlossen.");


        }


        private static void CopyFile(string Filee,string oldFolder, string NewFolder)
        {
            string oldFile = oldFolder+"/"+ Filee;
            string newFile = NewFolder+"/"+ Filee;
            Directory.CreateDirectory(newFile);
            Directory.Delete(newFile); // hacky way
            try
            {
                File.Copy(oldFile, newFile);
            }
            catch (Exception)
            {
                Console.WriteLine("Fehler beim kopieren,\n" + oldFile + " >>> " + newFile);
            }
        }
        
        private static string[] TreeScan(string mainDir,string GmodDir, string search)
        {
            string Path = mainDir + "/" + GmodDir+"/";
            if (Directory.Exists(Path))
            {
                return Directory.GetFiles(Path, search, SearchOption.AllDirectories);
            } else
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
                        System.IO.File.Delete(Path);
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

        private void BZip2Compress(string source)
        {
                string target = source + ".bz2";
                int blockSize = 4096;
                BZip2.Compress(File.OpenRead(source), File.Create(target), true, blockSize);
        }
    }
} 