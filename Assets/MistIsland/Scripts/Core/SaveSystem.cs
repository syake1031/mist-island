using System;
using System.IO;
using UnityEngine;

namespace MistIsland
{
    public static class SaveSystem
    {
        const string FileName = "mist_island_save.json";

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (data != null) return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MistIsland] セーブデータを読めませんでした: " + e.Message);
            }
            return null;
        }

        public static void Save(SaveData data)
        {
            try
            {
                data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MistIsland] セーブに失敗しました: " + e.Message);
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MistIsland] セーブデータを削除できませんでした: " + e.Message);
            }
        }
    }
}
