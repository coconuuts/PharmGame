using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Systems.Persistence 
{
    public class FileDataService : IDataService 
    {
        readonly ISerializer serializer;
        readonly string dataPath;
        readonly string fileExtension;

        public FileDataService(ISerializer serializer) 
        {
            dataPath = Application.persistentDataPath;
            fileExtension = "sav";
            this.serializer = serializer;
        }

        string GetPathToFile(string fileName) => 
            Path.Combine(dataPath, $"{fileName}.{fileExtension}");

        string GetPathToImage(string fileName) => 
            Path.Combine(dataPath, $"{fileName}.png");

        public void Save(GameData data, bool overwrite = true) 
        {
            string fileLocation = GetPathToFile(data.Id.ToHexString());
            if (!overwrite && File.Exists(fileLocation)) 
            {
                throw new IOException($"The file '{data.Name}.{fileExtension}' already exists and cannot be overwritten.");
            }

            byte[] bytes = serializer.Serialize(data);
            File.WriteAllBytes(fileLocation, bytes);
        }

        public GameData Load(string name) 
        {
            string fileLocation = GetPathToFile(name);
            if (!File.Exists(fileLocation)) 
            {
                throw new ArgumentException($"No persisted GameData with name '{name}'");
            }

            byte[] bytes = File.ReadAllBytes(fileLocation);
            return serializer.Deserialize<GameData>(bytes);
        }

        public void Delete(string name) 
        {
            string fileLocation = GetPathToFile(name);
            if (File.Exists(fileLocation)) File.Delete(fileLocation);
            DeleteScreenshot(name);
        }

        public void DeleteScreenshot(string saveId) 
        {
            string fileLocation = GetPathToImage(saveId);
            if (File.Exists(fileLocation)) File.Delete(fileLocation);
        }

        public void DeleteAll() 
        {
            foreach (string filePath in Directory.GetFiles(dataPath)) 
            {
                File.Delete(filePath);
            }
        }

        public IEnumerable<string> ListSaves() 
        {
            if (Directory.Exists(dataPath)) 
            {
                var directory = new DirectoryInfo(dataPath);
                var files = directory.GetFiles($"*.{fileExtension}");

                return files.OrderByDescending(f => f.LastWriteTime)
                            .Select(f => Path.GetFileNameWithoutExtension(f.Name));
            }

            return new List<string>();
        }

        public void SaveScreenshot(string saveId, Texture2D screenshot) 
        {
            if (screenshot == null) return;
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(GetPathToImage(saveId), bytes);
        }

        public Texture2D LoadScreenshot(string saveId) 
        {
            string fileLocation = GetPathToImage(saveId);
            if (File.Exists(fileLocation)) 
            {
                byte[] bytes = File.ReadAllBytes(fileLocation);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                return texture;
            }
            return null;
        }
    }
}