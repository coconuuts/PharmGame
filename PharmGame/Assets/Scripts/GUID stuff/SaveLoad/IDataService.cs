using System.Collections.Generic;
using UnityEngine;

namespace Systems.Persistence {
    public interface IDataService {
        void Save(GameData data, bool overwrite = true);
        GameData Load(string name);
        void Delete(string name);
        void DeleteAll();
        IEnumerable<string> ListSaves();

        // Image Handling
        void SaveScreenshot(string saveId, Texture2D screenshot);
        Texture2D LoadScreenshot(string saveId);
        void DeleteScreenshot(string saveId);
    }
}