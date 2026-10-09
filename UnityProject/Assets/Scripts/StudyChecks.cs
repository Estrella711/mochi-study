using System;
using System.IO;
using UnityEngine;

namespace MochiDay
{
    /// <summary>Deterministic integration checks. Five-second ticks simulate time without waiting.</summary>
    public static class StudyChecks
    {
        static void Assert(bool value, string reason)
        { if (!value) throw new Exception(reason); }

        static void Near(double actual, double expected, string reason)
        { Assert(Math.Abs(actual - expected) < 0.0001, reason + " (" + actual + " != " + expected + ")"); }

        static StudyService Service(int minutes = 5)
        {
            var service = new StudyService(new SaveData());
            Assert(service.SetSettings(new FocusSettings {
                focusMinutes = minutes, shortBreakMinutes = 2, longBreakMinutes = 7,
                cyclesBeforeLongBreak = 4, eyeReminderMinutes = 10,
                eyeReminderEnabled = true, soundEnabled = false
            }), "settings failed");
            return service;
        }

        static DateTime Advance(StudyService service, int seconds, DateTime now)
        {
            for (int remaining = seconds; remaining > 0;)
            {
                int step = Math.Min(5, remaining); now = now.AddSeconds(step);
                service.Tick(step, now); remaining -= step;
            }
            return now;
        }

        static SaveData RoundTrip(SaveData data)
        { return LocalStore.Parse(JsonUtility.ToJson(data, true)); }

