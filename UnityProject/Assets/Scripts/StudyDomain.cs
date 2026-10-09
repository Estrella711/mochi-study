using System;
using System.Globalization;

namespace MochiDay
{
    [Serializable] public class PetState
    {
        public string kind = "cat";
        public string catName = "银米";
        public string bunnyName = "糯米";
        public string hat = "none";
        public string outfit = "none";
        public string accessory = "none";
        public string Name { get { return kind == "bunny" ? bunnyName : catName; } }
    }

    [Serializable] public class FocusSettings
    {
        public int focusMinutes = 25;
        public int shortBreakMinutes = 5;
        public int longBreakMinutes = 15;
        public int cyclesBeforeLongBreak = 4;
        public int eyeReminderMinutes = 20;
        public int dailyGoalMinutes = 120;
        public bool eyeReminderEnabled = true;
        public bool soundEnabled = true;
        public void Repair()
        {
            focusMinutes = Bound(focusMinutes, 5, 90, 25);
            shortBreakMinutes = Bound(shortBreakMinutes, 1, 30, 5);
            longBreakMinutes = Bound(longBreakMinutes, 1, 30, 15);
            cyclesBeforeLongBreak = Bound(cyclesBeforeLongBreak, 2, 8, 4);
            eyeReminderMinutes = Bound(eyeReminderMinutes, 10, 60, 20);
            dailyGoalMinutes = Bound(dailyGoalMinutes, 30, 480, 120);
        }
        static int Bound(int value, int min, int max, int defaultValue) { return value <= 0 ? defaultValue : Math.Max(min, Math.Min(max, value)); }
        public FocusSettings Copy()
        {
            return new FocusSettings { focusMinutes = focusMinutes, shortBreakMinutes = shortBreakMinutes,
                longBreakMinutes = longBreakMinutes, cyclesBeforeLongBreak = cyclesBeforeLongBreak,
                eyeReminderMinutes = eyeReminderMinutes, dailyGoalMinutes = dailyGoalMinutes,
                eyeReminderEnabled = eyeReminderEnabled, soundEnabled = soundEnabled };
        }
    }

    [Serializable] public class TimerState
    {
        public string phase = "focus";
        public bool running;
        public double remainingSeconds;
        public double phaseDurationSeconds;
        public double eyeElapsedSeconds;
        public int cycle;
        public string sessionId;
        public string taskId;
        public string taskTitle;
        public int sessionFocusMinutes;
    }

    [Serializable] public class FocusRecord
    {
        public string id;
        public string date;
        public string taskTitle;
        public int minutes;
        public int coins;
    }

    public class ShopItem
    {
        public string id, name, description, slot;
        public int price;
        public ShopItem(string id, string name, string description, string slot, int price)
        { this.id = id; this.name = name; this.description = description; this.slot = slot; this.price = price; }
    }

    public static class PetCatalog
    {
        public static readonly ShopItem[] Items = {
            new ShopItem("hat-beret", "画家贝雷帽", "柔软的珊瑚色小帽，陪你写下灵感。", "hat", 30),
            new ShopItem("hat-crown", "小小皇冠", "为认真努力的你和搭子加冕。", "hat", 80),
            new ShopItem("outfit-sweater", "奶油针织衫", "一件暖暖的小衣服，学习也要舒服。", "outfit", 40),
            new ShopItem("accessory-bow", "樱桃蝴蝶结", "一抹可爱的粉色，今天也元气满满。", "accessory", 20),
            new ShopItem("accessory-glasses", "学霸圆眼镜", "和搭子一起进入专注状态。", "accessory", 35),
            new ShopItem("accessory-satchel", "迷你学习包", "把每天的小进步装进包里。", "accessory", 50)
        };
        public static ShopItem Find(string id) { foreach (var item in Items) if (item.id == id) return item; return null; }
        public static bool IsKnown(string id) { return Find(id) != null; }
    }

    /// <summary>Pure state transitions. The caller supplies monotonic active time and saves checkpoints.</summary>
    public class StudyService
    {
        public readonly SaveData Data;
        public bool ChangedThisTick { get; private set; }
        public bool FocusFinishedThisTick { get; private set; }
        public bool BreakFinishedThisTick { get; private set; }
        public bool EyeReminderThisTick { get; private set; }
        public int LastReward { get; private set; }
        public string LastError { get; private set; }

        public StudyService(SaveData data)
        {
            Data = data ?? new SaveData();
            LocalStore.Repair(Data);
        }

        public bool BeginFocus(string taskId, string taskTitle)
        {
            LastError = null;
            if (Data.timer.running) return Fail("请先暂停当前计时，再开启新的专注。");
            PrepareFocus(taskId, taskTitle);
            Data.timer.sessionId = Guid.NewGuid().ToString("N");
            Data.timer.running = true;
            return true;
        }

        public void Pause() { Data.timer.running = false; }

