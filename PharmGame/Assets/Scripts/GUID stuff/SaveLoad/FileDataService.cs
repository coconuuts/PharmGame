using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace Systems.Persistence {
    public class FileDataService : IDataService {
        ISerializer serializer;
        string dataPath;
        string fileExtension;

        public FileDataService(ISerializer serializer) {
            this.dataPath = Application.persistentDataPath;
            this.fileExtension = "json";
            this.serializer = serializer;
        }

        string GetPathToFile(string fileName) {
            return Path.Combine(dataPath, string.Concat(fileName, ".", fileExtension));
        }
        string GetPathToImage(string fileName) {
            return Path.Combine(dataPath, string.Concat(fileName, ".png"));
        }
        
        public void Save(GameData data, bool overwrite = true) {
            string fileLocation = GetPathToFile(data.Id.ToHexString());

            if (!overwrite && File.Exists(fileLocation)) {
                throw new IOException($"The file '{data.Name}.{fileExtension}' already exists and cannot be overwritten.");
            }

            File.WriteAllText(fileLocation, serializer.Serialize(data));
        }

        public GameData Load(string name) {
            string fileLocation = GetPathToFile(name);

            if (!File.Exists(fileLocation)) {
                throw new ArgumentException($"No persisted GameData with name '{name}'");
            }

            return serializer.Deserialize<GameData>(File.ReadAllText(fileLocation));
        }

        public void Delete(string name) {
            string fileLocation = GetPathToFile(name);
            if (File.Exists(fileLocation)) File.Delete(fileLocation);
            
            DeleteScreenshot(name);
        }

        public void DeleteScreenshot(string saveId) {
            string fileLocation = GetPathToImage(saveId);
            if (File.Exists(fileLocation)) File.Delete(fileLocation);
        }

        public void DeleteAll() {
            foreach (string filePath in Directory.GetFiles(dataPath)) {
                File.Delete(filePath);
            }
        }

        public IEnumerable<string> ListSaves() 
        {
            if (Directory.Exists(dataPath)) 
            {
                // Create a DirectoryInfo object to access file metadata
                DirectoryInfo d = new DirectoryInfo(dataPath);
                
                // Get all files matching the extension
                FileInfo[] files = d.GetFiles("*." + fileExtension);
                
                // Sort the files by LastWriteTime in Descending order (Newest -> Oldest)
                // Then select just the file name without extension to return
                return files.OrderByDescending(f => f.LastWriteTime)
                            .Select(f => Path.GetFileNameWithoutExtension(f.Name));
            }
            
            return new List<string>();
        }

        public void SaveScreenshot(string saveId, Texture2D screenshot) {
            if (screenshot == null) return;
            
            byte[] bytes = screenshot.EncodeToPNG();
            string fileLocation = GetPathToImage(saveId);
            File.WriteAllBytes(fileLocation, bytes);
        }

        public Texture2D LoadScreenshot(string saveId) {
            string fileLocation = GetPathToImage(saveId);
            
            if (File.Exists(fileLocation)) {
                byte[] bytes = File.ReadAllBytes(fileLocation);
                // Create a temporary texture; LoadImage will replace size and format
                Texture2D texture = new Texture2D(2, 2); 
                texture.LoadImage(bytes); 
                return texture;
            }
            return null; // Return null if no screenshot exists
        }
    }
}