        public static void Run(Action<string, Action> check, string dir)
        {
            check("V1 migration preserves original tasks and source file", () => {
                string path = Path.Combine(dir, "migration"); Directory.CreateDirectory(path);
                var store = new LocalStore(path);
                const string legacy = "{\"version\":1,\"days\":[{\"date\":\"2026-10-08\",\"tasks\":[{\"id\":\"legacy-1\",\"title\":\"旧版阅读任务\",\"important\":true,\"urgent\":false,\"done\":true}]}],\"topmost\":true,\"width\":960,\"height\":640}";
                File.WriteAllText(store.LegacyFilePath, legacy);
                var data = store.Load();
                Assert(store.Writable && data.version == 2 && data.days.Count == 1, "migration schema failed");
                Assert(data.days[0].tasks[0].title == "旧版阅读任务" && data.days[0].tasks[0].done, "legacy tasks lost");
                Assert(data.pet.kind == "bunny" && data.pet.Name == "糯米", "original pet not retained");
                Assert(data.topmost && data.width == 960 && data.height == 640, "window preferences lost");
                Assert(data.coins == 0 && data.focusRecords.Count == 0 && data.settings.focusMinutes == 25, "migration defaults incorrect");
                Assert(store.Save(data) && File.Exists(store.FilePath), "new study save failed");
                Assert(File.ReadAllText(store.LegacyFilePath) == legacy, "original days.json changed");
                data.coins = 12; Assert(store.Save(data), "second v2 save failed");
                var loaded = new LocalStore(path).Load();
                Assert(loaded.coins == 12 && loaded.days[0].tasks[0].done, "v2 not preferred to v1");
                Assert(File.ReadAllText(store.LegacyFilePath) == legacy, "restart modified legacy source");
            });

            check("Future schema remains read-only despite a valid older backup", () => {
                string path = Path.Combine(dir, "future-version"); var store = new LocalStore(path);
                Assert(store.Save(new SaveData()) && store.Save(new SaveData()), "backup setup failed");
                const string future = "{\"version\":99,\"days\":[],\"futureData\":\"keep me\"}";
                File.WriteAllText(store.FilePath, future);
                var reopened = new LocalStore(path); reopened.Load();
                Assert(!reopened.Writable && !reopened.Save(new SaveData()), "future schema downgraded");
                Assert(File.ReadAllText(store.FilePath) == future, "future file replaced by old backup");
            });

            check("Invalid legacy and unrecoverable v2 keep source files", () => {
                string path = Path.Combine(dir, "invalid-legacy"); Directory.CreateDirectory(path);
                var legacy = new LocalStore(path); File.WriteAllText(legacy.LegacyFilePath, "not JSON");
                legacy.Load(); Assert(!legacy.Writable && !legacy.Save(new SaveData()), "invalid legacy was writable");
                Assert(File.ReadAllText(legacy.LegacyFilePath) == "not JSON" && !File.Exists(legacy.FilePath), "legacy evidence changed");
                string badPath = Path.Combine(dir, "bad-backup"); Directory.CreateDirectory(badPath);
                var bad = new LocalStore(badPath); File.WriteAllText(bad.FilePath, "bad primary"); File.WriteAllText(bad.FilePath + ".bak", "bad backup");
                bad.Load(); Assert(!bad.Writable && !bad.Save(new SaveData()), "bad backups accepted");
                Assert(File.ReadAllText(bad.FilePath) == "bad primary" && File.ReadAllText(bad.FilePath + ".bak") == "bad backup", "corrupt evidence overwritten");
            });

            check("Cat and bunny keep independent Chinese names through restart", () => {
                var service = Service();
                Assert(service.SelectPet("cat") && service.RenamePet("  银豆  "), "cat name failed");
                Assert(service.Data.pet.Name == "银豆", "cat name not trimmed");
                Assert(service.SelectPet("bunny") && service.RenamePet("糯团"), "bunny name failed");
                Assert(service.SelectPet("cat") && service.Data.pet.Name == "银豆", "cat name lost on switch");
                Assert(!service.SelectPet("unknown") && !service.RenamePet(" ") && !service.RenamePet("第一行\n第二行") && !service.RenamePet(new string('米', 13)), "invalid pet input accepted");
                Assert(service.Data.pet.kind == "cat" && service.Data.pet.Name == "银豆", "invalid input changed pet");
                var fresh = new StudyService(RoundTrip(service.Data));
                Assert(fresh.Data.pet.catName == "银豆" && fresh.Data.pet.bunnyName == "糯团", "names lost in JSON");
                Assert(fresh.SelectPet("bunny") && fresh.Data.pet.Name == "糯团", "bunny restart name lost");
            });

            check("Shop requires funds and ownership, repeated purchases charge once", () => {
                var service = Service();
                Assert(!service.Buy("hat-beret") && !service.Equip("hat-beret"), "unearned item accepted");
                Assert(service.Data.coins == 0 && service.Data.ownedItems.Count == 0, "failed purchase changed wallet");
                service.Data.coins = 100;
                Assert(service.Buy("hat-beret") && service.Data.coins == 70, "purchase price mismatch");
                Assert(service.Buy("hat-beret") && service.Data.coins == 70 && service.Data.ownedItems.Count == 1, "duplicate charge");
                Assert(service.Equip("hat-beret") && service.Data.pet.hat == "hat-beret", "owned hat failed");
                Assert(service.Buy("outfit-sweater") && service.Equip("outfit-sweater"), "outfit failed");
                Assert(service.Buy("accessory-bow") && service.Equip("accessory-bow") && service.Data.coins == 10, "accessory failed");
                Assert(!service.Buy("missing-item") && !service.Equip("hat-crown"), "unknown or unowned item accepted");
                Assert(service.Unequip("hat") && service.Data.pet.hat == "none" && !service.Unequip("missing-slot"), "unequip failed");
                Assert(service.Data.ownedItems.Count == 3 && service.Data.coins == 10, "unequip changed inventory or wallet");
            });

            check("Focus counts 60 small ticks and grants one reward and one record", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 10, 0, 0);
                Assert(service.BeginFocus("task-1", "数学练习"), "start failed");
                Assert(!service.BeginFocus("task-2", "另一个任务"), "running focus overwritten");
                now = Advance(service, 295, now);
                Assert(service.Data.coins == 0 && service.Data.focusRecords.Count == 0 && service.Data.timer.running, "early reward");
                Near(service.Data.timer.remainingSeconds, 5, "countdown wrong");
                now = Advance(service, 5, now);
                Assert(service.FocusFinishedThisTick && service.LastReward == 5, "completion event or reward missing");
                Assert(service.Data.timer.phase == "focusDone" && !service.Data.timer.running && service.Data.timer.cycle == 1, "completion phase wrong");
                Assert(service.Data.coins == 5 && service.Data.focusRecords.Count == 1, "completion reward mismatch");
                var record = service.Data.focusRecords[0];
                Assert(record.minutes == 5 && record.coins == 5 && record.taskTitle == "数学练习" && record.date == "2026-10-09", "focus record wrong");
                Advance(service, 600, now);
                Assert(service.Data.coins == 5 && service.Data.focusRecords.Count == 1 && !service.FocusFinishedThisTick && !service.Resume(), "finished timer rewarded again");
            });

