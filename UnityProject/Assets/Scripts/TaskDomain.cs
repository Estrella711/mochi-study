using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MochiDay
{
    [Serializable] public class Todo
    {
        public string id;
        public string title;
        public bool important;
        public bool urgent;
        public bool done;
        public int Quadrant { get { return important ? (urgent ? 0 : 1) : (urgent ? 2 : 3); } }
    }
    [Serializable] public class Day
    {
        public string date;
        public List<Todo> tasks = new List<Todo>();
        public int Completed { get { return tasks.FindAll(t => t.done).Count; } }
        public int Energy { get { return Math.Min(100, 70 + Completed * 3); } }
        public int Mood { get { return Math.Min(100, 65 + Completed * 5); } }
    }
    [Serializable] public class SaveData
    {
        public int version = 2;
        public List<Day> days = new List<Day>();
        public bool topmost;
        public int width = 1100;
        public int height = 720;
        public PetState pet = new PetState();
        public FocusSettings settings = new FocusSettings();
        public TimerState timer = new TimerState();
        public List<FocusRecord> focusRecords = new List<FocusRecord>();
        public int coins;
        public List<string> ownedItems = new List<string>();
    }
    public class TaskBook
    {
        public readonly SaveData Data;
        public Day Today { get; private set; }
        public TaskBook(SaveData data, DateTime date) { Data = data ?? new SaveData(); if (Data.days == null) Data.days = new List<Day>(); SelectDate(date); }
        public bool SelectDate(DateTime date)
        {
            string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (Today != null && Today.date == key) return false;
            Today = Data.days.Find(d => d.date == key);
            if (Today == null) { Today = new Day { date = key }; Data.days.Add(Today); }
            return true;
        }
        public Todo Add(string text, bool important, bool urgent)
        {
            var task = new Todo { id = Guid.NewGuid().ToString("N"), title = Clean(text), important = important, urgent = urgent };
            Today.tasks.Add(task); return task;
        }
        public void Edit(Todo task, string text, bool important, bool urgent)
        {
            if (!Today.tasks.Contains(task)) throw new ArgumentException("任务已不在今天的列表中");
            string title = Clean(text);
            task.title = title; task.important = important; task.urgent = urgent;
        }
        public void Toggle(Todo task) { if (Today.tasks.Contains(task)) task.done = !task.done; }
        public void Delete(Todo task) { Today.tasks.Remove(task); }
        public static string Clean(string text)
        {
            text = (text ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            if (text.Length == 0 || text.Length > 80) throw new ArgumentException("请输入 1–80 个字符的任务名称");
            return text;
        }
    }
    public class LocalStore
    {
        public readonly string DirectoryPath;
        public string FilePath { get { return Path.Combine(DirectoryPath, "study.json"); } }
        public string LegacyFilePath { get { return Path.Combine(DirectoryPath, "days.json"); } }
        public string Warning { get; private set; }
        public bool Writable { get; private set; } = true;
        bool inspected;
        public LocalStore(string path) { DirectoryPath = path; }
        public SaveData Load()
        {
            Warning = null; Writable = true; inspected = true;
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    var header = JsonUtility.FromJson<SchemaHeader>(json);
                    if (header != null && header.version > 2)
                    {
                        Warning = "学习存档来自更新版本，原文件与备份均未修改。";
                        return ReadOnlyFallback(" 请使用对应的新版本打开，当前暂时只读。");
                    }
                    return ParseV2(json);
                }
                catch (Exception)
                {
                    Warning = "学习存档损坏：原文件已保留。";
                    SaveData recovered;
                    try { recovered = ParseV2(File.ReadAllText(FilePath + ".bak", Encoding.UTF8)); }
                    catch (Exception) { return ReadOnlyFallback(" 无有效备份，暂时只读，请先备份并检查存档。"); }
                    try
                    {
                        File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), false);
                        string recoveryPath = FilePath + ".recover";
                        File.Copy(FilePath + ".bak", recoveryPath, true);
                        File.Replace(recoveryPath, FilePath, null);
                        Warning += " 已恢复上一份备份。";
                    }
                    catch (Exception e) { Writable = false; Warning += " 已读取有效备份，但无法修复原文件，暂时只读：" + e.Message; }
                    return recovered;
                }
            }
            // An interrupted replacement can leave a valid backup without a primary file.
            if (File.Exists(FilePath + ".bak"))
            {
                try
                {
                    var recovered = ParseV2(File.ReadAllText(FilePath + ".bak", Encoding.UTF8));
                    Warning = "学习存档主文件缺失，已读取上一份备份。";
                    return recovered;
                }
                catch (Exception) { Warning = "学习存档备份无效，原文件已保留。"; return ReadOnlyFallback(" 暂时只读，请先检查存档。"); }
            }
            if (!File.Exists(LegacyFilePath)) return new SaveData();
            try
            {
                var migrated = ParseLegacy(File.ReadAllText(LegacyFilePath, Encoding.UTF8));
                Warning = "已导入原版的任务和历史记录，并保留原来的糯米小兔。原 days.json 未修改，新进度将保存到 study.json。";
                return migrated;
            }
            catch (Exception)
            {
                Warning = "原版 days.json 无效，原文件未修改。";
                return ReadOnlyFallback(" 暂时只读，请先备份并检查原版存档。");
            }
        }
        SaveData ReadOnlyFallback(string message)
        {
            Writable = false; Warning += message; return new SaveData();
        }
        public static SaveData Parse(string json)
        {
            var header = JsonUtility.FromJson<SchemaHeader>(json);
            if (header == null) throw new InvalidDataException("存档为空");
            if (header.version == 1) return ParseLegacy(json);
            if (header.version == 2) return ParseV2(json);
            throw new InvalidDataException("不支持的存档版本");
        }
        [Serializable] class SchemaHeader { public int version; public List<Day> days; }
        static SaveData ParseV2(string json)
        {
            var header = JsonUtility.FromJson<SchemaHeader>(json);
            if (header == null || header.version != 2 || header.days == null) throw new InvalidDataException("学习存档结构无效");
            var data = JsonUtility.FromJson<SaveData>(json);
            ValidateDays(data);
            Repair(data, json);
            return data;
        }
        static SaveData ParseLegacy(string json)
        {
            var header = JsonUtility.FromJson<SchemaHeader>(json);
            if (header == null || header.version != 1 || header.days == null) throw new InvalidDataException("原版存档结构无效");
            var data = JsonUtility.FromJson<SaveData>(json);
            ValidateDays(data);
            if (!HasField(json, "width")) data.width = 900;
            if (!HasField(json, "height")) data.height = 600;
            data.version = 2;
            data.pet = new PetState { kind = "bunny" };
            data.settings = new FocusSettings(); data.timer = new TimerState();
            data.focusRecords = new List<FocusRecord>(); data.ownedItems = new List<string>(); data.coins = 0;
            Repair(data, null);
            return data;
        }
        static void ValidateDays(SaveData data)
        {
            if (data == null || data.days == null) throw new InvalidDataException();
            var dates = new HashSet<string>(); var ids = new HashSet<string>();
            foreach (var day in data.days)
            {
                DateTime parsed;
                if (day == null || !DateTime.TryParseExact(day.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) || !dates.Add(day.date) || day.tasks == null) throw new InvalidDataException();
                foreach (var task in day.tasks)
                    if (task == null || string.IsNullOrEmpty(task.id) || !ids.Add(task.id) || TaskBook.Clean(task.title) != task.title) throw new InvalidDataException();
            }
        }
        static bool HasField(string json, string name)
        {
            return json != null && Regex.IsMatch(json, "(?<!\\\\)\"" + Regex.Escape(name) + "\"\\s*:");
        }
        public static void Repair(SaveData data, string sourceJson = null)
        {
            if (data == null) throw new ArgumentNullException("data");
            data.version = 2;
            if (data.days == null) data.days = new List<Day>();
            if (data.width <= 0) data.width = 1100; if (data.height <= 0) data.height = 720;
            data.width = Math.Max(680, Math.Min(1600, data.width)); data.height = Math.Max(480, Math.Min(1100, data.height));
            if (data.pet == null) data.pet = new PetState();
            data.pet.kind = data.pet.kind == "bunny" ? "bunny" : "cat";
            data.pet.catName = StudyService.RepairName(data.pet.catName, "银米");
            data.pet.bunnyName = StudyService.RepairName(data.pet.bunnyName, "糯米");
            if (data.settings == null) data.settings = new FocusSettings();
            if (sourceJson != null && !HasField(sourceJson, "eyeReminderEnabled")) data.settings.eyeReminderEnabled = true;
            if (sourceJson != null && !HasField(sourceJson, "soundEnabled")) data.settings.soundEnabled = true;
            data.settings.Repair();
            if (data.timer == null) data.timer = new TimerState();
            if (data.focusRecords == null) data.focusRecords = new List<FocusRecord>();
            var recordIds = new HashSet<string>();
            foreach (var record in data.focusRecords)
            {
                DateTime date;
                if (record == null || string.IsNullOrEmpty(record.id) || !recordIds.Add(record.id) ||
                    !DateTime.TryParseExact(record.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                    record.minutes < 1 || record.minutes > 90 || record.coins < 0 || record.coins > 90)
                    throw new InvalidDataException("专注记录无效");
                record.taskTitle = StudyService.RepairTitle(record.taskTitle);
            }
            if (data.ownedItems == null) data.ownedItems = new List<string>();
            var owned = new HashSet<string>();
            data.ownedItems.RemoveAll(id => PetCatalog.Find(id) == null || !owned.Add(id));
            data.coins = Math.Max(0, data.coins);
            RepairEquipment(data, "hat"); RepairEquipment(data, "outfit"); RepairEquipment(data, "accessory");
            StudyService.RepairTimer(data);
        }
        static void RepairEquipment(SaveData data, string slot)
        {
            string id = slot == "hat" ? data.pet.hat : slot == "outfit" ? data.pet.outfit : data.pet.accessory;
            var item = PetCatalog.Find(id);
            if (item != null && item.slot == slot && data.ownedItems.Contains(id)) return;
            if (slot == "hat") data.pet.hat = "none"; else if (slot == "outfit") data.pet.outfit = "none"; else data.pet.accessory = "none";
        }
        public bool Save(SaveData data)
        {
            if (!Writable) return false;
            try
            {
                if (!inspected && (File.Exists(FilePath) || File.Exists(FilePath + ".bak") || File.Exists(LegacyFilePath))) Load();
                if (!Writable) return false;
                if (data == null || data.version != 2) throw new InvalidDataException("拒绝保存未知版本的存档");
                ValidateDays(data); Repair(data);
                Directory.CreateDirectory(DirectoryPath);
                string tmp = FilePath + ".tmp";
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + ".bak");
                else File.Move(tmp, FilePath);
                // Keep migration/recovery notices visible for the current session.
                return true;
            }
            catch (Exception e) { Warning = "保存失败，请检查存档目录的空间和权限：" + e.Message; return false; }
        }
    }
}