        public bool Resume()
        {
            LastError = null;
            var timer = Data.timer;
            if (timer.phase != "focus" && timer.phase != "break") return Fail("这轮已完成，请选择休息或开始下一轮。");
            if (timer.remainingSeconds <= 0) return Fail("计时已结束，请开始下一轮。");
            if (timer.phase == "focus" && string.IsNullOrEmpty(timer.sessionId)) timer.sessionId = Guid.NewGuid().ToString("N");
            timer.running = true;
            return true;
        }

        public bool Reset()
        {
            LastError = null;
            var timer = Data.timer;
            PrepareFocus(timer.taskId, timer.taskTitle);
            return true;
        }

        public bool StartBreak()
        {
            LastError = null;
            var timer = Data.timer;
            if (timer.phase != "focusDone") return Fail("先完成一轮专注，再开启番茄钟休息。");
            int minutes = timer.cycle > 0 && timer.cycle % Data.settings.cyclesBeforeLongBreak == 0
                ? Data.settings.longBreakMinutes : Data.settings.shortBreakMinutes;
            timer.phase = "break"; timer.phaseDurationSeconds = minutes * 60.0;
            timer.remainingSeconds = timer.phaseDurationSeconds; timer.running = true;
            // A deliberately chosen break also starts a new eye-use interval.
            timer.eyeElapsedSeconds = 0;
            return true;
        }

        public bool SkipBreak()
        {
            LastError = null;
            if (Data.timer.phase != "break" && Data.timer.phase != "breakDone" && Data.timer.phase != "focusDone")
                return Fail("当前没有需要跳过的休息。");
            return Reset();
        }

        void PrepareFocus(string taskId, string taskTitle)
        {
            var timer = Data.timer;
            timer.phase = "focus"; timer.running = false;
            timer.sessionFocusMinutes = Data.settings.focusMinutes;
            timer.phaseDurationSeconds = timer.sessionFocusMinutes * 60.0;
            timer.remainingSeconds = timer.phaseDurationSeconds;
            timer.sessionId = null;
            timer.taskId = taskId ?? ""; timer.taskTitle = RepairTitle(taskTitle);
        }

        public void Tick(double elapsedSeconds, DateTime localNow)
        {
            ChangedThisTick = false; FocusFinishedThisTick = false; BreakFinishedThisTick = false; EyeReminderThisTick = false;
            var timer = Data.timer;
            // Never count sleep, suspension, debugger stops, a wall-clock change, or an invalid delta.
            if (!timer.running || !Finite(elapsedSeconds) || elapsedSeconds <= 0 || elapsedSeconds > 5) return;
            if (timer.phase != "focus" && timer.phase != "break") { timer.running = false; ChangedThisTick = true; return; }
            double counted = Math.Min(elapsedSeconds, Math.Max(0, timer.remainingSeconds));
            timer.remainingSeconds = Math.Max(0, timer.remainingSeconds - counted);
            ChangedThisTick = counted > 0;
            if (timer.phase == "focus")
            {
                timer.eyeElapsedSeconds += counted;
                if (Data.settings.eyeReminderEnabled && timer.eyeElapsedSeconds >= Data.settings.eyeReminderMinutes * 60.0)
                {
                    timer.eyeElapsedSeconds = 0;
                    EyeReminderThisTick = true;
                }
            }
            if (timer.remainingSeconds > 0) return;
            timer.running = false;
            ChangedThisTick = true;
            if (timer.phase == "break")
            {
                timer.phase = "breakDone"; BreakFinishedThisTick = true;
                return;
            }
            timer.phase = "focusDone";
            LastReward = 0;
            if (string.IsNullOrEmpty(timer.sessionId)) { LastError = "这轮专注缺少有效编号，未发放金币。"; return; }
            // The session identifier is part of the saved state and also the record's key.
            // Completing the same saved session again is therefore idempotent.
            if (Data.focusRecords.Exists(record => record.id == timer.sessionId)) return;
            int reward = Math.Max(5, Math.Min(90, timer.sessionFocusMinutes));
            int credited = Math.Min(reward, int.MaxValue - Data.coins);
            Data.coins += credited;
            Data.focusRecords.Add(new FocusRecord {
                id = timer.sessionId, date = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                taskTitle = timer.taskTitle ?? "", minutes = reward, coins = credited
            });
            if (timer.cycle < int.MaxValue) timer.cycle++;
            LastReward = credited;
            FocusFinishedThisTick = true;
        }

        public void RecoverAfterRestart()
        {
            RepairTimer(Data);
            Data.timer.running = false;
            ChangedThisTick = false; FocusFinishedThisTick = false; BreakFinishedThisTick = false; EyeReminderThisTick = false;
        }

        public void ResetEyeCounter() { Data.timer.eyeElapsedSeconds = 0; }

        public bool SelectPet(string kind)
        {
            LastError = null;
            if (kind != "cat" && kind != "bunny") return Fail("请选择银渐层小猫或糯米小兔。");
            Data.pet.kind = kind; return true;
        }