            check("Pause, resume and reset never award abandoned focus", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 12, 0, 0);
                Assert(service.BeginFocus("", "背单词"), "start failed"); now = Advance(service, 120, now);
                service.Pause(); double remaining = service.Data.timer.remainingSeconds;
                now = Advance(service, 300, now); Near(service.Data.timer.remainingSeconds, remaining, "paused countdown advanced");
                Assert(service.Data.coins == 0 && service.Resume(), "pause awarded coins or resume failed");
                now = Advance(service, 60, now); Near(service.Data.timer.remainingSeconds, 120, "resume lost progress");
                Assert(service.Reset() && !service.Data.timer.running && service.Data.timer.phase == "focus", "reset failed");
                Near(service.Data.timer.remainingSeconds, 300, "reset did not restore full duration");
                Assert(service.Data.coins == 0 && service.Data.focusRecords.Count == 0 && service.Data.timer.cycle == 0, "partial focus rewarded");
                Assert(service.Resume(), "prepared focus failed"); Advance(service, 300, now);
                Assert(service.Data.coins == 5 && service.Data.focusRecords.Count == 1, "fresh focus after reset failed");
            });

            check("Short and fourth-cycle long breaks have no coin rewards", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 8, 0, 0);
                Assert(!service.StartBreak() && !service.SkipBreak(), "break allowed before any focus");
                for (int cycle = 1; cycle <= 4; cycle++)
                {
                    Assert(service.BeginFocus("", "第 " + cycle + " 轮"), "cycle start failed"); now = Advance(service, 300, now);
                    Assert(service.StartBreak(), "break start failed");
                    int expected = cycle == 4 ? 420 : 120;
                    Near(service.Data.timer.remainingSeconds, expected, "break duration wrong");
                    int balance = service.Data.coins;
                    now = Advance(service, expected, now);
                    Assert(service.BreakFinishedThisTick && service.Data.timer.phase == "breakDone" && !service.Data.timer.running, "break completion failed");
                    Assert(service.Data.coins == balance && service.Data.focusRecords.Count == cycle, "break granted coins");
                    Assert(service.SkipBreak() && !service.Data.timer.running, "next focus not prepared");
                }
                Assert(service.Data.timer.cycle == 4 && service.Data.coins == 20, "cycle totals wrong");
            });

            check("Sleep gaps, invalid deltas and clock jumps cannot fabricate time", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 9, 0, 0);
                Assert(service.BeginFocus("", "稳定计时"), "start failed");
                foreach (double invalid in new[] { -1.0, 0.0, 5.01, 3600.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                { service.Tick(invalid, now.AddYears(10)); Near(service.Data.timer.remainingSeconds, 300, "invalid delta counted"); }
                service.Tick(5, now.AddYears(10)); Near(service.Data.timer.remainingSeconds, 295, "forward clock jump counted as elapsed time");
                service.Tick(5, now.AddYears(-10)); Near(service.Data.timer.remainingSeconds, 290, "clock rollback advanced or reset timer");
                Assert(service.Data.coins == 0 && service.Data.focusRecords.Count == 0 && service.Data.timer.running, "clock jump granted reward");
                Near(service.Data.timer.eyeElapsedSeconds, 10, "invalid gaps counted for reminders");
            });

            check("Restart pauses countdown and keeps partial progress without offline coins", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 9, 0, 0);
                Assert(service.BeginFocus("restart-task", "复习笔记"), "start failed"); Advance(service, 115, now);
                string session = service.Data.timer.sessionId;
                string path = Path.Combine(dir, "restart-focus"); var store = new LocalStore(path);
                Assert(store.Save(service.Data), "checkpoint failed");
                var fresh = new StudyService(new LocalStore(path).Load()); fresh.RecoverAfterRestart();
                Assert(!fresh.Data.timer.running && fresh.Data.timer.sessionId == session && fresh.Data.timer.taskTitle == "复习笔记", "restart state lost");
                Near(fresh.Data.timer.remainingSeconds, 185, "restart countdown reset");
                Advance(fresh, 600, now.AddDays(3));
                Assert(fresh.Data.coins == 0 && fresh.Data.focusRecords.Count == 0, "offline restart granted coins");
                Near(fresh.Data.timer.remainingSeconds, 185, "recovered paused timer advanced");
                Assert(fresh.Resume(), "recovered resume failed"); Advance(fresh, 185, now.AddDays(3));
                Assert(fresh.Data.coins == 5 && fresh.Data.focusRecords.Count == 1 && fresh.Data.focusRecords[0].id == session, "recovered completion lost or duplicated");
            });

            check("Completed saved session is idempotent when a stale timer is replayed", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 9, 0, 0);
                Assert(service.BeginFocus("", "重复恢复防护"), "start failed"); Advance(service, 300, now);
                var stale = RoundTrip(service.Data);
                stale.timer.phase = "focus"; stale.timer.remainingSeconds = 5; stale.timer.running = true;
                var recovered = new StudyService(stale); recovered.Tick(5, now.AddDays(1));
                Assert(recovered.Data.coins == 5 && recovered.Data.focusRecords.Count == 1 && recovered.Data.timer.cycle == 1, "same session rewarded twice");
                Assert(recovered.Data.timer.phase == "focusDone" && !recovered.Data.timer.running, "stale completed session not repaired");
            });

            check("Cross-midnight focus records use completion date and preserve tasks", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 23, 58, 0);
                var book = new TaskBook(service.Data, now); book.Add("昨日日程", true, false);
                Assert(service.BeginFocus("", "跨日学习"), "start failed"); now = Advance(service, 300, now);
                Assert(service.Data.focusRecords[0].date == "2026-10-10" && service.Data.focusRecords[0].minutes == 5, "cross-midnight date wrong");
                Assert(book.SelectDate(now) && book.Today.tasks.Count == 0 && book.Data.days[0].tasks.Count == 1, "focus rollover changed tasks");
                Assert(service.Data.coins == 5, "midnight lost balance");
            });

            check("Eye reminders repeat by active-focus cadence and never pause focus", () => {
                var service = Service(25); DateTime now = new DateTime(2026, 10, 9, 9, 0, 0);
                Assert(service.BeginFocus("", "阅读"), "start failed");
                now = Advance(service, 595, now);
                Assert(!service.EyeReminderThisTick && service.Data.timer.running, "early reminder");
                now = Advance(service, 5, now);
                Assert(service.EyeReminderThisTick && service.Data.timer.running && service.Data.timer.phase == "focus", "reminder missing or focus interrupted");
                Near(service.Data.timer.remainingSeconds, 900, "reminder changed countdown");
                Near(service.Data.timer.eyeElapsedSeconds, 0, "reminder cadence not reset");
                service.Pause(); now = Advance(service, 600, now);
                Assert(!service.EyeReminderThisTick && service.Resume(), "pause counted eye time");
                now = Advance(service, 600, now);
                Assert(service.EyeReminderThisTick && service.Data.timer.running, "second reminder missing");
                var off = service.Data.settings.Copy(); off.eyeReminderEnabled = false; Assert(service.SetSettings(off), "disable reminder failed");
                now = Advance(service, 300, now);
                Assert(!service.EyeReminderThisTick && service.Data.coins == 25, "disabled reminder fired or focus reward failed");
                Assert(service.StartBreak(), "break start failed"); Advance(service, 120, now);
                Assert(!service.EyeReminderThisTick, "break counted as eye use");
                service.Data.timer.eyeElapsedSeconds = 120; service.ResetEyeCounter(); Near(service.Data.timer.eyeElapsedSeconds, 0, "manual eye break not recorded");
            });

            check("Disabled eye reminders stay silent across a full long focus", () => {
                var service = Service(25); var settings = service.Data.settings.Copy(); settings.eyeReminderEnabled = false;
                Assert(service.SetSettings(settings) && service.BeginFocus("", "安静专注"), "silent focus start failed");
                DateTime now = new DateTime(2026, 10, 9, 13, 0, 0);
                for (int tick = 0; tick < 300; tick++)
                { now = now.AddSeconds(5); service.Tick(5, now); Assert(!service.EyeReminderThisTick, "disabled reminder fired"); }
                Assert(service.Data.coins == 25 && service.Data.focusRecords.Count == 1, "silent focus lost completion");
            });

            check("Changing settings preserves the current session duration and reward", () => {
                var service = Service(); DateTime now = new DateTime(2026, 10, 9, 9, 0, 0);
                Assert(service.BeginFocus("", "原设置专注"), "start failed"); now = Advance(service, 60, now);
                var changed = service.Data.settings.Copy(); changed.focusMinutes = 90;
                Assert(service.SetSettings(changed), "settings update failed");
                Near(service.Data.timer.remainingSeconds, 240, "settings changed active countdown");
                Assert(service.Data.timer.sessionFocusMinutes == 5, "settings changed active reward");
                now = Advance(service, 240, now);
                Assert(service.Data.coins == 5 && service.Data.focusRecords[0].minutes == 5, "preference change inflated reward");
                Assert(service.BeginFocus("", "新设置专注") && service.Data.timer.sessionFocusMinutes == 90, "next session ignored settings");
                Near(service.Data.timer.remainingSeconds, 5400, "next session duration wrong");
            });

            check("Wallet overflow is capped and the record reflects credited coins", () => {
                var service = Service(); service.Data.coins = int.MaxValue - 2;
                Assert(service.BeginFocus("", "钱包上限"), "start failed"); Advance(service, 300, new DateTime(2026, 10, 9));
                Assert(service.Data.coins == int.MaxValue && service.LastReward == 2 && service.Data.focusRecords[0].coins == 2, "wallet overflow or record mismatch");
            });

            check("Full v2 JSON and local save preserve statistics, settings and wardrobe", () => {
                var service = Service(); service.Data.coins = 100;
                Assert(service.SelectPet("cat") && service.RenamePet("银饼") && service.Buy("hat-beret") && service.Equip("hat-beret"), "profile fixture failed");
                Assert(service.Buy("accessory-glasses") && service.Equip("accessory-glasses"), "wardrobe fixture failed");
                var book = new TaskBook(service.Data, new DateTime(2026, 10, 9)); book.Toggle(book.Add("复习完成", true, true));
                Assert(service.BeginFocus("", "持久化专注"), "start failed"); Advance(service, 300, new DateTime(2026, 10, 9));
                var parsed = RoundTrip(service.Data);
                Assert(parsed.version == 2 && parsed.pet.Name == "银饼" && parsed.pet.hat == "hat-beret" && parsed.pet.accessory == "accessory-glasses", "JSON pet or clothes lost");
                Assert(parsed.ownedItems.Count == 2 && parsed.coins == 40 && parsed.focusRecords.Count == 1 && parsed.focusRecords[0].minutes == 5, "JSON inventory or stats lost");
                Assert(parsed.settings.focusMinutes == 5 && parsed.settings.shortBreakMinutes == 2 && !parsed.settings.soundEnabled && parsed.days[0].Completed == 1, "JSON preferences or tasks lost");
                string path = Path.Combine(dir, "full-v2"); var store = new LocalStore(path);
                Assert(store.Save(parsed), "full save failed"); var fresh = new StudyService(new LocalStore(path).Load()); fresh.RecoverAfterRestart();
                Assert(fresh.Data.coins == 40 && fresh.Data.focusRecords.Count == 1 && fresh.Data.timer.phase == "focusDone", "restart statistics or completion lost");
                Assert(fresh.Data.pet.hat == "hat-beret" && fresh.Data.ownedItems.Count == 2 && !fresh.Data.timer.running, "restart wardrobe or pause state lost");
            });
        }
    }
}