        public bool RenamePet(string name)
        {
            LastError = null;
            name = (name ?? "").Trim();
            if (name.Length < 1 || name.Length > 12 || name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0 || name.IndexOf('\t') >= 0)
                return Fail("搭子名字请输入 1–12 个字符。");
            if (Data.pet.kind == "bunny") Data.pet.bunnyName = name; else Data.pet.catName = name;
            return true;
        }

        public bool Buy(string id)
        {
            LastError = null;
            var item = PetCatalog.Find(id);
            if (item == null) return Fail("没有找到这件物品。");
            if (Data.ownedItems.Contains(id)) return true;
            if (Data.coins < item.price) return Fail("金币还差 " + (item.price - Data.coins) + " 枚，完成专注就能获得。");
            Data.coins -= item.price; Data.ownedItems.Add(id); return true;
        }

        public bool Equip(string id)
        {
            LastError = null;
            var item = PetCatalog.Find(id);
            if (item == null || !Data.ownedItems.Contains(id)) return Fail("请先拥有这件物品再穿戴。");
            if (item.slot == "hat") Data.pet.hat = id;
            else if (item.slot == "outfit") Data.pet.outfit = id;
            else if (item.slot == "accessory") Data.pet.accessory = id;
            else return Fail("这件物品无法穿戴。");
            return true;
        }

        public bool Unequip(string slot)
        {
            LastError = null;
            if (slot == "hat") Data.pet.hat = "none";
            else if (slot == "outfit") Data.pet.outfit = "none";
            else if (slot == "accessory") Data.pet.accessory = "none";
            else return Fail("没有找到这个穿戴位置。");
            return true;
        }

        public bool SetSettings(FocusSettings settings)
        {
            LastError = null;
            if (settings == null) return Fail("设置为空，请重试。");
            Data.settings = settings.Copy(); Data.settings.Repair();
            // A session keeps its original duration and reward even when preferences change.
            if (Data.timer.phase == "focus" && !Data.timer.running && string.IsNullOrEmpty(Data.timer.sessionId))
                PrepareFocus(Data.timer.taskId, Data.timer.taskTitle);
            return true;
        }

        bool Fail(string error) { LastError = error; return false; }
        static bool Finite(double number) { return !double.IsNaN(number) && !double.IsInfinity(number); }

        internal static string RepairName(string name, string fallback)
        {
            name = (name ?? "").Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            if (name.Length == 0) return fallback;
            return name.Length > 12 ? name.Substring(0, 12) : name;
        }
        internal static string RepairTitle(string title)
        {
            title = (title ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            return title.Length > 80 ? title.Substring(0, 80) : title;
        }

        public static void RepairTimer(SaveData data)
        {
            var timer = data.timer;
            if (timer == null) { data.timer = new TimerState(); timer = data.timer; }
            if (timer.phase != "focus" && timer.phase != "focusDone" && timer.phase != "break" && timer.phase != "breakDone") timer.phase = "focus";
            timer.sessionFocusMinutes = timer.sessionFocusMinutes <= 0 ? data.settings.focusMinutes : Math.Max(5, Math.Min(90, timer.sessionFocusMinutes));
            if (!Finite(timer.phaseDurationSeconds) || timer.phaseDurationSeconds <= 0)
                timer.phaseDurationSeconds = (timer.phase == "break" || timer.phase == "breakDone" ? data.settings.shortBreakMinutes : timer.sessionFocusMinutes) * 60.0;
            if (timer.phase == "focus" || timer.phase == "focusDone") timer.phaseDurationSeconds = timer.sessionFocusMinutes * 60.0;
            else timer.phaseDurationSeconds = Math.Max(60, Math.Min(30 * 60, timer.phaseDurationSeconds));
            if (!Finite(timer.remainingSeconds) || timer.remainingSeconds < 0) timer.remainingSeconds = timer.phaseDurationSeconds;
            if (timer.remainingSeconds > timer.phaseDurationSeconds) timer.remainingSeconds = timer.phaseDurationSeconds;
            if (timer.phase == "focusDone" || timer.phase == "breakDone") { timer.remainingSeconds = 0; timer.running = false; }
            else if (timer.remainingSeconds == 0)
            {
                // A partial/missing timer block is a ready timer, never an instant reward.
                timer.remainingSeconds = timer.phaseDurationSeconds; timer.running = false; timer.sessionId = null;
            }
            if (!Finite(timer.eyeElapsedSeconds) || timer.eyeElapsedSeconds < 0) timer.eyeElapsedSeconds = 0;
            timer.eyeElapsedSeconds = Math.Min(timer.eyeElapsedSeconds, 60 * 60);
            timer.cycle = Math.Max(0, timer.cycle);
            timer.taskId = timer.taskId ?? ""; timer.taskTitle = RepairTitle(timer.taskTitle);
            if (timer.phase == "focus" && !string.IsNullOrEmpty(timer.sessionId) && data.focusRecords.Exists(record => record.id == timer.sessionId))
            { timer.phase = "focusDone"; timer.remainingSeconds = 0; timer.running = false; }
        }
    }
}
